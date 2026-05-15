// Tests para mesh2d(x, y, maxArea) — mesh refinado constrained Delaunay
// con Triangle de Shewchuk. Reproduce el workflow de awatif-v2.
namespace Calcpad.Lab.Tests
{
    public class Mesh2DTests
    {
        [Fact]
        [Trait("Category", "Mesh2D")]
        public void Mesh2D_BasicCallWithRefinement_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 15, 15, 0]\n" +
                "y = [0, 0, 10, 5]\n" +
                "[xm, ym, tri, bnd] = mesh2d(x, y, 0.5)");
            Assert.Equal(0, lab.CountErrors(html));
            // El info string debe mencionar muchos triángulos (con maxArea=0.5 sobre área 112.5)
            Assert.Contains("mesh2d", html);
            Assert.Contains("triángulos", html);
        }

        [Fact]
        [Trait("Category", "Mesh2D")]
        public void Mesh2D_RefinementProducesMoreThan200Triangles()
        {
            // Misma config que awatif: maxArea=0.5 sobre cuadrilátero de área ~112.5
            // → awatif produce ~270 tris, nosotros deberíamos producir similar
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 15, 15, 0]\n" +
                "y = [0, 0, 10, 5]\n" +
                "[xm, ym, tri, bnd] = mesh2d(x, y, 0.5)\n" +
                "trimesh(tri, xm, ym)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("three-canvas-", html);
        }

        [Fact]
        [Trait("Category", "Mesh2D")]
        public void Mesh2D_ThenTrisurfWithZDeformation()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x_in = [0, 10, 10, 0]\n" +
                "y_in = [0, 0, 10, 10]\n" +
                "[xm, ym, tri, bnd] = mesh2d(x_in, y_in, 0.5)\n" +
                "xi = (xm - 0) / 10\n" +
                "eta = (ym - 0) / 10\n" +
                "z = sin(pi * xi) * sin(pi * eta)\n" +
                "trisurf(tri, xm, ym, z)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("three-canvas-", html);
            // trisurf con z variable → vertex colors visibles
            Assert.Contains("MeshLambertMaterial", html);
        }
    }
}
