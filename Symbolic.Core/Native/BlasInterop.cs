// =============================================================================
// Calcpad Lab — BLAS bindings (OpenBLAS DGEMM via P/Invoke)
// =============================================================================
//   OpenBLAS exporta `DGEMM` con Fortran calling convention:
//     - Todos los argumentos por referencia (ref/pointer)
//     - Layout column-major (transpuesto vs C row-major)
//     - Caracteres TRANSA/TRANSB pasados como pointers a byte
//
//   Truco column-major → row-major:
//     C = A*B (row-major) ≡ C^T = B^T * A^T (column-major)
//     Pasamos A_rowmajor como si fuera A^T_colmajor (K×M en col-major)
//     → DGEMM('N','N', n, m, k, 1, B_rowmajor, n, A_rowmajor, k, 0, C_rowmajor, n)
//
//   Threshold: BLAS solo conviene para matrices > 32×32 (sino la overhead
//   del DllImport call > compute de loop nativo).
// =============================================================================
using System;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    public static class BlasInterop
    {
        private const string DllName = "libopenblas";
        private const byte NoTrans = (byte)'N';

        /// <summary>Threshold por debajo del cual usamos loop naive (overhead BLAS > compute).</summary>
        public const int BlasThreshold = 32;

        /// <summary>True si la DLL libopenblas.dll está disponible en runtime.</summary>
        public static readonly bool Available;

        static BlasInterop()
        {
            try
            {
                // Probe: tratar de llamar a una multiplicacion trivial 1×1
                var a = new[] { 2.0 };
                var b = new[] { 3.0 };
                var c = new[] { 0.0 };
                MatMul(1, 1, 1, a, b, c);
                Available = c[0] == 6.0;
            }
            catch
            {
                Available = false;
            }
        }

        [DllImport(DllName, EntryPoint = "DGEMM", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DGEMM(
            ref byte transa,
            ref byte transb,
            ref int m,
            ref int n,
            ref int k,
            ref double alpha,
            [In] double[] A,
            ref int lda,
            [In] double[] B,
            ref int ldb,
            ref double beta,
            [In, Out] double[] C,
            ref int ldc);

        /// <summary>
        /// C[m×n] = A[m×k] * B[k×n] (row-major) via OpenBLAS DGEMM column-major trick.
        /// Si las dimensiones son pequeñas (max < BlasThreshold) o si DLL no disponible,
        /// cae al loop naive.
        /// </summary>
        public static void MatMul(int m, int k, int n,
                                  double[] A, double[] B, double[] C)
        {
            // Always-correct fallback para tamaños chicos o sin BLAS
            int maxDim = m > n ? (m > k ? m : k) : (n > k ? n : k);
            if (!Available || maxDim < BlasThreshold)
            {
                MatMulNaive(m, k, n, A, B, C);
                return;
            }

            byte tN = NoTrans;
            double alpha = 1.0, beta = 0.0;
            int mF = n, nF = m, kF = k;      // Swap m↔n para el truco column-major
            int lda = n, ldb = k, ldc = n;   // leading dims
            // En column-major: pasamos B como "A" (mF×kF) y A como "B" (kF×nF).
            DGEMM(ref tN, ref tN, ref mF, ref nF, ref kF,
                  ref alpha, B, ref lda, A, ref ldb,
                  ref beta, C, ref ldc);
        }

        private static void MatMulNaive(int m, int k, int n, double[] A, double[] B, double[] C)
        {
            Array.Clear(C, 0, m * n);
            for (int i = 0; i < m; i++)
            {
                int rowA = i * k;
                int rowC = i * n;
                for (int p = 0; p < k; p++)
                {
                    double aip = A[rowA + p];
                    int rowB = p * n;
                    for (int j = 0; j < n; j++)
                        C[rowC + j] += aip * B[rowB + j];
                }
            }
        }
    }
}
