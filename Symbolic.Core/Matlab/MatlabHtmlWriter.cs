// =============================================================================
// Calcpad Lab — MATLAB HTML Writer (output MATLAB puro, sin keywords Calcpad)
// =============================================================================
//   Toma un AST + valor evaluado y produce HTML que muestra:
//
//     Z = sin(X) .* cos(Y) = [matrix]
//
//   Sin `hprod`, sin `;` como separador de args, sin keywords de Calcpad.
//   El usuario nunca ve un nombre interno de Calcpad en su pantalla.
// =============================================================================
using System.Globalization;
using System.Text;
using System.Web;

namespace Calcpad.Core.Matlab
{
    public static class MatlabHtmlWriter
    {
        /// <summary>Render statement (formula + result) en HTML MATLAB-style.</summary>
        public static string RenderStatement(MatlabNode stmt, StatementResult result)
        {
            var sb = new StringBuilder();
            switch (stmt)
            {
                case CommentStmt cs:
                    if (cs.IsHeading)
                        // Encabezado limpio que usa el CSS del template (como Calcpad),
                        // sin estilos inline ni color forzado.
                        sb.Append($"<h3>{HttpUtility.HtmlEncode(cs.Text)}</h3>");
                    // El texto de comentario en Calcpad-Lab admite HTML enriquecido — igual
                    // que el texto `'...` de Calcpad puro (headings, <p>, <table>, <svg>...).
                    // Si contiene tags HTML, se emite RAW (se renderiza).
                    else if (System.Text.RegularExpressions.Regex.IsMatch(cs.Text, "<[a-zA-Z/!]"))
                        sb.Append(cs.Text);
                    else
                        // Comentario visible = texto normal (como Calcpad `'...`), NUNCA verde.
                        // Para ocultar: usar `%--` (filtrado en MatlabPipeline).
                        sb.Append(HttpUtility.HtmlEncode(cs.Text));
                    break;
                case Assignment asg:
                    sb.Append("<span class=\"eq\">");
                    RenderAssignmentLhs(sb, asg);
                    sb.Append(" = ");
                    {
                        // Reglas (en orden):
                        //  1. Si RHS es llamada simbólica (int/diff/limit/taylor/subs/...),
                        //     SIEMPRE mostrar la notación pretty (∫ … dx, d/dx …, lim, etc.)
                        //     seguida del valor — esa es la informacion mas util.
                        //  2. Si el resultado es Symbolic puro (RHS = polinomio/expresion sin
                        //     funcion simbolica wrapper), mostrar solo el valor normalizado —
                        //     evita duplicacion tipo `Φ₁ = 1 - 3·ξ² + 2·ξ³ = -3·ξ² + 2·ξ³ + 1`.
                        //  3. Otros casos: rhs source = value, con short-circuit si ambos
                        //     renderean identico (literales, matrices puras, etc).
                        bool symbolicCall = IsSymbolicFunctionCall(asg.Rhs);
                        bool symbolicResult = result.Value != null && result.Value.IsSymbolic;
                        if (symbolicCall)
                        {
                            // (1) Pretty notation + value
                            sb.Append(RenderExpression(asg.Rhs));
                            sb.Append(" = ");
                            sb.Append(RenderValue(result.Value));
                        }
                        else if (symbolicResult)
                        {
                            // (2) Solo valor (evita duplicacion polinomica)
                            sb.Append(RenderValue(result.Value));
                        }
                        else
                        {
                            // (3) Default: source = value con short-circuit visual
                            var rhsHtml = RenderExpression(asg.Rhs);
                            sb.Append(rhsHtml);
                            if (!IsTrivialAssignment(asg))
                            {
                                var valHtml = RenderValue(result.Value);
                                if (valHtml != rhsHtml)
                                {
                                    sb.Append(" = ");
                                    sb.Append(valHtml);
                                }
                            }
                        }
                    }
                    sb.Append("</span>");
                    break;
                case ExprStmt es:
                    sb.Append("<span class=\"eq\">");
                    {
                        var lhsHtml = RenderExpression(es.Expr);
                        var rhsHtml = RenderValue(result.Value);
                        // Si la expresion es literal/identica al valor (ej. `2`, `x^2+1`
                        // simbolico sin variables a sustituir), no mostrar `expr = expr`
                        // redundante. Solo mostrar el lado derecho.
                        if (lhsHtml == rhsHtml)
                            sb.Append(rhsHtml);
                        else
                        { sb.Append(lhsHtml); sb.Append(" = "); sb.Append(rhsHtml); }
                    }
                    sb.Append("</span>");
                    break;
                case ForLoop fl:
                    sb.Append($"<span class=\"eq\"><b>for</b> {RenderIdentName(fl.VarName)} = ");
                    sb.Append(RenderExpression(fl.Iter));
                    sb.Append($" … <b>end</b>  <span style=\"color:#888\">(loop executed)</span></span>");
                    break;
                case WhileLoop wl:
                    sb.Append("<span class=\"eq\"><b>while</b> ");
                    sb.Append(RenderExpression(wl.Cond));
                    sb.Append(" … <b>end</b>  <span style=\"color:#888\">(loop executed)</span></span>");
                    break;
                case IfBlock ib:
                    sb.Append("<span class=\"eq\"><b>if</b> ");
                    sb.Append(RenderExpression(ib.Branches[0].Cond));
                    sb.Append(" … <b>end</b>  <span style=\"color:#888\">(branch executed)</span></span>");
                    break;
                case ClassDef classd:
                    sb.Append("<span class=\"eq\"><b>classdef</b> ");
                    sb.Append($"<var>{HttpUtility.HtmlEncode(classd.Name)}</var>");
                    if (!string.IsNullOrEmpty(classd.ParentName))
                        sb.Append($" &lt; <var>{HttpUtility.HtmlEncode(classd.ParentName)}</var>");
                    sb.Append($" … <b>end</b>  <span style=\"color:#888\">(class registered: {classd.Properties.Count} props, {classd.Methods.Count} methods)</span></span>");
                    break;
                case FunctionDef fd:
                    sb.Append("<span class=\"eq\"><b>function</b> ");
                    if (fd.OutputNames.Count == 1) sb.Append($"<var>{HttpUtility.HtmlEncode(fd.OutputNames[0])}</var> = ");
                    else if (fd.OutputNames.Count > 1)
                    {
                        sb.Append("[");
                        for (int i = 0; i < fd.OutputNames.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append($"<var>{HttpUtility.HtmlEncode(fd.OutputNames[i])}</var>");
                        }
                        sb.Append("] = ");
                    }
                    sb.Append($"<var>{HttpUtility.HtmlEncode(fd.Name)}</var>(");
                    for (int i = 0; i < fd.ParamNames.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"<var>{HttpUtility.HtmlEncode(fd.ParamNames[i])}</var>");
                    }
                    sb.Append($") … <b>end</b>  <span style=\"color:#888\">(defined)</span></span>");
                    break;
            }
            return sb.ToString();
        }

