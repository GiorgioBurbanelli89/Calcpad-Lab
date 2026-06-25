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
using SkiaSharp;

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
            public string Align = "left";   // HorizontalAlignment de text(): left|center|right (MATLAB def = left)
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
            // DIBUJO 2D estructural (malla: patches/texto/markers) → SVG inline (nítido,
            // numeración fiable). Plotly se reserva para resultados (surf/contour/datos).
            if (!_figIs3D && _figPrims != null &&
                _figPrims.Exists(p => p.Kind == "patch2d" || p.Kind == "text2d" || p.Kind == "markers2d"))
            {
                string svgInner = ExportSvg(760, 580);
                _figTraces = null; _figAnnotations = null; _figPrims = null;
                return svgInner == null ? "" : $"<div class=\"matlab-plot matlab-svg\">{svgInner}</div>\n";
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
                // xaxis: unir partes presentes con coma (evita '{,' inicial inválido)
                var xparts = new System.Collections.Generic.List<string>();
                if (_figXLabel != null) xparts.Add($"title:'{EscapeJs(_figXLabel)}'");
                if (_figXMin.HasValue) xparts.Add($"range:[{_figXMin.Value.ToString(Inv)}, {_figXMax.Value.ToString(Inv)}]");
                sb.Append(", xaxis:{").Append(string.Join(", ", xparts)).Append("}");
                // yaxis: igual + aspecto cuadrado (scaleanchor)
                var yparts = new System.Collections.Generic.List<string>();
                if (_figYLabel != null) yparts.Add($"title:'{EscapeJs(_figYLabel)}'");
                if (_figYMin.HasValue) yparts.Add($"range:[{_figYMin.Value.ToString(Inv)}, {_figYMax.Value.ToString(Inv)}]");
                yparts.Add("scaleanchor:'x'");
                yparts.Add("scaleratio:1");
                sb.Append(", yaxis:{").Append(string.Join(", ", yparts)).Append("}");
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
                    sb.Append($", reversescale:{(ColormapReversed(colormap) ? "true" : "false")}");
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
        /// <summary>Markers 2D — puntos (scatter mode:markers) que se ACUMULAN en la figura.
        /// Permite que plot(x,y,'o') componga con patch/line/text en los mismos ejes.</summary>
        public static void Markers2D(double[] xs, double[] ys, string fillColor, string edgeColor,
                                      string symbol, double size)
        {
            var sb = new StringBuilder();
            sb.Append("{type:'scatter', mode:'markers'");
            sb.Append($", marker:{{symbol:'{symbol}', size:{size.ToString(Inv)}, color:'{fillColor}'");
            sb.Append($", line:{{color:'{edgeColor}', width:1}}}}");
            sb.Append($", x:[{Csv(xs)}], y:[{Csv(ys)}]");
            sb.Append(", showlegend:false, hoverinfo:'skip'}");
            AddTrace(sb.ToString());
            if (_figPrims != null) _figPrims.Add(new FigPrim{
                Kind="markers2d", Xs=(double[])xs.Clone(), Ys=(double[])ys.Clone(),
                FaceColor=fillColor, EdgeColor=edgeColor, FontSize=size, Text=symbol
            });
        }
        public static void Text2D(double x, double y, string text, string color, double fontSize, string align = "left")
        {
            string xanchor = align == "center" ? "center" : (align == "right" ? "right" : "left");
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"x:{x.ToString(Inv)}, y:{y.ToString(Inv)}, ");
            sb.Append("xref:'x', yref:'y', ");   // posicionar en coords de DATOS, no de papel
            sb.Append($"text:'{EscapeJs(text)}', ");
            sb.Append($"font:{{color:'{color}', size:{fontSize.ToString(Inv)}}}, ");
            sb.Append($"xanchor:'{xanchor}', ");  // honra HorizontalAlignment de MATLAB
            sb.Append("showarrow:false");
            sb.Append("}");
            AddAnnotation(sb.ToString());
            if (_figPrims != null) _figPrims.Add(new FigPrim{
                Kind="text2d", Xs=new[]{x}, Ys=new[]{y}, Text=text, Color=color, FontSize=fontSize, Align=align
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
                else if (p.Kind == "markers2d" && p.Xs.Length > 0)
                {
                    double r = Math.Max(2.0, p.FontSize / 2.0);
                    string sym = p.Text ?? "circle";
                    for (int i = 0; i < p.Xs.Length; i++)
                    {
                        double cx = TX(p.Xs[i]); double cy = TY(p.Ys[i]);
                        if (sym.StartsWith("triangle"))
                        {
                            // triángulo equilátero (apuntando arriba)
                            double h = r * 1.3;
                            string pts = $"{cx.ToString("F2",Inv)},{(cy-h).ToString("F2",Inv)} " +
                                         $"{(cx-h).ToString("F2",Inv)},{(cy+h*0.7).ToString("F2",Inv)} " +
                                         $"{(cx+h).ToString("F2",Inv)},{(cy+h*0.7).ToString("F2",Inv)}";
                            svg.AppendLine($"    <polygon points='{pts}' fill='{p.FaceColor}' stroke='{p.EdgeColor}' stroke-width='1'/>");
                        }
                        else
                        {
                            svg.AppendLine($"    <circle cx='{cx.ToString("F2", Inv)}' cy='{cy.ToString("F2", Inv)}' r='{r.ToString("F2", Inv)}' fill='{p.FaceColor}' stroke='{p.EdgeColor}' stroke-width='1'/>");
                        }
                    }
                }
                else if (p.Kind == "text2d")
                {
                    double tx = TX(p.Xs[0]); double ty = TY(p.Ys[0]);
                    string anchor = p.Align == "center" ? "middle" : (p.Align == "right" ? "end" : "start");
                    svg.AppendLine($"    <text x='{tx.ToString("F2", Inv)}' y='{ty.ToString("F2", Inv)}' fill='{p.Color}' font-family='sans-serif' font-size='{p.FontSize.ToString(Inv)}' text-anchor='{anchor}' dominant-baseline='central'>{EscapeXml(p.Text)}</text>");
                }
            }
            svg.AppendLine($"  </g>");
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        /// <summary>Rasteriza la figura 2D actual (primitives line/patch/marker/text)
        /// a RGB row-major (byte[h*w*3]) vía SkiaSharp. Para getframe → GIF.</summary>
        public static byte[] RasterizeFigure(int width, int height)
        {
            if (_figPrims == null || _figPrims.Count == 0) return null;
            double xmin = double.MaxValue, xmax = double.MinValue, ymin = double.MaxValue, ymax = double.MinValue;
            foreach (var p in _figPrims)
            {
                if (p.Xs == null) continue;
                foreach (var x in p.Xs) { if (x < xmin) xmin = x; if (x > xmax) xmax = x; }
                foreach (var y in p.Ys) { if (y < ymin) ymin = y; if (y > ymax) ymax = y; }
            }
            if (xmax - xmin < 1e-9) xmax = xmin + 1;
            if (ymax - ymin < 1e-9) ymax = ymin + 1;
            double padf = 0.06, ddx = xmax - xmin, ddy = ymax - ymin;
            xmin -= ddx * padf; xmax += ddx * padf; ymin -= ddy * padf; ymax += ddy * padf;
            ddx = xmax - xmin; ddy = ymax - ymin;
            int mL = 50, mR = 20, mT = 40, mB = 30;
            double sx = (width - mL - mR) / ddx, sy = (height - mT - mB) / ddy;
            float TX(double x) => (float)(mL + (x - xmin) * sx);
            float TY(double y) => (float)(height - mB - (y - ymin) * sy);

            using var bmp = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.White);
                using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
                using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
                using var font = new SKFont(SKTypeface.Default, 13);
                using var txt = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.Black };
                if (!string.IsNullOrEmpty(_figTitle))
                    canvas.DrawText(_figTitle, width / 2f, 22, SKTextAlign.Center, new SKFont(SKTypeface.Default, 14), txt);
                foreach (var p in _figPrims)
                {
                    if ((p.Kind == "patch2d" || p.Kind == "line2d") && p.Xs != null && p.Xs.Length >= 2)
                    {
                        var path = new SKPath();
                        path.MoveTo(TX(p.Xs[0]), TY(p.Ys[0]));
                        for (int i = 1; i < p.Xs.Length; i++) path.LineTo(TX(p.Xs[i]), TY(p.Ys[i]));
                        if (p.Kind == "patch2d")
                        {
                            path.Close();
                            var fc = ParseColor(p.FaceColor);
                            if (fc.Alpha > 0) { fill.Color = fc.WithAlpha((byte)(255 * p.FaceAlpha)); canvas.DrawPath(path, fill); }
                            stroke.Color = ParseColor(p.EdgeColor); stroke.StrokeWidth = (float)Math.Max(0.5, p.LineWidth); canvas.DrawPath(path, stroke);
                        }
                        else { stroke.Color = ParseColor(p.Color); stroke.StrokeWidth = (float)Math.Max(0.8, p.LineWidth); canvas.DrawPath(path, stroke); }
                        path.Dispose();
                    }
                    else if (p.Kind == "markers2d" && p.Xs != null)
                    {
                        fill.Color = ParseColor(p.FaceColor);
                        float r = (float)Math.Max(2.0, p.FontSize / 2.0);
                        for (int i = 0; i < p.Xs.Length; i++) canvas.DrawCircle(TX(p.Xs[i]), TY(p.Ys[i]), r, fill);
                    }
                    else if (p.Kind == "text2d" && p.Xs != null && p.Xs.Length > 0)
                    {
                        txt.Color = ParseColor(p.Color);
                        var al = p.Align == "center" ? SKTextAlign.Center : (p.Align == "right" ? SKTextAlign.Right : SKTextAlign.Left);
                        canvas.DrawText(p.Text ?? "", TX(p.Xs[0]), TY(p.Ys[0]), al, font, txt);
                    }
                }
            }
            var px = bmp.Pixels;   // SKColor[]
            var rgb = new byte[width * height * 3];
            for (int i = 0; i < px.Length; i++) { rgb[i * 3] = px[i].Red; rgb[i * 3 + 1] = px[i].Green; rgb[i * 3 + 2] = px[i].Blue; }
            return rgb;
        }

        private static SKColor ParseColor(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "none") return SKColors.Transparent;
            if (s[0] == '#' && s.Length >= 7)
            {
                try { return new SKColor(Convert.ToByte(s.Substring(1, 2), 16), Convert.ToByte(s.Substring(3, 2), 16), Convert.ToByte(s.Substring(5, 2), 16)); }
                catch { return SKColors.Black; }
            }
            switch (s.ToLowerInvariant())
            {
                case "r": case "red": return SKColors.Red;
                case "g": case "green": return new SKColor(0, 128, 0);
                case "b": case "blue": return SKColors.Blue;
                case "k": case "black": return SKColors.Black;
                case "w": case "white": return SKColors.White;
                case "y": case "yellow": return SKColors.Gold;
                case "m": case "magenta": return SKColors.Magenta;
                case "c": case "cyan": return SKColors.DarkCyan;
                default: return SKColors.Black;
            }
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
            sb.Append($"    type: 'surface', colorscale: '{ColormapToPlotly(colormap)}', reversescale: {(ColormapReversed(colormap) ? "true" : "false")},\n");
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
            sb.Append($"    type: 'contour', colorscale: '{ColormapToPlotly(colormap)}', reversescale: {(ColormapReversed(colormap) ? "true" : "false")}, ncontours: {nLevels}, contours: {{coloring: 'fill'}},\n");
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
            sb.Append($"  var data = [{{ type: 'heatmap', colorscale: '{ColormapToPlotly(colormap)}', reversescale: {(ColormapReversed(colormap) ? "true" : "false")}, z: {EmitMatrixJs(Z)} }}];\n");
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

        /// <summary>Viewer interactivo de campo sobre losa: 2D (canvas jet + crosshair hover)
        /// + 3D (Three.js, malla deformada coloreada + hover) con selector de resultado.
        /// Lo GENERA el script Lab (no HTML crudo): los campos vienen del .m.</summary>
        public static string SlabView3D(double A, double B, double H, int na, int nb,
            System.Collections.Generic.List<(string key, string label, string unit, double[] data)> fields)
        {
            int id = ++_plotCounter;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var opts = new StringBuilder(); var dinit = new StringBuilder();
            var uinit = new StringBuilder(); var ddata = new StringBuilder();
            for (int f = 0; f < fields.Count; f++)
            {
                var fl = fields[f];
                opts.Append($"<option value=\"{fl.key}\">{fl.label}</option>");
                if (f > 0) { dinit.Append(','); uinit.Append(','); }
                dinit.Append(fl.key).Append(":[]");
                uinit.Append(fl.key).Append(":'").Append(fl.unit).Append('\'');
                ddata.Append("D.").Append(fl.key).Append("=[");
                for (int k = 0; k < fl.data.Length; k++) { if (k > 0) ddata.Append(','); ddata.Append(fl.data[k].ToString("0.#####", ci)); }
                ddata.Append("];");
            }
            return SlabViewerTemplate
                .Replace("__ID__", id.ToString(ci))
                .Replace("__NA__", na.ToString(ci)).Replace("__NB__", nb.ToString(ci))
                .Replace("__A__", A.ToString(ci)).Replace("__BB__", B.ToString(ci)).Replace("__HC__", H.ToString(ci))
                .Replace("__OPTIONS__", opts.ToString())
                .Replace("__DINIT__", dinit.ToString()).Replace("__UINIT__", uinit.ToString())
                .Replace("__DDATA__", ddata.ToString())
                .Replace("__FIRSTKEY__", fields.Count > 0 ? fields[0].key : "w");
        }

        // Template del viewer (JS interactivo 2D canvas jet + 3D Three.js, con hover y selector).
        // Placeholders: __ID__ __NA__ __NB__ __A__ __BB__ __HC__ __OPTIONS__ __DINIT__ __UINIT__ __DDATA__ __FIRSTKEY__
        private const string SlabViewerTemplate =
