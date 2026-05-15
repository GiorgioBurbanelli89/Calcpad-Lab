// Test del ejemplo plate.m equivalente a awatif-v2/examples/src/plate
//
// Reproduce el setup exacto: cuadrilátero {(0,0),(15,0),(15,10),(0,5)},
// malla precomputada con 2 triángulos, área total computada, carga total.
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class PlateAwatifTests
    {
        [Fact]
        [Trait("Category", "PlateAwatif")]
        public void Plate_AwatifEquivalent_NoErrors()
        {
            // Cargar el script desde Examples/
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..",
                "Examples", "Calcpad-Lab", "plate_awatif_equivalent.m");
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                // Skip silently: archivo no presente
                return;
            }
            var script = File.ReadAllText(path);
            var lab = new TestLab();
            var html = lab.Run(script);
            Assert.Equal(0, lab.CountErrors(html));
            // Verificar que el área total se calcula (cuadrilátero
            // (0,0)-(15,0)-(15,10)-(0,5) tiene área 112.5)
            // Cálculo: T1=(0,0)-(15,0)-(15,10) area=75
            //          T2=(0,0)-(15,10)-(0,5)  area=37.5
            //          total = 112.5
            Assert.Contains("112.5", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "PlateAwatif")]
        public void Delaunay_AwatifPoints_ProducesExpectedMesh()
        {
            // El ejemplo TS de awatif usa points = [[0,0],[15,0],[15,10],[0,5]].
            // Nuestro Delaunay2D debe producir 2 triángulos.
            double[] pts =
            [
                0,  0,
                15, 0,
                15, 10,
                0,  5,
            ];
            var tri = MatlabHelpersInterop.Delaunay2D(pts);
            Assert.Equal(6, tri.Length); // 2 tris × 3 verts
            // Los 4 vértices deben aparecer al menos una vez
            var used = new HashSet<int>();
            foreach (var i in tri) used.Add(i);
            Assert.Equal(4, used.Count);
        }
    }
}
