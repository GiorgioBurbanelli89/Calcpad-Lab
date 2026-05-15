// =============================================================================
// Calcpad Lab — MATLAB Plot functions (Plotly.js HTML embed, sin Calcpad)
// =============================================================================
//   surf / contourf / contour / imagesc / mesh / pcolor / plot / plot3
//   Emiten HTML con un <div> Plotly inline. No usan EmitSvgHeatmap de Calcpad.
//   El usuario sólo ve sintaxis MATLAB y HTML/Plotly final.
// =============================================================================
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core.Matlab
{
    internal static class MatlabPlots
    {
        private static int _plotCounter = 0;
        /// <summary>ID del último plot emitido (para title/xlabel/etc. post-hoc).</summary>
        public static int LastPlotId => _plotCounter;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── Acumulador de figura ─────────────────────────────────────────────
        // Permite acumular múltiples patch/line/text en UN solo plot Plotly,
        // hasta que se cierre la figura (con figure() nuevo, saveas o end).
        // Además mantiene representación intermedia para export SVG.
        public sealed class FigPrim
        {
            public string Kind;          // "patch2d", "line2d", "text2d", "mesh3d"
            public double[] Xs, Ys, Zs;
            public string FaceColor, EdgeColor, Color, Text;
            public double FaceAlpha = 1, LineWidth = 1, FontSize = 11;
            public int[] FaceI, FaceJ, FaceK;
            public bool IsRgb;
            public int Rgb_R, Rgb_G, Rgb_B;
        }
        private static System.Collections.Generic.List<FigPrim> _figPrims = null;
        private static System.Collections.Generic.List<string> _figTraces = null;
        private static System.Collections.Generic.List<string> _figAnnotations = null;
        private static int _figId = 0;
        private static bool _figIs3D = false;
        private static string _figTitle = "";
        private static string _figXLabel = null, _figYLabel = null, _figZLabel = null;
        private static double? _figXMin, _figXMax, _figYMin, _figYMax;
        public static bool HasOpenFigure => _figTraces != null;

        /// <summary>Comienza nueva figura. Devuelve el HTML del anterior figura (si la había) para emitirlo.</summary>
        public static string BeginFigure()
        {
            string prev = FinishFigure();
            _figTraces = new System.Collections.Generic.List<string>();
            _figAnnotations = new System.Collections.Generic.List<string>();
            _figPrims = new System.Collections.Generic.List<FigPrim>();
            _figId = ++_plotCounter;
            _figIs3D = false;
            _figTitle = "";
            _figXLabel = null; _figYLabel = null; _figZLabel = null;
            _figXMin = null; _figXMax = null; _figYMin = null; _figYMax = null;
            return prev;
        }
        /// <summary>Cierra figura abierta y devuelve su HTML.</summary>
        public static string FinishFigure()
        {
            if (_figTraces == null || _figTraces.Count == 0)
            {
                _figTraces = null; _figAnnotations = null;
                return "";
            }
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{_figId}\" class=\"matlab-plot\" style=\"width:720px;height:560px\"></div>\n");
            sb.Append("<script>(function() {\n  var data = [\n");
            for (int i = 0; i < _figTraces.Count; i++)
            {
                sb.Append("    ").Append(_figTraces[i]);
                if (i < _figTraces.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("  ];\n  var layout = { ");
            sb.Append($"title: '{EscapeJs(_figTitle)}', margin:{{l:50,r:30,t:40,b:50}}");
            if (_figIs3D)
            {
                sb.Append(", scene: { ");
                sb.Append($"xaxis:{{title:'{EscapeJs(_figXLabel ?? "x")}'}},");
                sb.Append($"yaxis:{{title:'{EscapeJs(_figYLabel ?? "y")}'}},");
                sb.Append($"zaxis:{{title:'{EscapeJs(_figZLabel ?? "z")}'}}");
                sb.Append(" }");
            }
            else
            {
                if (_figXLabel != null) sb.Append($", xaxis:{{title:'{EscapeJs(_figXLabel)}'");
                else sb.Append(", xaxis:{");
                if (_figXMin.HasValue) sb.Append($", range:[{_figXMin.Value.ToString(Inv)}, {_figXMax.Value.ToString(Inv)}]");
                sb.Append("}");
                if (_figYLabel != null) sb.Append($", yaxis:{{title:'{EscapeJs(_figYLabel)}'");
                else sb.Append(", yaxis:{");
                if (_figYMin.HasValue) sb.Append($", range:[{_figYMin.Value.ToString(Inv)}, {_figYMax.Value.ToString(Inv)}]");
                sb.Append(", scaleanchor:'x', scaleratio:1");
                sb.Append("}");
            }
            // annotations
            if (_figAnnotations.Count > 0)
            {
                sb.Append(", annotations:[");
                for (int i = 0; i < _figAnnotations.Count; i++)
                {
                    sb.Append(_figAnnotations[i]);
                    if (i < _figAnnotations.Count - 1) sb.Append(",");
                }
                sb.Append("]");
            }
            sb.Append(" };\n  Plotly.newPlot('matlab_plot_").Append(_figId)
              .Append("', data, layout, {responsive:true});\n})();</script>\n");
            _figTraces = null; _figAnnotations = null;
            return sb.ToString();
        }
        public static void AddTrace(string traceJson) {
            if (_figTraces == null) BeginFigure();
            _figTraces.Add(traceJson);
        }
        public static void AddAnnotation(string annJson) {
            if (_figAnnotations == null) BeginFigure();
            _figAnnotations.Add(annJson);
        }
        public static void SetFigure3D(bool is3d) { if (_figTraces != null) _figIs3D = is3d; }
        public static void SetFigTitle(string t) { _figTitle = t ?? ""; }
        public static void SetFigXLabel(string s) { _figXLabel = s; }
        public static void SetFigYLabel(string s) { _figYLabel = s; }
        public static void SetFigZLabel(string s) { _figZLabel = s; }
        private static string EscapeJs(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
        public static string Csv(MValue v)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < v.Data.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(v.Data[i].ToString("G", Inv));
            }
            return sb.ToString();
        }
        public static string Csv(double[] arr)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(arr[i].ToString("G", Inv));
            }
            return sb.ToString();
        }

        /// <summary>Mesh 2D/3D con conectividad triangular + CData nodal o por elemento.</summary>
        /// <param name="faces">Conectividad nF×3 (índices 1-based).</param>
        /// <param name="verts">Coordenadas nV×2 ó nV×3.</param>
        /// <param name="cdata">CData por nodo (length nV) o por elemento (length nF) o null.</param>
        /// <param name="colorMode">"interp" (nodal), "flat" (por elemento), o "uniform" (color sólido).</param>
        public static void PatchMesh(MValue faces, MValue verts, MValue cdata, string colorMode,
                                      string faceColor, string edgeColor, double faceAlpha, double lineWidth,
                                      string colormap)
        {
            int nF = faces.Rows;
            int nV = verts.Rows;
            bool is3D = verts.Cols >= 3;
            // Construir arrays x, y, z
            var xArr = new double[nV];
            var yArr = new double[nV];
            var zArr = new double[nV];
            for (int i = 0; i < nV; i++)
            {
                xArr[i] = verts.At(i, 0);
                yArr[i] = verts.At(i, 1);
                zArr[i] = is3D ? verts.At(i, 2) : 0.0;
            }
            // Construir índices i, j, k (0-based para Plotly)
            var iArr = new int[nF];
            var jArr = new int[nF];
            var kArr = new int[nF];
            for (int f = 0; f < nF; f++)
            {
                iArr[f] = (int)faces.At(f, 0) - 1;
                jArr[f] = (int)faces.At(f, 1) - 1;
                kArr[f] = (int)faces.At(f, 2) - 1;
            }
            var sb = new StringBuilder();
            sb.Append("{type:'mesh3d'");
            sb.Append($", x:[{Csv(xArr)}]");
            sb.Append($", y:[{Csv(yArr)}]");
            sb.Append($", z:[{Csv(zArr)}]");
            sb.Append($", i:[{IntCsv(iArr)}]");
            sb.Append($", j:[{IntCsv(jArr)}]");
            sb.Append($", k:[{IntCsv(kArr)}]");
            sb.Append($", opacity:{faceAlpha.ToString(Inv)}");
            sb.Append($", flatshading:{(colorMode == "flat" ? "true" : "false")}");
            // Color: 3 modos
            if (cdata != null && (colorMode == "interp" || colorMode == "flat"))
            {
                if (colorMode == "flat" && cdata.Data.Length == nF)
                {
                    // Color por elemento: usar facecolor con rgb strings derivados de cdata
                    sb.Append(", facecolor:[");
                    double cmin = double.MaxValue, cmax = double.MinValue;
                    for (int f = 0; f < nF; f++)
                    {
                        if (cdata.Data[f] < cmin) cmin = cdata.Data[f];
                        if (cdata.Data[f] > cmax) cmax = cdata.Data[f];
                    }
                    double rng = (cmax - cmin) > 1e-12 ? cmax - cmin : 1;
                    for (int f = 0; f < nF; f++)
                    {
                        if (f > 0) sb.Append(",");
                        double tc = (cdata.Data[f] - cmin) / rng;   // [0,1]
                        sb.Append("'").Append(ColorscaleSampleRgb(colormap, tc)).Append("'");
                    }
                    sb.Append("]");
                }
                else
                {
                    sb.Append($", intensity:[{Csv(cdata)}]");
                    sb.Append($", intensitymode:'{(colorMode == "flat" ? "cell" : "vertex")}'");
                    sb.Append($", colorscale:'{ColormapToPlotly(colormap)}'");
                    sb.Append(", showscale:true");
                }
            }
            else
            {
                sb.Append($", color:'{faceColor}'");
            }
            // Lighting realista (Plotly mesh3d acepta esto)
            sb.Append(", lighting:{ambient:0.6, diffuse:0.8, specular:0.2, roughness:0.5, fresnel:0.2}");
            sb.Append(", lightposition:{x:200, y:200, z:200}");
            sb.Append("}");
            AddTrace(sb.ToString());
            // Edges (wireframe) si edgeColor distinto a 'none'
            if (edgeColor != "none" && lineWidth > 0)
            {
                // Emitir como scatter3d/scatter de aristas
                EmitMeshEdges(xArr, yArr, zArr, iArr, jArr, kArr, edgeColor, lineWidth, is3D);
            }
            if (is3D) _figIs3D = true;
        }
        private static string IntCsv(int[] arr)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(arr[i]);
            }
            return sb.ToString();
        }
        private static void EmitMeshEdges(double[] x, double[] y, double[] z,
                                           int[] iIdx, int[] jIdx, int[] kIdx,
                                           string color, double lw, bool is3D)
        {
            // Construir lista de aristas únicas
            var edges = new System.Collections.Generic.HashSet<(int, int)>();
            for (int f = 0; f < iIdx.Length; f++)
            {
                AddEdge(edges, iIdx[f], jIdx[f]);
                AddEdge(edges, jIdx[f], kIdx[f]);
                AddEdge(edges, kIdx[f], iIdx[f]);
            }
            // Para Plotly: emitir como scatter3d con cada arista separada por NaN
            var xLines = new System.Collections.Generic.List<double>();
            var yLines = new System.Collections.Generic.List<double>();
            var zLines = new System.Collections.Generic.List<double>();
            foreach (var (u, v) in edges)
            {
                xLines.Add(x[u]); xLines.Add(x[v]); xLines.Add(double.NaN);
                yLines.Add(y[u]); yLines.Add(y[v]); yLines.Add(double.NaN);
                zLines.Add(z[u]); zLines.Add(z[v]); zLines.Add(double.NaN);
            }
            var sb = new StringBuilder();
            sb.Append(is3D ? "{type:'scatter3d', mode:'lines'" : "{type:'scatter', mode:'lines'");
            sb.Append($", line:{{color:'{color}', width:{lw.ToString(Inv)}}}");
            sb.Append($", x:[{CsvNaN(xLines)}]");
            sb.Append($", y:[{CsvNaN(yLines)}]");
            if (is3D) sb.Append($", z:[{CsvNaN(zLines)}]");
            sb.Append(", showlegend:false, hoverinfo:'skip'}");
            AddTrace(sb.ToString());
        }
        private static void AddEdge(System.Collections.Generic.HashSet<(int, int)> edges, int u, int v)
        {
            if (u > v) (u, v) = (v, u);
            edges.Add((u, v));
        }
        private static string CsvNaN(System.Collections.Generic.List<double> arr)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Count; i++)
            {
                if (i > 0) sb.Append(",");
                if (double.IsNaN(arr[i])) sb.Append("null");
                else sb.Append(arr[i].ToString("G", Inv));
            }
            return sb.ToString();
        }

        /// <summary>Patch 2D simple — polígono cerrado con relleno.</summary>
        public static void Patch2D(double[] xs, double[] ys, string faceColor, string edgeColor,
                                    double faceAlpha, double lineWidth)
        {
            var sb = new StringBuilder();
            sb.Append("{type:'scatter', mode:'lines', fill:'toself'");
            sb.Append($", fillcolor:'{faceColor}'");
            sb.Append($", opacity:{faceAlpha.ToString(Inv)}");
            sb.Append($", line:{{color:'{edgeColor}', width:{lineWidth.ToString(Inv)}}}");
            sb.Append($", x:[{Csv(xs)},{xs[0].ToString("G",Inv)}]");
            sb.Append($", y:[{Csv(ys)},{ys[0].ToString("G",Inv)}]");
            sb.Append(", showlegend:false, hoverinfo:'skip'}");
            AddTrace(sb.ToString());
            if (_figPrims != null) _figPrims.Add(new FigPrim{
                Kind="patch2d", Xs=(double[])xs.Clone(), Ys=(double[])ys.Clone(),
                FaceColor=faceColor, EdgeColor=edgeColor, FaceAlpha=faceAlpha, LineWidth=lineWidth
            });
        }
        public static void Line2D(double[] xs, double[] ys, string color, double lineWidth)
        {
            var sb = new StringBuilder();
            sb.Append("{type:'scatter', mode:'lines'");
            sb.Append($", line:{{color:'{color}', width:{lineWidth.ToString(Inv)}}}");
            sb.Append($", x:[{Csv(xs)}], y:[{Csv(ys)}]");
            sb.Append(", showlegend:false, hoverinfo:'skip'}");
            AddTrace(sb.ToString());
            if (_figPrims != null) _figPrims.Add(new FigPrim{
                Kind="line2d", Xs=(double[])xs.Clone(), Ys=(double[])ys.Clone(),
                Color=color, LineWidth=lineWidth
            });
        }
        public static void Text2D(double x, double y, string text, string color, double fontSize)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"x:{x.ToString(Inv)}, y:{y.ToString(Inv)}, ");
            sb.Append($"text:'{EscapeJs(text)}', ");
            sb.Append($"font:{{color:'{color}', size:{fontSize.ToString(Inv)}}}, ");
            sb.Append("showarrow:false");
            sb.Append("}");
            AddAnnotation(sb.ToString());
            if (_figPrims != null) _figPrims.Add(new FigPrim{
                Kind="text2d", Xs=new[]{x}, Ys=new[]{y}, Text=text, Color=color, FontSize=fontSize
            });
        }

        /// <summary>Exporta la figura actual como SVG standalone (sólo primitives 2D).</summary>
        public static string ExportSvg(int width = 800, int height = 800)
        {
            if (_figPrims == null || _figPrims.Count == 0) return null;
            // Calcular bounding box
            double xmin = double.MaxValue, xmax = double.MinValue;
            double ymin = double.MaxValue, ymax = double.MinValue;
            foreach (var p in _figPrims)
            {
                if (p.Xs == null) continue;
                foreach (var x in p.Xs) { if (x < xmin) xmin = x; if (x > xmax) xmax = x; }
                foreach (var y in p.Ys) { if (y < ymin) ymin = y; if (y > ymax) ymax = y; }
            }
            if (xmax - xmin < 1e-9) { xmax = xmin + 1; }
            if (ymax - ymin < 1e-9) { ymax = ymin + 1; }
            double pad = 0.05;
            double dx = xmax - xmin, dy = ymax - ymin;
            xmin -= dx * pad; xmax += dx * pad;
            ymin -= dy * pad; ymax += dy * pad;
            dx = xmax - xmin; dy = ymax - ymin;
            // Margenes para ejes/labels
            int marginL = 60, marginR = 30, marginT = 50, marginB = 60;
            int plotW = width - marginL - marginR;
            int plotH = height - marginT - marginB;
            double sx = plotW / dx, sy = plotH / dy;
            double TX(double x) => marginL + (x - xmin) * sx;
            double TY(double y) => height - marginB - (y - ymin) * sy;   // Y invertida (SVG top-left)

            var svg = new StringBuilder();
            svg.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
            svg.AppendLine($"  <rect x='0' y='0' width='{width}' height='{height}' fill='white'/>");
            // Plot area
            svg.AppendLine($"  <rect x='{marginL}' y='{marginT}' width='{plotW}' height='{plotH}' fill='none' stroke='#ccc'/>");
            // Title
            if (!string.IsNullOrEmpty(_figTitle))
                svg.AppendLine($"  <text x='{width/2}' y='25' text-anchor='middle' font-family='sans-serif' font-size='14' font-weight='bold'>{EscapeXml(_figTitle)}</text>");
            // X label
            if (!string.IsNullOrEmpty(_figXLabel))
                svg.AppendLine($"  <text x='{marginL + plotW/2}' y='{height-15}' text-anchor='middle' font-family='sans-serif' font-size='12'>{EscapeXml(_figXLabel)}</text>");
            // Y label
            if (!string.IsNullOrEmpty(_figYLabel))
                svg.AppendLine($"  <text x='15' y='{marginT + plotH/2}' text-anchor='middle' font-family='sans-serif' font-size='12' transform='rotate(-90 15 {marginT + plotH/2})'>{EscapeXml(_figYLabel)}</text>");
            // Tick marks simples cada 5 divisions
            for (int t = 0; t <= 5; t++)
            {
                double xv = xmin + dx * t / 5.0;
                double yv = ymin + dy * t / 5.0;
                double tx = TX(xv); double ty = TY(yv);
                svg.AppendLine($"  <text x='{tx}' y='{height-marginB+15}' text-anchor='middle' font-family='sans-serif' font-size='10'>{xv.ToString("G3", Inv)}</text>");
                svg.AppendLine($"  <text x='{marginL-5}' y='{ty+4}' text-anchor='end' font-family='sans-serif' font-size='10'>{yv.ToString("G3", Inv)}</text>");
            }
            // Clip path para plot area
            svg.AppendLine($"  <defs><clipPath id='plot'><rect x='{marginL}' y='{marginT}' width='{plotW}' height='{plotH}'/></clipPath></defs>");
            svg.AppendLine($"  <g clip-path='url(#plot)'>");
            // Render primitives
            foreach (var p in _figPrims)
            {
                if (p.Kind == "patch2d" && p.Xs.Length > 0)
                {
                    var pts = new StringBuilder();
                    for (int i = 0; i < p.Xs.Length; i++)
                    {
                        if (i > 0) pts.Append(" ");
                        pts.Append(TX(p.Xs[i]).ToString("F2", Inv));
                        pts.Append(",");
                        pts.Append(TY(p.Ys[i]).ToString("F2", Inv));
                    }
                    svg.AppendLine($"    <polygon points='{pts}' fill='{p.FaceColor}' fill-opacity='{p.FaceAlpha.ToString(Inv)}' stroke='{p.EdgeColor}' stroke-width='{p.LineWidth.ToString(Inv)}'/>");
                }
                else if (p.Kind == "line2d" && p.Xs.Length >= 2)
                {
                    var pts = new StringBuilder();
                    for (int i = 0; i < p.Xs.Length; i++)
                    {
                        if (i > 0) pts.Append(" ");
                        pts.Append(TX(p.Xs[i]).ToString("F2", Inv));
                        pts.Append(",");
                        pts.Append(TY(p.Ys[i]).ToString("F2", Inv));
                    }
                    svg.AppendLine($"    <polyline points='{pts}' fill='none' stroke='{p.Color}' stroke-width='{p.LineWidth.ToString(Inv)}'/>");
                }
                else if (p.Kind == "text2d")
                {
                    double tx = TX(p.Xs[0]); double ty = TY(p.Ys[0]);
                    svg.AppendLine($"    <text x='{tx.ToString("F2", Inv)}' y='{ty.ToString("F2", Inv)}' fill='{p.Color}' font-family='sans-serif' font-size='{p.FontSize.ToString(Inv)}' text-anchor='middle'>{EscapeXml(p.Text)}</text>");
                }
            }
            svg.AppendLine($"  </g>");
            svg.AppendLine("</svg>");
            return svg.ToString();
        }
        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("'","&apos;").Replace("\"","&quot;");
        }

        /// <summary>Genera el <div> Plotly para una superficie 3D.</summary>
        public static string Surf(MValue X, MValue Y, MValue Z, string colormap = "viridis", string title = "surf")
        {
            ValidateGrid(X, Y, Z);
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{\n");
            sb.Append($"    type: 'surface', colorscale: '{ColormapToPlotly(colormap)}',\n");
            sb.Append($"    x: {EmitMatrixJs(X)},\n");
            sb.Append($"    y: {EmitMatrixJs(Y)},\n");
            sb.Append($"    z: {EmitMatrixJs(Z)}\n");
            sb.Append($"  }}];\n");
            sb.Append($"  var layout = {{ title: '{title}', margin: {{l:40,r:40,t:40,b:40}}, scene: {{xaxis:{{title:'X'}}, yaxis:{{title:'Y'}}, zaxis:{{title:'Z'}}}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        /// <summary>Contour filled — heatmap 2D con isolíneas.</summary>
        public static string Contourf(MValue X, MValue Y, MValue Z, int nLevels = 10, string colormap = "viridis")
        {
            ValidateGrid(X, Y, Z);
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{\n");
            sb.Append($"    type: 'contour', colorscale: '{ColormapToPlotly(colormap)}', ncontours: {nLevels}, contours: {{coloring: 'fill'}},\n");
            sb.Append($"    x: {EmitRowJs(X, true)},\n");
            sb.Append($"    y: {EmitColJs(Y)},\n");
            sb.Append($"    z: {EmitMatrixJs(Z)}\n");
            sb.Append($"  }}];\n");
            sb.Append($"  var layout = {{ title: 'contourf', margin:{{l:40,r:40,t:40,b:40}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Imagesc(MValue Z, string colormap = "viridis")
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'heatmap', colorscale: '{ColormapToPlotly(colormap)}', z: {EmitMatrixJs(Z)} }}];\n");
            sb.Append($"  var layout = {{ title: 'imagesc', margin:{{l:40,r:40,t:40,b:40}}, yaxis: {{autorange:'reversed'}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Plot(MValue X, MValue Y, string label = null)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:400px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatter', mode: 'lines',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)} }}];\n");
            sb.Append($"  var layout = {{ title: '{label ?? "plot"}', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Plot3(MValue X, MValue Y, MValue Z)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatter3d', mode: 'lines',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, z: {EmitVecJs(Z)} }}];\n");
            sb.Append($"  var layout = {{ title: 'plot3', margin:{{l:0,r:0,t:40,b:0}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        /// <summary>Spy plot — visualiza el pattern de sparsity de una matriz.</summary>
        public static string Spy(MValue M)
        {
            int id = ++_plotCounter;
            int nr = M.Rows, nc = M.Cols;
            var xs = new System.Collections.Generic.List<int>();
            var ys = new System.Collections.Generic.List<int>();
            int nzCount = 0;
            for (int i = 0; i < nr; i++)
                for (int j = 0; j < nc; j++)
                    if (M.At(i, j) != 0) { xs.Add(j + 1); ys.Add(nr - i); nzCount++; }
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:520px;height:520px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatter', mode: 'markers',\n");
            sb.Append($"    x: [{string.Join(",", xs)}],\n");
            sb.Append($"    y: [{string.Join(",", ys)}],\n");
            sb.Append($"    marker: {{ symbol: 'square', size: 8, color: '#1a4f8a' }} }}];\n");
            sb.Append($"  var layout = {{ title: 'spy ({nr}×{nc}, nnz={nzCount})',\n");
            sb.Append($"    xaxis: {{ range:[0, {nc + 1}], title:'col' }},\n");
            sb.Append($"    yaxis: {{ range:[0, {nr + 1}], title:'row', scaleanchor:'x', scaleratio:1 }},\n");
            sb.Append($"    margin: {{l:50, r:30, t:40, b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Bar(MValue X, MValue Y, bool horizontal = false)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:400px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'bar', orientation: '{(horizontal?'h':'v')}',\n");
            sb.Append($"    x: {EmitVecJs(horizontal ? Y : X)}, y: {EmitVecJs(horizontal ? X : Y)} }}];\n");
            sb.Append($"  var layout = {{ title: 'bar', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Scatter(MValue X, MValue Y)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:400px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatter', mode: 'markers',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)} }}];\n");
            sb.Append($"  var layout = {{ title: 'scatter', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Scatter3(MValue X, MValue Y, MValue Z)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatter3d', mode: 'markers',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, z: {EmitVecJs(Z)} }}];\n");
            sb.Append($"  var layout = {{ title: 'scatter3', margin:{{l:0,r:0,t:40,b:0}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Histogram2D(MValue X, MValue Y, int nBins = 20)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'histogram2d', colorscale: 'Viridis',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, nbinsx: {nBins}, nbinsy: {nBins} }}];\n");
            sb.Append($"  var layout = {{ title: 'histogram2', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Heatmap(MValue Z, string colormap = "viridis")
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'heatmap', colorscale: '{ColormapToPlotly(colormap)}', z: {EmitMatrixJs(Z)} }}];\n");
            sb.Append($"  var layout = {{ title: 'heatmap', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Stem(MValue X, MValue Y)
        {
            int id = ++_plotCounter;
            int n = X.Data.Length;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:400px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append("  var data = [\n");
            // Líneas verticales como segmentos
            sb.Append("    {type:'scatter', mode:'lines', x:[],y:[],line:{color:'#333',width:1},showlegend:false,name:''}");
            for (int i = 0; i < n; i++) { /* puntos uno por uno */ }
            // En vez de bucles complejos, usamos 'stem' approach: scatter + bar
            sb.Append($",\n    {{type:'bar', x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, width: 0.05, marker:{{color:'#333'}}, name:'stem'}},");
            sb.Append($"\n    {{type:'scatter', mode:'markers', x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, marker:{{size:8, color:'#1a4f8a'}}, name:'samples'}}");
            sb.Append("\n  ];\n");
            sb.Append("  var layout = { title: 'stem', margin:{l:50,r:30,t:40,b:50} };\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Histogram(MValue X, int nBins = 20)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:400px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'histogram', x: {EmitVecJs(X)}, nbinsx: {nBins} }}];\n");
            sb.Append($"  var layout = {{ title: 'histogram', margin:{{l:50,r:30,t:40,b:50}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Polar(MValue Theta, MValue R)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:500px;height:500px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append($"  var data = [{{ type: 'scatterpolar', mode: 'lines',\n");
            sb.Append($"    theta: [");
            for (int i = 0; i < Theta.Data.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append((Theta.Data[i] * 180.0 / Math.PI).ToString("G6", Inv));
            }
            sb.Append("],\n");
            sb.Append($"    r: {EmitVecJs(R)} }}];\n");
            sb.Append($"  var layout = {{ title: 'polar', margin:{{l:40,r:40,t:40,b:40}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Quiver(MValue X, MValue Y, MValue U, MValue V)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            // Plotly no tiene quiver nativo — emulamos con líneas + arrowheads
            sb.Append("  var arrows = []; var lines = [];\n");
            sb.Append($"  var xs = {EmitVecJs(X)}, ys = {EmitVecJs(Y)}, us = {EmitVecJs(U)}, vs = {EmitVecJs(V)};\n");
            sb.Append("  var scale = 0.5;\n");
            sb.Append("  for (var i = 0; i < xs.length; i++) {\n");
            sb.Append("    arrows.push({ x: xs[i]+us[i]*scale, y: ys[i]+vs[i]*scale, ax: xs[i], ay: ys[i],\n");
            sb.Append("                  xref:'x', yref:'y', axref:'x', ayref:'y', showarrow:true, arrowhead:2, arrowsize:1 });\n");
            sb.Append("  }\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', [{{x:xs, y:ys, mode:'markers', type:'scatter', marker:{{size:2}}}}],\n");
            sb.Append("    { title: 'quiver', annotations: arrows, margin:{l:40,r:40,t:40,b:40} }, {responsive:true});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        /// <summary>Bode plot — dos paneles: magnitud (dB) y fase (deg) vs ω en escala log.</summary>
        public static string BodeDual(double[] w, double[] magDb, double[] phaseDeg)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:680px;height:560px\"></div>\n");
            sb.Append("<script>(function() {\n");
            sb.Append("  var data = [\n");
            sb.Append($"    {{x: [{string.Join(",", w.Select(x => x.ToString("G6", Inv)))}], y: [{string.Join(",", magDb.Select(x => x.ToString("G6", Inv)))}], type:'scatter', mode:'lines', name:'Magnitude (dB)', xaxis:'x1', yaxis:'y1'}},\n");
            sb.Append($"    {{x: [{string.Join(",", w.Select(x => x.ToString("G6", Inv)))}], y: [{string.Join(",", phaseDeg.Select(x => x.ToString("G6", Inv)))}], type:'scatter', mode:'lines', name:'Phase (deg)', xaxis:'x2', yaxis:'y2'}}\n");
            sb.Append("  ];\n");
            sb.Append("  var layout = {\n");
            sb.Append("    title: 'Bode Diagram',\n");
            sb.Append("    grid: {rows:2, columns:1, pattern:'independent'},\n");
            sb.Append("    xaxis:  {type:'log', title:'ω [rad/s]'},\n");
            sb.Append("    yaxis:  {title:'Magnitude (dB)'},\n");
            sb.Append("    xaxis2: {type:'log', title:'ω [rad/s]'},\n");
            sb.Append("    yaxis2: {title:'Phase (deg)'},\n");
            sb.Append("    margin: {l:60, r:30, t:50, b:50}\n");
            sb.Append("  };\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static string Quiver3(MValue X, MValue Y, MValue Z, MValue U, MValue V, MValue W)
        {
            int id = ++_plotCounter;
            int n = X.Data.Length;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            // Plotly cone trace
            sb.Append($"  var data = [{{ type: 'cone', sizemode: 'scaled', sizeref: 0.5,\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, z: {EmitVecJs(Z)},\n");
            sb.Append($"    u: {EmitVecJs(U)}, v: {EmitVecJs(V)}, w: {EmitVecJs(W)} }}];\n");
            sb.Append($"  var layout = {{ title: 'quiver3', margin: {{l:0,r:0,t:40,b:0}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }
        public static string Slice(MValue X, MValue Y, MValue Z, MValue V, double[] xPlanes, double[] yPlanes, double[] zPlanes)
        {
            int id = ++_plotCounter;
            var sb = new StringBuilder();
            sb.Append($"<div id=\"matlab_plot_{id}\" class=\"matlab-plot\" style=\"width:640px;height:480px\"></div>\n");
            sb.Append("<script>(function() {\n");
            // Plotly isosurface o volume — usamos volume con planos como slices
            sb.Append($"  var data = [{{ type: 'volume',\n");
            sb.Append($"    x: {EmitVecJs(X)}, y: {EmitVecJs(Y)}, z: {EmitVecJs(Z)},\n");
            sb.Append($"    value: {EmitVecJs(V)},\n");
            sb.Append($"    isomin: {V.Data[0].ToString("G", Inv)}, isomax: {V.Data[V.Data.Length - 1].ToString("G", Inv)},\n");
            sb.Append($"    opacity: 0.4, surface_count: 5 }}];\n");
            sb.Append($"  var layout = {{ title: 'slice', margin: {{l:0,r:0,t:40,b:0}} }};\n");
            sb.Append($"  Plotly.newPlot('matlab_plot_{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();</script>\n");
            return sb.ToString();
        }

        public static MValue Peaks(double[] xVec, double[] yVec)
        {
            int Nx = xVec.Length, Ny = yVec.Length;
            var Z = new MValue(Ny, Nx);
            for (int i = 0; i < Ny; i++)
            {
                double y = yVec[i];
                for (int j = 0; j < Nx; j++)
                {
                    double x = xVec[j];
                    double t1 = 3.0 * (1 - x) * (1 - x) * Math.Exp(-x*x - (y+1)*(y+1));
                    double t2 = -10.0 * (x/5 - x*x*x - Math.Pow(y, 5)) * Math.Exp(-x*x - y*y);
                    double t3 = -(1.0/3.0) * Math.Exp(-(x+1)*(x+1) - y*y);
                    Z.Set(i, j, t1 + t2 + t3);
                }
            }
            return Z;
        }

        public static MValue PeaksFromGrid(MValue X, MValue Y)
        {
            if (X.Rows != Y.Rows || X.Cols != Y.Cols)
                throw new MatlabRuntimeException($"peaks(X,Y): X {X.Rows}×{X.Cols} ≠ Y {Y.Rows}×{Y.Cols}");
            int rows = X.Rows, cols = X.Cols;
            var Z = new MValue(rows, cols);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    double x = X.At(i, j), y = Y.At(i, j);
                    double t1 = 3.0 * (1 - x) * (1 - x) * Math.Exp(-x*x - (y+1)*(y+1));
                    double t2 = -10.0 * (x/5 - x*x*x - Math.Pow(y, 5)) * Math.Exp(-x*x - y*y);
                    double t3 = -(1.0/3.0) * Math.Exp(-(x+1)*(x+1) - y*y);
                    Z.Set(i, j, t1 + t2 + t3);
                }
            return Z;
        }

        // ─── Helpers ────────────────────────────────────────────────────────
        private static void ValidateGrid(MValue X, MValue Y, MValue Z)
        {
            if (X.Rows != Z.Rows || X.Cols != Z.Cols)
                throw new MatlabRuntimeException($"surf/contourf: X {X.Rows}×{X.Cols} ≠ Z {Z.Rows}×{Z.Cols}");
            if (Y.Rows != Z.Rows || Y.Cols != Z.Cols)
                throw new MatlabRuntimeException($"surf/contourf: Y {Y.Rows}×{Y.Cols} ≠ Z {Z.Rows}×{Z.Cols}");
        }
        private static string EmitMatrixJs(MValue m)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < m.Rows; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("[");
                for (int j = 0; j < m.Cols; j++)
                {
                    if (j > 0) sb.Append(",");
                    sb.Append(m.At(i, j).ToString("G6", Inv));
                }
                sb.Append("]");
            }
            sb.Append("]");
            return sb.ToString();
        }
        private static string EmitVecJs(MValue v)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < v.Data.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(v.Data[i].ToString("G6", Inv));
            }
            sb.Append("]");
            return sb.ToString();
        }
        private static string EmitRowJs(MValue m, bool firstRowOnly)
        {
            // Para contourf el axis x es la primera fila del grid X
            var sb = new StringBuilder("[");
            for (int j = 0; j < m.Cols; j++)
            {
                if (j > 0) sb.Append(",");
                sb.Append(m.At(0, j).ToString("G6", Inv));
            }
            sb.Append("]");
            return sb.ToString();
        }
        private static string EmitColJs(MValue m)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < m.Rows; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(m.At(i, 0).ToString("G6", Inv));
            }
            sb.Append("]");
            return sb.ToString();
        }
        private static string ColormapToPlotly(string name) => name?.ToLowerInvariant() switch
        {
            "jet" => "Jet",
            "parula" or "viridis" => "Viridis",
            "hot" => "Hot",
            "cool" => "Bluered",
            "gray" or "grey" => "Greys",
            "hsv" => "HSV",
            "bone" => "Greys",
            "spring" => "YlOrRd",
            "summer" => "YlGn",
            "autumn" => "YlOrRd",
            "winter" => "Blues",
            "copper" => "YlOrBr",
            _ => "Viridis"
        };
        /// <summary>Muestra un colormap en t∈[0,1] devolviendo rgb(r,g,b).</summary>
        public static string ColorscaleSampleRgb(string name, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            (int r, int g, int b) c;
            switch (name?.ToLowerInvariant())
            {
                case "jet":
                    c = JetRgb(t);
                    break;
                case "hot":
                    c = HotRgb(t);
                    break;
                case "gray":
                case "grey":
                    int g0 = (int)(t * 255);
                    c = (g0, g0, g0);
                    break;
                case "parula":
                case "viridis":
                default:
                    c = ViridisRgb(t);
                    break;
            }
            return $"rgb({c.r},{c.g},{c.b})";
        }
        private static (int, int, int) JetRgb(double t)
        {
            // Jet: blue → cyan → green → yellow → red
            double r = Math.Max(0, Math.Min(1, Math.Min(4*t - 1.5, -4*t + 4.5)));
            double g = Math.Max(0, Math.Min(1, Math.Min(4*t - 0.5, -4*t + 3.5)));
            double b = Math.Max(0, Math.Min(1, Math.Min(4*t + 0.5, -4*t + 2.5)));
            return ((int)(r*255), (int)(g*255), (int)(b*255));
        }
        private static (int, int, int) HotRgb(double t)
        {
            // Hot: black → red → yellow → white
            double r = t < 0.4 ? t / 0.4 : 1;
            double g = t < 0.4 ? 0 : (t < 0.8 ? (t - 0.4) / 0.4 : 1);
            double b = t < 0.8 ? 0 : (t - 0.8) / 0.2;
            return ((int)(r*255), (int)(g*255), (int)(b*255));
        }
        private static (int, int, int) ViridisRgb(double t)
        {
            // Aproximación lineal a Viridis con 4 puntos clave
            var stops = new (double Pos, int R, int G, int B)[]
            {
                (0.0,  68,   1,  84),
                (0.33, 59,  82, 139),
                (0.66, 33, 144, 141),
                (1.0, 253, 231,  37)
            };
            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t >= stops[i].Pos && t <= stops[i+1].Pos)
                {
                    double tt = (t - stops[i].Pos) / (stops[i+1].Pos - stops[i].Pos);
                    return ((int)(stops[i].R + tt * (stops[i+1].R - stops[i].R)),
                            (int)(stops[i].G + tt * (stops[i+1].G - stops[i].G)),
                            (int)(stops[i].B + tt * (stops[i+1].B - stops[i].B)));
                }
            }
            return (stops[stops.Length-1].R, stops[stops.Length-1].G, stops[stops.Length-1].B);
        }
    }
}