        /// <summary>Render únicamente el valor (sin formula). Útil para errores o displays simples.</summary>
        public static string RenderValue(MValue v)
        {
            if (v == null) return "<i>(undefined)</i>";
            if (v.IsCallable) return $"<i style=\"color:#666\">{HttpUtility.HtmlEncode(v.CallableName ?? "@function_handle")}</i>";
            if (v.IsSymbolic) return v.Symbolic.ToHtml();
            if (v.IsStringArray)
            {
                int srA = v.StringArrayData.GetLength(0), scA = v.StringArrayData.GetLength(1);
                var sbStr = new StringBuilder();
                sbStr.Append("<span class=\"matrix\" style=\"color:#0a6e3a\">");
                for (int i = 0; i < srA; i++)
                {
                    sbStr.Append("<span class=\"tr\"><span class=\"td\"></span>");
                    for (int j = 0; j < scA; j++)
                    {
                        sbStr.Append("<span class=\"td\">\"");
                        sbStr.Append(HttpUtility.HtmlEncode(v.StringArrayData[i, j] ?? ""));
                        sbStr.Append("\"</span>");
                    }
                    sbStr.Append("<span class=\"td\"></span></span>");
                }
                sbStr.Append("</span>");
                return sbStr.ToString();
            }
            if (v.IsString)
            {
                if (v.IsDoubleQuoted)
                    return "<span style=\"color:#0a6e3a\">\"" + HttpUtility.HtmlEncode(v.StringValue ?? "") + "\"</span>";
                return HttpUtility.HtmlEncode("'" + v.StringValue + "'");
            }
            if (v.IsInstance)
            {
                var sbi = new StringBuilder();
                sbi.Append($"<span style=\"font-family:monospace;color:#2050a0\">{HttpUtility.HtmlEncode(v.ClassName)}{{");
                bool firstI = true;
                foreach (var kv in v.Fields)
                {
                    if (!firstI) sbi.Append(", ");
                    sbi.Append($"<var>{HttpUtility.HtmlEncode(kv.Key)}</var>: ");
                    sbi.Append(RenderValueInline(kv.Value));
                    firstI = false;
                }
                sbi.Append("}</span>");
                return sbi.ToString();
            }
            if (v.IsStruct)
            {
                var sbs = new StringBuilder();
                sbs.Append("<span style=\"font-family:monospace;color:#444\">struct{");
                bool first = true;
                foreach (var kv in v.Fields)
                {
                    if (!first) sbs.Append(", ");
                    sbs.Append($"<var>{HttpUtility.HtmlEncode(kv.Key)}</var>: ");
                    sbs.Append(RenderValueInline(kv.Value));
                    first = false;
                }
                sbs.Append("}</span>");
                return sbs.ToString();
            }
            if (v.IsCell)
            {
                int cnr = v.CellData.GetLength(0), cnc = v.CellData.GetLength(1);
                var sbc = new StringBuilder();
                sbc.Append("<span class=\"matrix\" style=\"border-left:2px solid #aaa;border-right:2px solid #aaa;padding:0 .2em\">");
                for (int i = 0; i < cnr; i++)
                {
                    sbc.Append("<span class=\"tr\"><span class=\"td\"></span>");
                    for (int j = 0; j < cnc; j++)
                    {
                        sbc.Append("<span class=\"td\">");
                        sbc.Append(RenderValueInline(v.CellData[i, j]));
                        sbc.Append("</span>");
                    }
                    sbc.Append("<span class=\"td\"></span></span>");
                }
                sbc.Append("</span>");
                return sbc.ToString();
            }
            // Complex scalar tiene prioridad sobre IsScalar real
            if (v.IsComplex && v.Rows == 1 && v.Cols == 1)
                return FormatComplex(v.Data[0], v.Imag[0]);
            if (v.IsScalar) return FormatNumber(v.Scalar);
            if (v.Is3D)
            {
                var sb3 = new StringBuilder();
                int nPages = v.Pages.Length;
                var p0 = v.Pages[0];
                sb3.Append($"<span style=\"font-family:monospace;color:#444\">array3d({p0.Rows}×{p0.Cols}×{nPages}):</span><br>");
                int showPages = System.Math.Min(nPages, 3);
                for (int p = 0; p < showPages; p++)
                {
                    sb3.Append($"<span style=\"color:#888;font-style:italic\">(:,:,{p + 1}) =</span><br>");
                    sb3.Append(RenderValue(v.Pages[p]));
                    sb3.Append("<br>");
                }
                if (nPages > showPages) sb3.Append($"<span style=\"color:#888\">… (+{nPages - showPages} more pages)</span>");
                return sb3.ToString();
            }
            if (v.IsSymMatrix)
            {
                int snr = v.SymCells.GetLength(0), snc = v.SymCells.GetLength(1);
                var sbSym = new StringBuilder();
                sbSym.Append("<span class=\"matrix\" style=\"color:#5d2b8a;font-style:italic\">");
                for (int i = 0; i < snr; i++)
                {
                    sbSym.Append("<span class=\"tr\"><span class=\"td\"></span>");
                    for (int j = 0; j < snc; j++)
                    {
                        sbSym.Append("<span class=\"td\">");
                        sbSym.Append(HttpUtility.HtmlEncode(v.SymCells[i, j].ToInfix()));
                        sbSym.Append("</span>");
                    }
                    sbSym.Append("<span class=\"td\"></span></span>");
                }
                sbSym.Append("</span>");
                return sbSym.ToString();
            }
            if (v.IsSparseReal)
            {
                int snr = v.Rows, snc = v.Cols;
                int nnz = v.SparseVals.Length;
                // Mapa rápido (i,j)→val para acceso O(log n) en filas/cols pequeños
                double SparseAt(int i, int j)
                {
                    for (int k = v.SparseRowPtr[i]; k < v.SparseRowPtr[i + 1]; k++)
                        if (v.SparseCols[k] == j) return v.SparseVals[k];
                    return 0.0;
                }
                bool HasEntry(int i, int j)
                {
                    for (int k = v.SparseRowPtr[i]; k < v.SparseRowPtr[i + 1]; k++)
                        if (v.SparseCols[k] == j) return true;
                    return false;
                }
                // Tabla bordeada con valores; ceros en gris claro; truncación si N o M > 8
                const int spMax = 8;
                bool truncR = snr > spMax;
                bool truncC = snc > spMax;
                int spShowR = truncR ? spMax : snr;
                int spShowC = truncC ? spMax : snc;
                var sbSp = new StringBuilder();
                // Header: tamaño + nnz + density
                double density = snr * snc > 0 ? (double)nnz / (snr * snc) * 100.0 : 0.0;
                sbSp.Append($"<span style=\"font:italic 11px sans-serif;color:#888\">sparse({snr}×{snc}, nnz={nnz}, density={density.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}%)</span><br>");
                sbSp.Append("<span class=\"matrix\" style=\"border-left:2px solid #888;border-right:2px solid #888;padding:0 .25em\">");
                for (int i = 0; i < spShowR; i++)
                {
                    int ri = (truncR && i == spShowR - 1) ? snr - 1 : i;
                    sbSp.Append("<span class=\"tr\"><span class=\"td\"></span>");
                    for (int j = 0; j < spShowC; j++)
                    {
                        int cj = (truncC && j == spShowC - 1) ? snc - 1 : j;
                        sbSp.Append("<span class=\"td\">");
                        if (truncR && i == spShowR - 2 && truncC && j == spShowC - 2)
                            sbSp.Append("<span style=\"color:#bbb\">⋱</span>");
                        else if (truncC && j == spShowC - 2)
                            sbSp.Append("<span style=\"color:#bbb\">⋯</span>");
                        else if (truncR && i == spShowR - 2)
                            sbSp.Append("<span style=\"color:#bbb\">⋮</span>");
                        else if (HasEntry(ri, cj))
                            sbSp.Append(FormatNumber(SparseAt(ri, cj)));
                        else
                            sbSp.Append("<span style=\"color:#ddd\">·</span>");
                        sbSp.Append("</span>");
                    }
                    sbSp.Append("<span class=\"td\"></span></span>");
                }
                sbSp.Append("</span>");
                return sbSp.ToString();
            }
            // Matrix display — con truncación tipo MATLAB para matrices grandes
            const int maxCount = 6;
            int nr = v.Rows, nc = v.Cols;
            bool truncRow = nr > maxCount;
            bool truncCol = nc > maxCount;
            var sb = new StringBuilder();
            sb.Append("<span class=\"matrix\">");
            int showR = truncRow ? maxCount : nr;
            int showC = truncCol ? maxCount : nc;
            for (int i = 0; i < showR; i++)
            {
                sb.Append("<span class=\"tr\">");
                int ri = (truncRow && i == showR - 1) ? nr - 1 : i;
                // Bracket izquierdo (celda vacía con border-left por CSS Calcpad)
                sb.Append("<span class=\"td\"></span>");
                for (int j = 0; j < showC; j++)
                {
                    int cj = (truncCol && j == showC - 1) ? nc - 1 : j;
                    sb.Append("<span class=\"td\">");
                    if (truncRow && i == showR - 1 && truncCol && j == showC - 2)
                        sb.Append("⋱");
                    else if (truncCol && j == showC - 2)
                        sb.Append("⋯");
                    else if (truncRow && i == showR - 2)
                        sb.Append("⋮");
                    else if (v.IsComplex)
                    {
                        int idx = ri * v.Cols + cj;
                        sb.Append(FormatComplex(v.Data[idx], v.Imag[idx]));
                    }
                    else
                        sb.Append(FormatNumber(v.At(ri, cj)));
                    sb.Append("</span>");
                }
                // Bracket derecho
                sb.Append("<span class=\"td\"></span>");
                sb.Append("</span>");
            }
            sb.Append("</span>");
            // Etiqueta de tamaño cuando truncado
            if (truncRow || truncCol)
                sb.Append($"<span style=\"font:italic 11px sans-serif;color:#888;margin-left:.5em\">[{nr}×{nc} matrix]</span>");
            return sb.ToString();
        }

