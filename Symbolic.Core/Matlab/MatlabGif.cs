using System;
using System.Collections.Generic;
using System.IO;

namespace Calcpad.Core.Matlab
{
    /// <summary>
    /// Encoder GIF89a animado (paleta global + LZW) embebido en el motor del Lab.
    /// Permite que un script .m genere GIFs (getframe + imwrite '...gif' append)
    /// sin herramientas externas. Frames = RGB row-major (byte[h*w*3]).
    /// </summary>
    internal static class MatlabGif
    {
        public sealed class Frame { public byte[] Rgb; public int W, H, DelayCs; }

        // Acumulador por archivo (imwrite append re-escribe el GIF completo cada vez).
        private static readonly Dictionary<string, List<Frame>> _acc =
            new(StringComparer.OrdinalIgnoreCase);

        public static void Reset(string path) { _acc.Remove(NormKey(path)); }

        /// <summary>Agrega un frame y reescribe el GIF completo (siempre válido).</summary>
        public static void AppendAndWrite(string path, byte[] rgb, int w, int h, int delayCs, bool loop)
        {
            var key = NormKey(path);
            if (!_acc.TryGetValue(key, out var list)) { list = new List<Frame>(); _acc[key] = list; }
            list.Add(new Frame { Rgb = rgb, W = w, H = h, DelayCs = delayCs });
            WriteFile(path, list, loop);
        }

        private static string NormKey(string p) => Path.GetFullPath(p);

        private static void WriteFile(string path, List<Frame> frames, bool loop)
        {
            int w = frames[0].W, h = frames[0].H;

            // ---- Paleta global: colores exactos si ≤256, si no web-safe 6x6x6 ----
            var uniq = new Dictionary<int, int>();
            bool overflow = false;
            foreach (var f in frames)
            {
                for (int i = 0; i < f.Rgb.Length; i += 3)
                {
                    int c = (f.Rgb[i] << 16) | (f.Rgb[i + 1] << 8) | f.Rgb[i + 2];
                    if (!uniq.ContainsKey(c)) { if (uniq.Count >= 256) { overflow = true; break; } uniq[c] = uniq.Count; }
                }
                if (overflow) break;
            }
            byte[] pal = new byte[256 * 3];
            Func<byte, byte, byte, int> indexOf;
            if (!overflow)
            {
                foreach (var kv in uniq) { pal[kv.Value * 3] = (byte)(kv.Key >> 16); pal[kv.Value * 3 + 1] = (byte)(kv.Key >> 8); pal[kv.Value * 3 + 2] = (byte)kv.Key; }
                indexOf = (r, g, b) => uniq[(r << 16) | (g << 8) | b];
            }
            else
            {
                // web-safe 216
                int n = 0; int[] lv = { 0, 51, 102, 153, 204, 255 };
                for (int r = 0; r < 6; r++) for (int g = 0; g < 6; g++) for (int b = 0; b < 6; b++)
                { pal[n * 3] = (byte)lv[r]; pal[n * 3 + 1] = (byte)lv[g]; pal[n * 3 + 2] = (byte)lv[b]; n++; }
                indexOf = (r, g, b) => (Nearest6(r) * 36 + Nearest6(g) * 6 + Nearest6(b));
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            // Header
            foreach (char c in "GIF89a") bw.Write((byte)c);
            // Logical Screen Descriptor
            WriteU16(bw, w); WriteU16(bw, h);
            bw.Write((byte)0xF7);  // GCT flag=1, color res=7, sort=0, GCT size=7 (256)
            bw.Write((byte)0);     // bg color index
            bw.Write((byte)0);     // pixel aspect ratio
            bw.Write(pal);
            // NETSCAPE2.0 loop
            if (loop)
            {
                bw.Write((byte)0x21); bw.Write((byte)0xFF); bw.Write((byte)0x0B);
                foreach (char c in "NETSCAPE2.0") bw.Write((byte)c);
                bw.Write((byte)0x03); bw.Write((byte)0x01); WriteU16(bw, 0); bw.Write((byte)0x00);
            }
            // Frames
            foreach (var f in frames)
            {
                // Graphic Control Extension (delay en centisegundos)
                bw.Write((byte)0x21); bw.Write((byte)0xF9); bw.Write((byte)0x04);
                bw.Write((byte)0x00); WriteU16(bw, f.DelayCs); bw.Write((byte)0x00); bw.Write((byte)0x00);
                // Image Descriptor
                bw.Write((byte)0x2C); WriteU16(bw, 0); WriteU16(bw, 0); WriteU16(bw, w); WriteU16(bw, h); bw.Write((byte)0x00);
                // Índices de pixel
                var idx = new byte[w * h];
                for (int p = 0; p < w * h; p++)
                    idx[p] = (byte)indexOf(f.Rgb[p * 3], f.Rgb[p * 3 + 1], f.Rgb[p * 3 + 2]);
                LzwEncode(bw, idx, 8);
            }
            bw.Write((byte)0x3B); // trailer
        }

        private static int Nearest6(byte v) { int[] lv = { 0, 51, 102, 153, 204, 255 }; int best = 0; int bd = 1 << 30; for (int i = 0; i < 6; i++) { int d = Math.Abs(v - lv[i]); if (d < bd) { bd = d; best = i; } } return best; }
        private static void WriteU16(BinaryWriter bw, int v) { bw.Write((byte)(v & 0xFF)); bw.Write((byte)((v >> 8) & 0xFF)); }

        // ---- LZW (GIF) con minCodeSize = 8 ----
        private static void LzwEncode(BinaryWriter bw, byte[] indices, int minCodeSize)
        {
            bw.Write((byte)minCodeSize);
            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            var dict = new Dictionary<string, int>();
            void ResetDict() { dict.Clear(); for (int i = 0; i < clearCode; i++) dict[((char)i).ToString()] = i; }
            ResetDict();
            int codeSize = minCodeSize + 1;
            int next = endCode + 1;

            var outBuf = new List<byte>();
            int bitBuf = 0, bitCnt = 0;
            void Emit(int code)
            {
                bitBuf |= code << bitCnt; bitCnt += codeSize;
                while (bitCnt >= 8) { outBuf.Add((byte)(bitBuf & 0xFF)); bitBuf >>= 8; bitCnt -= 8; }
            }
            Emit(clearCode);
            string cur = ((char)indices[0]).ToString();
            for (int i = 1; i < indices.Length; i++)
            {
                char k = (char)indices[i];
                string ck = cur + k;
                if (dict.ContainsKey(ck)) { cur = ck; }
                else
                {
                    Emit(dict[cur]);
                    dict[ck] = next++;
                    if (next > (1 << codeSize) && codeSize < 12) codeSize++;
                    if (next >= 4096) { Emit(clearCode); ResetDict(); codeSize = minCodeSize + 1; next = endCode + 1; }
                    cur = k.ToString();
                }
            }
            Emit(dict[cur]);
            Emit(endCode);
            if (bitCnt > 0) outBuf.Add((byte)(bitBuf & 0xFF));

            // Sub-bloques de máx 255 bytes
            int pos = 0;
            while (pos < outBuf.Count)
            {
                int n = Math.Min(255, outBuf.Count - pos);
                bw.Write((byte)n);
                for (int i = 0; i < n; i++) bw.Write(outBuf[pos + i]);
                pos += n;
            }
            bw.Write((byte)0x00); // block terminator
        }
    }
}
