// =============================================================================
// Calcpad Lab — FFT / IFFT (Cooley-Tukey radix-2, con Bluestein para tamaños no-2ⁿ)
// =============================================================================
//   Soporta vectores 1D (row o column). Para matrices, FFT por columna.
//   Sin dependencias externas.
// =============================================================================
using System;

namespace Calcpad.Core.Matlab
{
    internal static class MatlabFFT
    {
        public static MValue Fft(MValue v, bool inverse)
        {
            int n = v.Data.Length;
            // Para vectores 1D
            if (v.Rows == 1 || v.Cols == 1)
            {
                var re = new double[n];
                var im = new double[n];
                Array.Copy(v.Data, re, n);
                if (v.IsComplex) Array.Copy(v.Imag, im, n);
                if ((n & (n - 1)) == 0)
                    FftRadix2(re, im, inverse);
                else
                    FftBluestein(ref re, ref im, n, inverse);
                return new MValue(v.Rows, v.Cols, re, im);
            }
            // Matriz: FFT por columna (convención MATLAB)
            int nr = v.Rows, nc = v.Cols;
            var Re = new double[nr * nc];
            var Im = new double[nr * nc];
            for (int c = 0; c < nc; c++)
            {
                var re = new double[nr];
                var im = new double[nr];
                for (int r = 0; r < nr; r++)
                {
                    re[r] = v.Data[r * nc + c];
                    if (v.IsComplex) im[r] = v.Imag[r * nc + c];
                }
                if ((nr & (nr - 1)) == 0) FftRadix2(re, im, inverse);
                else FftBluestein(ref re, ref im, nr, inverse);
                for (int r = 0; r < nr; r++)
                {
                    Re[r * nc + c] = re[r];
                    Im[r * nc + c] = im[r];
                }
            }
            return new MValue(nr, nc, Re, Im);
        }

        /// <summary>FFT 2D: aplica FFT 1D primero por columnas, luego por filas.</summary>
        public static MValue Fft2(MValue v, bool inverse)
        {
            int nr = v.Rows, nc = v.Cols;
            var Re = new double[nr * nc];
            var Im = new double[nr * nc];
            Array.Copy(v.Data, Re, v.Data.Length);
            if (v.IsComplex) Array.Copy(v.Imag, Im, v.Imag.Length);
            // FFT por columna
            for (int c = 0; c < nc; c++)
            {
                var re = new double[nr]; var im = new double[nr];
                for (int r = 0; r < nr; r++) { re[r] = Re[r * nc + c]; im[r] = Im[r * nc + c]; }
                if ((nr & (nr - 1)) == 0) FftRadix2Public(re, im, inverse);
                else FftBluesteinPublic(re, im, nr, inverse);
                for (int r = 0; r < nr; r++) { Re[r * nc + c] = re[r]; Im[r * nc + c] = im[r]; }
            }
            // FFT por fila
            for (int r = 0; r < nr; r++)
            {
                var re = new double[nc]; var im = new double[nc];
                for (int c = 0; c < nc; c++) { re[c] = Re[r * nc + c]; im[c] = Im[r * nc + c]; }
                if ((nc & (nc - 1)) == 0) FftRadix2Public(re, im, inverse);
                else FftBluesteinPublic(re, im, nc, inverse);
                for (int c = 0; c < nc; c++) { Re[r * nc + c] = re[c]; Im[r * nc + c] = im[c]; }
            }
            return new MValue(nr, nc, Re, Im);
        }
        internal static void FftRadix2Public(double[] re, double[] im, bool inverse) => FftRadix2(re, im, inverse);
        internal static void FftBluesteinPublic(double[] re, double[] im, int n, bool inverse)
        {
            FftBluestein(ref re, ref im, n, inverse);
        }

        /// <summary>FFT in-place Cooley-Tukey radix-2 para n = 2^k.</summary>
        private static void FftRadix2(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            // Bit-reversal
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            // Butterflies
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = (inverse ? 2 : -2) * Math.PI / len;
                double wRe = Math.Cos(ang), wIm = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1, curIm = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int idx1 = i + k;
                        int idx2 = i + k + len / 2;
                        double tRe = curRe * re[idx2] - curIm * im[idx2];
                        double tIm = curRe * im[idx2] + curIm * re[idx2];
                        re[idx2] = re[idx1] - tRe;
                        im[idx2] = im[idx1] - tIm;
                        re[idx1] += tRe;
                        im[idx1] += tIm;
                        double newRe = curRe * wRe - curIm * wIm;
                        double newIm = curRe * wIm + curIm * wRe;
                        curRe = newRe;
                        curIm = newIm;
                    }
                }
            }
            if (inverse)
                for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
        }

        /// <summary>Bluestein chirp-z para tamaños no-2ⁿ.</summary>
        private static void FftBluestein(ref double[] re, ref double[] im, int n, bool inverse)
        {
            // Buscar potencia de 2 ≥ 2n - 1
            int m = 1;
            while (m < 2 * n - 1) m <<= 1;
            // Chirp factors w_k = exp(±i π k² / n)
            var aRe = new double[m]; var aIm = new double[m];
            var bRe = new double[m]; var bIm = new double[m];
            double sign = inverse ? 1 : -1;
            for (int k = 0; k < n; k++)
            {
                double ang = sign * Math.PI * ((long)k * k % (2L * n)) / n;
                double c = Math.Cos(ang), s = Math.Sin(ang);
                aRe[k] = re[k] * c - im[k] * s;
                aIm[k] = re[k] * s + im[k] * c;
                bRe[k] = c;
                bIm[k] = -s;
                if (k > 0)
                {
                    bRe[m - k] = bRe[k];
                    bIm[m - k] = bIm[k];
                }
            }
            // FFT(a), FFT(b)
            FftRadix2(aRe, aIm, false);
            FftRadix2(bRe, bIm, false);
            // Multiplicar
            var cRe = new double[m]; var cIm = new double[m];
            for (int k = 0; k < m; k++)
            {
                cRe[k] = aRe[k] * bRe[k] - aIm[k] * bIm[k];
                cIm[k] = aRe[k] * bIm[k] + aIm[k] * bRe[k];
            }
            FftRadix2(cRe, cIm, true);  // inverso para conv
            // Aplicar chirp final
            for (int k = 0; k < n; k++)
            {
                double ang = sign * Math.PI * ((long)k * k % (2L * n)) / n;
                double c = Math.Cos(ang), s = Math.Sin(ang);
                re[k] = cRe[k] * c - cIm[k] * s;
                im[k] = cRe[k] * s + cIm[k] * c;
            }
            if (inverse)
                for (int k = 0; k < n; k++) { re[k] /= n; im[k] /= n; }
        }
    }
}
