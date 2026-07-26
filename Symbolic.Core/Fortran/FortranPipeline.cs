#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core.Fortran;

/// <summary>
/// Adapts the embedded Fortran engine to the same contract the WPF already uses for
/// MATLAB: <c>RunLine(source)</c> returns an HTML fragment plus the first error and
/// its line, so the editor can highlight it. No external compiler is involved.
/// </summary>
public sealed class FortranPipeline
{
    /// <summary>Optional: called once with the whole output (kept for symmetry with MatlabPipeline).</summary>
    public event Action<int, string>? StatementCompleted;

    public (string Html, string? Error, int ErrorLine) RunLine(string source)
    {
        var result = FortranEngine.Run(source ?? "");
        var html = BuildHtml(result);
        if (html.Length > 0) StatementCompleted?.Invoke(1, html);
        return (html, result.Error, result.ErrorLine);
    }

    private static string BuildHtml(FortranResult result)
    {
        var sb = new StringBuilder();
        var text = result.Output.TrimEnd('\n');

        if (text.Length > 0)
        {
            sb.Append("<pre class=\"fortran-output\" style=\"margin:0 0 12px;padding:12px 14px;")
              .Append("border-left:3px solid #2e9e5b;background:rgba(127,127,127,.08);")
              .Append("border-radius:6px;font:13px/1.5 Consolas,ui-monospace,monospace;")
              .Append("white-space:pre;overflow-x:auto;\">")
              .Append(Escape(text))
              .Append("</pre>");
        }
        else if (result.Ok)
        {
            sb.Append("<p style=\"color:#888\">(el programa no imprimio nada)</p>");
        }

        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