@"<div id=""rv__ID__"" style=""font:13px Segoe UI""> <b>Resultado:</b> <select id=""rvs__ID__"" style=""font:13px Segoe UI;padding:2px 6px;margin:4px 0"">__OPTIONS__</select> <div style=""display:flex;flex-wrap:wrap;gap:10px""><div id=""rv2__ID__""></div><div id=""rv3__ID__""></div></div></div> <script>(function(){ var na=__NA__,nb=__NB__,A=__A__,Bb=__BB__,Hc=__HC__; var D={__DINIT__},U={__UINIT__}; __DDATA__ var nx=na+1,ny=nb+1; function jt(t){t=Math.max(0,Math.min(1,t));return[Math.max(0,Math.min(1,Math.min(4*t-1.5,-4*t+4.5)))*255|0,Math.max(0,Math.min(1,Math.min(4*t-0.5,-4*t+3.5)))*255|0,Math.max(0,Math.min(1,Math.min(4*t+0.5,-4*t+2.5)))*255|0];} function jc(t){var c=jt(t);return new THREE.Color(c[0]/255,c[1]/255,c[2]/255);} var P2=document.getElementById(""rv2__ID__""),P3=document.getElementById(""rv3__ID__""),SEL=document.getElementById(""rvs__ID__""); var tip=document.createElement(""div"");tip.style.cssText=""position:fixed;pointer-events:none;background:rgba(20,20,28,.9);color:#fff;font:12px Consolas;padding:3px 7px;border-radius:4px;display:none;z-index:99999"";document.body.appendChild(tip); function draw2D(g,uni){var W=420,H=380,ml=38,mr=64,mt=16,pw=W-ml-mr,ph=H-mt-26;P2.innerHTML='';var hd=document.createElement(""div"");var wr=document.createElement(""div"");wr.style.cssText=""position:relative;width:""+W+""px;height:""+H+""px;flex:0 0 auto"";var bs=document.createElement(""canvas"");bs.width=W;bs.height=H;bs.style.cssText=""position:absolute;border:1px solid #ddd"";var ov=document.createElement(""canvas"");ov.width=W;ov.height=H;ov.style.cssText=""position:absolute;pointer-events:none"";wr.appendChild(bs);wr.appendChild(ov);P2.appendChild(hd);P2.appendChild(wr);var cx=bs.getContext(""2d""),ox=ov.getContext(""2d""); var xs=[];for(var i=0;i<nx;i++)xs.push(i*A/na);var ys=[];for(var j=0;j<ny;j++)ys.push(j*Bb/nb);function gv(i,j){return g[i*ny+j];} function SX(x){return ml+x/A*pw;}function SY(y){return mt+(Bb-y)/Bb*ph;}function wX(p){return(p-ml)/pw*A;}function wY(p){return Bb-(p-mt)/ph*Bb;} function bl(x,y){if(x<0||x>A||y<0||y>Bb)return null;var i=0;while(i<nx-2&&xs[i+1]<x)i++;var j=0;while(j<ny-2&&ys[j+1]<y)j++;var u=(x-xs[i])/(xs[i+1]-xs[i]),v=(y-ys[j])/(ys[j+1]-ys[j]);return gv(i,j)*(1-u)*(1-v)+gv(i+1,j)*u*(1-v)+gv(i,j+1)*(1-u)*v+gv(i+1,j+1)*u*v;} var vn=1e30,vx=-1e30;for(var k=0;k<g.length;k++){if(g[k]<vn)vn=g[k];if(g[k]>vx)vx=g[k];}if(vx-vn<1e-9)vx=vn+1; var im=cx.createImageData(pw,ph),dd=im.data;for(var py=0;py<ph;py++)for(var px=0;px<pw;px++){var v=bl(wX(ml+px),wY(mt+py)),qq=(py*pw+px)*4;if(v==null){dd[qq+3]=0;}else{var c=jt((v-vn)/(vx-vn));dd[qq]=c[0];dd[qq+1]=c[1];dd[qq+2]=c[2];dd[qq+3]=255;}}cx.putImageData(im,ml,mt); cx.strokeStyle=""rgba(40,40,40,.25)"";for(var i=0;i<nx;i++){cx.beginPath();cx.moveTo(SX(xs[i]),mt);cx.lineTo(SX(xs[i]),mt+ph);cx.stroke();}for(var j=0;j<ny;j++){cx.beginPath();cx.moveTo(ml,SY(ys[j]));cx.lineTo(ml+pw,SY(ys[j]));cx.stroke();}cx.strokeStyle=""#888"";cx.strokeRect(ml,mt,pw,ph); var cbx=W-mr+20;cx.font=""10px Consolas"";for(var k=0;k<ph;k++){var c=jt(1-k/ph);cx.fillStyle=""rgb(""+c[0]+"",""+c[1]+"",""+c[2]+"")"";cx.fillRect(cbx,mt+k,13,1);}cx.fillStyle=""#333"";cx.fillText(vx.toFixed(2),cbx-2,mt-3);cx.fillText(vn.toFixed(2),cbx-2,mt+ph+10); hd.innerHTML=""<b>2D (planta)</b> max=""+vx.toFixed(2)+uni+"" min=""+vn.toFixed(2)+uni; bs.onmousemove=function(ev){var rc=bs.getBoundingClientRect();var px=ev.clientX-rc.left,py=ev.clientY-rc.top,x=wX(px),y=wY(py);var v=(px>=ml&&px<=ml+pw&&py>=mt&&py<=mt+ph)?bl(x,y):null;ox.clearRect(0,0,W,H);if(v==null)return;ox.strokeStyle=""#000"";ox.beginPath();ox.moveTo(px,mt);ox.lineTo(px,mt+ph);ox.moveTo(ml,py);ox.lineTo(ml+pw,py);ox.stroke();ox.fillStyle=""rgba(20,20,28,.9)"";ox.fillRect(px+8,py-15,140,15);ox.fillStyle=""#fff"";ox.font=""11px Consolas"";ox.fillText(v.toFixed(2)+uni+"" @(""+x.toFixed(1)+"",""+y.toFixed(1)+"")"",px+11,py-4);};bs.onmouseleave=function(){ox.clearRect(0,0,W,H);};} var scn,cam,ren,ctrl,grp,mesh,geo,vv,rdy=false; function init3D(){var W=440,H=400;scn=new THREE.Scene();scn.background=new THREE.Color(0xeef0f4);cam=new THREE.PerspectiveCamera(45,W/H,.001,9000);ren=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});ren.setSize(W,H);var hd=document.createElement(""div"");hd.id=""rv3h__ID__"";P3.appendChild(hd);P3.appendChild(ren.domElement);cam.up.set(0,0,1);var dg0=Math.hypot(A,Bb,Hc)||1;cam.position.set(A/2+dg0,Bb/2-dg0*1.4,Hc/2+dg0);cam.lookAt(A/2,Bb/2,Hc/2);ctrl=new THREE.OrbitControls(cam,ren.domElement);ctrl.target.set(A/2,Bb/2,Hc/2);ctrl.update();scn.add(new THREE.AmbientLight(0xffffff,.9));var dl=new THREE.DirectionalLight(0xffffff,.5);dl.position.set(8,-12,18);scn.add(dl);var ray=new THREE.Raycaster(),mo=new THREE.Vector2();ren.domElement.addEventListener(""mousemove"",function(ev){if(!mesh)return;var r=ren.domElement.getBoundingClientRect();mo.x=((ev.clientX-r.left)/r.width)*2-1;mo.y=-((ev.clientY-r.top)/r.height)*2+1;ray.setFromCamera(mo,cam);var h=ray.intersectObject(mesh,false);if(h.length){var f=h[0].face,ap=geo.attributes.position,p0=new THREE.Vector3().fromBufferAttribute(ap,f.a),p1=new THREE.Vector3().fromBufferAttribute(ap,f.b),p2=new THREE.Vector3().fromBufferAttribute(ap,f.c),bc=new THREE.Vector3();new THREE.Triangle(p0,p1,p2).getBarycoord(h[0].point,bc);var val=bc.x*vv[f.a]+bc.y*vv[f.b]+bc.z*vv[f.c];tip.style.display=""block"";tip.style.left=(ev.clientX+13)+""px"";tip.style.top=(ev.clientY+8)+""px"";tip.innerHTML=val.toFixed(2);}else tip.style.display=""none"";});ren.domElement.addEventListener(""mouseleave"",function(){tip.style.display=""none"";});function anim(){requestAnimationFrame(anim);ctrl.update();ren.render(scn,cam);}anim();rdy=true;} function build3D(colorG,uni){if(!rdy)return;if(grp)scn.remove(grp);grp=new THREE.Group(); var wG=D.w;function wv(i,j){return wG[i*ny+j];}function cg(i,j){return colorG[i*ny+j];} var wn=Math.min.apply(null,wG),wx=Math.max.apply(null,wG);var wa=Math.max(Math.abs(wn),Math.abs(wx),1e-9);var ampw=(.40*Hc)/wa; function Pt(i,j){return new THREE.Vector3(i*A/na,j*Bb/nb,Hc+wv(i,j)*ampw);} var cn=Math.min.apply(null,colorG),cx2=Math.max.apply(null,colorG);if(cx2-cn<1e-9)cx2=cn+1; var pos=[],col=[];vv=[];function pv(p,t){pos.push(p.x,p.y,p.z);var c=jc(t);col.push(c.r,c.g,c.b);} for(var i=0;i<nx-1;i++)for(var j=0;j<ny-1;j++){var pa=Pt(i,j),pb=Pt(i+1,j),pc=Pt(i+1,j+1),pd=Pt(i,j+1),ta=(cg(i,j)-cn)/(cx2-cn),tb=(cg(i+1,j)-cn)/(cx2-cn),tc=(cg(i+1,j+1)-cn)/(cx2-cn),td=(cg(i,j+1)-cn)/(cx2-cn);pv(pa,ta);pv(pb,tb);pv(pc,tc);vv.push(cg(i,j),cg(i+1,j),cg(i+1,j+1));pv(pa,ta);pv(pc,tc);pv(pd,td);vv.push(cg(i,j),cg(i+1,j+1),cg(i,j+1));} geo=new THREE.BufferGeometry();geo.setAttribute(""position"",new THREE.Float32BufferAttribute(pos,3));geo.setAttribute(""color"",new THREE.Float32BufferAttribute(col,3));geo.computeVertexNormals();mesh=new THREE.Mesh(geo,new THREE.MeshBasicMaterial({vertexColors:true,side:THREE.DoubleSide}));grp.add(mesh); var wp=[];for(var i=0;i<nx;i++)for(var j=0;j<ny-1;j++){var aa=Pt(i,j),bb=Pt(i,j+1);wp.push(aa.x,aa.y,aa.z,bb.x,bb.y,bb.z);}for(var j=0;j<ny;j++)for(var i=0;i<nx-1;i++){var aa=Pt(i,j),bb=Pt(i+1,j);wp.push(aa.x,aa.y,aa.z,bb.x,bb.y,bb.z);}var wg=new THREE.BufferGeometry();wg.setAttribute(""position"",new THREE.Float32BufferAttribute(wp,3));grp.add(new THREE.LineSegments(wg,new THREE.LineBasicMaterial({color:0x556677}))); var corn=[[0,0],[nx-1,0],[nx-1,ny-1],[0,ny-1]]; var cp=[];for(var k=0;k<4;k++){var ci=corn[k][0],cj=corn[k][1],ptop=Pt(ci,cj);cp.push(ci*A/na,cj*Bb/nb,0,ptop.x,ptop.y,ptop.z);}var cgeo=new THREE.BufferGeometry();cgeo.setAttribute(""position"",new THREE.Float32BufferAttribute(cp,3));grp.add(new THREE.LineSegments(cgeo,new THREE.LineBasicMaterial({color:0x222222}))); var bp=[];function edge(ii,jj,di,dj,n){for(var s=0;s<n;s++){var a1=Pt(ii+di*s,jj+dj*s),a2=Pt(ii+di*(s+1),jj+dj*(s+1));bp.push(a1.x,a1.y,a1.z,a2.x,a2.y,a2.z);}} edge(0,0,1,0,nx-1);edge(0,ny-1,1,0,nx-1);edge(0,0,0,1,ny-1);edge(nx-1,0,0,1,ny-1);var bgeo=new THREE.BufferGeometry();bgeo.setAttribute(""position"",new THREE.Float32BufferAttribute(bp,3));grp.add(new THREE.LineSegments(bgeo,new THREE.LineBasicMaterial({color:0x8d6e63,linewidth:2}))); for(var k=0;k<4;k++){var ci=corn[k][0]*A/na,cj=corn[k][1]*Bb/nb,cm=new THREE.Mesh(new THREE.ConeGeometry(.05*Math.max(A,Bb),.10*Math.max(A,Bb),4),new THREE.MeshBasicMaterial({color:0x2244aa}));cm.position.set(ci,cj,-.05*Math.max(A,Bb));cm.rotation.x=Math.PI/2;grp.add(cm);} scn.add(grp); var cxx=A/2,cyy=Bb/2,czz=Hc/2,diag=Math.hypot(A,Bb,Hc)||1;cam.up.set(0,0,1);cam.position.set(cxx+diag*1.0,cyy-diag*1.4,czz+diag*.9);cam.lookAt(cxx,cyy,czz);ctrl.target.set(cxx,cyy,czz);ctrl.update(); document.getElementById(""rv3h__ID__"").innerHTML=""<b>Mesa 3D - ""+SEL.options[SEL.selectedIndex].text+""</b> max=""+cx2.toFixed(2)+uni+"" min=""+cn.toFixed(2)+uni+"" (arrastra/zoom/hover)"";} function render(){var k=SEL.value,g=D[k],uni=U[k];draw2D(g,uni);build3D(g,uni);} SEL.onchange=render; var s1=document.createElement(""script"");s1.src=""https://cdn.jsdelivr.net/npm/three@0.145.0/build/three.min.js"";s1.onload=function(){var s2=document.createElement(""script"");s2.src=""https://cdn.jsdelivr.net/npm/three@0.145.0/examples/js/controls/OrbitControls.js"";s2.onload=function(){init3D();render();};document.head.appendChild(s2);};document.head.appendChild(s1); draw2D(D.__FIRSTKEY__,U.__FIRSTKEY__); })();</script>";

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
            sb.Append($"  var data = [{{ type: 'heatmap', colorscale: '{ColormapToPlotly(colormap)}', reversescale: {(ColormapReversed(colormap) ? "true" : "false")}, z: {EmitMatrixJs(Z)} }}];\n");
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
            // MATLAB acepta X/Y como GRID (igual que Z) o como VECTOR de coordenadas
            // (longitud = Cols para X, = Rows para Y). Solo error si no encaja ninguno.
            int xn = X.Rows * X.Cols, yn = Y.Rows * Y.Cols;
            bool xOk = (X.Rows == Z.Rows && X.Cols == Z.Cols) || xn == Z.Cols || xn == Z.Rows;
            bool yOk = (Y.Rows == Z.Rows && Y.Cols == Z.Cols) || yn == Z.Rows || yn == Z.Cols;
            if (!xOk)
                throw new MatlabRuntimeException($"surf/contourf: X {X.Rows}×{X.Cols} ≠ Z {Z.Rows}×{Z.Cols}");
            if (!yOk)
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
            // Eje x de contourf: primera fila del grid X, o el vector si X es columna.
            var sb = new StringBuilder("[");
            if (m.Cols == 1 && m.Rows > 1)
                for (int i = 0; i < m.Rows; i++) { if (i > 0) sb.Append(","); sb.Append(m.At(i, 0).ToString("G6", Inv)); }
            else
                for (int j = 0; j < m.Cols; j++) { if (j > 0) sb.Append(","); sb.Append(m.At(0, j).ToString("G6", Inv)); }
            sb.Append("]");
            return sb.ToString();
        }
        private static string EmitColJs(MValue m)
        {
            // Eje y de contourf: primera columna, o el vector si Y es fila.
            var sb = new StringBuilder("[");
            if (m.Rows == 1 && m.Cols > 1)
                for (int j = 0; j < m.Cols; j++) { if (j > 0) sb.Append(","); sb.Append(m.At(0, j).ToString("G6", Inv)); }
            else
                for (int i = 0; i < m.Rows; i++) { if (i > 0) sb.Append(","); sb.Append(m.At(i, 0).ToString("G6", Inv)); }
            sb.Append("]");
            return sb.ToString();
        }
        /// <summary>True si el nombre pide colormap INVERTIDO (sufijo `_r`, ej. jet_r — estilo SAP2000/PyVista).</summary>
        private static bool ColormapReversed(string name) =>
            (name ?? "").ToLowerInvariant().EndsWith("_r");

        private static string ColormapToPlotly(string nameRaw)
        {
            var name = (nameRaw ?? "").ToLowerInvariant();
            if (name.EndsWith("_r")) name = name.Substring(0, name.Length - 2);   // jet_r -> jet (+reversescale)
            return ColormapToPlotlyBase(name);
        }

        private static string ColormapToPlotlyBase(string name) => name switch
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
