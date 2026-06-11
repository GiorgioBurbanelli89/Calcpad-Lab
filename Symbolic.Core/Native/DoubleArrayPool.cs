// =============================================================================
// DoubleArrayPool — reuse pool para double[] de tamaños comunes en FEM
// =============================================================================
//   En FEM Q4-BFS asembly: cada iteracion del Gauss 4x4 crea/descarta MValues
//   intermedias (B^T, B^T*D, B^T*D*B, scalar mults). 384 iteraciones por
//   elemento = 1500+ allocaciones intermedias. Bajo WPF+WebView2 esto genera
//   GC pressure que probablemente corrompe heap nativo (causa AV).
//
//   Pool por tamaño: 16x16 = 256, 3x16 = 48, 16x1 = 16, 3x1 = 3, 140x140 = 19600.
//   Reutiliza arrays clearados en vez de allocar nuevos.
// =============================================================================
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Calcpad.Core
{
    /// <summary>Pool de double[] para reducir GC pressure en hot loops FEM.
    /// Thread-safe via ConcurrentBag. Buckets por tamaño exacto (no rounding).</summary>
    public static class DoubleArrayPool
    {
        private static readonly ConcurrentDictionary<int, ConcurrentBag<double[]>> _pool = new();
        public static long Hits, Misses, Returns;
        /// <summary>Habilita/deshabilita pooling en runtime (default: true).</summary>
        public static bool Enabled = true;

        /// <summary>Rentar un array de tamaño exacto. El array sale CLEARED (todos zeros).</summary>
        public static double[] Rent(int size)
        {
            if (size <= 0) return Array.Empty<double>();
            if (!Enabled) { Interlocked.Increment(ref Misses); return new double[size]; }
            if (_pool.TryGetValue(size, out var bag) && bag.TryTake(out var arr))
            {
                Interlocked.Increment(ref Hits);
                Array.Clear(arr, 0, size);
                return arr;
            }
            Interlocked.Increment(ref Misses);
            return new double[size];
        }

        /// <summary>Devolver array al pool. NO clearear acá — el clear es en Rent
        /// para garantizar arrays limpios (algunos callers asumen zeros).</summary>
        public static void Return(double[] arr)
        {
            if (arr == null || arr.Length == 0 || !Enabled) return;
            // Limitar pool size: max 32 buffers por bucket (~5 MB para 16384)
            var bag = _pool.GetOrAdd(arr.Length, _ => new ConcurrentBag<double[]>());
            if (bag.Count < 32)
            {
                bag.Add(arr);
                Interlocked.Increment(ref Returns);
            }
        }

        /// <summary>Vaciar pool — útil entre parses para reset baseline.</summary>
        public static void Clear()
        {
            foreach (var kv in _pool) while (kv.Value.TryTake(out _)) { }
            _pool.Clear();
        }
    }
}
