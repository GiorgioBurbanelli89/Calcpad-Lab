using System;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    /// <summary>
    /// P/Invoke bindings para <c>matlab_helpers.dll</c> — implementación nativa
    /// C++ de funciones MATLAB que faltan en Calcpad: <c>linspace</c>,
    /// <c>logspace</c>, <c>unique</c>, <c>sort</c>, <c>find</c>, <c>arange</c>.
    ///
    /// Performance: O(n log n) para sort/unique vía <c>std::sort</c>;
    /// O(n) lineal para linspace/find. Sin overhead JNI/Node — directo
    /// del runtime .NET via P/Invoke.
    /// </summary>
    public static class MatlabHelpersInterop
    {
        private const string DllName = "matlab_helpers";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_linspace(double a, double b, int n, [Out] double[] outArr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_logspace(double a, double b, int n, [Out] double[] outArr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_unique([In] double[] inArr, int n,
                                            [Out] double[] outArr, out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_sort([In] double[] inArr, int n,
                                          int ascending, [Out] double[] outArr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_find_gt([In] double[] inArr, int n, double threshold,
                                             [Out] int[] outIdx, out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_arange(double start, double stop, double step,
                                            [Out] double[] outArr, out int outCount);

        // ─────────────────────────────────────────────────────────────────
        // FEM kernels
        // ─────────────────────────────────────────────────────────────────

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_axpy(int n, double alpha,
                                          [In] double[] x, [In, Out] double[] y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_axpy_scatter(int n, double alpha,
                                                  [In] int[] idx, [In] double[] x,
                                                  [In] int[] idy, [In, Out] double[] y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_matmul(int m, int k, int n,
                                            [In] double[] A, [In] double[] B,
                                            [Out] double[] C);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_matvec(int m, int n,
                                            [In] double[] A, [In] double[] x,
                                            [Out] double[] y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_assemble_K(int ndofLocal, int ndofGlobal,
                                                [In] double[] KLocal, [In] int[] dofs,
                                                [In, Out] double[] KGlobal);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_gauss_2d(int nx, int ny,
                                              [Out] double[] xiOut, [Out] double[] etaOut,
                                              [Out] double[] wOut, out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double ml_dot(int n, [In] double[] x, [In] double[] y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_trsolve_lower(int n, [In] double[] L,
                                                   [In] double[] b, [Out] double[] y);

        // ─────────────────────────────────────────────────────────────────
        // BLAS/LAPACK-style kernels (sin dep externa, competitivos hasta n=200)
        // ─────────────────────────────────────────────────────────────────

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_solve_LU(int n, [In, Out] double[] A,
                                              [In] double[] b, [Out] double[] x,
                                              [Out] int[] piv);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_eig_sym_2x2([In] double[] A, [Out] double[] lambda);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_eig_sym_3x3([In] double[] A, [Out] double[] lambda);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_polyval(int nc, [In] double[] c,
                                             int nx, [In] double[] x, [Out] double[] outArr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_interp1_linear(int nx, [In] double[] xs, [In] double[] ys,
                                                    int nq, [In] double[] xq, [Out] double[] yq);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_fft_radix2(int n, [In, Out] double[] re,
                                                [In, Out] double[] im, int sign);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_matmul_tiled(int m, int k, int n,
                                                  [In] double[] A, [In] double[] B,
                                                  [Out] double[] C);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ml_delaunay_2d(int nPts, [In] double[] pts,
                                                 [Out] int[] triOut, int maxTris,
                                                 out int nTrisOut);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr matlab_helpers_version();

        /// <summary>Devuelve la versión del DLL nativo (e.g. "matlab_helpers v0.1.0 ...").</summary>
        public static string GetVersion()
        {
            try
            {
                var p = matlab_helpers_version();
                return Marshal.PtrToStringAnsi(p) ?? "unknown";
            }
            catch
            {
                return "<dll not loaded>";
            }
        }

        /// <summary>Chequea si el DLL está disponible cargando una función trivial.</summary>
        public static bool IsAvailable()
        {
            try
            {
                var p = matlab_helpers_version();
                return p != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // API pública C# — wrappers seguros sobre las P/Invoke
        // ─────────────────────────────────────────────────────────────────

        /// <summary>linspace(a, b, n) — n puntos equiespaciados.</summary>
        public static double[] Linspace(double a, double b, int n)
        {
            if (n < 0) throw new ArgumentException("n must be >= 0", nameof(n));
            var outArr = new double[n];
            var rc = ml_linspace(a, b, n, outArr);
            if (rc != 0) throw new InvalidOperationException($"ml_linspace failed with code {rc}");
            return outArr;
        }

        /// <summary>logspace(a, b, n) — n puntos log-equiespaciados 10^a..10^b.</summary>
        public static double[] Logspace(double a, double b, int n)
        {
            if (n < 0) throw new ArgumentException("n must be >= 0", nameof(n));
            var outArr = new double[n];
            var rc = ml_logspace(a, b, n, outArr);
            if (rc != 0) throw new InvalidOperationException($"ml_logspace failed with code {rc}");
            return outArr;
        }

        /// <summary>unique(v) — sort + dedupe.</summary>
        public static double[] Unique(double[] input)
        {
            if (input == null || input.Length == 0) return Array.Empty<double>();
            var buf = new double[input.Length];
            var rc = ml_unique(input, input.Length, buf, out int cnt);
            if (rc != 0) throw new InvalidOperationException($"ml_unique failed with code {rc}");
            var result = new double[cnt];
            Array.Copy(buf, result, cnt);
            return result;
        }

        /// <summary>sort(v) — ascendente por default.</summary>
        public static double[] Sort(double[] input, bool ascending = true)
        {
            if (input == null || input.Length == 0) return Array.Empty<double>();
            var outArr = new double[input.Length];
            var rc = ml_sort(input, input.Length, ascending ? 1 : 0, outArr);
            if (rc != 0) throw new InvalidOperationException($"ml_sort failed with code {rc}");
            return outArr;
        }

        /// <summary>find(v &gt; threshold) — devuelve índices 1-based (estilo MATLAB).</summary>
        public static int[] FindGreaterThan(double[] input, double threshold)
        {
            if (input == null || input.Length == 0) return Array.Empty<int>();
            var buf = new int[input.Length];
            var rc = ml_find_gt(input, input.Length, threshold, buf, out int cnt);
            if (rc != 0) throw new InvalidOperationException($"ml_find_gt failed with code {rc}");
            var result = new int[cnt];
            Array.Copy(buf, result, cnt);
            return result;
        }

        /// <summary>arange(start, stop, step) — equivalente a start:step:stop en MATLAB.</summary>
        public static double[] Arange(double start, double stop, double step)
        {
            // Pre-allocate generous (over-estimate)
            int est = (int)(Math.Abs(stop - start) / Math.Max(1e-15, Math.Abs(step))) + 4;
            var buf = new double[est];
            var rc = ml_arange(start, stop, step, buf, out int cnt);
            if (rc != 0) throw new InvalidOperationException($"ml_arange failed with code {rc}");
            var result = new double[cnt];
            Array.Copy(buf, result, cnt);
            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // FEM kernels — wrappers públicos
        // ─────────────────────────────────────────────────────────────────

        /// <summary>AXPY: <c>y += alpha * x</c> in-place. Equivalente a Eigen/BLAS.</summary>
        public static void Axpy(double alpha, double[] x, double[] y)
        {
            if (x == null || y == null) throw new ArgumentNullException();
            if (x.Length != y.Length)
                throw new ArgumentException("x and y must have same length");
            ml_axpy(x.Length, alpha, x, y);
        }

        /// <summary>
        /// AXPY con índices (gather/scatter): <c>y[idy[k]] += alpha * x[idx[k]]</c>.
        /// Patrón típico de ensamble FEM cuando DOFs locales mapean a DOFs globales no contiguos.
        /// </summary>
        public static void AxpyScatter(double alpha, int[] idx, double[] x, int[] idy, double[] y)
        {
            if (idx.Length != idy.Length)
                throw new ArgumentException("idx and idy must have same length");
            ml_axpy_scatter(idx.Length, alpha, idx, x, idy, y);
        }

        /// <summary>Dense matmul: <c>C = A * B</c>. A=m×k, B=k×n, C=m×n (row-major).</summary>
        public static double[] Matmul(double[] A, int m, int k, double[] B, int n)
        {
            if (A.Length != m * k) throw new ArgumentException("A size mismatch");
            if (B.Length != k * n) throw new ArgumentException("B size mismatch");
            var C = new double[m * n];
            ml_matmul(m, k, n, A, B, C);
            return C;
        }

        /// <summary>Matrix-vector: <c>y = A * x</c>. A=m×n, x=n, y=m.</summary>
        public static double[] Matvec(double[] A, int m, int n, double[] x)
        {
            if (A.Length != m * n) throw new ArgumentException("A size mismatch");
            if (x.Length != n) throw new ArgumentException("x size mismatch");
            var y = new double[m];
            ml_matvec(m, n, A, x, y);
            return y;
        }

        /// <summary>
        /// FEM assemble: <c>K_global[dofs[i], dofs[j]] += K_local[i, j]</c>.
        /// Modifica K_global in-place. ndof = K_local es ndof×ndof (row-major).
        /// </summary>
        public static void AssembleK(double[] K_local, int[] dofs,
                                     double[] K_global, int ndofGlobal)
        {
            int n = dofs.Length;
            if (K_local.Length != n * n)
                throw new ArgumentException("K_local size must match dofs.Length²");
            if (K_global.Length != ndofGlobal * ndofGlobal)
                throw new ArgumentException("K_global size must be ndofGlobal²");
            var rc = ml_assemble_K(n, ndofGlobal, K_local, dofs, K_global);
            if (rc == -2) throw new IndexOutOfRangeException("dof index out of K_global range");
            if (rc != 0) throw new InvalidOperationException($"ml_assemble_K failed: {rc}");
        }

        /// <summary>
        /// Gauss 2D (cuadratura producto): retorna nx*ny puntos {xi, eta, w}.
        /// Soporta nx, ny en {1, 2, 3, 4}.
        /// </summary>
        public static (double[] xi, double[] eta, double[] w) Gauss2D(int nx, int ny)
        {
            int max = nx * ny;
            var xi = new double[max];
            var eta = new double[max];
            var w = new double[max];
            var rc = ml_gauss_2d(nx, ny, xi, eta, w, out int cnt);
            if (rc != 0) throw new ArgumentException($"ml_gauss_2d: nx={nx},ny={ny} not supported");
            return (xi, eta, w);
        }

        /// <summary>Dot product: <c>sum(x[i]*y[i])</c>.</summary>
        public static double Dot(double[] x, double[] y)
        {
            if (x.Length != y.Length) throw new ArgumentException("size mismatch");
            return ml_dot(x.Length, x, y);
        }

        /// <summary>
        /// Triangular solve: <c>L*y = b</c> con L lower-triangular n×n.
        /// </summary>
        public static double[] TrsolveLower(double[] L, double[] b, int n)
        {
            if (L.Length != n * n) throw new ArgumentException("L size mismatch");
            if (b.Length != n) throw new ArgumentException("b size mismatch");
            var y = new double[n];
            var rc = ml_trsolve_lower(n, L, b, y);
            if (rc == -2) throw new InvalidOperationException("L is singular");
            if (rc != 0) throw new InvalidOperationException($"ml_trsolve_lower failed: {rc}");
            return y;
        }

        // ─────────────────────────────────────────────────────────────────
        // Wrappers: BLAS/LAPACK-style kernels (Octave delega a LAPACK; este
        // es código propio competitivo para matrices pequeñas/medianas).
        // ─────────────────────────────────────────────────────────────────

        /// <summary>LU solve: <c>A*x = b</c>. A destructivo (factorización).</summary>
        public static double[] SolveLU(double[] A, int n, double[] b)
        {
            if (A.Length != n * n) throw new ArgumentException("A size mismatch");
            if (b.Length != n) throw new ArgumentException("b size mismatch");
            var Acopy = (double[])A.Clone();
            var x = new double[n];
            var piv = new int[n];
            var rc = ml_solve_LU(n, Acopy, b, x, piv);
            if (rc == -1) throw new InvalidOperationException("A is singular");
            if (rc != 0) throw new InvalidOperationException($"ml_solve_LU failed: {rc}");
            return x;
        }

        /// <summary>Eigenvalues simétricos 2×2 (closed form). A row-major.</summary>
        public static double[] EigSym2x2(double[] A)
        {
            if (A.Length != 4) throw new ArgumentException("A must be 2×2 (length 4)");
            var lam = new double[2];
            ml_eig_sym_2x2(A, lam);
            return lam;
        }

        /// <summary>Eigenvalues simétricos 3×3 (Cardano cerrado). A row-major.</summary>
        public static double[] EigSym3x3(double[] A)
        {
            if (A.Length != 9) throw new ArgumentException("A must be 3×3 (length 9)");
            var lam = new double[3];
            ml_eig_sym_3x3(A, lam);
            return lam;
        }

        /// <summary>Polyval Horner: <c>p(x) = c[0]*x^(n-1) + c[1]*x^(n-2) + ... + c[n-1]</c>.</summary>
        public static double[] Polyval(double[] coeffs, double[] x)
        {
            var outArr = new double[x.Length];
            var rc = ml_polyval(coeffs.Length, coeffs, x.Length, x, outArr);
            if (rc != 0) throw new InvalidOperationException($"ml_polyval failed: {rc}");
            return outArr;
        }

        /// <summary>Interpolación lineal en grid (xs, ys), evaluar en xq.
        /// xs debe estar ordenado ascendente.</summary>
        public static double[] Interp1Linear(double[] xs, double[] ys, double[] xq)
        {
            if (xs.Length != ys.Length) throw new ArgumentException("xs/ys size mismatch");
            if (xs.Length < 2) throw new ArgumentException("need at least 2 points");
            var yq = new double[xq.Length];
            var rc = ml_interp1_linear(xs.Length, xs, ys, xq.Length, xq, yq);
            if (rc != 0) throw new InvalidOperationException($"ml_interp1_linear failed: {rc}");
            return yq;
        }

        /// <summary>
        /// FFT in-place radix-2. <c>n</c> debe ser potencia de 2.
        /// <c>forward=true</c>: FFT directa (sign=-1).
        /// <c>forward=false</c>: IFFT sin normalizar (caller debe dividir por n).
        /// </summary>
        public static void FftRadix2(double[] re, double[] im, bool forward = true)
        {
            if (re.Length != im.Length) throw new ArgumentException("re/im size mismatch");
            int n = re.Length;
            // Verificar potencia de 2
            if (n <= 0 || (n & (n - 1)) != 0)
                throw new ArgumentException("n must be power of 2");
            var rc = ml_fft_radix2(n, re, im, forward ? -1 : 1);
            if (rc != 0) throw new InvalidOperationException($"ml_fft_radix2 failed: {rc}");
        }

        /// <summary>Tiled matmul (cache-friendly): C = A*B. A=m×k, B=k×n, C=m×n.</summary>
        public static double[] MatmulTiled(double[] A, int m, int k, double[] B, int n)
        {
            if (A.Length != m * k) throw new ArgumentException("A size mismatch");
            if (B.Length != k * n) throw new ArgumentException("B size mismatch");
            var C = new double[m * n];
            ml_matmul_tiled(m, k, n, A, B, C);
            return C;
        }

        // ─────────────────────────────────────────────────────────────────
        // Delaunay 2D triangulation (Bowyer-Watson)
        //
        // Reemplaza el uso de Triangle de Shewchuk (non-commercial license)
        // que usa awatif-v2 vía triangle-wasm. Implementación propia BSD.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triangulación Delaunay 2D de un conjunto de puntos.
        /// Retorna matriz <c>n_triangles × 3</c> con índices 0-based a <c>points</c>.
        ///
        /// Equivalente MATLAB: <c>tri = delaunay(x, y)</c>.
        /// Para mallas constrained (con bordes forzados, como awatif's polygon),
        /// usar <see cref="DelaunayConstrained"/> (pendiente).
        /// </summary>
        /// <param name="points">Array plano [x0,y0, x1,y1, ...] de n puntos.</param>
        /// <returns>Array plano [t0a,t0b,t0c, t1a,t1b,t1c, ...] de n_tris × 3.</returns>
        public static int[] Delaunay2D(double[] points)
        {
            if (points.Length < 6 || points.Length % 2 != 0)
                throw new ArgumentException("points must have at least 3 pairs (length 6)");
            int nPts = points.Length / 2;
            // Capacidad: por la teoría de Euler, n_tris ≤ 2n - 5 para puntos en
            // posición general. Damos margen 3× para seguridad.
            int maxTris = 4 * nPts + 16;
            var buf = new int[3 * maxTris];
            var rc = ml_delaunay_2d(nPts, points, buf, maxTris, out int nTris);
            if (rc != 0)
                throw new InvalidOperationException($"ml_delaunay_2d failed: {rc}");
            var result = new int[3 * nTris];
            Array.Copy(buf, result, 3 * nTris);
            return result;
        }
    }
}
