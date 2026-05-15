// =============================================================================
// Calcpad Lab — Native MATLAB functions
// =============================================================================
//   Funciones MATLAB que SE EJECUTAN COMO MATLAB REAL pero con un backend
//   nativo en Calcpad Lab. El script .m que las usa es 100% portable:
//
//     x = [0 15 15 0];                ← MATLAB válido
//     y = [0 0 10 5];                 ← MATLAB válido
//     tri = delaunay(x, y);           ← MATLAB válido — en Calcpad Lab usa Triangle (Shewchuk)
//     trimesh(tri, x, y);             ← MATLAB válido — en Calcpad Lab emite HTML Three.js
//
//   En MATLAB nativo: ventana figura tradicional.
//   En Calcpad Lab:   HTML con Three.js — mesh 3D orbital rotable, mejor UX.
//
//   Implementación:
//     - DetectNativeMatlabCall  intercepta `name(args)` antes del MathParser
//     - Cada función nativa lee args, ejecuta C++ kernel via P/Invoke,
//       retorna IValue (asignación) y/o emite HTML al _sb
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        // ─────────────────────────────────────────────────────────────────
        // Registro de funciones nativas MATLAB intercept-aware
        // ─────────────────────────────────────────────────────────────────
        // Key: function name (case-sensitive MATLAB)
        // Value: handler que recibe args evaluadas + variable destino (LHS)
        // ─────────────────────────────────────────────────────────────────
        private static readonly HashSet<string> _matlabNativeFunctions =
            new(StringComparer.Ordinal)
            {
                "delaunay",     // delaunay(x, y) → tri matriz Nx3 (Bowyer-Watson simple)
                "mesh2d",       // [xm, ym, tri, bnd] = mesh2d(x, y, maxArea[, minAngle])
                                //   → mesh refinado constrained Delaunay (Triangle Shewchuk)
                "trimesh",      // trimesh(T, x, y[, z]) → emite HTML 3D Three.js
                "triplot",      // triplot(T, x, y) → emite HTML 2D
                "trisurf",      // trisurf(T, x, y, z) → emite HTML 3D con superficie coloreada
                // ─ 2D scientific visualization (heatmap con jet colormap) ─
                "meshgrid",     // [X, Y] = meshgrid(x, y) → matrices grid
                "surf",         // surf(X, Y, Z) / surf(Z) → heatmap 2D con jet
                "mesh",         // mesh(X, Y, Z) → wireframe (igual que surf por ahora)
                "contourf",     // contourf(X, Y, Z[, N]) → filled contour con jet
                "contour",      // contour(X, Y, Z[, N]) → contour lines
                "imagesc",      // imagesc(Z) → heatmap 2D
                "pcolor",       // pcolor(X, Y, Z) → pseudo-color
                "quiver",       // quiver(X, Y, U, V) → 2D vector field arrows
                "plot3",        // plot3(x, y, z) → 3D curve via Three.js
                "scatter3",     // scatter3(x, y, z) → 3D points via Three.js
                "randn",        // randn(N, M) → matriz N×M de samples normales
                "histogram",    // histogram(data, nbins) → bar chart of distribution
                "bar",          // bar(values) → bar chart
                "barh",         // barh(values) → horizontal bar
                "stem",         // stem(x, y) → stem plot
                "stairs",       // stairs(x, y) → staircase
                "area",         // area(x, y) → filled area
                "polar",        // polar(theta, r) → polar plot
                "compass",      // compass(u, v) → compass plot
                "subplot",      // subplot(m, n, p) → no-op (Calcpad muestra todo en flow)
                "semilogy",     // semilogy(x, y) → log-y plot
                "semilogx",     // semilogx(x, y) → log-x plot
                "loglog",       // loglog(x, y) → log-log plot
                "waterfall",    // waterfall(X, Y, Z) → wireframe
                "surfc",        // surfc(X, Y, Z) → surf+contour
                "errorbar",     // errorbar(x, y, e) → error bars
                "peaks",        // peaks() / peaks(N) / peaks(X, Y) → MATLAB demo surface
                // ─ Cosmetic plot commands (NO-OPs en Calcpad Lab) ─
                "colormap",     // colormap('jet') / colormap(jet) → no-op
                "colorbar",     // colorbar → no-op (legend SVG ya emitida)
                "shading",      // shading('interp') / shading interp → no-op
                "view",         // view(45, 30) → no-op (OrbitControls maneja vista)
                "axis",         // axis('equal') / axis equal → no-op
            };

        /// <summary>
        /// Chequea si una línea contiene una llamada a función MATLAB nativa
        /// que debe ser interceptada (no procesada por MathParser).
        ///
        /// Ignora coincidencias dentro de comentarios <c>%</c> y strings.
        /// </summary>
        private string DetectNativeMatlabCall(ReadOnlySpan<char> line)
        {
            // Determinar el límite "código útil": antes del primer '%' fuera de strings
            int codeEnd = line.Length;
            bool inSq = false, inDq = false;
            for (int j = 0; j < line.Length; j++)
            {
                var ch = line[j];
                if (ch == '\'' && !inDq) inSq = !inSq;
                else if (ch == '"' && !inSq) inDq = !inDq;
                else if (ch == '%' && !inSq && !inDq) { codeEnd = j; break; }
            }
            if (codeEnd == 0) return null;  // línea es 100% comment
            var codeSlice = line[..codeEnd];

            foreach (var name in _matlabNativeFunctions)
            {
                var idx = codeSlice.IndexOf(name + "(");
                if (idx < 0) continue;
                // Boundary izquierda: no debe ser parte de un identifier más largo
                if (idx > 0 && (char.IsLetterOrDigit(codeSlice[idx - 1]) || codeSlice[idx - 1] == '_'))
                    continue;
                // Verificar que la llamada esté FUERA de strings dentro del code-slice
                bool insideS = false, insideD = false;
                for (int j = 0; j < idx; j++)
                {
                    var ch = codeSlice[j];
                    if (ch == '\'' && !insideD) insideS = !insideS;
                    else if (ch == '"' && !insideS) insideD = !insideD;
                }
                if (insideS || insideD) continue;
                return name;
            }
            return null;
        }

        /// <summary>
        /// Ejecuta una llamada nativa MATLAB. Lee args con MathParser auxiliar,
        /// despacha al handler correspondiente, y emite output HTML cuando aplica.
        /// </summary>
        private void ExecuteNativeMatlabCall(ReadOnlySpan<char> line, string funcName)
        {
            var lineStr = line.ToString();
            int funcIdx = lineStr.IndexOf(funcName + "(", StringComparison.Ordinal);
            if (funcIdx < 0) return;

            // Variable de asignación (si hay). Soporta:
            //   var = func(args)              → assignVar = "var", assignVars = null
            //   [v1; v2; v3] = func(args)     → assignVars = ["v1","v2","v3"]  (multi-output)
            //   func(args)                    → ningún assign (efecto solo viz)
            //
            // Recordemos que el MatlabPreprocessor ya convirtió las `,` dentro
            // de [..] en `;`. Así que vemos `[v1; v2; v3] = ...`.
            string assignVar = null;
            string[] assignVars = null;
            if (funcIdx > 0)
            {
                var before = lineStr[..funcIdx].Trim();
                if (before.EndsWith("="))
                {
                    var lhs = before[..^1].Trim();
                    if (lhs.StartsWith("[") && lhs.EndsWith("]"))
                    {
                        // Multi-output destructuring
                        var inner = lhs[1..^1].Trim();
                        assignVars = inner.Split(';')
                            .Select(p => p.Trim())
                            .Where(p => p.Length > 0)
                            .ToArray();
                    }
                    else
                    {
                        assignVar = lhs;
                    }
                }
            }

            // Extraer args
            int callStart = funcIdx + funcName.Length + 1;
            int callEnd = lineStr.LastIndexOf(')');
            if (callEnd < callStart) callEnd = lineStr.Length;
            var argsRaw = lineStr[callStart..callEnd];
            // Calcpad ya convirtió `,` → `;` dentro de paréntesis (preprocessor)
            var argExprs = SplitArguments(argsRaw);

            // Evaluar cada arg con el _parser (MathParser). Cada arg puede ser
            // una variable simple ya en scope o una expresión.
            var args = new IValue[argExprs.Length];
            for (int i = 0; i < argExprs.Length; i++)
            {
                var e = argExprs[i].Trim();
                if (string.IsNullOrEmpty(e)) { args[i] = null; continue; }
                // MATLAB quoted-string arg: 'jet', "interp", 'equal', etc.
                // El MathParser no entiende `'` (lo rechaza con "Invalid symbol").
                // Para funciones que aceptan strings (colormap, shading, axis, view,
                // title cosmetics, etc.) dejamos args[i] = null — el handler usa
                // la fallback de lineStr para recuperar el texto.
                if (IsQuotedStringArg(e))
                {
                    args[i] = null;
                    continue;
                }
                // Special-case: MATLAB range a:b o a:step:b → expandir a vector
                if (TryParseRangeArg(e, out IValue rangeVec))
                {
                    args[i] = rangeVec;
                    continue;
                }
                try
                {
                    _parser.Parse(e);
                    _parser.Calculate(false, -1);
                    args[i] = _parser.ResultValue;
                }
                catch (Exception ex)
                {
                    AppendError(lineStr, $"{funcName} arg {i + 1}: {ex.Message}", _currentLine);
                    return;
                }
            }

            // Despachar al handler concreto
            try
            {
                switch (funcName)
                {
                    case "delaunay":
                        ExecuteDelaunay(assignVar, args, lineStr);
                        break;
                    case "mesh2d":
                        ExecuteMesh2D(assignVar, assignVars, args, lineStr);
                        break;
                    case "trimesh":
                        ExecuteTrimesh(args, lineStr);
                        break;
                    case "triplot":
                        ExecuteTriplot(args, lineStr);
                        break;
                    case "trisurf":
                        ExecuteTrisurf(args, lineStr);
                        break;
                    case "meshgrid":
                        ExecuteMeshgrid(assignVar, assignVars, args, lineStr);
                        break;
                    case "surf":
                    case "mesh":
                        ExecuteSurf(args, lineStr, funcName);
                        break;
                    case "contourf":
                    case "contour":
                        ExecuteContour(args, lineStr, funcName == "contourf");
                        break;
                    case "imagesc":
                    case "pcolor":
                        ExecuteImagesc(args, lineStr, funcName);
                        break;
                    case "quiver":
                        ExecuteQuiver(args, lineStr);
                        break;
                    case "plot3":
                        ExecutePlot3(args, lineStr);
                        break;
                    case "scatter3":
                        ExecuteScatter3(args, lineStr);
                        break;
                    case "randn":
                        ExecuteRandn(assignVar, args, lineStr);
                        break;
                    case "histogram":
                        ExecuteHistogram(args, lineStr);
                        break;
                    case "bar":
                    case "barh":
                        ExecuteBar(args, lineStr, funcName == "barh");
                        break;
                    case "stem":
                    case "stairs":
                    case "area":
                    case "semilogy":
                    case "semilogx":
                    case "loglog":
                    case "errorbar":
                        ExecuteLinePlot(args, lineStr, funcName);
                        break;
                    case "polar":
                        ExecutePolar(args, lineStr);
                        break;
                    case "compass":
                        ExecuteCompass(args, lineStr);
                        break;
                    case "peaks":
                        ExecutePeaks(assignVar, args, lineStr);
                        break;
                    case "subplot":
                    case "waterfall":
                    case "surfc":
                        // Fallback: tratar como surf básico o no-op
                        if (args.Length >= 3 && funcName != "subplot")
                            ExecuteSurf(args, lineStr, funcName);
                        break;
                    case "colormap":
                        if (args.Length >= 1 && args[0] is Matrix mat)
                        {
                            SetColormapFromMatrix(mat);
                        }
                        else if (args.Length >= 1)
                        {
                            var lhsParens = lineStr.IndexOf('(');
                            var rhsParens = lineStr.LastIndexOf(')');
                            if (lhsParens > 0 && rhsParens > lhsParens)
                            {
                                var inner = lineStr[(lhsParens + 1)..rhsParens].Trim();
                                SetColormap(inner);
                            }
                        }
                        break;
                    case "colorbar":
                    case "shading":
                    case "view":
                    case "axis":
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendError(lineStr, $"{funcName}: {ex.Message}", _currentLine);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // delaunay(x, y)  →  tri matriz Nx3 (1-based MATLAB)
        // ─────────────────────────────────────────────────────────────────
        private void ExecuteDelaunay(string assignVar, IValue[] args, string lineStr)
        {
            if (args.Length != 2)
                throw new ArgumentException("delaunay requiere 2 argumentos: delaunay(x, y)");
            var x = ExtractVector(args[0], "x");
            var y = ExtractVector(args[1], "y");
            if (x.Length != y.Length)
                throw new ArgumentException("x e y deben tener la misma longitud");

            // Interleave [x0,y0, x1,y1, ...]
            var pts = new double[2 * x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                pts[2 * i] = x[i];
                pts[2 * i + 1] = y[i];
            }
            var mesh = TriangleInterop.Triangulate(pts);
            int nt = mesh.NumTriangles;
            int[] tri = mesh.Triangles;

            // Construir matriz Calcpad Nx3 con índices 1-based (estilo MATLAB)
            var matrix = new Matrix(nt, 3);
            for (int i = 0; i < nt; i++)
            {
                matrix[i, 0] = new RealValue(tri[3 * i + 0] + 1);
                matrix[i, 1] = new RealValue(tri[3 * i + 1] + 1);
                matrix[i, 2] = new RealValue(tri[3 * i + 2] + 1);
            }

            if (!string.IsNullOrEmpty(assignVar))
            {
                _parser.SetVariable(assignVar, matrix);
                if (_isVisible)
                {
                    _sb.Append($"<p class=\"line\"><span class=\"eq\"><var>{assignVar}</var> = " +
                               $"<span class=\"info\">delaunay → {nt} triángulos</span></span></p>\n");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // mesh2d(x, y, maxArea[, minAngle])  →  mesh refinado con Triangle
        //
        // Equivalente a awatif:
        //   getMesh({points, polygon, maxMeshSize})  vía Triangle 'pzQOq30a${maxMeshSize}'
        //
        // Uso MATLAB-compatible:
        //   [xm, ym, tri, bnd] = mesh2d(x, y, 0.5)           % minAngle=30 default
        //   [xm, ym, tri, bnd] = mesh2d(x, y, 0.5, 30)       % minAngle explícito
        //
        // Devuelve:
        //   xm     = vector con x de TODOS los nodos (originales + Steiner refinement)
        //   ym     = vector con y de los nodos
        //   tri    = matriz Nx3 con triángulos (1-based MATLAB)
        //   bnd    = vector de índices 1-based de nodos en el borde
        // ─────────────────────────────────────────────────────────────────
        private void ExecuteMesh2D(string assignVar, string[] assignVars, IValue[] args, string lineStr)
        {
            if (args.Length < 3)
                throw new ArgumentException("mesh2d requiere ≥3 args: mesh2d(x, y, maxArea[, minAngle])");
            var x = ExtractVector(args[0], "x");
            var y = ExtractVector(args[1], "y");
            if (x.Length != y.Length)
                throw new ArgumentException("x e y deben tener la misma longitud");
            double maxArea = ExtractScalar(args[2], "maxArea");
            double minAngle = args.Length >= 4 && args[3] != null
                ? ExtractScalar(args[3], "minAngle") : 30.0;

            // Convertir a formato Triangle: pointlist + polygon = convex hull (índices secuenciales)
            var pts = new double[2 * x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                pts[2 * i] = x[i];
                pts[2 * i + 1] = y[i];
            }
            var polygon = new int[x.Length];
            for (int i = 0; i < x.Length; i++) polygon[i] = i;

            var mesh = TriangleInterop.MeshPolygon(pts, polygon, maxMeshSize: maxArea, minAngle: minAngle);

            // Construir resultados
            int np = mesh.NumPoints;
            var xm = new Vector(np);
            var ym = new Vector(np);
            for (int i = 0; i < np; i++)
            {
                xm[i] = new RealValue(mesh.Points[2 * i]);
                ym[i] = new RealValue(mesh.Points[2 * i + 1]);
            }
            int nt = mesh.NumTriangles;
            var triMatrix = new Matrix(nt, 3);
            for (int i = 0; i < nt; i++)
            {
                triMatrix[i, 0] = new RealValue(mesh.Triangles[3 * i + 0] + 1);
                triMatrix[i, 1] = new RealValue(mesh.Triangles[3 * i + 1] + 1);
                triMatrix[i, 2] = new RealValue(mesh.Triangles[3 * i + 2] + 1);
            }
            var boundary = mesh.BoundaryIndices;
            var bndVec = new Vector(boundary.Length);
            for (int i = 0; i < boundary.Length; i++)
                bndVec[i] = new RealValue(boundary[i] + 1);

            // Asignar resultados según destructuring del LHS
            if (assignVars != null)
            {
                // Multi-output: [xm; ym; tri[; bnd]] = mesh2d(...)
                IValue[] outputs = { xm, ym, triMatrix, bndVec };
                for (int i = 0; i < assignVars.Length && i < outputs.Length; i++)
                {
                    _parser.SetVariable(assignVars[i], outputs[i]);
                }
                if (_isVisible)
                {
                    _sb.Append($"<p class=\"line\"><span class=\"eq\">" +
                               $"<span class=\"info\">mesh2d → {np} nodos, {nt} triángulos, {boundary.Length} boundary</span>" +
                               $"</span></p>\n");
                }
            }
            else if (!string.IsNullOrEmpty(assignVar))
            {
                // Single output: solo retorna la matriz de triángulos
                _parser.SetVariable(assignVar, triMatrix);
                if (_isVisible)
                {
                    _sb.Append($"<p class=\"line\"><span class=\"eq\"><var>{assignVar}</var> = " +
                               $"<span class=\"info\">mesh2d → {nt} triángulos (sin destructuring; usar [xm;ym;tri;bnd] = mesh2d(...))</span></span></p>\n");
                }
            }
        }

        /// <summary>Extrae un valor escalar (RealValue o primer elemento de Vector).</summary>
        private static double ExtractScalar(IValue v, string name)
        {
            if (v is RealValue r) return r.D;
            if (v is Vector vec && vec.Length > 0) return vec[0].D;
            throw new ArgumentException($"{name}: se esperaba un escalar");
        }

        // ─────────────────────────────────────────────────────────────────
        // trimesh(T, x, y[, z])  →  HTML Three.js orbital 3D
        // ─────────────────────────────────────────────────────────────────
        private void ExecuteTrimesh(IValue[] args, string lineStr)
        {
            if (args.Length < 3)
                throw new ArgumentException("trimesh requiere ≥3 args: trimesh(T, x, y[, z])");
            var tri = ExtractTriangleMatrix(args[0]);
            var x = ExtractVector(args[1], "x");
            var y = ExtractVector(args[2], "y");
            double[] z = null;
            if (args.Length >= 4 && args[3] != null)
                z = ExtractVector(args[3], "z");
            else
            {
                z = new double[x.Length]; // z=0 plano horizontal
            }
            EmitThreeJsMesh(tri, x, y, z, wireframe: true, title: "trimesh");
        }

        // ─────────────────────────────────────────────────────────────────
        // trisurf(T, x, y, z)  →  HTML Three.js superficie con colormap
        // ─────────────────────────────────────────────────────────────────
        private void ExecuteTrisurf(IValue[] args, string lineStr)
        {
            if (args.Length != 4)
                throw new ArgumentException("trisurf requiere 4 args: trisurf(T, x, y, z)");
            var tri = ExtractTriangleMatrix(args[0]);
            var x = ExtractVector(args[1], "x");
            var y = ExtractVector(args[2], "y");
            var z = ExtractVector(args[3], "z");
            EmitThreeJsMesh(tri, x, y, z, wireframe: false, title: "trisurf");
        }

        // ─────────────────────────────────────────────────────────────────
        // triplot(T, x, y)  →  HTML SVG 2D
        // ─────────────────────────────────────────────────────────────────
        private void ExecuteTriplot(IValue[] args, string lineStr)
        {
            if (args.Length != 3)
                throw new ArgumentException("triplot requiere 3 args: triplot(T, x, y)");
            var tri = ExtractTriangleMatrix(args[0]);
            var x = ExtractVector(args[1], "x");
            var y = ExtractVector(args[2], "y");
            EmitSvgMesh2D(tri, x, y);
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers: extracción de vectores/matrices desde IValue de Calcpad
        // ─────────────────────────────────────────────────────────────────
        private static double[] ExtractVector(IValue v, string name)
        {
            if (v is RealValue r) return [r.D];
            if (v is Vector vec)
            {
                var arr = new double[vec.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = vec[i].D;
                return arr;
            }
            if (v is Matrix m)
            {
                // Tratar como vector si es 1×N o N×1
                if (m.RowCount == 1)
                {
                    var arr = new double[m.ColCount];
                    for (int i = 0; i < arr.Length; i++) arr[i] = m[0, i].D;
                    return arr;
                }
                if (m.ColCount == 1)
                {
                    var arr = new double[m.RowCount];
                    for (int i = 0; i < arr.Length; i++) arr[i] = m[i, 0].D;
                    return arr;
                }
                throw new ArgumentException($"{name}: matriz {m.RowCount}×{m.ColCount} no es un vector");
            }
            throw new ArgumentException($"{name}: tipo no soportado ({v?.GetType().Name ?? "null"})");
        }

        /// <summary>Extrae triángulos como int[][] (0-based) desde una matriz N×3 de Calcpad (1-based MATLAB).</summary>
        private static int[][] ExtractTriangleMatrix(IValue v)
        {
            if (v is not Matrix m)
                throw new ArgumentException("T debe ser una matriz N×3");
            if (m.ColCount != 3)
                throw new ArgumentException($"T tiene {m.ColCount} columnas, esperaba 3");
            var tris = new int[m.RowCount][];
            for (int i = 0; i < m.RowCount; i++)
            {
                // MATLAB usa 1-based → restar 1
                tris[i] = new[]
                {
                    (int)m[i, 0].D - 1,
                    (int)m[i, 1].D - 1,
                    (int)m[i, 2].D - 1,
                };
            }
            return tris;
        }

        // ─────────────────────────────────────────────────────────────────
        // HTML emit: Three.js (mesh 3D orbital)
        // ─────────────────────────────────────────────────────────────────
        private static int _threeJsCanvasCounter = 0;
        private bool _threeJsBundleEmitted;

        /// <summary>
        /// Lee three.min.js (Three.js r149 UMD, ~600 KB) del recurso embebido
        /// del assembly. Cacheado tras primer acceso. Si el recurso no está
        /// disponible (build incompleto) retorna null.
        /// </summary>
        private static string _threeJsSource;
        private static string LoadThreeJsResource()
        {
            if (_threeJsSource != null) return _threeJsSource;
            try
            {
                var asm = typeof(ExpressionParser).Assembly;
                // Buscar el recurso por nombre — varía según namespace del assembly
                var names = asm.GetManifestResourceNames();
                string target = null;
                foreach (var n in names)
                    if (n.EndsWith("three.min.js", StringComparison.OrdinalIgnoreCase))
                    { target = n; break; }
                if (target == null) return _threeJsSource = "";
                using var stream = asm.GetManifestResourceStream(target);
                if (stream == null) return _threeJsSource = "";
                using var reader = new System.IO.StreamReader(stream);
                _threeJsSource = reader.ReadToEnd();
                return _threeJsSource;
            }
            catch
            {
                return _threeJsSource = "";
            }
        }

        /// <summary>
        /// Emite Three.js r149 (UMD) inline + Mini OrbitControls UNA sola vez
        /// por documento. NO depende de CDN externo — todo está embebido en
        /// el assembly. Funciona perfecto desde <c>file://</c>.
        ///
        /// El template Calcpad tiene su propio Three.js privado en un IIFE,
        /// pero NO lo expone como <c>window.THREE</c>. Esta carga independiente
        /// crea su propio <c>window.THREE</c> que nuestros canvases necesitan.
        /// </summary>
        private void EmitThreeJsBundleOnce()
        {
            if (_threeJsBundleEmitted) return;
            _threeJsBundleEmitted = true;
            // 1) Three.js r149 UMD (con window.THREE global)
            var threeJs = LoadThreeJsResource();
            if (!string.IsNullOrEmpty(threeJs))
            {
                _sb.Append("\n<!-- Calcpad Lab — Three.js r149 UMD inline (608 KB embebido) -->\n");
                _sb.Append("<script>\n");
                _sb.Append(threeJs);
                _sb.Append("\n</script>\n");
            }
            // 2) Mini-OrbitControls: drag = rotar, wheel = zoom, right-drag = pan.
            _sb.Append(@"
<!-- Calcpad Lab — Mini OrbitControls inline (no depende de CDN externo) -->
<script>
(function(){
  if (window.MiniOrbit) return;
  window.MiniOrbit = function(camera, dom, target){
    target = target || new THREE.Vector3(0,0,0);
    var spherical = new THREE.Spherical();
    var offset = new THREE.Vector3();
    var rotateStart = new THREE.Vector2();
    var rotateEnd = new THREE.Vector2();
    var panStart = new THREE.Vector2();
    var panEnd = new THREE.Vector2();
    var state = 'none';
    function updateSpherical(){
      offset.copy(camera.position).sub(target);
      spherical.setFromVector3(offset);
    }
    function applySpherical(){
      offset.setFromSpherical(spherical);
      camera.position.copy(target).add(offset);
      camera.lookAt(target);
    }
    updateSpherical();
    dom.addEventListener('mousedown', function(e){
      e.preventDefault();
      if (e.button === 0){ state = 'rotate'; rotateStart.set(e.clientX, e.clientY); }
      else if (e.button === 2){ state = 'pan'; panStart.set(e.clientX, e.clientY); }
    });
    window.addEventListener('mousemove', function(e){
      if (state === 'rotate'){
        rotateEnd.set(e.clientX, e.clientY);
        var delta = rotateEnd.clone().sub(rotateStart);
        spherical.theta -= 2 * Math.PI * delta.x / dom.clientWidth;
        spherical.phi   -= Math.PI * delta.y / dom.clientHeight;
        spherical.phi = Math.max(0.01, Math.min(Math.PI - 0.01, spherical.phi));
        applySpherical();
        rotateStart.copy(rotateEnd);
      } else if (state === 'pan'){
        panEnd.set(e.clientX, e.clientY);
        var dx = panEnd.x - panStart.x;
        var dy = panEnd.y - panStart.y;
        var dist = camera.position.distanceTo(target);
        var panScale = dist * 0.001;
        var right = new THREE.Vector3();
        camera.getWorldDirection(right);
        right.cross(camera.up).normalize();
        var up = new THREE.Vector3().copy(camera.up).normalize();
        var pan = right.multiplyScalar(-dx * panScale).add(up.multiplyScalar(dy * panScale));
        target.add(pan);
        applySpherical();
        panStart.copy(panEnd);
      }
    });
    window.addEventListener('mouseup', function(){ state = 'none'; });
    dom.addEventListener('contextmenu', function(e){ e.preventDefault(); });
    dom.addEventListener('wheel', function(e){
      e.preventDefault();
      var scale = e.deltaY > 0 ? 1.1 : 0.9;
      spherical.radius *= scale;
      applySpherical();
    });
    return { update: function(){}, target: target };
  };
})();
</script>
");
        }

        private void EmitThreeJsMesh(int[][] tris, double[] x, double[] y, double[] z,
                                     bool wireframe, string title)
        {
            EmitThreeJsBundleOnce();
            int canvasId = System.Threading.Interlocked.Increment(ref _threeJsCanvasCounter);
            var verts = new StringBuilder();
            verts.Append('[');
            for (int i = 0; i < x.Length; i++)
            {
                if (i > 0) verts.Append(',');
                verts.Append(x[i].ToString("G6", CultureInfo.InvariantCulture));
                verts.Append(',');
                verts.Append(z[i].ToString("G6", CultureInfo.InvariantCulture));  // Y_three = Z_matlab
                verts.Append(',');
                verts.Append(y[i].ToString("G6", CultureInfo.InvariantCulture));  // Z_three = Y_matlab (depth)
            }
            verts.Append(']');

            var indices = new StringBuilder();
            indices.Append('[');
            for (int i = 0; i < tris.Length; i++)
            {
                if (i > 0) indices.Append(',');
                indices.Append(tris[i][0]).Append(',')
                       .Append(tris[i][1]).Append(',')
                       .Append(tris[i][2]);
            }
            indices.Append(']');

            // Compute Z range para color gradient (solo si !wireframe)
            double zMin = double.MaxValue, zMax = double.MinValue;
            for (int i = 0; i < z.Length; i++)
            {
                if (z[i] < zMin) zMin = z[i];
                if (z[i] > zMax) zMax = z[i];
            }
            // Compute X range para auto-center camera
            double xMin = x[0], xMax = x[0], yMin = y[0], yMax = y[0];
            for (int i = 1; i < x.Length; i++)
            {
                if (x[i] < xMin) xMin = x[i]; if (x[i] > xMax) xMax = x[i];
                if (y[i] < yMin) yMin = y[i]; if (y[i] > yMax) yMax = y[i];
            }
            double centerX = (xMin + xMax) * 0.5;
            double centerY = (yMin + yMax) * 0.5;
            double rangeMax = Math.Max(xMax - xMin, yMax - yMin);
            double camDist = rangeMax * 1.5;

            // Legend SVG estilo SAP2000 — barra vertical de color con valores
            // Min/Max numéricos. Solo se muestra cuando hay variación de Z
            // (no wireframe puro).
            string legendHtml = "";
            if (!wireframe && Math.Abs(zMax - zMin) > 1e-12)
            {
                var sbLeg = new StringBuilder();
                sbLeg.Append(@"<svg width=""80"" height=""420"" style=""vertical-align:top"" xmlns=""http://www.w3.org/2000/svg"">");
                // Gradient jet (10 stops)
                sbLeg.Append(@"<defs><linearGradient id=""jet-grad-").Append(canvasId).Append(@""" x1=""0"" y1=""1"" x2=""0"" y2=""0"">");
                // Three.js Lut 'rainbow' (estilo awatif) con sRGB→linear×0.6 aplicado
                // Resultado: tonos oscurecidos para mejor lectura con wireframe negro encima
                string[] stops = {
                    "#000099", // 0.0   azul oscurecido
                    "#009999", // 0.2   cian oscurecido
                    "#009900", // 0.5   verde oscurecido
                    "#999900", // 0.8   amarillo oscurecido
                    "#990000"  // 1.0   rojo oscurecido
                };
                double[] offs = { 0.0, 0.2, 0.5, 0.8, 1.0 };
                for (int i = 0; i < stops.Length; i++)
                {
                    sbLeg.Append($"<stop offset=\"{(offs[i] * 100).ToString("G3", CultureInfo.InvariantCulture)}%\" stop-color=\"{stops[i]}\"/>");
                }
                sbLeg.Append(@"</linearGradient></defs>");
                // Barra de color
                sbLeg.Append($"<rect x=\"10\" y=\"20\" width=\"22\" height=\"380\" fill=\"url(#jet-grad-{canvasId})\" stroke=\"#333\" stroke-width=\"0.5\"/>");
                // Tick labels (5 ticks)
                for (int i = 0; i <= 4; i++)
                {
                    double t = i / 4.0;
                    double v = zMin + t * (zMax - zMin);
                    double yLeg = 400 - t * 380; // invertido (Min abajo, Max arriba)
                    sbLeg.Append($"<line x1=\"32\" y1=\"{yLeg.ToString("G6", CultureInfo.InvariantCulture)}\" x2=\"37\" y2=\"{yLeg.ToString("G6", CultureInfo.InvariantCulture)}\" stroke=\"#333\"/>");
                    sbLeg.Append($"<text x=\"40\" y=\"{(yLeg + 4).ToString("G6", CultureInfo.InvariantCulture)}\" font-family=\"sans-serif\" font-size=\"10\" fill=\"#222\">{v.ToString("G4", CultureInfo.InvariantCulture)}</text>");
                }
                // Title
                sbLeg.Append($"<text x=\"10\" y=\"15\" font-family=\"sans-serif\" font-size=\"10\" font-weight=\"bold\" fill=\"#222\">Z</text>");
                sbLeg.Append("</svg>");
                legendHtml = sbLeg.ToString();
            }

            // Emit HTML — usa Three.js global (cargado por EmitThreeJsBundleOnce)
            _sb.Append($@"
<div class=""mesh-viz"" style=""margin:1em 0"">
  <div style=""font:bold 13px sans-serif;color:#444;margin-bottom:4px"">{title} — {tris.Length} triángulos, {x.Length} nodos</div>
  <div style=""display:inline-flex;align-items:flex-start;gap:6px"">
  <canvas id=""three-canvas-{canvasId}"" width=""640"" height=""420"" style=""border:1px solid #ccc;background:#f8f9fb""></canvas>
  {legendHtml}
  </div>
  <script>
  (function() {{
    // El template Calcpad ya carga Three.js inline (r170). Esperamos a que
    // window.THREE y nuestro MiniOrbit estén disponibles.
    function ready() {{ return typeof THREE !== 'undefined' && typeof window.MiniOrbit === 'function'; }}
    function init() {{
      if (!ready()) {{ setTimeout(init, 50); return; }}
      const canvas = document.getElementById('three-canvas-{canvasId}');
      if (!canvas) return;
      const renderer = new THREE.WebGLRenderer({{canvas: canvas, antialias: true}});
      renderer.setClearColor(0xf8f9fb, 1);
      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(45, 640/420, 0.1, 10000);
      camera.position.set({(centerX + camDist).ToString("G6", CultureInfo.InvariantCulture)},
                         {camDist.ToString("G6", CultureInfo.InvariantCulture)},
                         {(centerY + camDist).ToString("G6", CultureInfo.InvariantCulture)});
      const target = new THREE.Vector3({centerX.ToString("G6", CultureInfo.InvariantCulture)}, 0, {centerY.ToString("G6", CultureInfo.InvariantCulture)});
      camera.lookAt(target);
      const controls = window.MiniOrbit(camera, canvas, target);

      // Mesh
      const verts = new Float32Array({verts});
      const idx = new Uint32Array({indices});
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(verts, 3));
      geo.setIndex(new THREE.BufferAttribute(idx, 1));
      geo.computeVertexNormals();

      {(wireframe
        ? @"const mat = new THREE.MeshBasicMaterial({color:0x2266aa, wireframe:true, wireframeLinewidth:1.5});
      const mesh = new THREE.Mesh(geo, mat);
      scene.add(mesh);"
        : $@"// Vertex colors estilo awatif/SAP2000: Three.js LUT 'rainbow'
      // + sRGB→linear + dim×0.6 + MeshBasicMaterial (color puro sin luz).
      // 5 stops: azul → cian → verde → amarillo → rojo
      const zMin={zMin.ToString("G6", CultureInfo.InvariantCulture)}, zMax={zMax.ToString("G6", CultureInfo.InvariantCulture)};
      function rainbowLut(t){{
        // Three.js Lut 'rainbow' stops:
        //   0.0 → #0000FF azul
        //   0.2 → #00FFFF cian
        //   0.5 → #00FF00 verde
        //   0.8 → #FFFF00 amarillo
        //   1.0 → #FF0000 rojo
        // Interpolación lineal entre stops.
        const stops = [
          [0.0, 0.0, 0.0, 1.0],
          [0.2, 0.0, 1.0, 1.0],
          [0.5, 0.0, 1.0, 0.0],
          [0.8, 1.0, 1.0, 0.0],
          [1.0, 1.0, 0.0, 0.0]
        ];
        if (t <= 0) return [stops[0][1], stops[0][2], stops[0][3]];
        if (t >= 1) return [stops[4][1], stops[4][2], stops[4][3]];
        for (let i = 0; i < stops.length - 1; i++) {{
          if (t >= stops[i][0] && t <= stops[i+1][0]) {{
            const u = (t - stops[i][0]) / (stops[i+1][0] - stops[i][0]);
            return [
              stops[i][1] + u * (stops[i+1][1] - stops[i][1]),
              stops[i][2] + u * (stops[i+1][2] - stops[i][2]),
              stops[i][3] + u * (stops[i+1][3] - stops[i][3])
            ];
          }}
        }}
        return [0,0,0];
      }}
      // sRGB → linear (gamma 2.2 aprox) y dim ×0.6 — efecto 'engineering'
      function processColor(c){{
        return [
          Math.pow(c[0], 2.2) * 0.6,
          Math.pow(c[1], 2.2) * 0.6,
          Math.pow(c[2], 2.2) * 0.6
        ];
      }}
      const colors = new Float32Array(verts.length);
      for (let i = 0; i < verts.length / 3; i++) {{
        const z = verts[i*3+1];  // Y_three es Z_matlab
        const t = (zMax > zMin) ? (z - zMin)/(zMax - zMin) : 0.5;
        const c = processColor(rainbowLut(t));
        colors[i*3+0] = c[0];
        colors[i*3+1] = c[1];
        colors[i*3+2] = c[2];
      }}
      geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
      // MeshBasicMaterial: color puro sin lighting (estilo awatif)
      const mat = new THREE.MeshBasicMaterial({{vertexColors:true, side:THREE.DoubleSide}});
      const mesh = new THREE.Mesh(geo, mat);
      mesh.renderOrder = -1;  // detrás de las líneas wireframe
      scene.add(mesh);
      // Wireframe overlay — líneas negras claras (estilo awatif elements)
      const wmat = new THREE.LineBasicMaterial({{color:0x000, opacity:0.9, transparent:false}});
      const wgeo = new THREE.WireframeGeometry(geo);
      scene.add(new THREE.LineSegments(wgeo, wmat));")}

      // Lighting
      scene.add(new THREE.AmbientLight(0xffffff, 0.6));
      const dir = new THREE.DirectionalLight(0xffffff, 0.7);
      dir.position.set(1, 1, 1);
      scene.add(dir);

      // Grid + axes para referencia
      const grid = new THREE.GridHelper({(rangeMax * 1.2).ToString("G6", CultureInfo.InvariantCulture)}, 10, 0xcccccc, 0xeeeeee);
      grid.position.set({centerX.ToString("G6", CultureInfo.InvariantCulture)}, 0, {centerY.ToString("G6", CultureInfo.InvariantCulture)});
      scene.add(grid);
      scene.add(new THREE.AxesHelper({(rangeMax * 0.5).ToString("G6", CultureInfo.InvariantCulture)}));

      function animate() {{
        requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
      }}
      animate();
    }}
    init();
  }})();
  </script>
</div>
");
        }

        // ─────────────────────────────────────────────────────────────────
        // HTML emit: SVG 2D (triplot)
        // ─────────────────────────────────────────────────────────────────
        // ═════════════════════════════════════════════════════════════════
        // 2D Scientific Visualization — heatmap con jet colormap
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// meshgrid(x_vec, y_vec) → [X, Y] matrices.
        /// X[i, j] = x[j], Y[i, j] = y[i] (igual que MATLAB)
        /// </summary>
        private void ExecuteMeshgrid(string assignVar, string[] assignVars, IValue[] args, string lineStr)
        {
            if (args.Length < 1 || args.Length > 2)
                throw new ArgumentException("meshgrid requiere 1 o 2 args: meshgrid(x[, y])");
            var x = ExtractVector(args[0], "x");
            var y = args.Length == 2 ? ExtractVector(args[1], "y") : x;
            int Ny = y.Length, Nx = x.Length;
            var X = new Matrix(Ny, Nx);
            var Y = new Matrix(Ny, Nx);
            for (int i = 0; i < Ny; i++)
            {
                for (int j = 0; j < Nx; j++)
                {
                    X[i, j] = new RealValue(x[j]);
                    Y[i, j] = new RealValue(y[i]);
                }
            }
            if (assignVars != null && assignVars.Length >= 2)
            {
                _parser.SetVariable(assignVars[0], X);
                _parser.SetVariable(assignVars[1], Y);
            }
            else if (!string.IsNullOrEmpty(assignVar))
            {
                _parser.SetVariable(assignVar, X);
            }
        }

        /// <summary>
        /// surf(X, Y, Z) / surf(Z) / mesh(...) → 2D heatmap con jet colormap.
        /// Render: SVG con celdas coloreadas por valor de Z.
        /// </summary>
        private void ExecuteSurf(IValue[] args, string lineStr, string funcName)
        {
            Matrix Z;
            double[] xVec = null, yVec = null;
            if (args.Length == 1)
            {
                Z = IValue.AsMatrix(args[0]);
            }
            else if (args.Length >= 3)
            {
                Z = IValue.AsMatrix(args[2]);
                // X e Y pueden ser matrices (meshgrid) o vectores
                xVec = ExtractGridX(args[0], Z.ColCount);
                yVec = ExtractGridY(args[1], Z.RowCount);
            }
            else
                throw new ArgumentException($"{funcName} requiere 1 o 3 args");
            EmitSvgHeatmap(Z, xVec, yVec, title: funcName, contourLines: 0);
        }

        /// <summary>
        /// contour / contourf con N niveles. contourf=true → filled, false → líneas solas.
        /// </summary>
        private void ExecuteContour(IValue[] args, string lineStr, bool filled)
        {
            Matrix Z;
            double[] xVec = null, yVec = null;
            int nLevels = 10;
            if (args.Length == 1)
                Z = IValue.AsMatrix(args[0]);
            else if (args.Length == 2)
            {
                Z = IValue.AsMatrix(args[0]);
                nLevels = (int)IValue.AsReal(args[1]).D;
            }
            else if (args.Length >= 3)
            {
                Z = IValue.AsMatrix(args[2]);
                xVec = ExtractGridX(args[0], Z.ColCount);
                yVec = ExtractGridY(args[1], Z.RowCount);
                if (args.Length >= 4)
                    nLevels = (int)IValue.AsReal(args[3]).D;
            }
            else
                throw new ArgumentException("contour/contourf requiere 1-4 args");
            EmitSvgHeatmap(Z, xVec, yVec, title: filled ? "contourf" : "contour", contourLines: filled ? 0 : nLevels, fillLevels: filled ? nLevels : 0);
        }

        /// <summary>imagesc(Z) / pcolor(X, Y, Z) → 2D heatmap básico</summary>
        private void ExecuteImagesc(IValue[] args, string lineStr, string funcName)
        {
            Matrix Z;
            double[] xVec = null, yVec = null;
            if (args.Length == 1)
                Z = IValue.AsMatrix(args[0]);
            else if (args.Length >= 3)
            {
                Z = IValue.AsMatrix(args[2]);
                xVec = ExtractGridX(args[0], Z.ColCount);
                yVec = ExtractGridY(args[1], Z.RowCount);
            }
            else
                throw new ArgumentException($"{funcName} requiere 1 o 3 args");
            EmitSvgHeatmap(Z, xVec, yVec, title: funcName, contourLines: 0);
        }

        /// <summary>quiver(X, Y, U, V) → 2D arrows.</summary>
        private void ExecuteQuiver(IValue[] args, string lineStr)
        {
            if (args.Length < 4) throw new ArgumentException("quiver requiere 4 args: quiver(X, Y, U, V)");
            var X = IValue.AsMatrix(args[0]);
            var Y = IValue.AsMatrix(args[1]);
            var U = IValue.AsMatrix(args[2]);
            var V = IValue.AsMatrix(args[3]);
            EmitSvgQuiver(X, Y, U, V);
        }

        /// <summary>plot3(x, y, z) → 3D curve usando Three.js</summary>
        private void ExecutePlot3(IValue[] args, string lineStr)
        {
            if (args.Length < 3) throw new ArgumentException("plot3 requiere 3 args");
            var x = ExtractVector(args[0], "x");
            var y = ExtractVector(args[1], "y");
            var z = ExtractVector(args[2], "z");
            EmitThreeJsCurve3D(x, y, z, "plot3");
        }

        /// <summary>scatter3(x, y, z) → 3D points usando Three.js</summary>
        private void ExecuteScatter3(IValue[] args, string lineStr)
        {
            if (args.Length < 3) throw new ArgumentException("scatter3 requiere 3 args");
            var x = ExtractVector(args[0], "x");
            var y = ExtractVector(args[1], "y");
            var z = ExtractVector(args[2], "z");
            EmitThreeJsPoints3D(x, y, z, "scatter3");
        }

        // ─────────────────────────────────────────────────────────────────
        // peaks: MATLAB demo surface
        //   Z = 3*(1-x)^2 * exp(-x^2 - (y+1)^2)
        //     - 10*(x/5 - x^3 - y^5) * exp(-x^2 - y^2)
        //     - 1/3 * exp(-(x+1)^2 - y^2)
        // Formas soportadas:
        //   peaks()           → 49×49 matriz sobre linspace(-3, 3, 49)
        //   peaks(N)          → N×N matriz sobre linspace(-3, 3, N)
        //   peaks(X, Y)       → evalúa en el grid (X, Y) de meshgrid
        //   peaks(V)          → si V es vector: meshgrid(V, V), luego evalúa
        // ─────────────────────────────────────────────────────────────────
        private void ExecutePeaks(string assignVar, IValue[] args, string lineStr)
        {
            Matrix Z;
            if (args.Length == 0 || args[0] == null)
            {
                Z = ComputePeaksGrid(BuildLinspace(-3.0, 3.0, 49), BuildLinspace(-3.0, 3.0, 49));
            }
            else if (args.Length == 1)
            {
                // Puede ser N (scalar) o V (vector). Si es matriz tipo meshgrid: error,
                // peaks(X) sin Y no aplica (MATLAB tampoco lo acepta).
                if (args[0] is RealValue r)
                {
                    int n = (int)r.D;
                    if (n < 2) throw new ArgumentException("peaks(N): N >= 2");
                    var v = BuildLinspace(-3.0, 3.0, n);
                    Z = ComputePeaksGrid(v, v);
                }
                else
                {
                    var v = ExtractVector(args[0], "peaks");
                    Z = ComputePeaksGrid(v, v);
                }
            }
            else
            {
                // peaks(X, Y) — ambos son matrices del meshgrid (Ny×Nx). Evaluar
                // punto a punto con los valores ya gridded.
                var X = IValue.AsMatrix(args[0]);
                var Y = IValue.AsMatrix(args[1]);
                if (X.RowCount != Y.RowCount || X.ColCount != Y.ColCount)
                    throw new ArgumentException(
                        $"peaks(X,Y): X es {X.RowCount}×{X.ColCount} y Y es {Y.RowCount}×{Y.ColCount}");
                int rows = X.RowCount, cols = X.ColCount;
                Z = new Matrix(rows, cols);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        double xv = X[i, j].D;
                        double yv = Y[i, j].D;
                        Z[i, j] = new RealValue(PeaksValue(xv, yv));
                    }
            }
            if (!string.IsNullOrEmpty(assignVar))
                _parser.SetVariable(assignVar, Z);
        }

        private static double[] BuildLinspace(double a, double b, int n)
        {
            var v = new double[n];
            if (n == 1) { v[0] = a; return v; }
            double step = (b - a) / (n - 1);
            for (int i = 0; i < n; i++) v[i] = a + i * step;
            return v;
        }

        private static Matrix ComputePeaksGrid(double[] xVec, double[] yVec)
        {
            int Nx = xVec.Length, Ny = yVec.Length;
            var Z = new Matrix(Ny, Nx);
            for (int i = 0; i < Ny; i++)
            {
                double y = yVec[i];
                for (int j = 0; j < Nx; j++)
                {
                    double x = xVec[j];
                    Z[i, j] = new RealValue(PeaksValue(x, y));
                }
            }
            return Z;
        }

        private static double PeaksValue(double x, double y)
        {
            double term1 = 3.0 * (1.0 - x) * (1.0 - x) * Math.Exp(-x * x - (y + 1.0) * (y + 1.0));
            double term2 = -10.0 * (x / 5.0 - x * x * x - Math.Pow(y, 5)) * Math.Exp(-x * x - y * y);
            double term3 = -(1.0 / 3.0) * Math.Exp(-(x + 1.0) * (x + 1.0) - y * y);
            return term1 + term2 + term3;
        }

        /// <summary>
        /// Detecta si el arg es un string literal MATLAB: <c>'foo'</c> o <c>"bar"</c>.
        /// Estos no son parseables por MathParser (rechaza <c>'</c> con "Invalid symbol")
        /// y deben dejarse pasar sin evaluar para que los handlers que aceptan strings
        /// (colormap, shading, axis, view) los recuperen via lineStr fallback.
        /// </summary>
        private static bool IsQuotedStringArg(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            if (expr.Length < 2) return false;
            char first = expr[0];
            char last = expr[^1];
            return (first == '\'' && last == '\'') || (first == '"' && last == '"');
        }

        /// <summary>
        /// Intenta parsear un arg como rango MATLAB <c>a:b</c> o <c>a:step:b</c>
        /// (literales numéricos o expresiones simples). Devuelve un Vector Calcpad
        /// con los valores expandidos.
        /// </summary>
        private bool TryParseRangeArg(string expr, out IValue result)
        {
            result = null;
            // El preprocessor convirtió `,` → `;` en args, pero `:` se mantiene.
            // Splittar por `:` top-level (sin () [] {})
            var parts = SplitTopLevelOnChar(expr, ':');
            if (parts.Count < 2 || parts.Count > 3) return false;
            double[] vals = new double[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                try
                {
                    _parser.Parse(parts[i].Trim());
                    _parser.Calculate(false, -1);
                    if (_parser.ResultValue is RealValue r) vals[i] = r.D;
                    else return false;
                }
                catch { return false; }
            }
            double start = vals[0], stop = parts.Count == 2 ? vals[1] : vals[2];
            double step = parts.Count == 3 ? vals[1] : 1.0;
            if (step == 0) return false;
            int n = (int)Math.Floor((stop - start) / step + 1e-9) + 1;
            if (n < 1) n = 0;
            var v = new Vector(n);
            for (int k = 0; k < n; k++)
                v[k] = new RealValue(start + k * step);
            result = v;
            return true;
        }

        private static List<string> SplitTopLevelOnChar(string s, char sep)
        {
            var result = new List<string>();
            int depth = 0;
            bool inSq = false, inDq = false;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(' || c == '[' || c == '{') { depth++; continue; }
                if (c == ')' || c == ']' || c == '}') { depth--; continue; }
                if (depth == 0 && c == sep)
                {
                    result.Add(s[start..i]);
                    start = i + 1;
                }
            }
            result.Add(s[start..]);
            return result;
        }

        /// <summary>randn(N) o randn(N, M) → matriz de samples normales N(0,1)</summary>
        private void ExecuteRandn(string assignVar, IValue[] args, string lineStr)
        {
            int rows = 1, cols = 1;
            if (args.Length >= 1) rows = (int)IValue.AsReal(args[0]).D;
            if (args.Length >= 2) cols = (int)IValue.AsReal(args[1]).D;
            else cols = rows;
            var rng = new Random(42);
            var mat = new Matrix(rows, cols);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    // Box-Muller transform
                    double u1 = rng.NextDouble(), u2 = rng.NextDouble();
                    if (u1 < 1e-12) u1 = 1e-12;
                    double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
                    mat[i, j] = new RealValue(z);
                }
            if (!string.IsNullOrEmpty(assignVar))
                _parser.SetVariable(assignVar, mat);
        }

        /// <summary>histogram(data, [nbins]) → bar chart de distribución</summary>
        private void ExecuteHistogram(IValue[] args, string lineStr)
        {
            if (args.Length < 1) throw new ArgumentException("histogram requiere ≥1 arg");
            double[] data = ExtractFlatData(args[0]);
            int nbins = args.Length >= 2 ? (int)IValue.AsReal(args[1]).D : 20;
            double dMin = double.PositiveInfinity, dMax = double.NegativeInfinity;
            foreach (var d in data)
            {
                if (d < dMin) dMin = d;
                if (d > dMax) dMax = d;
            }
            double binW = (dMax - dMin) / nbins;
            if (binW < 1e-12) binW = 1;
            var counts = new int[nbins];
            foreach (var d in data)
            {
                int bi = (int)((d - dMin) / binW);
                if (bi < 0) bi = 0; if (bi >= nbins) bi = nbins - 1;
                counts[bi]++;
            }
            var xs = new double[nbins];
            var ys = new double[nbins];
            for (int i = 0; i < nbins; i++)
            {
                xs[i] = dMin + (i + 0.5) * binW;
                ys[i] = counts[i];
            }
            EmitSvgBar(xs, ys, $"histogram ({data.Length} samples, {nbins} bins)", binW * 0.8, "#3366cc");
        }

        /// <summary>bar(y) / bar(x, y) / barh(y) → bar chart</summary>
        private void ExecuteBar(IValue[] args, string lineStr, bool horizontal)
        {
            double[] xs, ys;
            if (args.Length == 1)
            {
                ys = ExtractFlatData(args[0]);
                xs = new double[ys.Length];
                for (int i = 0; i < xs.Length; i++) xs[i] = i + 1;
            }
            else
            {
                xs = ExtractFlatData(args[0]);
                ys = ExtractFlatData(args[1]);
            }
            EmitSvgBar(xs, ys, horizontal ? "barh" : "bar", 0.8, "#cc6633");
        }

        /// <summary>stem/stairs/area/semilogy/semilogx/loglog/errorbar → line plot generic</summary>
        private void ExecuteLinePlot(IValue[] args, string lineStr, string funcName)
        {
            if (args.Length < 1) throw new ArgumentException($"{funcName} requiere ≥1 arg");
            double[] xs, ys;
            if (args.Length == 1)
            {
                ys = ExtractFlatData(args[0]);
                xs = new double[ys.Length];
                for (int i = 0; i < xs.Length; i++) xs[i] = i + 1;
            }
            else
            {
                xs = ExtractFlatData(args[0]);
                ys = ExtractFlatData(args[1]);
            }
            EmitSvgLinePlot(xs, ys, funcName, funcName);
        }

        /// <summary>polar(theta, r) → curva en coords polares</summary>
        private void ExecutePolar(IValue[] args, string lineStr)
        {
            if (args.Length < 2) throw new ArgumentException("polar requiere 2 args");
            double[] theta = ExtractFlatData(args[0]);
            double[] r = ExtractFlatData(args[1]);
            int n = Math.Min(theta.Length, r.Length);
            double[] xs = new double[n], ys = new double[n];
            for (int i = 0; i < n; i++) { xs[i] = r[i] * Math.Cos(theta[i]); ys[i] = r[i] * Math.Sin(theta[i]); }
            EmitSvgLinePlot(xs, ys, "polar", "polar");
        }

        /// <summary>compass(u, v) → flechas desde origen</summary>
        private void ExecuteCompass(IValue[] args, string lineStr)
        {
            if (args.Length < 2) throw new ArgumentException("compass requiere 2 args");
            double[] us = ExtractFlatData(args[0]);
            double[] vs = ExtractFlatData(args[1]);
            int n = Math.Min(us.Length, vs.Length);
            var X = new Matrix(1, n);
            var Y = new Matrix(1, n);
            var U = new Matrix(1, n);
            var V = new Matrix(1, n);
            for (int i = 0; i < n; i++)
            {
                X[0, i] = new RealValue(0);
                Y[0, i] = new RealValue(0);
                U[0, i] = new RealValue(us[i]);
                V[0, i] = new RealValue(vs[i]);
            }
            EmitSvgQuiver(X, Y, U, V);
        }

        /// <summary>Extrae datos numéricos como flat array desde scalar/vector/matrix</summary>
        private static double[] ExtractFlatData(IValue v)
        {
            if (v is RealValue r) return new[] { r.D };
            if (v is Vector vec)
            {
                var arr = new double[vec.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = vec[i].D;
                return arr;
            }
            if (v is Matrix m)
            {
                var arr = new double[m.RowCount * m.ColCount];
                int k = 0;
                for (int i = 0; i < m.RowCount; i++)
                    for (int j = 0; j < m.ColCount; j++)
                        arr[k++] = m[i, j].D;
                return arr;
            }
            return new double[0];
        }

        // ─────────────────────────────────────────────────────────────────
        // SVG render: bar chart
        // ─────────────────────────────────────────────────────────────────
        private void EmitSvgBar(double[] xs, double[] ys, string title, double barW, string color)
        {
            const int W = 640, H = 400, M = 50;
            int plotW = W - 2 * M, plotH = H - 2 * M;
            double xMin = xs[0], xMax = xs[0], yMin = 0, yMax = 0;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < xMin) xMin = xs[i];
                if (xs[i] > xMax) xMax = xs[i];
                if (ys[i] < yMin) yMin = ys[i];
                if (ys[i] > yMax) yMax = ys[i];
            }
            double rangeX = Math.Max(xMax - xMin, 1e-12), rangeY = Math.Max(yMax - yMin, 1e-12);
            string TX(double v) => (M + (v - xMin) / rangeX * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TY(double v) => (H - M - (v - yMin) / rangeY * plotH).ToString("G6", CultureInfo.InvariantCulture);
            double bwPx = barW / rangeX * plotW;
            _sb.Append($"<div class=\"plot-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444\">{System.Web.HttpUtility.HtmlEncode(title)}</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\">");
            for (int i = 0; i < xs.Length; i++)
            {
                double cx = (M + (xs[i] - xMin) / rangeX * plotW);
                double ty = (H - M - (ys[i] - yMin) / rangeY * plotH);
                double bh = (H - M) - ty;
                _sb.Append($"<rect x=\"{(cx - bwPx / 2).ToString("G6", CultureInfo.InvariantCulture)}\" y=\"{ty.ToString("G6", CultureInfo.InvariantCulture)}\" width=\"{bwPx.ToString("G6", CultureInfo.InvariantCulture)}\" height=\"{bh.ToString("G6", CultureInfo.InvariantCulture)}\" fill=\"{color}\" stroke=\"#333\"/>");
            }
            _sb.Append($"<rect x=\"{M}\" y=\"{M}\" width=\"{plotW}\" height=\"{plotH}\" stroke=\"#666\" fill=\"none\"/>");
            _sb.Append($"<text x=\"{M}\" y=\"{H - M + 18}\" font-size=\"10\">{xMin:G3}</text>");
            _sb.Append($"<text x=\"{M + plotW - 30}\" y=\"{H - M + 18}\" font-size=\"10\">{xMax:G3}</text>");
            _sb.Append($"<text x=\"{M - 35}\" y=\"{H - M}\" font-size=\"10\">{yMin:G3}</text>");
            _sb.Append($"<text x=\"{M - 35}\" y=\"{M + 8}\" font-size=\"10\">{yMax:G3}</text>");
            _sb.Append("</svg></div>\n");
        }

        // ─────────────────────────────────────────────────────────────────
        // SVG render: line plot (también semilog, polar, area, stairs, stem)
        // ─────────────────────────────────────────────────────────────────
        private void EmitSvgLinePlot(double[] xs, double[] ys, string title, string style)
        {
            const int W = 640, H = 400, M = 50;
            int plotW = W - 2 * M, plotH = H - 2 * M;
            double xMin = xs[0], xMax = xs[0], yMin = ys[0], yMax = ys[0];
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < xMin) xMin = xs[i]; if (xs[i] > xMax) xMax = xs[i];
                if (ys[i] < yMin) yMin = ys[i]; if (ys[i] > yMax) yMax = ys[i];
            }
            bool logY = style == "semilogy" || style == "loglog";
            bool logX = style == "semilogx" || style == "loglog";
            double[] xx = xs, yy = ys;
            if (logX)
            {
                xx = new double[xs.Length];
                for (int i = 0; i < xs.Length; i++) xx[i] = xs[i] > 0 ? Math.Log10(xs[i]) : 0;
                xMin = double.PositiveInfinity; xMax = double.NegativeInfinity;
                for (int i = 0; i < xx.Length; i++) { if (xx[i] < xMin) xMin = xx[i]; if (xx[i] > xMax) xMax = xx[i]; }
            }
            if (logY)
            {
                yy = new double[ys.Length];
                for (int i = 0; i < ys.Length; i++) yy[i] = ys[i] > 0 ? Math.Log10(ys[i]) : 0;
                yMin = double.PositiveInfinity; yMax = double.NegativeInfinity;
                for (int i = 0; i < yy.Length; i++) { if (yy[i] < yMin) yMin = yy[i]; if (yy[i] > yMax) yMax = yy[i]; }
            }
            double rangeX = Math.Max(xMax - xMin, 1e-12), rangeY = Math.Max(yMax - yMin, 1e-12);
            string TX(double v) => (M + (v - xMin) / rangeX * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TY(double v) => (H - M - (v - yMin) / rangeY * plotH).ToString("G6", CultureInfo.InvariantCulture);

            _sb.Append($"<div class=\"plot-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444\">{System.Web.HttpUtility.HtmlEncode(title)}</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\">");

            if (style == "area")
            {
                _sb.Append("<polygon points=\"");
                _sb.Append(TX(xx[0])).Append(',').Append(TY(0)).Append(' ');
                for (int i = 0; i < xx.Length; i++)
                    _sb.Append(TX(xx[i])).Append(',').Append(TY(yy[i])).Append(' ');
                _sb.Append(TX(xx[^1])).Append(',').Append(TY(0));
                _sb.Append("\" fill=\"#88aadd\" stroke=\"#2266aa\" stroke-width=\"1\"/>");
            }
            else if (style == "stairs")
            {
                _sb.Append("<polyline points=\"");
                for (int i = 0; i < xx.Length - 1; i++)
                {
                    _sb.Append(TX(xx[i])).Append(',').Append(TY(yy[i])).Append(' ');
                    _sb.Append(TX(xx[i + 1])).Append(',').Append(TY(yy[i])).Append(' ');
                }
                _sb.Append(TX(xx[^1])).Append(',').Append(TY(yy[^1]));
                _sb.Append("\" stroke=\"#2266aa\" stroke-width=\"1.5\" fill=\"none\"/>");
            }
            else if (style == "stem")
            {
                _sb.Append("<g stroke=\"#2266aa\" stroke-width=\"1\">");
                for (int i = 0; i < xx.Length; i++)
                {
                    _sb.Append($"<line x1=\"{TX(xx[i])}\" y1=\"{TY(0)}\" x2=\"{TX(xx[i])}\" y2=\"{TY(yy[i])}\"/>");
                    _sb.Append($"<circle cx=\"{TX(xx[i])}\" cy=\"{TY(yy[i])}\" r=\"3\" fill=\"#2266aa\"/>");
                }
                _sb.Append("</g>");
            }
            else
            {
                _sb.Append("<polyline points=\"");
                for (int i = 0; i < xx.Length; i++)
                    _sb.Append(TX(xx[i])).Append(',').Append(TY(yy[i])).Append(' ');
                _sb.Append("\" stroke=\"#2266aa\" stroke-width=\"1.5\" fill=\"none\"/>");
            }
            _sb.Append($"<rect x=\"{M}\" y=\"{M}\" width=\"{plotW}\" height=\"{plotH}\" stroke=\"#666\" fill=\"none\"/>");
            _sb.Append("</svg></div>\n");
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers de extracción de grid X/Y
        // ─────────────────────────────────────────────────────────────────
        private static double[] ExtractGridX(IValue v, int nCols)
        {
            if (v is Matrix m && m.RowCount > 1 && m.ColCount > 1)
            {
                // meshgrid output: tomar primera fila
                var arr = new double[m.ColCount];
                for (int j = 0; j < m.ColCount; j++) arr[j] = m[0, j].D;
                return arr;
            }
            return ExtractVector(v, "x");
        }

        private static double[] ExtractGridY(IValue v, int nRows)
        {
            if (v is Matrix m && m.RowCount > 1 && m.ColCount > 1)
            {
                var arr = new double[m.RowCount];
                for (int i = 0; i < m.RowCount; i++) arr[i] = m[i, 0].D;
                return arr;
            }
            return ExtractVector(v, "y");
        }

        // ─────────────────────────────────────────────────────────────────
        // Colormaps. Default: parula (MATLAB R2014b+). Cambiable via colormap('name').
        //   parula   → MATLAB default (purple → azul → teal → amarillo)
        //   jet      → MATLAB legacy (blue → cyan → green → yellow → red)
        //   sap2000  → estilo SAP2000 (rainbow azul → cyan → verde → amarillo → naranja → rojo)
        //   hot      → black → red → yellow → white
        //   cool     → cyan → magenta
        //   gray     → black → white
        // ─────────────────────────────────────────────────────────────────
        private static string _currentColormap = "parula";
        // Custom colormap (cuando user pasa matriz Nx3 RGB)
        private static double[][] _customColormap = null;

        internal static void SetColormap(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _currentColormap = name.Trim().ToLowerInvariant().Trim('\'', '"');
            _customColormap = null;
        }

        internal static void SetColormapFromMatrix(Matrix m)
        {
            // Espera Nx3 con RGB en [0, 1]
            if (m is null) return;
            if (m.ColCount != 3) return;
            if (m.RowCount < 2) return;
            var stops = new double[m.RowCount][];
            for (int i = 0; i < m.RowCount; i++)
                stops[i] = new[] { m[i, 0].D, m[i, 1].D, m[i, 2].D };
            _customColormap = stops;
            _currentColormap = "custom";
        }

        private static (int R, int G, int B) JetColor(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            // Custom colormap (de matriz)
            if (_customColormap != null && _customColormap.Length >= 2)
            {
                var n = _customColormap.Length;
                var rs = new double[n];
                var gs = new double[n];
                var bs = new double[n];
                for (int i = 0; i < n; i++) { rs[i] = _customColormap[i][0]; gs[i] = _customColormap[i][1]; bs[i] = _customColormap[i][2]; }
                return InterpStops(t, rs, gs, bs);
            }
            return _currentColormap switch
            {
                "jet" => InterpStops(t,
                    new[] { 0.0, 0.0, 0.0, 1.0, 1.0 },         // R
                    new[] { 0.0, 1.0, 1.0, 1.0, 0.0 },         // G
                    new[] { 1.0, 1.0, 0.0, 0.0, 0.0 }),        // B
                "sap2000" => InterpStops(t,
                    // SAP2000 9-stop rainbow (idéntico al sap2000_cmap del script MATLAB):
                    //   blue, lt-blue, cyan, aqua-green, green, yellow-green, yellow, orange, red
                    new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.5, 1.0, 1.0, 1.0 },
                    new[] { 0.0, 0.5, 1.0, 1.0, 1.0, 1.0, 1.0, 0.5, 0.0 },
                    new[] { 1.0, 1.0, 1.0, 0.5, 0.0, 0.0, 0.0, 0.0, 0.0 }),
                "hot" => InterpStops(t,
                    new[] { 0.0, 1.0, 1.0, 1.0 },
                    new[] { 0.0, 0.0, 1.0, 1.0 },
                    new[] { 0.0, 0.0, 0.0, 1.0 }),
                "cool" => InterpStops(t,
                    new[] { 0.0, 1.0 },
                    new[] { 1.0, 0.0 },
                    new[] { 1.0, 1.0 }),
                "gray" or "grey" => InterpStops(t,
                    new[] { 0.0, 1.0 },
                    new[] { 0.0, 1.0 },
                    new[] { 0.0, 1.0 }),
                _ => InterpStops(t,
                    // parula (default MATLAB R2014b+)
                    new[] { 0.208, 0.192, 0.157, 0.816, 0.976 },
                    new[] { 0.165, 0.467, 0.682, 0.780, 0.984 },
                    new[] { 0.529, 0.784, 0.518, 0.110, 0.055 }),
            };
        }

        private static (int R, int G, int B) InterpStops(double t, double[] rs, double[] gs, double[] bs)
        {
            int n = rs.Length;
            double scaled = t * (n - 1);
            int idx = (int)Math.Floor(scaled);
            if (idx >= n - 1) idx = n - 2;
            if (idx < 0) idx = 0;
            double frac = scaled - idx;
            double r = rs[idx] + (rs[idx + 1] - rs[idx]) * frac;
            double g = gs[idx] + (gs[idx + 1] - gs[idx]) * frac;
            double b = bs[idx] + (bs[idx + 1] - bs[idx]) * frac;
            return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        // ─────────────────────────────────────────────────────────────────
        // EmitSvgHeatmap: render principal de surf/contourf/imagesc/pcolor
        // Usa interpolación bilineal a alta resolución para output smooth
        // similar a MATLAB contourf con N levels.
        // ─────────────────────────────────────────────────────────────────
        private void EmitSvgHeatmap(Matrix Z, double[] xVec, double[] yVec, string title, int contourLines, int fillLevels = 0)
        {
            int nr = Z.RowCount, nc = Z.ColCount;
            if (xVec == null)
            {
                xVec = new double[nc];
                for (int j = 0; j < nc; j++) xVec[j] = j;
            }
            if (yVec == null)
            {
                yVec = new double[nr];
                for (int i = 0; i < nr; i++) yVec[i] = i;
            }
            // Find min/max of Z
            double zMin = Z[0, 0].D, zMax = Z[0, 0].D;
            for (int i = 0; i < nr; i++)
                for (int j = 0; j < nc; j++)
                {
                    double v = Z[i, j].D;
                    if (v < zMin) zMin = v;
                    if (v > zMax) zMax = v;
                }
            double zRange = Math.Max(zMax - zMin, 1e-12);

            // SVG layout
            const int W = 640, H = 420, M = 50, CBW = 30;
            int plotW = W - 2 * M - CBW;
            int plotH = H - 2 * M;
            double xMin = xVec[0], xMax = xVec[xVec.Length - 1];
            double yMin = yVec[0], yMax = yVec[yVec.Length - 1];
            double rangeX = Math.Max(xMax - xMin, 1e-12);
            double rangeY = Math.Max(yMax - yMin, 1e-12);

            string TX(double v) => (M + (v - xMin) / rangeX * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TY(double v) => (H - M - (v - yMin) / rangeY * plotH).ToString("G6", CultureInfo.InvariantCulture);

            _sb.Append($"<div class=\"plot-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444;margin-bottom:4px\">{System.Web.HttpUtility.HtmlEncode(title)} — {nr}×{nc} grid, range [{zMin:G4}, {zMax:G4}]</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\">");

            // High-resolution rendering via bilinear interpolation.
            // Subdivide each cell into PX × PY sub-cells for smooth contours.
            bool renderFill = contourLines == 0;
            if (renderFill)
            {
                int SUB = 16;  // subdivision per cell — más SUB = más smooth
                double cellW = (double)plotW / ((nc - 1) * SUB);
                double cellH = (double)plotH / ((nr - 1) * SUB);
                for (int i = 0; i < nr - 1; i++)
                {
                    double z00 = Z[i, 0].D;
                    for (int j = 0; j < nc - 1; j++)
                    {
                        double z00c = Z[i, j].D;
                        double z01c = Z[i, j + 1].D;
                        double z10c = Z[i + 1, j].D;
                        double z11c = Z[i + 1, j + 1].D;
                        for (int sy = 0; sy < SUB; sy++)
                        {
                            double fy = (sy + 0.5) / SUB;
                            double zl = z00c + (z10c - z00c) * fy;
                            double zr = z01c + (z11c - z01c) * fy;
                            for (int sx = 0; sx < SUB; sx++)
                            {
                                double fx = (sx + 0.5) / SUB;
                                double zVal = zl + (zr - zl) * fx;
                                double tNorm = (zVal - zMin) / zRange;
                                if (fillLevels > 0)
                                {
                                    int lvl = (int)(tNorm * fillLevels);
                                    if (lvl >= fillLevels) lvl = fillLevels - 1;
                                    if (lvl < 0) lvl = 0;
                                    tNorm = (lvl + 0.5) / fillLevels;
                                }
                                var (r, g, b) = JetColor(tNorm);
                                double px = M + (j * SUB + sx) * cellW;
                                double py = H - M - (i * SUB + sy + 1) * cellH;
                                _sb.Append($"<rect x=\"{px.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{py.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{(cellW + 0.5).ToString("F2", CultureInfo.InvariantCulture)}\" height=\"{(cellH + 0.5).ToString("F2", CultureInfo.InvariantCulture)}\" fill=\"rgb({r},{g},{b})\"/>");
                            }
                        }
                    }
                }
            }

            // Contour lines (simple marching-squares-lite: bisect cell edges)
            if (contourLines > 0)
            {
                for (int lvl = 1; lvl <= contourLines; lvl++)
                {
                    double lvlVal = zMin + (lvl / (double)(contourLines + 1)) * zRange;
                    _sb.Append("<g stroke=\"#333\" stroke-width=\"0.6\" fill=\"none\">");
                    for (int i = 0; i < nr - 1; i++)
                    {
                        for (int j = 0; j < nc - 1; j++)
                        {
                            double z00 = Z[i, j].D, z01 = Z[i, j + 1].D, z10 = Z[i + 1, j].D, z11 = Z[i + 1, j + 1].D;
                            var segs = MarchingSquares(z00, z01, z10, z11, lvlVal,
                                xVec[j], yVec[i], xVec[j + 1], yVec[i + 1]);
                            foreach (var seg in segs)
                            {
                                _sb.Append("<line x1=\"").Append(TX(seg.x1)).Append("\" y1=\"").Append(TY(seg.y1))
                                   .Append("\" x2=\"").Append(TX(seg.x2)).Append("\" y2=\"").Append(TY(seg.y2)).Append("\"/>");
                            }
                        }
                    }
                    _sb.Append("</g>");
                }
            }

            // Axes box
            _sb.Append($"<rect x=\"{M}\" y=\"{M}\" width=\"{plotW}\" height=\"{plotH}\" stroke=\"#666\" stroke-width=\"1\" fill=\"none\"/>");
            // Axis labels
            _sb.Append($"<text x=\"{M}\" y=\"{H - M + 18}\" font-size=\"10\">{xMin:G3}</text>");
            _sb.Append($"<text x=\"{M + plotW - 30}\" y=\"{H - M + 18}\" font-size=\"10\">{xMax:G3}</text>");
            _sb.Append($"<text x=\"{M - 35}\" y=\"{H - M}\" font-size=\"10\">{yMin:G3}</text>");
            _sb.Append($"<text x=\"{M - 35}\" y=\"{M + 8}\" font-size=\"10\">{yMax:G3}</text>");

            // Colorbar (right side)
            int cbX = M + plotW + 20;
            int cbSteps = 32;
            for (int s = 0; s < cbSteps; s++)
            {
                double t = s / (double)(cbSteps - 1);
                var (r, g, b) = JetColor(1 - t);  // top = max
                int cbY = M + (int)(t * plotH);
                int cbH = plotH / cbSteps + 1;
                _sb.Append($"<rect x=\"{cbX}\" y=\"{cbY}\" width=\"{CBW}\" height=\"{cbH}\" fill=\"rgb({r},{g},{b})\"/>");
            }
            _sb.Append($"<rect x=\"{cbX}\" y=\"{M}\" width=\"{CBW}\" height=\"{plotH}\" stroke=\"#666\" fill=\"none\"/>");
            _sb.Append($"<text x=\"{cbX + CBW + 4}\" y=\"{M + 8}\" font-size=\"10\">{zMax:G3}</text>");
            _sb.Append($"<text x=\"{cbX + CBW + 4}\" y=\"{H - M}\" font-size=\"10\">{zMin:G3}</text>");

            _sb.Append("</svg></div>\n");
        }

        // ─────────────────────────────────────────────────────────────────
        // Marching squares: para 1 celda y 1 nivel, devuelve 0-2 segmentos
        // ─────────────────────────────────────────────────────────────────
        private struct Seg { public double x1, y1, x2, y2; }

        private static List<Seg> MarchingSquares(double z00, double z01, double z10, double z11,
            double level, double x0, double y0, double x1, double y1)
        {
            var segs = new List<Seg>();
            int code = 0;
            if (z00 > level) code |= 1;
            if (z01 > level) code |= 2;
            if (z11 > level) code |= 4;
            if (z10 > level) code |= 8;
            if (code == 0 || code == 15) return segs;
            double Interp(double a, double b, double xa, double xb) =>
                xa + (level - a) / (b - a) * (xb - xa);
            // Edges: bottom (z00-z01), right (z01-z11), top (z10-z11), left (z00-z10)
            double bX = z00 != z01 ? Interp(z00, z01, x0, x1) : x0;
            double bY = y0;
            double rX = x1;
            double rY = z01 != z11 ? Interp(z01, z11, y0, y1) : y0;
            double tX = z10 != z11 ? Interp(z10, z11, x0, x1) : x0;
            double tY = y1;
            double lX = x0;
            double lY = z00 != z10 ? Interp(z00, z10, y0, y1) : y0;
            switch (code)
            {
                case 1: case 14: segs.Add(new Seg { x1 = lX, y1 = lY, x2 = bX, y2 = bY }); break;
                case 2: case 13: segs.Add(new Seg { x1 = bX, y1 = bY, x2 = rX, y2 = rY }); break;
                case 3: case 12: segs.Add(new Seg { x1 = lX, y1 = lY, x2 = rX, y2 = rY }); break;
                case 4: case 11: segs.Add(new Seg { x1 = rX, y1 = rY, x2 = tX, y2 = tY }); break;
                case 6: case 9: segs.Add(new Seg { x1 = bX, y1 = bY, x2 = tX, y2 = tY }); break;
                case 7: case 8: segs.Add(new Seg { x1 = lX, y1 = lY, x2 = tX, y2 = tY }); break;
                case 5: segs.Add(new Seg { x1 = lX, y1 = lY, x2 = bX, y2 = bY });
                        segs.Add(new Seg { x1 = rX, y1 = rY, x2 = tX, y2 = tY }); break;
                case 10: segs.Add(new Seg { x1 = bX, y1 = bY, x2 = rX, y2 = rY });
                         segs.Add(new Seg { x1 = lX, y1 = lY, x2 = tX, y2 = tY }); break;
            }
            return segs;
        }

        // ─────────────────────────────────────────────────────────────────
        // Quiver (2D vector field)
        // ─────────────────────────────────────────────────────────────────
        private void EmitSvgQuiver(Matrix X, Matrix Y, Matrix U, Matrix V)
        {
            const int W = 640, H = 420, M = 40;
            int plotW = W - 2 * M, plotH = H - 2 * M;
            int nr = X.RowCount, nc = X.ColCount;
            double xMin = double.PositiveInfinity, xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
            double maxLen = 0;
            for (int i = 0; i < nr; i++)
                for (int j = 0; j < nc; j++)
                {
                    double x = X[i, j].D, y = Y[i, j].D;
                    double u = U[i, j].D, v = V[i, j].D;
                    xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
                    yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
                    double l = Math.Sqrt(u * u + v * v);
                    if (l > maxLen) maxLen = l;
                }
            double rangeX = Math.Max(xMax - xMin, 1e-12), rangeY = Math.Max(yMax - yMin, 1e-12);
            double cellX = rangeX / nc, cellY = rangeY / nr;
            double arrowScale = Math.Min(cellX, cellY) / Math.Max(maxLen, 1e-12) * 0.8;
            string TX(double v) => (M + (v - xMin) / rangeX * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TY(double v) => (H - M - (v - yMin) / rangeY * plotH).ToString("G6", CultureInfo.InvariantCulture);

            _sb.Append($"<div class=\"plot-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444;margin-bottom:4px\">quiver — {nr}×{nc} arrows</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\">");
            _sb.Append("<defs><marker id=\"qa\" markerWidth=\"6\" markerHeight=\"6\" refX=\"5\" refY=\"3\" orient=\"auto\"><path d=\"M0,0 L6,3 L0,6 z\" fill=\"#2266aa\"/></marker></defs>");
            _sb.Append("<g stroke=\"#2266aa\" stroke-width=\"1.2\">");
            for (int i = 0; i < nr; i++)
            {
                for (int j = 0; j < nc; j++)
                {
                    double x = X[i, j].D, y = Y[i, j].D;
                    double u = U[i, j].D * arrowScale, v = V[i, j].D * arrowScale;
                    _sb.Append($"<line x1=\"{TX(x)}\" y1=\"{TY(y)}\" x2=\"{TX(x + u)}\" y2=\"{TY(y + v)}\" marker-end=\"url(#qa)\"/>");
                }
            }
            _sb.Append("</g></svg></div>\n");
        }

        // ─────────────────────────────────────────────────────────────────
        // 3D curve / points via Three.js (placeholder simple — SVG 2D proyectado)
        // ─────────────────────────────────────────────────────────────────
        private void EmitThreeJsCurve3D(double[] x, double[] y, double[] z, string title)
        {
            // Simple isometric projection 2D para empezar (Three.js full TODO)
            int n = x.Length;
            double xMin = x[0], xMax = x[0], yMin = y[0], yMax = y[0], zMin = z[0], zMax = z[0];
            for (int i = 1; i < n; i++)
            {
                if (x[i] < xMin) xMin = x[i]; if (x[i] > xMax) xMax = x[i];
                if (y[i] < yMin) yMin = y[i]; if (y[i] > yMax) yMax = y[i];
                if (z[i] < zMin) zMin = z[i]; if (z[i] > zMax) zMax = z[i];
            }
            const int W = 640, H = 420, M = 40;
            int plotW = W - 2 * M, plotH = H - 2 * M;
            // Iso: u = (x - y) cos(30), v = z + (x + y) sin(30)
            double cos30 = Math.Cos(Math.PI / 6), sin30 = Math.Sin(Math.PI / 6);
            double[] u = new double[n], v = new double[n];
            double uMin = double.PositiveInfinity, uMax = double.NegativeInfinity, vMin = double.PositiveInfinity, vMax = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                u[i] = (x[i] - y[i]) * cos30;
                v[i] = z[i] + (x[i] + y[i]) * sin30;
                uMin = Math.Min(uMin, u[i]); uMax = Math.Max(uMax, u[i]);
                vMin = Math.Min(vMin, v[i]); vMax = Math.Max(vMax, v[i]);
            }
            double rU = Math.Max(uMax - uMin, 1e-12), rV = Math.Max(vMax - vMin, 1e-12);
            string TU(double val) => (M + (val - uMin) / rU * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TV(double val) => (H - M - (val - vMin) / rV * plotH).ToString("G6", CultureInfo.InvariantCulture);

            _sb.Append($"<div class=\"plot-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444;margin-bottom:4px\">{title} — {n} puntos 3D (proyección isométrica)</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\">");
            _sb.Append("<polyline points=\"");
            for (int i = 0; i < n; i++)
            {
                _sb.Append(TU(u[i])).Append(',').Append(TV(v[i])).Append(' ');
            }
            _sb.Append("\" stroke=\"#2266aa\" stroke-width=\"1.5\" fill=\"none\"/>");
            _sb.Append("</svg></div>\n");
        }

        private void EmitThreeJsPoints3D(double[] x, double[] y, double[] z, string title)
        {
            int n = x.Length;
            double cos30 = Math.Cos(Math.PI / 6), sin30 = Math.Sin(Math.PI / 6);
            double[] u = new double[n], v = new double[n];
            double uMin = double.PositiveInfinity, uMax = double.NegativeInfinity, vMin = double.PositiveInfinity, vMax = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                u[i] = (x[i] - y[i]) * cos30;
                v[i] = z[i] + (x[i] + y[i]) * sin30;
                uMin = Math.Min(uMin, u[i]); uMax = Math.Max(uMax, u[i]);
                vMin = Math.Min(vMin, v[i]); vMax = Math.Max(vMax, v[i]);
            }
            const int W = 640, H = 420, M = 40;
            int plotW = W - 2 * M, plotH = H - 2 * M;
            double rU = Math.Max(uMax - uMin, 1e-12), rV = Math.Max(vMax - vMin, 1e-12);
            string TU(double val) => (M + (val - uMin) / rU * plotW).ToString("G6", CultureInfo.InvariantCulture);
            string TV(double val) => (H - M - (val - vMin) / rV * plotH).ToString("G6", CultureInfo.InvariantCulture);
            _sb.Append($"<div class=\"plot-viz\"><div style=\"font:bold 13px sans-serif\">{title} — {n} puntos 3D</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#fafafa\"><g fill=\"#cc3333\">");
            for (int i = 0; i < n; i++)
                _sb.Append($"<circle cx=\"{TU(u[i])}\" cy=\"{TV(v[i])}\" r=\"3\"/>");
            _sb.Append("</g></svg></div>\n");
        }

        private void EmitSvgMesh2D(int[][] tris, double[] x, double[] y)
        {
            double xMin = x[0], xMax = x[0], yMin = y[0], yMax = y[0];
            for (int i = 1; i < x.Length; i++)
            {
                if (x[i] < xMin) xMin = x[i]; if (x[i] > xMax) xMax = x[i];
                if (y[i] < yMin) yMin = y[i]; if (y[i] > yMax) yMax = y[i];
            }
            double rangeX = Math.Max(xMax - xMin, 1e-9);
            double rangeY = Math.Max(yMax - yMin, 1e-9);
            const int W = 640, H = 420;
            const int M = 30; // margen
            double sx = (W - 2 * M) / rangeX;
            double sy = (H - 2 * M) / rangeY;
            double s = Math.Min(sx, sy);
            // Centro
            double offX = M + (W - 2 * M - rangeX * s) * 0.5;
            double offY = M + (H - 2 * M - rangeY * s) * 0.5;

            string TX(double v) => (offX + (v - xMin) * s).ToString("G6", CultureInfo.InvariantCulture);
            string TY(double v) => (H - offY - (v - yMin) * s).ToString("G6", CultureInfo.InvariantCulture);

            _sb.Append($"<div class=\"mesh-viz\" style=\"margin:1em 0\"><div style=\"font:bold 13px sans-serif;color:#444;margin-bottom:4px\">triplot — {tris.Length} triángulos, {x.Length} nodos</div>");
            _sb.Append($"<svg width=\"{W}\" height=\"{H}\" style=\"border:1px solid #ccc;background:#f8f9fb\">");
            // Edges
            _sb.Append("<g stroke=\"#2266aa\" stroke-width=\"1\" fill=\"#e8f0ff\" fill-opacity=\"0.5\">");
            foreach (var t in tris)
            {
                _sb.Append("<polygon points=\"");
                _sb.Append(TX(x[t[0]])); _sb.Append(',');
                _sb.Append(TY(y[t[0]])); _sb.Append(' ');
                _sb.Append(TX(x[t[1]])); _sb.Append(',');
                _sb.Append(TY(y[t[1]])); _sb.Append(' ');
                _sb.Append(TX(x[t[2]])); _sb.Append(',');
                _sb.Append(TY(y[t[2]]));
                _sb.Append("\"/>");
            }
            _sb.Append("</g>");
            // Nodes
            _sb.Append("<g fill=\"#cc3333\">");
            for (int i = 0; i < x.Length; i++)
            {
                _sb.Append($"<circle cx=\"{TX(x[i])}\" cy=\"{TY(y[i])}\" r=\"2\"/>");
            }
            _sb.Append("</g>");
            _sb.Append("</svg></div>\n");
        }
    }
}
