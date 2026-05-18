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

    /// <summary>P/Invoke a LAPACK DGESV (liblapack.dll) — solver lineal LU con pivoting parcial.
    /// Fortran calling convention: todos los args por referencia, column-major.</summary>
    public static class LapackInterop
    {
        private const string DllName = "liblapack";
        /// <summary>Threshold: DGESV solo conviene para n ≥ 64 (overhead transpose + dispatch).</summary>
        public const int LapackThreshold = 64;

        public static readonly bool Available;

        static LapackInterop()
        {
            try
            {
                // Probe: solve [1, 2; 3, 4] * x = [5; 11] → x = [1; 2]
                var A = new[] { 1.0, 3.0, 2.0, 4.0 };   // ya column-major
                var B = new[] { 5.0, 11.0 };
                var ipiv = new int[2];
                int n = 2, nrhs = 1, lda = 2, ldb = 2, info = 0;
                DGESV(ref n, ref nrhs, A, ref lda, ipiv, B, ref ldb, ref info);
                Available = info == 0 && Math.Abs(B[0] - 1.0) < 1e-9 && Math.Abs(B[1] - 2.0) < 1e-9;
            }
            catch
            {
                Available = false;
            }
        }

        [DllImport(DllName, EntryPoint = "dgesv_", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DGESV(
            ref int n,
            ref int nrhs,
            [In, Out] double[] A,
            ref int lda,
            [In, Out] int[] ipiv,
            [In, Out] double[] B,
            ref int ldb,
            ref int info);

        /// <summary>
        /// Resuelve A·x = b para A cuadrada n×n y b vector n×1.
        /// A es row-major, b es 1D n-vector. Retorna x row-major.
        /// </summary>
        public static double[] Solve(int n, double[] A_row, double[] b)
        {
            if (!Available) throw new InvalidOperationException("LAPACK no disponible");
            // Transponer A row-major → column-major (A_row[i*n+j] → A_col[j*n+i])
            var A_col = new double[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    A_col[j * n + i] = A_row[i * n + j];
            // b copy (DGESV lo sobreescribe con la solucion)
            var x = new double[n];
            Array.Copy(b, x, n);
            var ipiv = new int[n];
            int N = n, nrhs = 1, lda = n, ldb = n, info = 0;
            DGESV(ref N, ref nrhs, A_col, ref lda, ipiv, x, ref ldb, ref info);
            if (info != 0)
                throw new InvalidOperationException($"DGESV info={info} (singular or argument error)");
            return x;
        }
    }
}
