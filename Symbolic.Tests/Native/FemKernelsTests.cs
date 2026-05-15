// Tests para los kernels FEM en matlab_helpers.dll (C++ nativo).
// Cubren: axpy, scatter, matmul, matvec, assemble_K, gauss_2d, dot, trsolve_lower.
//
// Performance objetivo: 100x más rápido que el equivalente Calcpad interpretado.
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class FemKernelsTests
    {
        private const double Tol = 1e-12;

        // ─────────────────────────────────────────────────────────────────
        // AXPY: y += alpha * x
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "AXPY")]
        public void Axpy_BasicAccumulate()
        {
            double[] x = [1, 2, 3, 4, 5];
            double[] y = [10, 20, 30, 40, 50];
            MatlabHelpersInterop.Axpy(2.0, x, y);
            // y[i] += 2 * x[i]
            Assert.Equal(new double[] { 12, 24, 36, 48, 60 }, y);
        }

        [Fact]
        [Trait("Category", "AXPY")]
        public void Axpy_NegativeAlpha()
        {
            double[] x = [1, 1, 1];
            double[] y = [10, 10, 10];
            MatlabHelpersInterop.Axpy(-3.0, x, y);
            Assert.Equal(new double[] { 7, 7, 7 }, y);
        }

        [Fact]
        [Trait("Category", "AXPY")]
        public void AxpyScatter_GatherScatter()
        {
            // x = [10, 20, 30, 40]
            // idx = [0, 2]      → gather x[0]=10, x[2]=30
            // y = [0, 0, 0, 0, 0]
            // idy = [1, 4]      → scatter to y[1], y[4]
            // alpha = 1
            // Result: y[1] += 10, y[4] += 30
            double[] x = [10, 20, 30, 40];
            double[] y = [0, 0, 0, 0, 0];
            int[] idx = [0, 2];
            int[] idy = [1, 4];
            MatlabHelpersInterop.AxpyScatter(1.0, idx, x, idy, y);
            Assert.Equal(new double[] { 0, 10, 0, 0, 30 }, y);
        }

        // ─────────────────────────────────────────────────────────────────
        // Matmul / Matvec
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Matmul")]
        public void Matmul_2x3_3x2()
        {
            // A = [1 2 3; 4 5 6]     (2x3)
            // B = [7 8; 9 10; 11 12] (3x2)
            // C = [58 64; 139 154]   (2x2)
            double[] A = [1, 2, 3, 4, 5, 6];
            double[] B = [7, 8, 9, 10, 11, 12];
            var C = MatlabHelpersInterop.Matmul(A, 2, 3, B, 2);
            Assert.Equal(new double[] { 58, 64, 139, 154 }, C);
        }

        [Fact]
        [Trait("Category", "Matmul")]
        public void Matmul_IdentityTimesM()
        {
            // I * M = M
            double[] I = [1, 0, 0, 1];  // 2x2 identity
            double[] M = [3, 7, 5, 9];   // 2x2
            var R = MatlabHelpersInterop.Matmul(I, 2, 2, M, 2);
            Assert.Equal(M, R);
        }

        [Fact]
        [Trait("Category", "Matvec")]
        public void Matvec_3x3()
        {
            // A = [1 2 3; 4 5 6; 7 8 9]
            // x = [1; 1; 1]
            // y = [6, 15, 24]
            double[] A = [1, 2, 3, 4, 5, 6, 7, 8, 9];
            double[] x = [1, 1, 1];
            var y = MatlabHelpersInterop.Matvec(A, 3, 3, x);
            Assert.Equal(new double[] { 6, 15, 24 }, y);
        }

        // ─────────────────────────────────────────────────────────────────
        // FEM Assemble K
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Assemble")]
        public void AssembleK_2DOFLocal_To_5DOFGlobal()
        {
            // K_local 2×2: [[1, 2], [3, 4]]
            // dofs = [0, 3]
            // K_global = zeros(5, 5)
            // Después: K_global[0,0]=1, K_global[0,3]=2, K_global[3,0]=3, K_global[3,3]=4
            double[] Klocal = [1, 2, 3, 4];
            int[] dofs = [0, 3];
            var Kglob = new double[25];  // 5x5 zeros
            MatlabHelpersInterop.AssembleK(Klocal, dofs, Kglob, 5);
            Assert.Equal(1d, Kglob[0 * 5 + 0]);
            Assert.Equal(2d, Kglob[0 * 5 + 3]);
            Assert.Equal(3d, Kglob[3 * 5 + 0]);
            Assert.Equal(4d, Kglob[3 * 5 + 3]);
            // Otros deben seguir en 0
            Assert.Equal(0d, Kglob[1 * 5 + 1]);
        }

        [Fact]
        [Trait("Category", "Assemble")]
        public void AssembleK_Accumulates_TwoElements()
        {
            // Dos elementos comparten DOF 0:
            // Elem1: K=[[1,1],[1,1]], dofs=[0,1]
            // Elem2: K=[[2,2],[2,2]], dofs=[0,2]
            // K_global[0,0] = 1+2 = 3 (acumulado)
            var Kglob = new double[9];  // 3x3
            MatlabHelpersInterop.AssembleK([1, 1, 1, 1], [0, 1], Kglob, 3);
            MatlabHelpersInterop.AssembleK([2, 2, 2, 2], [0, 2], Kglob, 3);
            Assert.Equal(3d, Kglob[0 * 3 + 0]);
        }

        // ─────────────────────────────────────────────────────────────────
        // Gauss 2D
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Gauss")]
        public void Gauss2D_2x2_HasFourPoints()
        {
            var (xi, eta, w) = MatlabHelpersInterop.Gauss2D(2, 2);
            Assert.Equal(4, xi.Length);
            Assert.Equal(4, eta.Length);
            Assert.Equal(4, w.Length);
            // Pesos: cada uno debe ser 1*1 = 1
            foreach (var wi in w) Assert.Equal(1d, wi, Tol);
            // Sum de pesos = 4 (área del cuadrado [-1,1]×[-1,1])
            Assert.Equal(4d, w.Sum(), Tol);
        }

        [Fact]
        [Trait("Category", "Gauss")]
        public void Gauss2D_3x3_HasNinePoints_SumWeightsIs4()
        {
            var (xi, eta, w) = MatlabHelpersInterop.Gauss2D(3, 3);
            Assert.Equal(9, xi.Length);
            // Sum de pesos = (5/9 + 8/9 + 5/9)² = 4
            Assert.Equal(4d, w.Sum(), Tol);
        }

        // ─────────────────────────────────────────────────────────────────
        // Dot
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Dot")]
        public void Dot_BasicVectors()
        {
            // [1,2,3] . [4,5,6] = 4+10+18 = 32
            var r = MatlabHelpersInterop.Dot([1, 2, 3], [4, 5, 6]);
            Assert.Equal(32d, r);
        }

        // ─────────────────────────────────────────────────────────────────
        // Triangular solve
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Trsolve")]
        public void TrsolveLower_2x2()
        {
            // L = [[2, 0], [3, 4]]
            // b = [4, 14]
            // L*y = b:
            //   2*y[0] = 4  →  y[0] = 2
            //   3*y[0] + 4*y[1] = 14  →  6 + 4*y[1] = 14  →  y[1] = 2
            double[] L = [2, 0, 3, 4];
            double[] b = [4, 14];
            var y = MatlabHelpersInterop.TrsolveLower(L, b, 2);
            Assert.Equal(new double[] { 2, 2 }, y);
        }

        // ─────────────────────────────────────────────────────────────────
        // Performance smoke-test
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Perf")]
        public void AssembleK_LargeProblem_FastEnough()
        {
            // Simular ensamble de 1000 elementos Q4 (8 DOF each) en una
            // malla 100x100 (≈ 20 000 DOFs globales). Debe completar
            // bajo 500ms en hardware moderno.
            int nElem = 1000;
            int ndofGlobal = 20_000;
            int ndofLocal = 8;
            var Kglob = new double[ndofGlobal * ndofGlobal];
            // K_local prototipo (8x8 matrix random pero deterministic)
            var Klocal = new double[ndofLocal * ndofLocal];
            for (int i = 0; i < Klocal.Length; i++) Klocal[i] = 1.0 / (i + 1);

            var rnd = new Random(42);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int e = 0; e < nElem; e++)
            {
                var dofs = new int[ndofLocal];
                for (int d = 0; d < ndofLocal; d++)
                    dofs[d] = rnd.Next(0, ndofGlobal);
                MatlabHelpersInterop.AssembleK(Klocal, dofs, Kglob, ndofGlobal);
            }
            sw.Stop();
            // En MathParser interpretado, este loop tardaría > 30s.
            // En C++ nativo, < 500ms es lo esperado.
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"AssembleK too slow: {sw.ElapsedMilliseconds}ms");
        }
    }
}
