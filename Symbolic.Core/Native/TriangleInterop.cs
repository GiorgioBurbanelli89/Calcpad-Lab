using System;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    /// <summary>
    /// P/Invoke wrapper para <c>triangle.dll</c> — Triangle de Jonathan
    /// Shewchuk (wo80 fork con API moderna context-based).
    ///
    /// Reemplaza el Bowyer-Watson básico (<c>MatlabHelpersInterop.Delaunay2D</c>)
    /// con la implementación industrial de Triangle. Permite:
    ///   - Constrained Delaunay (con bordes forzados / PSLG)
    ///   - Quality mesh refinement (ángulo mínimo &gt; X°)
    ///   - Área máxima por triángulo
    ///   - Detección de boundary nodes (igual que awatif)
    ///
    /// Equivalente al uso típico de awatif:
    ///   triangle.triangulate(<c>'pzQOq30a${maxMeshSize}'</c>, in, out);
    /// </summary>
    public static class TriangleInterop
    {
        private const string DllName = "triangle";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int triangulate_simple(
            [In] double[] pointlist, int n_points,
            [In] int[] segmentlist, int n_segments,
            double min_angle, double max_area,
            [Out] int[] tri_out, int max_tris, out int n_tris_out,
            [Out] double[] pts_out, int max_pts, out int n_pts_out,
            [Out] int[] boundary_out
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr triangle_version_string();

        /// <summary>Versión del DLL Triangle (para diagnóstico).</summary>
        public static string GetVersion()
        {
            try
            {
                var p = triangle_version_string();
                return Marshal.PtrToStringAnsi(p) ?? "unknown";
            }
            catch { return "<dll not loaded>"; }
        }

        /// <summary>Chequea si el DLL está disponible.</summary>
        public static bool IsAvailable()
        {
            try { return triangle_version_string() != IntPtr.Zero; }
            catch { return false; }
        }

        /// <summary>
        /// Resultado de una triangulación: puntos refinados, triángulos,
        /// y boundary flags por punto.
        /// </summary>
        public sealed class MeshResult
        {
            /// <summary>Puntos finales [x0,y0, x1,y1, ...]. Incluye puntos
            /// añadidos por refinement (n ≥ inputPoints).</summary>
            public double[] Points { get; init; }

            /// <summary>Triángulos [t0a,t0b,t0c, t1a,t1b,t1c, ...] — índices
            /// 0-based en Points.</summary>
            public int[] Triangles { get; init; }

            /// <summary>Para cada punto en Points: 1 si está en el borde
            /// del dominio (apoyo posible), 0 si es interior. Mismo significado
            /// que <c>boundaryIndices</c> de awatif.</summary>
            public int[] PointMarkers { get; init; }

            /// <summary>Helper: índices de puntos que están en el borde
            /// (estilo <c>boundaryIndices</c> de awatif).</summary>
            public int[] BoundaryIndices
            {
                get
                {
                    var list = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < PointMarkers.Length; i++)
                        if (PointMarkers[i] != 0) list.Add(i);
                    return list.ToArray();
                }
            }

            /// <summary>Número de triángulos.</summary>
            public int NumTriangles => Triangles.Length / 3;

            /// <summary>Número de puntos (puede ser &gt; input por refinement).</summary>
            public int NumPoints => Points.Length / 2;
        }

        /// <summary>
        /// Triangulación constrained Delaunay con quality refinement.
        ///
        /// <para>Equivalente a awatif:
        /// <c>triangle.triangulate('pzQOq{minAngle}a{maxArea}', ...)</c>.</para>
        /// </summary>
        /// <param name="points">Puntos [x0,y0, x1,y1, ...] de entrada.</param>
        /// <param name="segments">Aristas forzadas [s0a,s0b, s1a,s1b, ...] como
        /// índices 0-based en <paramref name="points"/>. Pasar <c>null</c>
        /// para Delaunay puro (sin constraints).</param>
        /// <param name="minAngle">Ángulo mínimo en grados (típico: 30 para
        /// quality mesh). Pasar 0 para Delaunay puro sin refinement.</param>
        /// <param name="maxArea">Área máxima permitida por triángulo. Pasar 0
        /// para sin límite. Equivalente a <c>a{maxMeshSize}</c> de Shewchuk.</param>
        public static MeshResult Triangulate(
            double[] points,
            int[] segments = null,
            double minAngle = 0.0,
            double maxArea = 0.0)
        {
            if (points == null || points.Length < 6)
                throw new ArgumentException("Need at least 3 points (length 6)");
            if (points.Length % 2 != 0)
                throw new ArgumentException("points length must be even");

            int nPoints = points.Length / 2;
            int nSegments = segments == null ? 0 : segments.Length / 2;

            // Pre-allocar generously: refinement puede multiplicar tris × N.
            // Triangle con quality refinement (minAngle > 0) puede agregar
            // muchos puntos Steiner — la cota teórica es O(área/maxArea * log)
            // pero en práctica reservamos generoso.
            int maxTris = Math.Max(256, 10 * nPoints + 100);
            int maxPts = Math.Max(256, 5 * nPoints + 100);

            // Si hay refinement con maxArea, usamos estimación geométrica.
            if (maxArea > 0.0)
            {
                double xmin = points[0], xmax = points[0];
                double ymin = points[1], ymax = points[1];
                for (int i = 1; i < nPoints; i++)
                {
                    double x = points[2 * i], y = points[2 * i + 1];
                    if (x < xmin) xmin = x;
                    if (x > xmax) xmax = x;
                    if (y < ymin) ymin = y;
                    if (y > ymax) ymax = y;
                }
                double bboxArea = (xmax - xmin) * (ymax - ymin);
                int estimatedTris = (int)(4.0 * bboxArea / maxArea) + 256;
                if (estimatedTris > maxTris) maxTris = estimatedTris;
                if (estimatedTris > maxPts) maxPts = estimatedTris;
            }
            // Si hay refinement por ángulo SIN maxArea, no podemos estimar a
            // priori — reservamos buffer muy generoso para evitar -1 retorno.
            else if (minAngle > 0.0)
            {
                // Cota empírica: hasta 50× los puntos originales en geometrías
                // muy delgadas (aspect ratio extremo).
                maxTris = Math.Max(maxTris, 200 * nPoints + 1024);
                maxPts = Math.Max(maxPts, 100 * nPoints + 1024);
            }

            var triOut = new int[3 * maxTris];
            var ptsOut = new double[2 * maxPts];
            var boundaryOut = new int[maxPts];

            int rc = triangulate_simple(
                points, nPoints,
                segments ?? Array.Empty<int>(), nSegments,
                minAngle, maxArea,
                triOut, maxTris, out int nTris,
                ptsOut, maxPts, out int nPts,
                boundaryOut);

            if (rc == -1)
                throw new InvalidOperationException(
                    $"Triangle: capacidad insuficiente (maxTris={maxTris}, " +
                    $"maxPts={maxPts}). Subir maxArea o usar menos refinement.");
            if (rc != 0)
                throw new InvalidOperationException($"Triangle failed with code {rc}");

            // Trim arrays al tamaño real
            var trisResult = new int[3 * nTris];
            Array.Copy(triOut, trisResult, 3 * nTris);
            var ptsResult = new double[2 * nPts];
            Array.Copy(ptsOut, ptsResult, 2 * nPts);
            var markersResult = new int[nPts];
            Array.Copy(boundaryOut, markersResult, nPts);

            return new MeshResult
            {
                Points = ptsResult,
                Triangles = trisResult,
                PointMarkers = markersResult,
            };
        }

        /// <summary>
        /// Versión simplificada estilo awatif: pasar puntos + polígono (índices)
        /// y obtener mesh con quality refinement.
        ///
        /// El <paramref name="polygon"/> es una lista de índices 0-based en
        /// <paramref name="points"/> que define el borde cerrado. Se convierten
        /// automáticamente en segments para Triangle.
        /// </summary>
        public static MeshResult MeshPolygon(
            double[] points,
            int[] polygon,
            double maxMeshSize = 0.0,
            double minAngle = 30.0)
        {
            if (polygon == null || polygon.Length < 3)
                throw new ArgumentException("polygon needs at least 3 vertices");
            // Convertir polygon a segments: cada par (poly[i], poly[i+1])
            int nPoly = polygon.Length;
            var segments = new int[2 * nPoly];
            for (int i = 0; i < nPoly; i++)
            {
                segments[2 * i] = polygon[i];
                segments[2 * i + 1] = polygon[(i + 1) % nPoly];
            }
            return Triangulate(points, segments, minAngle, maxMeshSize);
        }
    }
}