        /// <summary>Versión compacta para mostrar dentro de structs (sin grandes matrices).</summary>
        private static string RenderValueInline(MValue v)
        {
            if (v == null) return "(undefined)";
            if (v.IsString)
            {
                if (v.IsDoubleQuoted)
                    return $"\"<span class=\"str\" style=\"color:#0a6e3a\">{HttpUtility.HtmlEncode(v.StringValue ?? "")}</span>\"";
                return $"'<span class=\"str\">{HttpUtility.HtmlEncode(v.StringValue)}</span>'";
            }
            if (v.IsCallable) return HttpUtility.HtmlEncode(v.CallableName ?? "@fn");
            if (v.IsStruct) return $"<i>struct({v.Fields.Count} fields)</i>";
            if (v.IsScalar) return FormatNumber(v.Scalar);
            return $"<i>[{v.Rows}×{v.Cols} matrix]</i>";
        }

        private static bool IsTrivialAssignment(Assignment asg)
        {
            // Si RHS es literal puro (número, string, matrix de números), no duplicar valor.
            return IsTrivialLiteral(asg.Rhs);
        }
        /// <summary>True si el RHS es una llamada a funcion simbolica builtin (diff, int,
        /// expand, factor, simplify, solve, taylor, limit, subs, dsolve, laplace, fourier).
        /// En ese caso preferimos mostrar SOLO el resultado simbolico, sin repetir la
        /// llamada de la funcion — comportamiento MATLAB Symbolic Toolbox.</summary>
        private static bool IsSymbolicFunctionCall(MatlabNode n)
        {
            if (n is CallOrIndex c && c.Target is IdentRef id)
            {
                return id.Name is "diff" or "int" or "expand" or "factor"
                    or "simplify" or "solve" or "taylor" or "limit" or "subs"
                    or "dsolve" or "laplace" or "fourier" or "trigsimplify"
                    or "collect" or "coeffs";
            }
            return false;
        }
        /// <summary>True si el nodo es un literal cuyo render visual coincidirá exactamente
        /// con el render del valor evaluado (i.e., no hay nada que "calcular").</summary>
        private static bool IsTrivialLiteral(MatlabNode n)
        {
            if (n is NumberLit || n is StringLit || n is ImaginaryLit) return true;
            if (n is UnaryOp u && u.Op == "-" && u.IsPrefix)
                return IsTrivialLiteral(u.Operand);   // -5 también es literal
            if (n is MatrixLit m)
            {
                // Matriz literal con TODOS elementos literales puros → trivial
                foreach (var row in m.Rows)
                    foreach (var elem in row)
                        if (!IsTrivialLiteral(elem)) return false;
                return true;
            }
            if (n is CellLit cl)
            {
                foreach (var row in cl.Rows)
                    foreach (var elem in row)
                        if (!IsTrivialLiteral(elem)) return false;
                return true;
            }
            return false;
        }

