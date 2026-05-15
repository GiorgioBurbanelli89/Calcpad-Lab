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

        /// <summary>
        /// Procesa un fragmento de código MATLAB. Devuelve HTML concatenado de
        /// todos los statements. Lanza <see cref="MatlabParseException"/> o
        /// <see cref="MatlabRuntimeException"/> en caso de error.
        /// </summary>
        public string Run(string source)
        {
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
            _evaluator.Output = msg => dispBuffer.AppendLine(msg);
            _evaluator.HtmlOut = html => htmlBuffer.Append(html);
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
                // Plot management
                "figure", "clf", "close", "hold", "axis", "grid", "legend", "colormap",
                "title", "xlabel", "ylabel", "zlabel", "colorbar", "sgtitle",
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
                if (innerRes.Suppressed) return;
                int innerLine = innerStmt?.Line ?? 0;
                htmlBuffer.Append($"<p class=\"line\" id=\"line-{innerLine}\" style=\"margin-left:1.5em;color:#555\">");
                htmlBuffer.Append(MatlabHtmlWriter.RenderStatement(innerStmt, innerRes));
                htmlBuffer.Append("</p>\n");
            };
            foreach (var stmt in stmts)
            {
                int stmtLine = stmt?.Line ?? 0;
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
                // Flush disp buffer
                if (dispBuffer.Length > 0)
                {
                    sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\"><span class=\"eq\"><pre style=\"display:inline\">{System.Net.WebUtility.HtmlEncode(dispBuffer.ToString().TrimEnd())}</pre></span></p>\n");
                    dispBuffer.Clear();
                }
                // Render del statement (incluye el comando como fórmula)
                // NO renderizar void functions (fprintf/disp/figure/plot/...) — solo su side effect.
                if (!result.Suppressed && !IsVoidStatement(stmt))
                {
                    try
                    {
                        if (stmt is CommentStmt cs && cs.IsHeading)
                        {
                            sb.Append(MatlabHtmlWriter.RenderStatement(stmt, result));
                            sb.Append("\n");
                        }
                        else
                        {
                            sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\">");
                            sb.Append(MatlabHtmlWriter.RenderStatement(stmt, result));
                            sb.Append("</p>\n");
                        }
                    }
                    catch (Exception renderEx)
                    {
                        sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">Render error: {System.Net.WebUtility.HtmlEncode(renderEx.GetType().Name + ": " + renderEx.Message)} (line {LineLink(stmtLine)})</p>\n");
                    }
                }
                // Flush plot HTML buffer DESPUÉS de la línea del statement
                if (htmlBuffer.Length > 0)
                {
                    sb.Append(htmlBuffer);
                    htmlBuffer.Clear();
                }
            }
            // Al final del script: cerrar figura abierta (patch/line acumulados sin saveas)
            if (MatlabPlots.HasOpenFigure)
            {
                var finalFig = MatlabPlots.FinishFigure();
                if (!string.IsNullOrEmpty(finalFig)) sb.Append(finalFig);
            }
            return sb.ToString();
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
    }
}
