// Tests para los kernels BLAS/LAPACK-style en matlab_helpers.dll.
// Estos kernels reemplazan funcionalmente lo que Octave delega a LAPACK
// (dgesv, dsyevd) o FFTW (dfft) — pero sin dep externa.
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class BlasLikeKernelsTests
    {
        private const double Tol = 1e-10;

        // ─────────────────────────────────────────────────────────────────
        // LU solve (denso)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "LU")]
        public void SolveLU_2x2()
        {
            // [[2, 1], [1, 3]] * x = [5, 10]
            // x = [1, 3]   (verificable a mano)
            double[] A = [2, 1, 1, 3];
            double[] b = [5, 10];
            var x = MatlabHelpersInterop.SolveLU(A, 2, b);
            Assert.Equal(1d, x[0], Tol);
            Assert.Equal(3d, x[1], Tol);
        }

        [Fact]
        [Trait("Category", "LU")]
        public void SolveLU_3x3_WithPivoting()
        {
            // Matrix que requiere pivoting:
            // [[0, 2, 1], [1, 0, 0], [1, 1, 1]] * x = [3, 1, 3]
            // x = [1, 1, 1]
            double[] A = [0, 2, 1, 1, 0, 0, 1, 1, 1];
            double[] b = [3, 1, 3];
            var x = MatlabHelpersInterop.SolveLU(A, 3, b);
            Assert.Equal(1d, x[0], Tol);
            Assert.Equal(1d, x[1], Tol);
            Assert.Equal(1d, x[2], Tol);
        }

        [Fact]
        [Trait("Category", "LU")]
        public void SolveLU_Singular_Throws()
        {
            // Singular: filas linearly dependent
            double[] A = [1, 2, 2, 4];
            double[] b = [3, 6];
            Assert.Throws<InvalidOperationException>(() =>
                MatlabHelpersInterop.SolveLU(A, 2, b));
        }

        [Fact]
        [Trait("Category", "LU")]
        public void SolveLU_100x100_Random_RoundTrip()
        {
            // Verificar A*x = b después de resolver
            int n = 100;
            var rnd = new Random(42);
            var A = new double[n * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    A[i * n + j] = rnd.NextDouble();
                A[i * n + i] += n; // diagonal-dominant ⇒ no singular
            }
            var x_true = new double[n];
            for (int i = 0; i < n; i++) x_true[i] = rnd.NextDouble();
            // Compute b = A*x_true
            var b = MatlabHelpersInterop.Matvec(A, n, n, x_true);
            // Solve
            var x = MatlabHelpersInterop.SolveLU(A, n, b);
            // Compare
            for (int i = 0; i < n; i++)
                Assert.Equal(x_true[i], x[i], 1e-8);
        }

        // ─────────────────────────────────────────────────────────────────
        // Eig 2×2, 3×3 (closed form)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Eig")]
        public void EigSym_2x2_DiagonalMatrix()
        {
            // Diagonal [[3, 0], [0, 5]] → eigenvalues = 3, 5
            double[] A = [3, 0, 0, 5];
            var lam = MatlabHelpersInterop.EigSym2x2(A);
            Assert.Equal(3d, lam[0], Tol);
            Assert.Equal(5d, lam[1], Tol);
        }

        [Fact]
        [Trait("Category", "Eig")]
        public void EigSym_2x2_OffDiagonal()
        {
            // [[2, 1], [1, 2]] → eigenvalues = 1, 3
            double[] A = [2, 1, 1, 2];
            var lam = MatlabHelpersInterop.EigSym2x2(A);
            Assert.Equal(1d, lam[0], Tol);
            Assert.Equal(3d, lam[1], Tol);
        }

        [Fact]
        [Trait("Category", "Eig")]
        public void EigSym_3x3_Identity()
        {
            double[] A = [1, 0, 0, 0, 1, 0, 0, 0, 1];
            var lam = MatlabHelpersInterop.EigSym3x3(A);
            Assert.Equal(1d, lam[0], Tol);
            Assert.Equal(1d, lam[1], Tol);
            Assert.Equal(1d, lam[2], Tol);
        }

        [Fact]
        [Trait("Category", "Eig")]
        public void EigSym_3x3_KnownDiagonal()
        {
            // diag(2, 4, 6) → eigenvalues sorted ascending
            double[] A = [2, 0, 0, 0, 4, 0, 0, 0, 6];
            var lam = MatlabHelpersInterop.EigSym3x3(A);
            Assert.Equal(2d, lam[0], Tol);
            Assert.Equal(4d, lam[1], Tol);
            Assert.Equal(6d, lam[2], Tol);
        }

        // ─────────────────────────────────────────────────────────────────
        // Polyval Horner
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Polyval")]
        public void Polyval_Quadratic()
        {
            // p(x) = x² + 2x + 1   en x=[0, 1, 2, 3]
            // → [1, 4, 9, 16]
            double[] c = [1, 2, 1];
            double[] x = [0, 1, 2, 3];
            var y = MatlabHelpersInterop.Polyval(c, x);
            Assert.Equal(new double[] { 1, 4, 9, 16 }, y);
        }

        [Fact]
        [Trait("Category", "Polyval")]
        public void Polyval_Constant()
        {
            double[] c = [7];
            double[] x = [-1, 0, 5, 100];
            var y = MatlabHelpersInterop.Polyval(c, x);
            foreach (var yi in y) Assert.Equal(7d, yi, Tol);
        }

        // ─────────────────────────────────────────────────────────────────
        // Interp1
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Interp1")]
        public void Interp1_Linear_Midpoints()
        {
            // xs = [0, 1, 2], ys = [0, 10, 20]
            // xq = [0.5, 1.5] → [5, 15]
            double[] xs = [0, 1, 2];
            double[] ys = [0, 10, 20];
            double[] xq = [0.5, 1.5];
            var yq = MatlabHelpersInterop.Interp1Linear(xs, ys, xq);
            Assert.Equal(5d, yq[0], Tol);
            Assert.Equal(15d, yq[1], Tol);
        }

        [Fact]
        [Trait("Category", "Interp1")]
        public void Interp1_Outside_Clamps()
        {
            double[] xs = [1, 2, 3];
            double[] ys = [10, 20, 30];
            double[] xq = [-5, 100];
            var yq = MatlabHelpersInterop.Interp1Linear(xs, ys, xq);
            // Behavior: clamp a los extremos
            Assert.Equal(10d, yq[0]);
            Assert.Equal(30d, yq[1]);
        }

        // ─────────────────────────────────────────────────────────────────
        // FFT radix-2
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "FFT")]
        public void Fft_OfImpulse_IsAllOnes()
        {
            // FFT([1, 0, 0, 0, 0, 0, 0, 0]) = [1, 1, 1, ..., 1]
            double[] re = [1, 0, 0, 0, 0, 0, 0, 0];
            double[] im = new double[8];
            MatlabHelpersInterop.FftRadix2(re, im, forward: true);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(1d, re[i], Tol);
                Assert.Equal(0d, im[i], Tol);
            }
        }

        [Fact]
        [Trait("Category", "FFT")]
        public void Fft_Then_Ifft_RoundTrip()
        {
            // FFT seguido por IFFT y dividir por n → original
            int n = 16;
            var rnd = new Random(42);
            var re = new double[n];
            var im = new double[n];
            for (int i = 0; i < n; i++)
            {
                re[i] = rnd.NextDouble();
                im[i] = rnd.NextDouble();
            }
            var re_orig = (double[])re.Clone();
            var im_orig = (double[])im.Clone();

            MatlabHelpersInterop.FftRadix2(re, im, forward: true);
            MatlabHelpersInterop.FftRadix2(re, im, forward: false);
            // Normalizar
            for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }

            for (int i = 0; i < n; i++)
            {
                Assert.Equal(re_orig[i], re[i], 1e-10);
                Assert.Equal(im_orig[i], im[i], 1e-10);
            }
        }

        [Fact]
        [Trait("Category", "FFT")]
        public void Fft_NotPowerOf2_Throws()
        {
            double[] re = new double[7];
            double[] im = new double[7];
            Assert.Throws<ArgumentException>(() =>
                MatlabHelpersInterop.FftRadix2(re, im));
        }

        // ─────────────────────────────────────────────────────────────────
        // Matmul tiled vs naive (correctness)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Matmul")]
        public void MatmulTiled_Equals_NaiveForLargeMatrix()
        {
            int n = 100;
            var rnd = new Random(42);
            var A = new double[n * n];
            var B = new double[n * n];
            for (int i = 0; i < n * n; i++)
            {
                A[i] = rnd.NextDouble();
                B[i] = rnd.NextDouble();
            }
            var C_naive = MatlabHelpersInterop.Matmul(A, n, n, B, n);
            var C_tiled = MatlabHelpersInterop.MatmulTiled(A, n, n, B, n);
            for (int i = 0; i < n * n; i++)
                Assert.Equal(C_naive[i], C_tiled[i], 1e-10);
        }

        // ─────────────────────────────────────────────────────────────────
        // Performance: nuestra ventaja vs Octave interpretado
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Perf")]
        public void Polyval_1MillionPoints_Fast()
        {
            // Octave: polyval() en script interpretado tarda ~1-2s para 1M points.
            // C++ Horner directo: < 50 ms.
            int n = 1_000_000;
            var c = new double[] { 1, 2, 3, 4, 5 }; // grado 4
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = i / 1000.0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var y = MatlabHelpersInterop.Polyval(c, x);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 500,
                $"Polyval 1M points too slow: {sw.ElapsedMilliseconds}ms");
            Assert.Equal(n, y.Length);
        }

        [Fact]
        [Trait("Category", "Perf")]
        public void MatmulTiled_200x200_Fast()
        {
            int n = 200;
            var A = new double[n * n];
            var B = new double[n * n];
            var rnd = new Random(42);
            for (int i = 0; i < n * n; i++) { A[i] = rnd.NextDouble(); B[i] = rnd.NextDouble(); }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var C = MatlabHelpersInterop.MatmulTiled(A, n, n, B, n);
            sw.Stop();
            // Octave dgemm via OpenBLAS: < 5 ms. Nuestro tiled: < 200 ms (sin SIMD).
            // El test es smoke-test (no perf-strict); solo verificar que completa.
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Matmul 200×200 too slow: {sw.ElapsedMilliseconds}ms");
            Assert.NotNull(C);
        }
    }
}