        private static void RenderAssignmentLhs(StringBuilder sb, Assignment asg)
        {
            if (asg.Targets.Count == 1)
            {
                sb.Append(RenderExpression(asg.Targets[0]));
                return;
            }
            sb.Append("[");
            for (int i = 0; i < asg.Targets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RenderExpression(asg.Targets[i]));
            }
            sb.Append("]");
        }

        /// <summary>Render una expresión AST como HTML MATLAB.</summary>
        public static string RenderExpression(MatlabNode node)
        {
            return node switch
            {
                NumberLit n => FormatNumber(n.Value),
                ImaginaryLit im => im.Value == 1 ? "i" : (im.Value == -1 ? "-i" : FormatNumber(im.Value) + "i"),
                StringLit s => s.Quote == '"'
                    ? $"\"<span class=\"str\" style=\"color:#0a6e3a\">{HttpUtility.HtmlEncode(s.Value)}</span>\""
                    : $"'<span class=\"str\">{HttpUtility.HtmlEncode(s.Value)}</span>'",
                IdentRef id => IsCommonBuiltin(id.Name)
                    ? $"<span style=\"font-family:'Segoe UI',sans-serif;font-weight:600;font-style:normal;color:#7c2bb2\">{HttpUtility.HtmlEncode(id.Name)}</span>"
                    : RenderIdentName(id.Name),
                UnaryOp u => RenderUnary(u),
                BinaryOp b => RenderBinary(b),
                CallOrIndex c => RenderCall(c),
                Range r => RenderRange(r),
                MatrixLit m => RenderMatrixLit(m),
                ColonAll => ":",
                AnonFunction af => RenderAnonFunction(af),
                FieldAccess fa => RenderExpression(fa.Target) + "." + RenderIdentName(fa.FieldName),
                CellLit cl => RenderCellLit(cl),
                CellIndex ci => RenderCellIndex(ci),
                _ => HttpUtility.HtmlEncode("[" + node?.GetType().Name + "]")
            };
        }
        /// <summary>Renderiza un identificador con underscore como subíndice HTML.
        /// Ej: "a_2" → "a<sub>2</sub>", "M_xx" → "M<sub>xx</sub>", "x_max" → "x<sub>max</sub>".
        /// Múltiples underscore: "sigma_x_max" → "sigma<sub>x,max</sub>" (notación Calcpad).
        /// Si el nombre empieza con _ o es solo _, lo deja literal.</summary>
        private static string RenderIdentName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int idx = name.IndexOf('_');
            if (idx <= 0 || idx == name.Length - 1)
            {
                // Sin underscore: igual probamos translit a letra griega.
                // Ej: `xi` → ξ, `phi` → φ, `Phi` → Φ. Si no matchea, se queda
                // como texto literal.
                string greek = GreekLetterMap(name);
                return $"<var>{(greek ?? HttpUtility.HtmlEncode(name))}</var>";
            }
            string baseName = name.Substring(0, idx);
            string sub = name.Substring(idx + 1).Replace("_", ",");
            // Greek letters: si baseName matches greek prefix, renderizar como letra griega
            string baseRendered = GreekLetterMap(baseName) ?? HttpUtility.HtmlEncode(baseName);
            return $"<var>{baseRendered}<sub>{HttpUtility.HtmlEncode(sub)}</sub></var>";
        }
        /// <summary>Mapea nombres de letras griegas a su unicode. Null si no es griega.</summary>
        private static string GreekLetterMap(string name) => name switch
        {
            "alpha" => "α", "beta" => "β", "gamma" => "γ", "delta" => "δ",
            "epsilon" => "ε", "zeta" => "ζ", "eta" => "η", "theta" => "θ",
            "iota" => "ι", "kappa" => "κ", "lambda" => "λ", "mu" => "μ",
            "nu" => "ν", "xi" => "ξ", "omicron" => "ο", "pi" => "π",
            "rho" => "ρ", "sigma" => "σ", "tau" => "τ", "upsilon" => "υ",
            "phi" => "φ", "chi" => "χ", "psi" => "ψ", "omega" => "ω",
            "Alpha" => "Α", "Beta" => "Β", "Gamma" => "Γ", "Delta" => "Δ",
            "Theta" => "Θ", "Lambda" => "Λ", "Pi" => "Π", "Sigma" => "Σ",
            "Phi" => "Φ", "Psi" => "Ψ", "Omega" => "Ω",
            _ => null
        };
        private static string RenderCellLit(CellLit cl)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < cl.Rows.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                for (int j = 0; j < cl.Rows[i].Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    sb.Append(RenderExpression(cl.Rows[i][j]));
                }
            }
            sb.Append("}");
            return sb.ToString();
        }
        private static string RenderCellIndex(CellIndex ci)
        {
            var sb = new StringBuilder(RenderExpression(ci.Target));
            sb.Append("{");
            for (int i = 0; i < ci.Args.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RenderExpression(ci.Args[i]));
            }
            sb.Append("}");
            return sb.ToString();
        }
        private static string RenderAnonFunction(AnonFunction af)
        {
            // @name (function handle por nombre) — caso especial: 1 param "__handle__" + body IdentRef
            if (af.ParamNames.Count == 1 && af.ParamNames[0] == "__handle__" && af.Body is IdentRef nameRef)
                return "@" + HttpUtility.HtmlEncode(nameRef.Name);
            var sb = new StringBuilder("@(");
            for (int i = 0; i < af.ParamNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"<var>{HttpUtility.HtmlEncode(af.ParamNames[i])}</var>");
            }
            sb.Append(") ");
            sb.Append(RenderExpression(af.Body));
            return sb.ToString();
        }
        private static string RenderUnary(UnaryOp u)
        {
            if (u.IsPrefix)
                return u.Op + RenderExpression(u.Operand);
            // postfix: `' ` o `.'`
            return RenderExpression(u.Operand) + u.Op;
        }
        /// <summary>Activar/desactivar rendering pretty-print (fracciones, raíces, exponentes).</summary>
        public static bool PrettyMath = true;

        private static string RenderBinary(BinaryOp b)
        {
            // Pretty-print: a/b → fracción vertical; a^b → superíndice; sqrt → raíz
            if (PrettyMath)
            {
                if (b.Op == "/" || b.Op == "./")
                {
                    var num = RenderExpression(b.Left);
                    var den = RenderExpression(b.Right);
                    return $"<span class=\"dvc\"><span class=\"dvc-num\">{num}</span><span class=\"dvl\"></span><span class=\"dvc-den\">{den}</span></span>";
                }
                if (b.Op == "^" || b.Op == ".^")
                {
                    string baseStr;
                    if (b.Left is BinaryOp || (b.Left is UnaryOp u_l && u_l.IsPrefix))
                        baseStr = "(" + RenderExpression(b.Left) + ")";
                    else baseStr = RenderExpression(b.Left);
                    var expStr = RenderExpression(b.Right);
                    return $"{baseStr}<sup>{expStr}</sup>";
                }
            }
            // Default: estilo plano con paréntesis por precedencia
            int myPrec = OpPrecedence(b.Op);
            string Render(MatlabNode child, bool rightAssoc)
            {
                if (child is BinaryOp cb)
                {
                    int childPrec = OpPrecedence(cb.Op);
                    bool needParens = childPrec < myPrec ||
                                       (childPrec == myPrec && rightAssoc && b.Op != "^" && b.Op != ".^");
                    var s = RenderExpression(cb);
                    return needParens ? $"({s})" : s;
                }
                if (child is UnaryOp u && u.IsPrefix && u.Op == "-" && (b.Op == "^" || b.Op == ".^"))
                    return $"({RenderExpression(u)})";
                return RenderExpression(child);
            }
            var l = Render(b.Left, rightAssoc: false);
            var r = Render(b.Right, rightAssoc: true);
            // Render bonito: * y .* como middle dot (Calcpad style)
            string opRender = b.Op switch
            {
                "*"  => "&middot;",
                ".*" => "&middot;",
                _    => b.Op,
            };
            string sep = (b.Op == "*" || b.Op == ".*" || b.Op == "/" || b.Op == "^") ? "" : " ";
            return $"{l}{sep}{opRender}{sep}{r}";
        }
        /// <summary>Precedencia MATLAB para rendering — más alto = más fuerte.</summary>
        private static int OpPrecedence(string op) => op switch
        {
            "||" or "|" => 1,
            "&&" or "&" => 2,
            "==" or "~=" or "<" or ">" or "<=" or ">=" => 3,
            "+" or "-" => 4,
            "*" or "/" or "\\" or ".*" or "./" or ".\\" => 5,
            "^" or ".^" => 6,
            _ => 10
        };
        private static string RenderCall(CallOrIndex c)
        {
            // ── Notacion matematica nativa Calcpad para funciones simbolicas ──
            // diff(f, x)     -> d/dx · f      (Leibniz fraction)
            // diff(f, x, n)  -> d^n/dx^n · f
            // int(f, x)      -> ∫ f dx        (nary symbol)
            // int(f, x, a, b)-> ∫_a^b f dx
            // limit(f, x, c) -> lim_{x→c} f
            if (PrettyMath && c.Target is IdentRef symFn)
            {
                string fname = symFn.Name;
                // diff
                if (fname == "diff" && c.Args.Count >= 2)
                {
                    var fExpr = RenderExpression(c.Args[0]);
                    var vExpr = RenderExpression(c.Args[1]);
                    string num, den;
                    if (c.Args.Count >= 3 && c.Args[2] is NumberLit nlit && nlit.Value >= 2)
                    {
                        int n = (int)nlit.Value;
                        num = $"d<sup>{n}</sup>";
                        den = $"d{vExpr}<sup>{n}</sup>";
                    }
                    else { num = "d"; den = $"d{vExpr}"; }
                    return $"<span class=\"dvc\"><span class=\"dvc-num\">{num}</span><span class=\"dvl\"></span><span class=\"dvc-den\">{den}</span></span>&thinsp;{fExpr}";
                }
                // int (indefinida o definida) — formato identico al HtmWriter de Calcpad:
                //   <span class="dvr"><small>SUP</small><span class="nary">∫</span><small>SUB</small></span>
                //   {f}&thinsp;<var>d{x}</var>
                // El diferencial va dentro de <var>...</var> para que se rendea italica como
                // las demas variables (Georgia Pro italic via .eq var en template.html).
                if (fname == "int" && c.Args.Count >= 2)
                {
                    var fExpr = RenderExpression(c.Args[0]);
                    // Diferencial: usar el nombre crudo (NO el render con <var> wrapper),
                    // sino quedaria `<var>d<var>x</var></var>` (anidado invalido).
                    string vName = c.Args[1] is IdentRef vId
                        ? (GreekLetterMap(vId.Name) ?? System.Web.HttpUtility.HtmlEncode(vId.Name))
                        : RenderExpression(c.Args[1]);
                    string sup = "", sub = "";
                    if (c.Args.Count >= 4)
                    {
                        sub = RenderExpression(c.Args[2]);
                        sup = RenderExpression(c.Args[3]);
                    }
                    return $"<span class=\"dvr\"><small>{sup}</small><span class=\"nary\">∫</span><small>{sub}</small></span>{fExpr} <var>d{vName}</var>";
                }
                // limit(f, x, c)
                if (fname == "limit" && c.Args.Count >= 3)
                {
                    var fExpr = RenderExpression(c.Args[0]);
                    var vExpr = RenderExpression(c.Args[1]);
                    var cExpr = RenderExpression(c.Args[2]);
                    return $"<span style=\"font-family:'Segoe UI',sans-serif;font-weight:600;color:#7c2bb2\">lim</span><sub>{vExpr}&rarr;{cExpr}</sub>&thinsp;{fExpr}";
                }
                // taylor(f, ...) — display como T_n(f) con subscript del orden
                if (fname == "taylor" && c.Args.Count >= 1)
                {
                    var fExpr = RenderExpression(c.Args[0]);
                    // Buscar 'Order' keyword o tercer arg numerico
                    string orderHtml = "";
                    for (int i = 1; i < c.Args.Count - 1; i++)
                    {
                        if (c.Args[i] is StringLit sl && sl.Value.Equals("Order", System.StringComparison.OrdinalIgnoreCase))
                        {
                            orderHtml = RenderExpression(c.Args[i + 1]);
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(orderHtml) && c.Args.Count >= 4)
                        orderHtml = RenderExpression(c.Args[3]);
                    string sub = string.IsNullOrEmpty(orderHtml) ? "" : $"<sub>{orderHtml}</sub>";
                    return $"<span style=\"font-family:'Segoe UI',sans-serif;font-weight:600;color:#7c2bb2\">T</span>{sub}({fExpr})";
                }
                // subs(f, x, val) -> f|_{x=val}
                if (fname == "subs" && c.Args.Count >= 3)
                {
                    var fExpr = RenderExpression(c.Args[0]);
                    var vExpr = RenderExpression(c.Args[1]);
                    var valExpr = RenderExpression(c.Args[2]);
                    return $"{fExpr}<sub>|&thinsp;{vExpr}={valExpr}</sub>";
                }
                // solve(f, x) -> {x : f=0} o solve sin notacion especial (mantener call style)
            }
            // Pretty-print de funciones especiales: sqrt → símbolo √ con vinculum
            if (PrettyMath && c.Target is IdentRef pid && c.Args.Count == 1)
            {
                if (pid.Name == "sqrt")
                {
                    var inner = RenderExpression(c.Args[0]);
                    return $"<span class=\"o0\"><span class=\"r\">√</span>&hairsp;{inner}</span>";
                }
                if (pid.Name == "abs")
                {
                    var inner = RenderExpression(c.Args[0]);
                    return $"<b class=\"b0\">|</b>&hairsp;{inner}&hairsp;<b class=\"b0\">|</b>";
                }
            }
            // Pretty: nthroot(x, n) → ⁿ√x
            if (PrettyMath && c.Target is IdentRef pid2 && pid2.Name == "nthroot" && c.Args.Count == 2)
            {
                var n = RenderExpression(c.Args[1]);
                var x = RenderExpression(c.Args[0]);
                return $"<span class=\"o0\"><sup style=\"font-size:.7em\">{n}</sup><span class=\"r\">√</span>&hairsp;{x}</span>";
            }
            var sb = new StringBuilder();
            // Funciones builtin: sans-serif bold morado para diferenciacion clara
            // de variables (que estan en italic serif).
            if (c.Target is IdentRef id)
            {
                if (IsCommonBuiltin(id.Name))
                    sb.Append($"<span style=\"font-family:'Segoe UI',sans-serif;font-weight:600;font-style:normal;color:#7c2bb2\">{HttpUtility.HtmlEncode(id.Name)}</span>");
                else
                    // Aplicar Greek mapping y underscore-subscript a nombres de funciones
                    // de usuario tambien: phi(u) -> φ(u), phi_d(u) -> φ_d(u), Phi(x) -> Φ(x).
                    sb.Append(RenderIdentName(id.Name));
            }
            else
                sb.Append(RenderExpression(c.Target));
            sb.Append("(");
            for (int i = 0; i < c.Args.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RenderExpression(c.Args[i]));
            }
            sb.Append(")");
            return sb.ToString();
        }
        private static string RenderRange(Range r)
        {
            if (r.Step != null)
                return $"{RenderExpression(r.Start)}:{RenderExpression(r.Step)}:{RenderExpression(r.End)}";
            return $"{RenderExpression(r.Start)}:{RenderExpression(r.End)}";
        }
        private static string RenderMatrixLit(MatrixLit m)
        {
            // Renderiza como matriz visual con corchetes CSS (Calcpad-style)
            var sb = new StringBuilder();
            sb.Append("<span class=\"matrix\">");
            for (int i = 0; i < m.Rows.Count; i++)
            {
                sb.Append("<span class=\"tr\"><span class=\"td\"></span>");
                for (int j = 0; j < m.Rows[i].Count; j++)
                {
                    sb.Append("<span class=\"td\">");
                    sb.Append(RenderExpression(m.Rows[i][j]));
                    sb.Append("</span>");
                }
                sb.Append("<span class=\"td\"></span></span>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }
        private static string FormatComplex(double re, double im)
        {
            if (im == 0) return FormatNumber(re);
            if (re == 0)
            {
                if (im == 1) return "i";
                if (im == -1) return "-i";
                return FormatNumber(im) + "i";
            }
            string sign = im < 0 ? "-" : "+";
            double absIm = System.Math.Abs(im);
            string imStr = absIm == 1 ? "" : FormatNumber(absIm);
            return $"{FormatNumber(re)} {sign} {imStr}i";
        }

        private static string FormatNumber(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Inf";
            if (double.IsNegativeInfinity(v)) return "-Inf";
            if (v == 0) return "0";
            double absV = System.Math.Abs(v);
            if (absV >= 1e-4 && absV < 1e6)
                return v.ToString("G6", CultureInfo.InvariantCulture);
            return v.ToString("G6", CultureInfo.InvariantCulture);
        }
        private static bool IsCommonBuiltin(string name) =>
            name is "sin" or "cos" or "tan" or "exp" or "log" or "log2" or "log10"
            or "sqrt" or "abs" or "sign" or "floor" or "ceil" or "round" or "fix"
            or "sum" or "prod" or "mean" or "min" or "max" or "cumsum" or "cumprod" or "diff"
            or "length" or "numel" or "size" or "zeros" or "ones" or "eye"
            or "linspace" or "logspace" or "meshgrid" or "transpose"
            or "atan2" or "mod" or "rem" or "power"
            or "asin" or "acos" or "atan" or "sinh" or "cosh" or "tanh"
            or "sind" or "cosd" or "tand" or "deg2rad" or "rad2deg"
            or "sort" or "unique" or "any" or "all" or "isempty" or "isscalar" or "isvector"
            or "reshape" or "repmat" or "disp"
            // Plots
            or "plot" or "plot3" or "scatter" or "scatter3" or "surf" or "mesh" or "imagesc"
            or "contour" or "contourf" or "pcolor" or "bar" or "barh" or "hist" or "histogram"
            or "stem" or "stairs" or "polar" or "compass" or "loglog" or "semilogx" or "semilogy"
            or "errorbar" or "quiver" or "peaks" or "colormap" or "colorbar" or "title"
            or "xlabel" or "ylabel" or "zlabel" or "legend" or "shading" or "axis" or "view"
            or "grid" or "hold" or "figure" or "clf" or "subplot"
            // Linear algebra
            or "det" or "inv" or "inverse" or "norm" or "dot" or "cross" or "trace" or "find"
            // String / IO
            or "sprintf" or "fprintf" or "num2str" or "str2num" or "strcat" or "strlen"
            or "upper" or "lower"
            // Higher-order
            or "feval" or "arrayfun" or "cellfun" or "structfun" or "map"
            // Simbolicos
            or "syms" or "expand" or "simplify" or "factor" or "solve" or "subs"
            or "int" or "taylor" or "limit" or "dsolve" or "laplace" or "fourier"
            or "trigsimplify" or "collect" or "coeffs"
            // Optim/interp/sigproc
            or "fzero" or "fminbnd" or "fminsearch" or "spline" or "pchip" or "polyfit" or "polyval" or "roots"
            or "trapz" or "cumtrapz" or "gradient" or "conv" or "filter"
            or "var" or "std" or "median"
            // FFT
            or "fft" or "ifft" or "fft2" or "ifft2" or "fftshift"
            // LinAlg/Sparse
            or "linsolve" or "mldivide" or "gauss_seidel" or "pcg"
            or "sparse" or "full" or "issparse" or "nnz" or "spdiags" or "speye" or "spones"
            // ODE
            or "ode45" or "ode23" or "ode4" or "ode_euler"
            // Complex
            or "real" or "imag" or "conj" or "angle" or "complex"
            // Strings extra
            or "strsplit" or "strjoin" or "strrep" or "strfind" or "contains"
            or "startsWith" or "endsWith" or "strtrim"
            // Workspace
            or "who" or "whos" or "clear" or "exist" or "tic" or "toc" or "assignin" or "evalin"
            // I/O
            or "csvread" or "csvwrite" or "dlmread" or "dlmwrite"
            // Optim
            or "fsolve" or "lsqnonlin" or "lsqcurvefit"
            // Integration
            or "integral" or "quad" or "quadl" or "quadgk" or "dblquad" or "triplequad"
            // Regex
            or "regexp" or "regexprep" or "regexpi"
            // Broadcasting / sparse viz
            or "bsxfun" or "spy" or "magic"
            // Random
            or "rand" or "randn" or "randi" or "randperm"
            // LinAlg avanzado
            or "expm" or "logm" or "sqrtm" or "lu" or "qr" or "chol" or "schur"
            or "bicg" or "gmres"
            // PDE / BVP
            or "pdepe" or "nthroot" or "bvp4c"
            // Symbolic
            or "sym" or "syms" or "diff" or "subs" or "simplify" or "expand" or "double" or "latex"
            // I/O extra
            or "imread" or "imwrite"
            // Modern integrals
            or "integral2" or "integral3"
            // Symbolic v10
            or "int" or "taylor" or "solve" or "pretty" or "laplace" or "fourier"
            // v11: ilaplace, limit, conv2/imfilter, quiver3/slice, text/annotation
            or "ilaplace" or "limit" or "conv2" or "imfilter" or "imresize"
            or "fspecial" or "rgb2gray"
            or "quiver3" or "slice" or "streamslice"
            or "text" or "annotation" or "sgtitle"
            // v12: signal, z-transform, coeffs
            or "heaviside" or "dirac" or "rectpuls" or "sinc"
            or "ztrans" or "iztrans"
            or "coeffs" or "sym2poly" or "poly2sym" or "collect"
            // v13: control systems + symbolic extras
            or "tf" or "zpk" or "step" or "impulse" or "bode" or "nyquist"
            or "series" or "parallel" or "feedback"
            or "pole" or "zero" or "dcgain" or "damp"
            or "symsum" or "piecewise" or "assume" or "assumeAlso"
            // v14: state-space, SVD, manipulation
            or "ss" or "tf2ss" or "ss2tf" or "lsim" or "c2d" or "d2c"
            or "margin" or "rlocus"
            or "svd" or "rank" or "pinv"
            or "vertcat" or "horzcat" or "cat" or "permute" or "squeeze"
            or "flipud" or "fliplr" or "rot90" or "ipermute"
            or "logspace"
            // v15: lqr/kalman, stats, signal filters, JSON
            or "lqr" or "care" or "lqe" or "stepinfo"
            or "normpdf" or "normcdf" or "norminv" or "tpdf" or "tcdf"
            or "chi2pdf" or "chi2cdf" or "fpdf" or "binopdf" or "poisspdf" or "gampdf"
            or "erf" or "erfc" or "erfinv" or "gamma" or "beta" or "factorial" or "nchoosek"
            or "butter" or "freqz" or "hilbert" or "xcorr" or "xcov"
            or "jsonencode" or "jsondecode"
            // v16: trig advanced + filtros extra + viz 2D + fmincon
            or "trigexpand" or "trigsimplify"
            or "cheby1" or "cheby2" or "ellip"
            or "histogram2" or "heatmap" or "stem"
            or "fmincon" or "linprog" or "quadprog"
            // v17: sparse real + linalg fundamentals + string arrays
            or "density" or "nonzeros"
            or "kron" or "null" or "orth" or "colspace" or "rowspace"
            or "string" or "strlength"
            // v18: 3D arrays + utilities + .mat I/O
            or "zeros3" or "ones3" or "ndims" or "cat3"
            or "mat2str" or "accumarray" or "tabulate" or "histcounts"
            or "save" or "load";
    }
}
