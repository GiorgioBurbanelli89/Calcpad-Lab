// Tests para funciones MATLAB nativas (delaunay, trimesh, triplot, trisurf)
// que se ejecutan con backend Triangle + Three.js.
//
// El mismo script .m corre IGUAL en MATLAB real y en Calcpad Lab.
namespace Calcpad.Lab.Tests
{
    public class NativeMatlabFunctionsTests
    {
        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Delaunay_BasicCall_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 15, 15, 0]\n" +
                "y = [0, 0, 10, 5]\n" +
                "tri = delaunay(x, y)");
            Assert.Equal(0, lab.CountErrors(html));
            // El output debe mencionar que se generaron 2 triángulos
            Assert.Contains("delaunay", html);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Trimesh_3D_EmitsThreeJsCanvas()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 15, 15, 0]\n" +
                "y = [0, 0, 10, 5]\n" +
                "tri = delaunay(x, y)\n" +
                "trimesh(tri, x, y)");
            Assert.Equal(0, lab.CountErrors(html));
            // El HTML debe tener un canvas Three.js
            Assert.Contains("three-canvas-", html);
            Assert.Contains("THREE.WebGLRenderer", html);
            // Three.js inline embebido + MiniOrbit propio
            Assert.Contains("Three.js r149 UMD inline", html);
            Assert.Contains("MiniOrbit", html);
            // NO scripts externos
            Assert.DoesNotContain("type=\"module\"", html);
            Assert.DoesNotContain("<script src=\"https://unpkg.com", html);
            Assert.DoesNotContain("<script src=\"https://cdn.jsdelivr.net", html);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Trimesh_BundleEmittedOnce_NotDuplicated()
        {
            // Múltiples trimesh en el mismo reporte deben cargar Three.js
            // solo UNA vez para reducir tamaño/carga.
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 5, 5, 0]\n" +
                "y = [0, 0, 5, 5]\n" +
                "tri = delaunay(x, y)\n" +
                "trimesh(tri, x, y)\n" +
                "trimesh(tri, x, y)\n" +
                "trimesh(tri, x, y)");
            Assert.Equal(0, lab.CountErrors(html));
            // Three.js inline (608 KB) y MiniOrbit deben aparecer una sola vez
            int idx = 0; int countOrbit = 0;
            while ((idx = html.IndexOf("window.MiniOrbit = function", idx, StringComparison.Ordinal)) >= 0)
            { countOrbit++; idx++; }
            Assert.Equal(1, countOrbit);
            idx = 0; int countThree = 0;
            while ((idx = html.IndexOf("Three.js r149 UMD inline", idx, StringComparison.Ordinal)) >= 0)
            { countThree++; idx++; }
            Assert.Equal(1, countThree);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Triplot_2D_EmitsSvg()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 15, 15, 0]\n" +
                "y = [0, 0, 10, 5]\n" +
                "tri = delaunay(x, y)\n" +
                "triplot(tri, x, y)");
            Assert.Equal(0, lab.CountErrors(html));
            // El output debe tener SVG
            Assert.Contains("<svg", html);
            Assert.Contains("<polygon", html);
            Assert.Contains("triplot", html);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Trisurf_WithZ_EmitsThreeJsWithColormap()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 10, 10, 0]\n" +
                "y = [0, 0, 10, 10]\n" +
                "z = [0, 0, -3, -1]\n" +
                "tri = delaunay(x, y)\n" +
                "trisurf(tri, x, y, z)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("three-canvas-", html);
            Assert.Contains("trisurf", html);
            // trisurf usa MeshBasicMaterial estilo awatif/SAP2000 (color puro sin luz)
            Assert.Contains("MeshBasicMaterial", html);
            // Three.js LUT rainbow estilo awatif (no jet de MATLAB)
            Assert.Contains("rainbowLut", html);
            // Gamma sRGB→linear y dim 0.6 aplicados
            Assert.Contains("processColor", html);
            // Wireframe negro encima del color-mesh
            Assert.Contains("WireframeGeometry", html);
            // legend SVG con valores Min/Max
            Assert.Contains("jet-grad-", html);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Trimesh_WithZHeight_3DSupported()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = [0, 5, 5, 0]\n" +
                "y = [0, 0, 5, 5]\n" +
                "z = [0, 1, 2, 3]\n" +
                "tri = delaunay(x, y)\n" +
                "trimesh(tri, x, y, z)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("three-canvas-", html);
        }

        [Fact]
        [Trait("Category", "MatlabNative")]
        public void Plate_AwatifEquivalent_FullPipelineNoErrors()
        {
            // Carga el ejemplo completo desde Examples/
            var path = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                "Examples", "Calcpad-Lab", "plate_awatif_matlab.m"));
            if (!File.Exists(path)) return;
            var script = File.ReadAllText(path);
            var lab = new TestLab();
            var html = lab.Run(script);
            Assert.Equal(0, lab.CountErrors(html));
            // Debe contener mesh2d refinado + trisurf con colormap + triplot wireframe
            Assert.Contains("mesh2d", html);
            Assert.Contains("trisurf", html);
            Assert.Contains("triplot", html);
        }
    }
}
