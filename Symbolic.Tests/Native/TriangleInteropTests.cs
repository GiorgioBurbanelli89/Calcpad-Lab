// Tests para Triangle de Shewchuk (wo80 fork) via P/Invoke a triangle.dll.
//
// Reemplaza Bowyer-Watson básico de matlab_helpers.dll con la implementación
// industrial: constrained Delaunay + quality refinement + boundary detection.
// Mismo motor que awatif-v2 usa (vía triangle-wasm).
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class TriangleInteropTests
    {
        // ─────────────────────────────────────────────────────────────────
        // Disponibilidad y versión
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void Dll_IsAvailable()
        {
            Assert.True(TriangleInterop.IsAvailable(),
                "triangle.dll no está disponible — verificar copiado al output");
        }

        [Fact]
        [Trait("Category", "Triangle")]
        public void Version_ReportsName()
        {
            var v = TriangleInterop.GetVersion();
            Assert.Contains("Triangle", v);
        }

        // ─────────────────────────────────────────────────────────────────
        // Casos triviales: Delaunay puro (sin segments, sin quality)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void ThreePoints_OneTriangle()
        {
            double[] pts = [0, 0, 1, 0, 0, 1];
            var m = TriangleInterop.Triangulate(pts);
            Assert.Equal(1, m.NumTriangles);
            Assert.Equal(3, m.NumPoints);
        }

        [Fact]
        [Trait("Category", "Triangle")]
        public void Square_TwoTriangles()
        {
            // Cuadrado unitario → 2 triángulos en Delaunay puro
            double[] pts = [0, 0, 1, 0, 1, 1, 0, 1];
            var m = TriangleInterop.Triangulate(pts);
            Assert.Equal(2, m.NumTriangles);
            Assert.Equal(4, m.NumPoints);
        }

        // ─────────────────────────────────────────────────────────────────
        // Constrained Delaunay con segments (PSLG)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void Polygon_RespectsSegments()
        {
            // L-shape (no convex) — sin segments, Delaunay genera el envolvente
            // convexo. Con segments, debe respetar el contorno L.
            double[] pts =
            [
                0, 0,
                2, 0,
                2, 1,
                1, 1,
                1, 2,
                0, 2,
            ];
            // Segments del contorno L (6 vértices, 6 segments cerrados):
            int[] segs =
            [
                0, 1,
                1, 2,
                2, 3,
                3, 4,
                4, 5,
                5, 0,
            ];
            var m = TriangleInterop.Triangulate(pts, segs);
            // Triangulación de L-shape debe tener ≥ 4 triángulos
            Assert.True(m.NumTriangles >= 4,
                $"L-shape debe tener ≥ 4 triángulos, obtuvo {m.NumTriangles}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Quality refinement (q30 = ángulo min 30°)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void Quality_Q30_RefinesMesh()
        {
            // Triángulo muy "delgado" (un cuadrilátero estirado)
            double[] pts =
            [
                0,   0,
                100, 0,
                100, 1,
                0,   1,
            ];
            int[] segs = [0, 1, 1, 2, 2, 3, 3, 0];
            // Con q30: añade puntos internos para que todos los tris tengan
            // ángulos ≥ 30°. Sin él: solo 2 tris muy alargados.
            var mPlain = TriangleInterop.Triangulate(pts, segs, minAngle: 0);
            var mQuality = TriangleInterop.Triangulate(pts, segs, minAngle: 30);
            Assert.True(mQuality.NumTriangles > mPlain.NumTriangles,
                $"q30 debe refinar más tris ({mQuality.NumTriangles}) vs plain ({mPlain.NumTriangles})");
        }

        // ─────────────────────────────────────────────────────────────────
        // maxArea — control de tamaño de malla
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void MaxArea_LimitsSize()
        {
            // Cuadrado de área 100, maxArea=10 → ≥10 triángulos
            double[] pts =
            [
                0,  0,
                10, 0,
                10, 10,
                0,  10,
            ];
            int[] segs = [0, 1, 1, 2, 2, 3, 3, 0];
            var m = TriangleInterop.Triangulate(pts, segs, minAngle: 30, maxArea: 10);
            Assert.True(m.NumTriangles >= 10,
                $"maxArea=10 sobre área 100 debe dar ≥10 tris, obtuvo {m.NumTriangles}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Detección de boundary nodes
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void BoundaryNodes_DetectedCorrectly()
        {
            // Cuadrado refinado: los 4 vértices iniciales son boundary;
            // puntos interiores no.
            double[] pts = [0, 0, 10, 0, 10, 10, 0, 10];
            int[] segs = [0, 1, 1, 2, 2, 3, 3, 0];
            var m = TriangleInterop.Triangulate(pts, segs, minAngle: 30, maxArea: 5);
            var boundary = m.BoundaryIndices;
            // Los 4 puntos originales (índices 0..3) deben ser boundary
            Assert.Contains(0, boundary);
            Assert.Contains(1, boundary);
            Assert.Contains(2, boundary);
            Assert.Contains(3, boundary);
            // Debe haber puntos interiores (no boundary)
            Assert.True(boundary.Length < m.NumPoints,
                "Tras refinement con maxArea, debe haber puntos interiores");
        }

        // ─────────────────────────────────────────────────────────────────
        // MeshPolygon API (helper estilo awatif)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Triangle")]
        public void MeshPolygon_AwatifEquivalent()
        {
            // Equivalente exacto al ejemplo plate de awatif-v2:
            //   points = [[0,0], [15,0], [15,10], [0,5]]
            //   polygon = [0, 1, 2, 3]
            //   maxMeshSize = 0.5
            // awatif: triangle.triangulate('pzQOq30a0.5', ...)
            double[] pts = [0, 0, 15, 0, 15, 10, 0, 5];
            int[] polygon = [0, 1, 2, 3];
            var m = TriangleInterop.MeshPolygon(pts, polygon, maxMeshSize: 0.5, minAngle: 30);
            // El cuadrilátero tiene área ~112.5, maxArea=0.5 → ≥ 225 tris
            Assert.True(m.NumTriangles >= 200,
                $"Plate awatif con maxArea=0.5: esperaba ≥200 tris, obtuvo {m.NumTriangles}");
            Assert.True(m.NumPoints >= 100);
            // Los 4 vértices originales deben estar en boundary
            var boundary = m.BoundaryIndices;
            Assert.Contains(0, boundary);
        }

        // ─────────────────────────────────────────────────────────────────
        // Performance
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "TrianglePerf")]
        public void LargeMesh_2000Tris_FastEnough()
        {
            // Generar malla de 2000+ triángulos. Triangle es industrial-grade
            // — debería terminar en < 100ms.
            double[] pts = [0, 0, 100, 0, 100, 100, 0, 100];
            int[] polygon = [0, 1, 2, 3];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var m = TriangleInterop.MeshPolygon(pts, polygon, maxMeshSize: 5, minAngle: 30);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Triangle mesh demasiado lento: {sw.ElapsedMilliseconds}ms");
            Assert.True(m.NumTriangles > 1000);
        }
    }
}
