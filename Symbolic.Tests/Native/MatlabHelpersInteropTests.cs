// Tests para la P/Invoke a matlab_helpers.dll (C++ nativo).
// Verifican: linspace, logspace, unique, sort, find_gt, arange.
//
// Si el DLL no está disponible (no compilado o no en el output dir),
// los tests se marcan como skip vía Assert.SkipUnless().
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class MatlabHelpersInteropTests
    {
        private const double Tol = 1e-12;

        [Fact]
        [Trait("Category", "Native")]
        public void Dll_IsAvailable()
        {
            Assert.True(MatlabHelpersInterop.IsAvailable(),
                "matlab_helpers.dll no está disponible — verificar copiado al output");
        }

        [Fact]
        [Trait("Category", "Native")]
        public void Version_ReportsName()
        {
            var v = MatlabHelpersInterop.GetVersion();
            Assert.Contains("matlab_helpers", v);
        }

        // ─────────────────────────────────────────────────────────────────
        // linspace
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_0_to_10_n5()
        {
            var r = MatlabHelpersInterop.Linspace(0, 10, 5);
            Assert.Equal(5, r.Length);
            Assert.Equal(0d, r[0], Tol);
            Assert.Equal(2.5d, r[1], Tol);
            Assert.Equal(5d, r[2], Tol);
            Assert.Equal(7.5d, r[3], Tol);
            Assert.Equal(10d, r[4], Tol);
        }

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_SinglePoint()
        {
            var r = MatlabHelpersInterop.Linspace(3.14, 9.99, 1);
            Assert.Single(r);
            Assert.Equal(3.14, r[0], Tol);
        }

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_Empty()
        {
            var r = MatlabHelpersInterop.Linspace(0, 1, 0);
            Assert.Empty(r);
        }

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_Large_LastIsExact()
        {
            // Sin drift: el último valor debe ser EXACTO == b
            var r = MatlabHelpersInterop.Linspace(0, 1, 1000);
            Assert.Equal(1d, r[^1]);  // strict equality, no tolerance
        }

        // ─────────────────────────────────────────────────────────────────
        // logspace
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Logspace")]
        public void Logspace_0_to_3_n4()
        {
            // logspace(0, 3, 4) → [10^0, 10^1, 10^2, 10^3] = [1, 10, 100, 1000]
            var r = MatlabHelpersInterop.Logspace(0, 3, 4);
            Assert.Equal(4, r.Length);
            Assert.Equal(1d, r[0], Tol);
            Assert.Equal(10d, r[1], Tol);
            Assert.Equal(100d, r[2], Tol);
            Assert.Equal(1000d, r[3], Tol);
        }

        // ─────────────────────────────────────────────────────────────────
        // unique
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Unique")]
        public void Unique_Sorted_NoDupes()
        {
            var r = MatlabHelpersInterop.Unique([3.0, 1.0, 2.0, 1.0, 3.0, 5.0, 2.0]);
            Assert.Equal(new[] { 1d, 2d, 3d, 5d }, r);
        }

        [Fact]
        [Trait("Category", "Unique")]
        public void Unique_AlreadySorted()
        {
            var r = MatlabHelpersInterop.Unique([1.0, 2.0, 3.0, 4.0]);
            Assert.Equal(new[] { 1d, 2d, 3d, 4d }, r);
        }

        [Fact]
        [Trait("Category", "Unique")]
        public void Unique_Empty_ReturnsEmpty()
        {
            var r = MatlabHelpersInterop.Unique(Array.Empty<double>());
            Assert.Empty(r);
        }

        [Fact]
        [Trait("Category", "Unique")]
        public void Unique_FloatsWithDupesNearTol()
        {
            // Tolerancia por default 1e-15 — valores con diff > 1e-15 son únicos
            var r = MatlabHelpersInterop.Unique([1.0, 1.0 + 1e-10, 2.0]);
            Assert.Equal(3, r.Length);
        }

        // ─────────────────────────────────────────────────────────────────
        // sort
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Sort")]
        public void Sort_Ascending_Default()
        {
            var r = MatlabHelpersInterop.Sort([5.0, 1.0, 3.0, 2.0, 4.0]);
            Assert.Equal(new[] { 1d, 2d, 3d, 4d, 5d }, r);
        }

        [Fact]
        [Trait("Category", "Sort")]
        public void Sort_Descending()
        {
            var r = MatlabHelpersInterop.Sort([5.0, 1.0, 3.0, 2.0, 4.0], ascending: false);
            Assert.Equal(new[] { 5d, 4d, 3d, 2d, 1d }, r);
        }

        // ─────────────────────────────────────────────────────────────────
        // find_gt
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Find")]
        public void FindGreater_ReturnsMatchingIndices_1Based()
        {
            // [10, 5, 30, 2, 50] > 8 → posiciones 1, 3, 5 (1-based MATLAB)
            var idx = MatlabHelpersInterop.FindGreaterThan([10, 5, 30, 2, 50], 8);
            Assert.Equal(new[] { 1, 3, 5 }, idx);
        }

        [Fact]
        [Trait("Category", "Find")]
        public void FindGreater_NoMatches_ReturnsEmpty()
        {
            var idx = MatlabHelpersInterop.FindGreaterThan([1, 2, 3], 99);
            Assert.Empty(idx);
        }

        // ─────────────────────────────────────────────────────────────────
        // arange
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Arange")]
        public void Arange_1_to_5_step_1()
        {
            var r = MatlabHelpersInterop.Arange(1, 5, 1);
            Assert.Equal(new[] { 1d, 2d, 3d, 4d, 5d }, r);
        }

        [Fact]
        [Trait("Category", "Arange")]
        public void Arange_0_to_1_step_0_25()
        {
            var r = MatlabHelpersInterop.Arange(0, 1, 0.25);
            Assert.Equal(new[] { 0d, 0.25d, 0.5d, 0.75d, 1d }, r);
        }

        [Fact]
        [Trait("Category", "Arange")]
        public void Arange_Descending()
        {
            var r = MatlabHelpersInterop.Arange(5, 1, -1);
            Assert.Equal(new[] { 5d, 4d, 3d, 2d, 1d }, r);
        }

        // ─────────────────────────────────────────────────────────────────
        // Performance smoke-test
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Perf")]
        public void Linspace_OneMillion_PointsCompletes()
        {
            var r = MatlabHelpersInterop.Linspace(0, 1, 1_000_000);
            Assert.Equal(1_000_000, r.Length);
            Assert.Equal(0d, r[0]);
            Assert.Equal(1d, r[^1]);
        }
    }
}
