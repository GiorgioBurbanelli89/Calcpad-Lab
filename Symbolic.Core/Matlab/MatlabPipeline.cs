// =============================================================================
// Calcpad Lab — MATLAB Pipeline: facade Tokenizer + Parser + Evaluator + HtmlWriter
// =============================================================================
//   Entry-point único para ejecutar un fragmento MATLAB y obtener HTML output.
//   ESTE pipeline NO usa MathParser/ExpressionParser de Calcpad — es 100% propio.
//
//   Uso desde ExpressionParser para líneas detectadas como MATLAB-puro:
//
//     var html = MatlabPipeline.Run(line, scope, out var result);
//     if (html != null) _sb.Append(html);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core.Matlab
{
    public sealed class MatlabPipeline
    {
        private readonly MatlabEvaluator _evaluator = new();
        public MatlabScope GlobalScope => _evaluator.Globals;

        /// <summary>Si está en true, evita las mutaciones retroactivas del StringBuilder
        /// (inline comments / multi-stmt same-line) que rompen el streaming chunk-based,
        /// ya que el chunk previo ya fue enviado al UI. En modo streaming los inline
        /// comments y multi-stmts se renderean como `<p>` standalone.</summary>
        public bool StreamingMode { get; set; }

        /// <summary>Modo Octave: habilita las extensiones de sintaxis Octave sobre el motor
        /// MATLAB (comentarios <c>#</c>, <c>+= ++ --</c>, <c>endfor/endif/...</c>, <c>!</c>,
        /// <c>do…until</c>, <c>printf</c>, continuación con <c>\</c>). Calcpad-Octave lo pone
        /// en true; Calcpad-Lab lo deja en false (MATLAB estricto). Por defecto se toma de
        /// la variable de entorno <c>CALCPAD_OCTAVE=1</c> (gancho de pruebas).</summary>
        public bool OctaveMode { get; set; }
            = Environment.GetEnvironmentVariable("CALCPAD_OCTAVE") == "1";

        /// <summary>Fires antes de ejecutar cada statement top-level (line, sourceText).
        /// La UI lo usa para mostrar "Calculando línea N..." progresivamente.</summary>
        public event Action<int> StatementStarting;
        /// <summary>Fires después de ejecutar cada statement top-level. `chunkHtml`
        /// es el HTML emitido por ese statement (incluyendo disp/plot flushes).
        /// La UI lo appendea al output panel sin esperar a que termine el script.</summary>
        public event Action<int, string> StatementCompleted;
        /// <summary>Fires una vez al finalizar el script (después del foreach principal,
        /// incluye la figura final si quedó abierta).</summary>
        public event Action<string> ScriptFinished;

        /// <summary>
        /// Procesa un fragmento de código MATLAB. Devuelve HTML concatenado de
        /// todos los statements. Lanza <see cref="MatlabParseException"/> o
        /// <see cref="MatlabRuntimeException"/> en caso de error.
        /// </summary>
        public string Run(string source)
        {
            MatlabTokenizer.OctaveMode = OctaveMode;
            _evaluator.OctaveMode = OctaveMode;
            var tokens = MatlabTokenizer.Tokenize(source);
            var parser = new MatlabParser(tokens);
            var stmts = parser.ParseAllStatements();
            // PRE-PASS: registrar todas las function/classdef ANTES de ejecutar
            // (MATLAB permite usar helpers definidos al final del script)
            foreach (var stmt in stmts)
            {
                if (stmt is FunctionDef fd2)
                    _evaluator.RegisterFunction(fd2);
                else if (stmt is ClassDef cd2)
                    _evaluator.RegisterClass(cd2);
            }
            var sb = new StringBuilder();
            // Re-route stdout (disp) y HTML inline (plots) al output
            var dispBuffer = new StringBuilder();
            var htmlBuffer = new StringBuilder();
            // Bloque  % #hide … % #show  (estilo Calcpad): ejecuta TODO pero no renderiza nada
            // en Lab (oculta fprintf/disp/resultados). En MATLAB el codigo corre igual.
            bool hidden = false;
            _evaluator.Output = msg => { if (!hidden) dispBuffer.AppendLine(msg); };
            _evaluator.HtmlOut = html => { if (!hidden) htmlBuffer.Append(html); };
            // Marca de streaming: hasta dónde de `sb` ya se emitió en vivo. Declarado
            // ACÁ (antes de InnerStmtOut) para que el lambda pueda avanzarlo al emitir
            // chunks por iteración y el flush top-level no los reenvíe (evita duplicados).
            int pendingChunkStart = 0;
            int pendingChunkLine = -1;
            // Helper para generar anchor de línea clickeable (formato Calcpad-compatible)
            // El template Calcpad captura <a href="#0">, lee data-text, y dispara LineClicked(N)
            string LineLink(int line) => $"[<a href=\"#0\" data-text=\"{line}\">{line}</a>]";

            // Funciones "void" (side-effect only): cuando se invocan como statement,
            // MATLAB NO muestra eco del call. Solo se muestra su side effect.
            // Si el statement es uno de estos calls, suprimimos el render.
            var voidFuncs = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                // I/O
                "fprintf", "printf", "disp", "display", "warning", "error",
                "puts", "fputs", "fdisp", "fflush",   // Octave
                // Plot management
                "figure", "clf", "close", "hold", "axis", "grid", "legend", "box", "colormap",
                "title", "xlabel", "ylabel", "zlabel", "colorbar", "sgtitle", "caxis", "clim",
                "shading", "view", "light", "lighting", "material", "camlight", "drawnow",
                // Plot primitives (efecto sobre figura, no return value útil)
                "plot", "plot3", "scatter", "scatter3", "bar", "barh", "stem", "stairs",
                "polar", "polarplot", "fill", "fill3", "patch", "line", "text",
                "histogram", "histogram2", "heatmap", "contour", "contourf", "imagesc",
                "surf", "mesh", "surfc", "meshc", "quiver", "quiver3", "streamslice",
                "trisurf", "trimesh", "triplot", "spy", "loglog", "semilogx", "semilogy",
                "area", "errorbar", "boxplot", "pie", "fplot",
                // System / file
                "mkdir", "save", "saveas", "load", "clear", "format", "echo", "pkg",
                "tic", "toc",
                "syms", "global", "persistent",
                "subplot", "tight_layout",
            };
            bool IsVoidStatement(MatlabNode s)
            {
                if (s is ExprStmt es && es.Expr is CallOrIndex ci && ci.Target is IdentRef ir)
                    return voidFuncs.Contains(ir.Name);
                return false;
            }

            // Inner statements (dentro de for/while/if/switch/try): emitir como
            // <p class="line indent"> con leve indentación visual y data-line para click→nav
            _evaluator.InnerStmtOut = (innerStmt, innerRes) =>
            {
                int innerLine = innerStmt?.Line ?? 0;
                // Se construye el chunk de ESTE statement interno y se emite enseguida:
                //   * StreamingMode (WPF) -> StatementCompleted EN VIVO, por iteracion
                //     (el usuario ve iter 1, iter 2... a medida que calcula).
                //   * Sino (CLI/export) -> a htmlBuffer (batch, se vuelca tras el loop).
                var chunk = new StringBuilder();
                // (A) Salida de I/O (fprintf/disp) producida por ESTE statement.
                if (dispBuffer.Length > 0)
                {
                    var dispRawI = dispBuffer.ToString().TrimEnd();
                    var dispProcessedI = RenderDispWithMatrices(dispRawI);
                    var encodedI = EncodeWithHtmlSegments(dispProcessedI);
                    var stretchedI = StretchInlineBrackets(encodedI);
                    chunk.Append($"<p class=\"line\" id=\"line-{innerLine}\" style=\"margin-left:1.5em;color:#555\"><span class=\"eq\"><span style=\"white-space:pre-wrap\">{stretchedI}</span></span></p>\n");
                    dispBuffer.Clear();
                }
                // (B) Echo del statement interno (NO suprimido, NO comentario, NO void).
                //     Los comentarios dentro de loops se omiten (sino se repiten por iter).
                if (!innerRes.Suppressed && !(innerStmt is CommentStmt) && !IsVoidStatement(innerStmt))
                {
                    chunk.Append($"<p class=\"line\" id=\"line-{innerLine}\" style=\"margin-left:1.5em;color:#555\">");
                    chunk.Append(MatlabHtmlWriter.RenderStatement(innerStmt, innerRes));
                    chunk.Append("</p>\n");
                }
                if (chunk.Length == 0) return;
                if (StreamingMode && StatementCompleted != null)
                {
                    sb.Append(chunk);                                  // persistir en el doc final
                    StatementCompleted.Invoke(innerLine, chunk.ToString()); // emitir EN VIVO (por iteración)
                    pendingChunkStart = sb.Length;                     // ya emitido: el flush top-level no lo repite
                }
                else
                {
                    htmlBuffer.Append(chunk);                          // batch (CLI/export)
                }
            };
            // Pre-pass: regla Calcpad-Lab para multi-stmt en una linea fuente.
            // Si `a=1; b=2; c=3` esta todo en una linea, el `;` FINAL (despues de c)
            // determina si TODOS los stmts de esa linea se muestran. Es decir,
            // override del Suppressed individual: todos heredan el Suppressed del
            // ULTIMO stmt no-comment de la linea. Esto desvia de MATLAB (que aplica
            // `;` per-stmt) pero matchea la expectativa del usuario.
            {
                var byLine = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
                for (int i = 0; i < stmts.Count; i++)
                {
                    if (stmts[i] is CommentStmt) continue;
                    int ln = stmts[i]?.Line ?? 0;
                    if (ln <= 0) continue;
                    if (!byLine.TryGetValue(ln, out var lst)) byLine[ln] = lst = new System.Collections.Generic.List<int>();
                    lst.Add(i);
                }
                foreach (var kv in byLine)
                {
                    if (kv.Value.Count < 2) continue;
                    int lastIdx = kv.Value[kv.Value.Count - 1];
                    bool lastSup = GetSuppressed(stmts[lastIdx]);
                    foreach (var idx in kv.Value)
                        SetSuppressed(stmts[idx], lastSup);
                }
                static bool GetSuppressed(MatlabNode n) =>
                    n is Assignment a ? a.Suppressed : (n is ExprStmt e ? e.Suppressed : false);
                static void SetSuppressed(MatlabNode n, bool v)
                { if (n is Assignment a) a.Suppressed = v; else if (n is ExprStmt e) e.Suppressed = v; }
            }

            // Tracking para comentarios inline (mismo line# que stmt previo no-comment):
            //   - Si el stmt previo NO fue suprimido por `;` → comentario se rendea
            //     como caption SIN `%` al frente.
            //   - Si el stmt previo SI fue suprimido (`;`) → el comentario tambien
            //     se suprime (no hay output al que adjuntarlo).
            //   - Si el comentario esta en su propia linea → comportamiento default
            //     (con `%`).
            int prevNonCommentLine = -1;
            bool prevWasSuppressed = false;
            // Tracking del line# del ultimo <p> emitido a `sb`. Usado por las
            // captions inline para decidir si pegar dentro del mismo <p> (cuando
            // matchea la linea) o emit standalone.
            int lastEmittedPLine = -1;
            // Streaming buffering por linea-fuente: acumulamos todos los stmts
            // que comparten line# en un solo chunk para que la logica de merge
            // (inline-comment / multi-stmt) pueda mutar `sb` antes de enviar al
            // UI. (pendingChunkStart/pendingChunkLine se declararon arriba para que
            // InnerStmtOut pueda avanzar la marca al emitir chunks por iteración.)
            pendingChunkStart = sb.Length;
            pendingChunkLine = -1;

            foreach (var stmt in stmts)
            {
                int stmtLine = stmt?.Line ?? 0;

                // Directiva de bloque  % #hide … % #show  (estilo Calcpad puro):
                // oculta el RENDER de todo lo que sigue (resultados, fprintf, disp, plots)
                // pero lo sigue EJECUTANDO. La directiva misma no se muestra.
                if (stmt is CommentStmt cdir0 && !cdir0.IsHeading)
                {
                    var dt0 = cdir0.Text.Trim();
                    if (dt0 == "#hide") { hidden = true; continue; }
                    if (dt0 == "#show") { hidden = false; continue; }
                }

                // Decision temprana sobre inline-comment: necesita conocer el
                // stmt previo no-comment.
                bool isInlineComment = stmt is CommentStmt csInline
                                       && !csInline.IsHeading
                                       && !csInline.Text.StartsWith("--")
                                       && prevNonCommentLine >= 0
                                       && stmtLine == prevNonCommentLine;
                if (isInlineComment && prevWasSuppressed)
                {
                    // Skipear sin ejecutar ni alterar el tracking
                    continue;
                }

                // Streaming: si cambiamos de linea-fuente, flushear chunk pendiente
                // (todos los stmts de la linea anterior ya rendearon a `sb`).
                if (StreamingMode && pendingChunkLine != -1
                    && pendingChunkLine != stmtLine
                    && sb.Length > pendingChunkStart
                    && StatementCompleted != null)
                {
                    var pending = sb.ToString(pendingChunkStart, sb.Length - pendingChunkStart);
                    StatementCompleted.Invoke(pendingChunkLine, pending);
                    pendingChunkStart = sb.Length;
                }
                pendingChunkLine = stmtLine;
                StatementStarting?.Invoke(stmtLine);
                try {

                StatementResult result;
                try { result = _evaluator.ExecuteOne(stmt, _evaluator.Globals); }
                catch (MatlabRuntimeException ex)
                {
                    sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">Error: {System.Net.WebUtility.HtmlEncode(ex.Message)} (line {LineLink(stmtLine)})</p>\n");
                    continue;
                }
                catch (Exception ex)
                {
                    sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">Internal error: {System.Net.WebUtility.HtmlEncode(ex.Message)} (line {LineLink(stmtLine)})</p>\n");
                    continue;
                }
                // Flush disp buffer.
                // Importante: NO usar <pre> (fuerza monospace del navegador y rompe
                // el Georgia Pro del template, además anula los colores `.eq var`,
                // `.eq i`, `.eq sub` definidos en template.html). Usar un <span>
                // con white-space:pre-wrap para preservar los espacios sin perder
                // la familia tipográfica heredada de .eq.
                if (dispBuffer.Length > 0)
                {
                    var dispRaw = dispBuffer.ToString().TrimEnd();
                    var dispProcessed = RenderDispWithMatrices(dispRaw);
                    var encoded = EncodeWithHtmlSegments(dispProcessed);
                    var stretched = StretchInlineBrackets(encoded);
                    sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\"><span class=\"eq\"><span style=\"white-space:pre-wrap\">{stretched}</span></span></p>\n");
                    lastEmittedPLine = stmtLine;
                    dispBuffer.Clear();
                }
                // Render del statement (incluye el comando como fórmula)
                // NO renderizar void functions (fprintf/disp/figure/plot/...) — solo su side effect.
                // NO renderizar comentarios `%-- ...` (anotación pura de código, opt-in hide).
                bool isHiddenComment = stmt is CommentStmt csHide
                                       && !csHide.IsHeading
                                       && csHide.Text.StartsWith("--");
                if (!result.Suppressed && !IsVoidStatement(stmt) && !isHiddenComment && !hidden)
                {
                    try
                    {
                        if (stmt is CommentStmt cs && cs.IsHeading)
                        {
                            sb.Append(MatlabHtmlWriter.RenderStatement(stmt, result));
                            sb.Append("\n");
                        }
                        else if (isInlineComment)
                        {
                            // Comentario inline: render como caption SIN `%`, en la
                            // MISMA linea visual que el assignment previo. Verifica
                            // que el ultimo <p> emitido sea de la misma linea fuente
                            // (caso assignment renderizado). Si no (void stmt
                            // intermedio, etc.), emit standalone.
                            var csInline2 = (CommentStmt)stmt;
                            var encodedText = System.Net.WebUtility.HtmlEncode(csInline2.Text);
                            // Comentario en NEGRO como el texto `'...` de Calcpad puro (no verde).
                            var captionSpan = $"<span style=\"margin-left:1.5em\">{encodedText}</span>";
                            const string closeTag = "</p>\n";
                            // Streaming mode tambien permite esta mutacion porque el chunk
                            // se difiere hasta el cambio de linea-fuente (ver loop principal):
                            // todos los stmts de la misma linea acumulan en `sb` antes de
                            // enviarse como un solo chunk al UI.
                            bool sameLinePreviousP = lastEmittedPLine == stmtLine
                                && sb.Length >= closeTag.Length
                                && sb.ToString(sb.Length - closeTag.Length, closeTag.Length) == closeTag;
                            if (sameLinePreviousP)
                            {
                                sb.Length -= closeTag.Length;
                                sb.Append(captionSpan);
                                sb.Append(closeTag);
                            }
                            else
                            {
                                // Fallback: void stmt o gap. Standalone.
                                sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\">{captionSpan}</p>\n");
                                lastEmittedPLine = stmtLine;
                            }
                        }
                        else
                        {
                            // Si el stmt anterior emitido pertenece a la MISMA linea
                            // fuente (caso multi-stmt `a=1; b=2`), appendear al mismo
                            // <p> con separador inline en vez de abrir uno nuevo.
                            var stmtHtml = MatlabHtmlWriter.RenderStatement(stmt, result);
                            const string closeTag2 = "</p>\n";
                            // Streaming mode tambien permite esta mutacion (chunk diferido).
                            bool appendSameLine = lastEmittedPLine == stmtLine
                                && sb.Length >= closeTag2.Length
                                && sb.ToString(sb.Length - closeTag2.Length, closeTag2.Length) == closeTag2;
                            if (appendSameLine)
                            {
                                sb.Length -= closeTag2.Length;
                                sb.Append("<span style=\"display:inline-block;width:2em\"></span>");
                                sb.Append(stmtHtml);
                                sb.Append(closeTag2);
                            }
                            else
                            {
                                sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\">");
                                sb.Append(stmtHtml);
                                sb.Append("</p>\n");
                                lastEmittedPLine = stmtLine;
                            }
                        }
                    }
                    catch (Exception renderEx)
                    {
                        sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">Render error: {System.Net.WebUtility.HtmlEncode(renderEx.GetType().Name + ": " + renderEx.Message)} (line {LineLink(stmtLine)})</p>\n");
                    }
                }

                // Actualizar tracking para el proximo statement
                if (!(stmt is CommentStmt))
                {
                    prevNonCommentLine = stmtLine;
                    prevWasSuppressed = result.Suppressed;
                }
                // Flush plot HTML buffer DESPUÉS de la línea del statement
                if (htmlBuffer.Length > 0)
                {
                    sb.Append(htmlBuffer);
                    htmlBuffer.Clear();
                }

                } finally {
                    // Streaming: NO emitir aquí — diferimos hasta el cambio de
                    // linea-fuente (siguiente iteracion) o el final del script,
                    // para que la logica de merge mismo-renglón pueda mutar `sb`
                    // antes de que el chunk se envie al UI.
                }
            }
            // Streaming: flushear el chunk pendiente de la ultima linea procesada.
            if (StreamingMode && pendingChunkLine != -1
                && sb.Length > pendingChunkStart
                && StatementCompleted != null)
            {
                var pending = sb.ToString(pendingChunkStart, sb.Length - pendingChunkStart);
                StatementCompleted.Invoke(pendingChunkLine, pending);
                pendingChunkStart = sb.Length;
            }
            // Al final del script: cerrar figura abierta (patch/line acumulados sin saveas)
            int finalChunkStart = sb.Length;
            if (MatlabPlots.HasOpenFigure)
            {
                var finalFig = MatlabPlots.FinishFigure();
                if (!string.IsNullOrEmpty(finalFig)) sb.Append(finalFig);
            }
            if (sb.Length > finalChunkStart && StatementCompleted != null)
            {
                var chunk = sb.ToString(finalChunkStart, sb.Length - finalChunkStart);
                StatementCompleted.Invoke(0, chunk);
            }
            var fullHtml = sb.ToString();
            // Auto-contenido: si la salida usa Plotly (plot/surf/contour de Lab),
            // anteponer la libreria UNA vez. Script bloqueante en document.write =>
            // queda definida antes de los Plotly.newPlot del cuerpo. Asi el HTML
            // sirve en web, WPF/CLI y exportado, sin inyeccion del host.
            if (fullHtml.Contains("Plotly.newPlot") && !fullHtml.Contains("cdn.plot.ly"))
                fullHtml = "<script src=\"https://cdn.plot.ly/plotly-2.35.2.min.js\" charset=\"utf-8\"></script>\n" + fullHtml;
            ScriptFinished?.Invoke(fullHtml);
            return fullHtml;
        }

        /// <summary>
        /// Procesa una línea sola. Devuelve (html, errMsg, errLine) — exactamente
        /// uno de html/errMsg será no-null. Usar desde ExpressionParser para
        /// integración fina.
        /// </summary>
        public (string Html, string Error, int ErrorLine) RunLine(string source, int lineOffset = 0)
        {
            try
            {
                var html = Run(source);
                return (html, null, 0);
            }
            catch (MatlabParseException pe)
            {
                return (null, pe.Message, pe.Line + lineOffset);
            }
            catch (MatlabRuntimeException re)
            {
                return (null, re.Message, lineOffset);
            }
            catch (Exception ex)
            {
                // Internal .NET error (IndexOutOfRange, ArgumentException, NullRef, etc.)
                // Devolverlo formateado para que aparezca como <p class="err">.
                return (null, $"Internal: {ex.GetType().Name}: {ex.Message}", lineOffset);
            }
        }

        /// <summary>Limpia el scope global (útil entre Parse() calls).</summary>
        public void Reset()
        {
            _evaluator.Globals.Vars.Clear();
        }

        // Sentinels PUA (Private Use Area) que char(symbolic) usa para marcar
        // segmentos HTML pre-renderizados que NO deben ser escapados al flush.
        private const char HtmlStart = '';
        private const char HtmlEnd   = '';

        /// <summary>
        /// HtmlEncode selectivo: escapa todo el texto SALVO los segmentos
        /// delimitados por ... (HTML pre-renderizado del simbólico).
        /// </summary>
        private static string RenderDispWithMatrices(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
            if (raw.IndexOf('[') < 0) return raw;

            var lines = raw.Split('\n');
            var outSb = new StringBuilder(raw.Length + 64);
            var matRows = new System.Collections.Generic.List<string>();

            void FlushMatrix()
            {
                if (matRows.Count == 0) return;
                outSb.Append(HtmlStart);
                outSb.Append("<span class=\"mat\"><span class=\"lb\"></span><span class=\"cells\">");
                foreach (var rowContent in matRows)
                {
                    outSb.Append("<span class=\"row\">");
                    var cells = System.Text.RegularExpressions.Regex.Split(rowContent, @"[ \t]{2,}");
                    foreach (var cellRaw in cells)
                    {
                        if (string.IsNullOrWhiteSpace(cellRaw)) continue;
                        outSb.Append("<span class=\"cell\">");
                        outSb.Append(EncodeWithHtmlSegments(cellRaw));
                        outSb.Append("</span>");
                    }
                    outSb.Append("</span>");
                }
                outSb.Append("</span><span class=\"rb\"></span></span>");
                outSb.Append(HtmlEnd);
                matRows.Clear();
            }

            for (int idx = 0; idx < lines.Length; idx++)
            {
                var line = lines[idx];
                if (TryParseMatrixRow(line, out var content))
                {
                    matRows.Add(content);
                    bool isLast = idx == lines.Length - 1;
                    bool nextIsRow = !isLast && TryParseMatrixRow(lines[idx + 1], out _);
                    if (isLast || !nextIsRow)
                    {
                        FlushMatrix();
                        if (!isLast) outSb.Append('\n');
                    }
                }
                else
                {
                    outSb.Append(line);
                    if (idx != lines.Length - 1) outSb.Append('\n');
                }
            }
            return outSb.ToString();
        }

        /// <summary>
        /// Post-procesa HTML ya encoded: busca patrones `[ ... ]` inline donde
        /// el contenido tiene fracciones (<span class="dvc">) y reemplaza los
        /// corchetes chicos por la estructura `.mat` con corchetes flex-stretch.
        /// Cubre el caso `LABEL = [ frac1  frac2 ]` que RenderDispWithMatrices
        /// no agarra (porque la linea no empieza con `[`).
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex InlineMatrixBracketRegex =
            new(@"\[\s+([^\[\]]*?<span class=""dvc""[^\[\]]*?)\s+\]",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string StretchInlineBrackets(string html)
        {
            if (string.IsNullOrEmpty(html)) return html ?? string.Empty;
            return InlineMatrixBracketRegex.Replace(html, m =>
            {
                var content = m.Groups[1].Value;
                var cells = System.Text.RegularExpressions.Regex.Split(content, @"[ \t]{2,}");
                var sb = new StringBuilder();
                sb.Append("<span class=\"mat\"><span class=\"lb\"></span><span class=\"cells\"><span class=\"row\">");
                foreach (var cellRaw in cells)
                {
                    if (string.IsNullOrWhiteSpace(cellRaw)) continue;
                    sb.Append("<span class=\"cell\">");
                    sb.Append(cellRaw.Trim());
                    sb.Append("</span>");
                }
                sb.Append("</span></span><span class=\"rb\"></span></span>");
                return sb.ToString();
            });
        }

        private static bool TryParseMatrixRow(string line, out string content)
        {
            content = null;
            if (string.IsNullOrEmpty(line)) return false;
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i >= line.Length || line[i] != '[') return false;
            i++;
            int j = line.Length - 1;
            while (j > i && (line[j] == ' ' || line[j] == '\t')) j--;
            if (j <= i || line[j] != ']') return false;
            var inner = line.Substring(i, j - i).Trim();
            if (inner.Length == 0) return false;
            int depth = 0;
            foreach (var ch in inner)
            {
                if (ch == '[') depth++;
                else if (ch == ']') { depth--; if (depth < 0) return false; }
            }
            if (depth != 0) return false;
            content = inner;
            return true;
        }

        private static string EncodeWithHtmlSegments(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
            if (raw.IndexOf(HtmlStart) < 0)
                return BeautifyMath(System.Net.WebUtility.HtmlEncode(raw));

            var outSb = new StringBuilder(raw.Length + 32);
            int i = 0;
            while (i < raw.Length)
            {
                int start = raw.IndexOf(HtmlStart, i);
                if (start < 0)
                {
                    outSb.Append(BeautifyMath(System.Net.WebUtility.HtmlEncode(raw.Substring(i))));
                    break;
                }
                if (start > i)
                    outSb.Append(BeautifyMath(System.Net.WebUtility.HtmlEncode(raw.Substring(i, start - i))));
                int end = raw.IndexOf(HtmlEnd, start + 1);
                if (end < 0)
                {
                    // Sentinel sin cierre: tratar como texto literal
                    outSb.Append(BeautifyMath(System.Net.WebUtility.HtmlEncode(raw.Substring(start))));
                    break;
                }
                // Insertar HTML crudo entre sentinels (sin escapar)
                outSb.Append(raw.AsSpan(start + 1, end - start - 1));
                i = end + 1;
            }
            return outSb.ToString();
        }

        // Unidades comunes (orden importa: compuestas primero)
        private static readonly string[] UnitTokens = new[] {
            "kN\\*m", "N\\*m", "kN/m", "N/m",
            "mm\\^4", "cm\\^4", "m\\^4", "cm\\^3", "m\\^3", "mm\\^3", "cm\\^2", "m\\^2", "mm\\^2",
            "GPa", "MPa", "kPa", "Pa", "kN", "kg", "kJ", "J", "Hz",
            "mm", "cm", "km", "m", "s", "rad", "deg",
            "N"
        };

        private static readonly System.Text.RegularExpressions.Regex UnitRegex =
            new(@"(?<![A-Za-z])(" + string.Join("|", UnitTokens) + @")(?![A-Za-z])",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Subíndice estilo MATLAB: ident_word — subscript puede empezar con dígito
        // (Phi_1, Phi_2, K_3 etc) o letra (M_xx, sigma_max).
        // Lookbehind/ahead excluye: letras Unicode (acentos á é í ó ú ñ),
        // dígitos, y `;` (cierre de HTML entity `&#243;` que rodea acentos).
        private static readonly System.Text.RegularExpressions.Regex SubscriptRegex =
            new(@"(?<![\p{L}\p{N};])([A-Za-z][A-Za-z]{0,9})_([A-Za-z0-9][A-Za-z0-9]{0,9})(?![\p{L}\p{N}])",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Letra suelta o palabra griega corta (variable matemática).
        // Excluye `a, y, o` para evitar capturar conjunciones/artículos españoles.
        // Lookbehind excluye letras Unicode + `;` (cierre de HTML entity `&#243;`
        // que rodea acentos), para no romper palabras como "Verificación".
        private static readonly System.Text.RegularExpressions.Regex LooseVarRegex =
            new(@"(?<![\p{L}\p{N}<>/""=;])(alpha|beta|gamma|delta|epsilon|zeta|eta|theta|kappa|lambda|mu|nu|xi|pi|rho|sigma|tau|phi|chi|psi|omega|[B-DF-NP-XZb-df-np-xz]|[Ee]|[Ii]|[Uu])(?![\p{L}\p{N}])",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // ^N (exponente entero)
        private static readonly System.Text.RegularExpressions.Regex PowerRegex =
            new(@"(?<=</var>|</sub>|\)|\d)\^(\d+)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // `*` entre tokens math → middle dot
        private static readonly System.Text.RegularExpressions.Regex MulRegex =
            new(@"(?<=</var>|</sub>|</sup>|\d)\*(?=<var\b|<i\b|<sup\b|\d)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Integral: `int_a^b` o `int_a` o `int` standalone → ∫ con limites como
        // sub/sup. Excluye `int(...)` (call de funcion sym).
        private static readonly System.Text.RegularExpressions.Regex IntegralRegex =
            new(@"\bint(?!\()(?:_([A-Za-z0-9]+))?(?:\^([A-Za-z0-9]+))?\b",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Sumatoria/productoria n-ary con limites: sum_a^b, prod_a^b, lim_x
        private static readonly System.Text.RegularExpressions.Regex NaryRegex =
            new(@"\b(sum|prod|lim)(?!\()(?:_([A-Za-z0-9]+))?(?:\^([A-Za-z0-9]+))?\b",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Collections.Generic.Dictionary<string, string> NarySym =
            new(System.StringComparer.Ordinal)
        {
            { "sum",  "∑" },
            { "prod", "∏" },
            { "lim",  "lim" },
        };

        // Mapa de palabras griegas → símbolo Unicode. Solo aplica DENTRO de
        // contextos matemáticos (variable suelta o identificador con subíndice)
        // para no transformar "alpha" o "pi" cuando aparecen en texto natural.
        // Calcpad-Lab vs MATLAB: en MATLAB R2017a las palabras se imprimen tal
        // cual ("alpha"), en Calcpad-Lab se renderizan con el glyph griego
        // gracias a este mapping aplicado en el render HTML.
        private static readonly System.Collections.Generic.Dictionary<string, string> GreekMap =
            new(System.StringComparer.Ordinal)
        {
            { "alpha", "α" }, { "beta", "β" }, { "gamma", "γ" }, { "delta", "δ" },
            { "epsilon", "ε" }, { "zeta", "ζ" }, { "eta", "η" }, { "theta", "θ" },
            { "kappa", "κ" }, { "lambda", "λ" }, { "mu", "μ" }, { "nu", "ν" },
            { "xi", "ξ" }, { "pi", "π" }, { "rho", "ρ" }, { "sigma", "σ" },
            { "tau", "τ" }, { "phi", "φ" }, { "chi", "χ" }, { "psi", "ψ" },
            { "omega", "ω" },
            // Mayúsculas más usadas
            { "Alpha", "Α" }, { "Beta", "Β" }, { "Gamma", "Γ" }, { "Delta", "Δ" },
            { "Theta", "Θ" }, { "Lambda", "Λ" }, { "Xi", "Ξ" }, { "Pi", "Π" },
            { "Sigma", "Σ" }, { "Phi", "Φ" }, { "Psi", "Ψ" }, { "Omega", "Ω" }
        };

        /// <summary>Si el token es nombre de letra griega, devuelve el glyph; si no, devuelve el token original.</summary>
        private static string ToGreekIfMatch(string name)
            => GreekMap.TryGetValue(name, out var glyph) ? glyph : name;

        /// <summary>
        /// Post-procesa texto HTML-escapado para detectar patrones matemáticos
        /// (subíndices, variables, unidades, exponentes) y aplicarles el CSS
        /// Calcpad. Conservador: solo transforma patrones inequívocos.
        /// </summary>
        private static string BeautifyMath(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;

            // 1) Unidades primero — antes de que cualquier `^N` o variable interfiera
            s = UnitRegex.Replace(s, m =>
            {
                var u = m.Value
                    .Replace("*", "&middot;")
                    .Replace("^4", "<sup>4</sup>")
                    .Replace("^3", "<sup>3</sup>")
                    .Replace("^2", "<sup>2</sup>");
                return $"<i class=\"unit\">{u}</i>";
            });

            // 1.5) Integrales: int_a^b → ∫_a^b con estilo template `.dvr > .nary`
            // (BIG integral, sub/sup stacked vertical) — mismo que el HtmWriter
            // produce para int(f,x,a,b) en expresiones reales.
            s = IntegralRegex.Replace(s, m =>
            {
                var sub = m.Groups[1].Success ? m.Groups[1].Value : "";
                var sup = m.Groups[2].Success ? m.Groups[2].Value : "";
                return $"<span class=\"dvr\"><small>{sup}</small><span class=\"nary\">&int;</span><small>{sub}</small></span>";
            });

            // 1.6) Sumatoria/productoria: sum_a^b → ∑_a^b, prod_a^b → ∏_a^b
            s = NaryRegex.Replace(s, m =>
            {
                var sym = NarySym.TryGetValue(m.Groups[1].Value, out var g) ? g : m.Groups[1].Value;
                var sub = m.Groups[2].Success ? $"<sub>{m.Groups[2].Value}</sub>" : "";
                var sup = m.Groups[3].Success ? $"<sup>{m.Groups[3].Value}</sup>" : "";
                return $"<span class=\"narysym\">{sym}</span>{sub}{sup}";
            });

            // 2) Subíndices: ident_word → <var>ident<sub>word</sub></var>
            //    Si "ident" es nombre griego (sigma, theta...), lo reemplaza por
            //    su glyph Unicode (σ, θ...). Mismo tratamiento al subíndice.
            s = SubscriptRegex.Replace(s, m =>
            {
                var ident = ToGreekIfMatch(m.Groups[1].Value);
                var sub = ToGreekIfMatch(m.Groups[2].Value);
                return $"<var>{ident}<sub>{sub}</sub></var>";
            });

            // 3) Variables sueltas: letra única o nombre griego corto → <var>X</var>
            //    Si es nombre griego, se sustituye por el glyph Unicode.
            s = LooseVarRegex.Replace(s, m =>
            {
                var name = m.Groups[1].Value;
                var glyph = ToGreekIfMatch(name);
                return $"<var>{glyph}</var>";
            });

            // 4) Exponentes: ^N tras var/sub/sup/dígito/)
            s = PowerRegex.Replace(s, "<sup>$1</sup>");

            // 5) `*` entre tokens math → &middot;
            s = MulRegex.Replace(s, "&middot;");

            return s;
        }
    }
}
