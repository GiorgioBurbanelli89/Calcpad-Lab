// =============================================================================
// Calcpad Lab — HighLighter.cs
// =============================================================================
//   Syntax highlighter MATLAB-only para el RichTextBox del editor.
//   Reescrito desde cero: el código previo soportaba sintaxis Calcpad clásica
//   (#if, #for, 'comment', #deq, #sym, #blk, #cen, #python, etc.) que ya no
//   se usa. Calcpad Lab procesa exclusivamente scripts MATLAB (.m).
//
//   Tokenización por línea:
//     - %  → comment hasta EOL  ;  %% → heading
//     - 'texto' / "texto"       → string literal
//     - bare keywords MATLAB    → Keyword (magenta)
//     - bare functions MATLAB   → Function (bold)
//     - identifiers definidos   → Variable / Function (según UserDefined)
//     - números 25e6 / 2.5e-3   → Const
//     - operadores              → Operator (== ~= <= >= .* .^ ./ etc.)
//     - brackets ( ) [ ] { }    → Bracket
//
//   API pública usada por MainWindow / AutoCompleteManager (mantenida intacta):
//     - Types enum
//     - Colors[] static
//     - Comments[] static (caracteres que abren comment — `%`)
//     - Defined (UserDefined instance)
//     - Parse(Paragraph, bool, int, bool, string?, Paragraph?)
//     - CheckHighlight(Paragraph, ref int)
//     - Clear(Paragraph)
//     - HighlightBrackets(Paragraph, int)
//     - FindPositionAtOffset(Paragraph, int)
//     - GetCSSClassFromColor(Brush)
//     - GetPartialSource(string)
//     - IncludeClickEventHandler delegate
// =============================================================================
using Calcpad.Core;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Calcpad.Wpf
{
    internal class HighLighter
    {
        // =====================================================================
        // Tipos de token  (índices en Colors[])
        // =====================================================================
        internal enum Types
        {
            None,        // 0  — texto plano / whitespace
            Const,       // 1  — números
            Units,       // 2  — sufijo de unidad (mm, kg, MPa, etc.)
            Operator,    // 3  — + - * / ^ = == ~= ...
            Variable,    // 4  — identificadores definidos por el usuario
            Function,    // 5  — funciones (MATLAB builtins + user)
            Keyword,     // 6  — for, while, if, end, function, ...
            Command,     // 7  — $Plot{...}, $Map{...}, $Integral{...}
            Bracket,     // 8  — ( ) [ ] { }
            Comment,     // 9  — % ...   o líneas de string
            Tag,         // 10 — (legacy slot, unused MATLAB)
            Input,       // 11 — (legacy slot, unused MATLAB)
            Include,     // 12 — (legacy slot)
            Macro,       // 13 — (legacy slot)
            HtmlComment, // 14 — (legacy slot)
            Format,      // 15 — (legacy slot)
            Error,       // 16 — token no reconocido
        }

        // =====================================================================
        // Colores por token
        // =====================================================================
        internal static readonly SolidColorBrush KeywordBrush = new(Color.FromRgb(166, 38, 164));
        private static readonly SolidColorBrush BracketHilite = new(Color.FromRgb(255, 200, 0));
        private static readonly SolidColorBrush BackgroundBrush = new(Color.FromArgb(160, 240, 248, 255));
        private static readonly SolidColorBrush HtmlCommentBrush = new(Color.FromRgb(160, 160, 160));

        internal static readonly Brush[] Colors =
        [
            Brushes.Gray,              // None
            Brushes.Black,             // Const
            Brushes.DarkCyan,          // Units
            Brushes.Goldenrod,         // Operator
            Brushes.Blue,              // Variable
            Brushes.Black,             // Function (bold)
            KeywordBrush,              // Keyword
            Brushes.Magenta,           // Command
            Brushes.DeepPink,          // Bracket
            Brushes.ForestGreen,       // Comment / String
            Brushes.DarkOrchid,        // Tag (legacy)
            Brushes.Red,               // Input (legacy)
            Brushes.Indigo,            // Include (legacy)
            Brushes.DarkMagenta,       // Macro (legacy)
            HtmlCommentBrush,          // HtmlComment (legacy)
            Brushes.DarkGray,          // Format (legacy)
            Brushes.Crimson,           // Error
        ];

        // =====================================================================
        // MATLAB bare keywords (sin '#'). Case-sensitive. Sincronizado con
        // MatlabParser.cs (control de flujo + OOP classdef).
        // =====================================================================
        internal static readonly FrozenSet<string> Keywords =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Control de flujo
            "break", "case", "catch", "continue", "do", "else", "elseif",
            "end", "for", "function", "if", "otherwise", "return",
            "switch", "try", "while",
            // Scope
            "global", "persistent",
            // OOP — classdef / properties / methods / events / enumeration
            "classdef", "properties", "methods", "events", "enumeration",
            // Lógicos textuales (no son operadores en MATLAB pero se usan así)
            "import",
        }.ToFrozenSet(StringComparer.Ordinal);

        // =====================================================================
        // MATLAB constantes built-in (pintadas como Types.Const).
        // Fuente: MatlabEvaluator.cs línea 6573 (switch de IdentRef sin scope).
        // =====================================================================
        internal static readonly FrozenSet<string> Constants =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "pi", "e", "Inf", "inf", "NaN", "nan", "eps", "i", "j",
            "ans", "realmax", "realmin", "intmax", "intmin",
        }.ToFrozenSet(StringComparer.Ordinal);

        // =====================================================================
        // MATLAB built-in functions. Pintadas como Types.Function (bold).
        // FUENTE DE VERDAD: `_builtins[...]` + `_multiOutBuiltins[...]` en
        // Symbolic.Core/Matlab/MatlabEvaluator.cs (412 entradas al 2026-05).
        //
        // Si agregás un builtin al engine, sumalo también acá para que aparezca
        // bold en el editor. `true`/`false` también son builtins (logical(N)) pero
        // los tratamos como variables/constantes visualmente.
        // =====================================================================
        internal static readonly FrozenSet<string> Functions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "abs", "accumarray", "acos", "addpath", "all", "and", "angle", "annotation",
            "any", "arrayfun", "asin", "assignin", "assume", "assumeAlso", "atan", "atan2",
            "axis", "bar", "barh", "beta", "bicg", "binopdf", "bode", "bsxfun",
            "btdb", "butter", "bvp4c", "c2d", "camlight", "care", "cat", "cat3",
            "ceil", "cellfun", "char", "cheby1", "cheby2", "chi2cdf", "chi2pdf", "chol",
            "cla", "clc", "clear", "clf", "close", "coeffs", "collect", "colorbar",
            "colormap", "colspace", "complex", "conj", "contains", "contour", "contourf", "conv",
            "conv2", "cos", "cosd", "cosh", "cross", "csvread", "csvwrite", "cumprod",
            "cumsum", "cumtrapz", "d2c", "damp", "dblquad", "dbz", "dcgain", "deg2rad",
            "delaunay", "density", "det", "diag", "diff", "dirac", "disp", "dlmread",
            "dlmwrite", "dot", "double", "drawnow", "dsolve", "eig", "eigenvals", "eigenvecs",
            "ellip", "endsWith", "eq", "erf", "erfc", "erfinv", "evalin", "exist",
            "exp", "expand", "expm", "eye", "factor", "factorial", "feedback",
            "feval", "fft", "fft2", "fftshift", "fieldnames", "figure", "fill", "fill3",
            "filter", "find", "fix", "fliplr", "flipud", "floor", "fminbnd", "fmincon",
            "fminsearch", "fourier", "fpdf", "fprintf", "freqz", "fsolve", "fspecial", "full",
            "funm", "fzero", "gamma", "gampdf", "gauss_seidel", "gca", "gcf", "ge",
            "gmres", "gradient", "grid", "gt", "heatmap", "heaviside", "hilbert", "hist",
            "histcounts", "histogram", "histogram2", "hold", "horzcat", "ifft", "ifft2", "ilaplace",
            "imag", "imagesc", "imfilter", "impulse", "imread", "imresize", "imwrite", "int",
            "integral", "integral2", "integral3", "interp1", "intersect", "inv", "inverse", "ipermute",
            "iscell", "ischar", "iscomplex", "isempty", "isfield", "isfinite", "isinf", "islogical",
            "ismember", "isnan", "isnumeric", "isreal", "isscalar", "issparse", "isstring", "isstruct",
            "isvector", "iztrans", "jsondecode", "jsonencode", "kron", "laplace", "latex", "ldivide",
            "le", "legend", "length", "light", "lighting", "limit", "line", "linprog",
            "linsolve", "linspace", "load", "log", "log10", "log2", "logm", "logspace",
            "lower", "lqe", "lqr", "lsim", "lsqcurvefit", "lsqnonlin", "lt", "lu",
            "magic", "map", "margin", "mat2str", "material", "max", "mean", "median",
            "mesh", "meshgrid", "min", "minus", "mkdir", "mldivide", "mod", "mtimes",
            "nchoosek", "ndims", "ne", "nnz", "nonzeros", "norm", "normcdf", "norminv",
            "normpdf", "not", "null", "num2str", "numel", "nyquist", "ode23", "ode4",
            "ode45", "ode_euler", "ones", "ones3", "or", "orth", "parallel", "patch",
            "pause", "pcg", "pchip", "pcolor", "pdepe", "peaks", "permute", "piecewise",
            "pinv", "plot", "plot3", "plus", "plus_str", "poisspdf", "polar", "pole",
            "poly2sym", "polyfit", "polyval", "power", "pretty", "prod", "qr", "quad",
            "quadgk", "quadl", "quadprog", "quiver", "quiver3", "rad2deg", "rand", "randi",
            "randn", "randperm", "rank", "rdivide", "real", "rectpuls", "regexp", "regexpi",
            "regexprep", "rem", "repmat", "reshape", "rgb2gray", "rlocus", "rmpath", "roots",
            "rot90", "round", "rowspace", "save", "saveas", "scatter", "scatter3", "schur",
            "series", "setdiff", "sgtitle", "shading", "sign", "simplify", "sin", "sinc",
            "sind", "sinh", "size", "slice", "solve", "sort", "sparse", "spdiags",
            "speye", "spline", "spones", "sprintf", "spy", "sqrt", "sqrtm", "squeeze",
            "ss", "ss2tf", "startsWith", "std", "stem", "step", "stepinfo", "str2num",
            "strcat", "strcmp", "strcmpi", "streamslice", "strfind", "string", "strjoin", "strlen",
            "strlength", "strncmp", "strncmpi", "strrep", "strsplit", "strtrim", "struct", "structfun",
            "subplot", "subs", "sum", "surf", "svd", "sym", "sym2poly", "syms",
            "symsum", "tabulate", "tan", "tand", "tanh", "taylor", "tcdf", "text",
            "tf", "tf2ss", "tic", "times", "title", "toc", "tpdf", "trace",
            "transpose", "trapz", "trigexpand", "trigsimplify", "triplequad", "trisurf", "trunc",
            "uminus", "union", "unique", "uplus", "upper", "var", "vertcat", "view",
            "who", "whos", "xcorr", "xcov", "xlabel", "ylabel", "zero", "zeros",
            "zeros3", "zlabel", "zpk", "ztrans",
            // Aliases comunes que el preprocessor mapea internamente:
            "asinh", "acosh", "atanh", "csc", "sec", "cot", "acsc", "asec", "acot",
            "ln", "cbrt", "nthroot", "display", "printf", "format", "input", "error", "warning",
            "flip", "reverse",
        }.ToFrozenSet(StringComparer.Ordinal);

        // =====================================================================
        // Operadores y caracteres especiales
        // =====================================================================
        private static readonly FrozenSet<char> Operators =
            new HashSet<char>() { '+', '-', '*', '/', '^', '\\', '=', '<', '>',
                                  '~', '&', '|', '!', '%', '?', '.' }
            .ToFrozenSet();

        // Caracteres que ABREN un comment. Usado por AutoCompleteManager
        // para detectar si el cursor está dentro de un comment.
        internal static readonly char[] Comments = ['%'];

        // =====================================================================
        // Estado y campos de instancia
        // =====================================================================
        internal UserDefined Defined = new();

        // Click handler para hyperlinks de #include — en MATLAB no se usa,
        // pero la API existe por compat con MainWindow.
        internal static MouseButtonEventHandler IncludeClickEventHandler;

        // Buffer de tokenizer
        private readonly StringBuilder _builder = new();

        // =====================================================================
        // Limpiar resaltado de un paragraph
        // =====================================================================
        internal static void Clear(Paragraph p)
        {
            if (p is null) return;
            // Reset font weight de runs (los Functions van en bold y queremos
            // que vuelvan a normal antes del re-render).
            foreach (var inline in p.Inlines)
                if (inline is Run r) r.FontWeight = FontWeights.Normal;
            p.Background = null;
        }

        // =====================================================================
        // Re-resaltar paréntesis matching alrededor del caret
        // =====================================================================
        internal static void HighlightBrackets(Paragraph p, int caretOffset)
        {
            if (p is null) return;
            // Versión simple: no resaltamos paréntesis explícitamente.
            // (El usuario detecta paréntesis sin balance por contexto visual.)
        }

        // =====================================================================
        // Encontrar TextPointer al offset de caracteres dentro de un paragraph
        // =====================================================================
        internal static TextPointer FindPositionAtOffset(Paragraph p, int offset)
        {
            if (p is null) return null;
            var ptr = p.ContentStart;
            int count = 0;
            while (ptr != null && count < offset)
            {
                var next = ptr.GetNextInsertionPosition(LogicalDirection.Forward);
                if (next == null) break;
                ptr = next;
                count++;
            }
            return ptr ?? p.ContentEnd;
        }

        // =====================================================================
        // Mapeo Brush → CSS class (usado al exportar HTML)
        // =====================================================================
        internal static string GetCSSClassFromColor(Brush b)
        {
            if (b == Colors[(int)Types.Comment]) return "comment";
            if (b == Colors[(int)Types.Keyword]) return "keyword";
            if (b == Colors[(int)Types.Function]) return "function";
            if (b == Colors[(int)Types.Variable]) return "variable";
            if (b == Colors[(int)Types.Const]) return "const";
            if (b == Colors[(int)Types.Operator]) return "operator";
            if (b == Colors[(int)Types.Bracket]) return "bracket";
            if (b == Colors[(int)Types.Command]) return "command";
            if (b == Colors[(int)Types.Units]) return "units";
            if (b == Colors[(int)Types.Error]) return "error";
            return "";
        }

        // =====================================================================
        // Obtener "partial source" — usado para tooltips. Retorna el texto.
        // =====================================================================
        internal static string GetPartialSource(string s) => s ?? "";

        // =====================================================================
        // CheckHighlight — stub que retorna el paragraph (legacy de Calcpad).
        // En MATLAB no hay re-flow especial de paragraphs entre líneas.
        // =====================================================================
        internal Paragraph CheckHighlight(Paragraph p, ref int lineNumber) => p;

        // =====================================================================
        // PARSE — método principal, llamado para cada paragraph del editor
        //
        //   p             paragraph a re-resaltar
        //   isComplex     legacy flag (sin uso en MATLAB)
        //   lineNumber    número de línea (1-based) para tracking
        //   single        legacy (no afecta lógica MATLAB)
        //   textOverride  si no es null, usar este texto en lugar de p.Text
        //   skipParagraph legacy (no afecta lógica MATLAB)
        // =====================================================================
        internal void Parse(Paragraph p, bool isComplex, int lineNumber, bool single,
                            string textOverride = null, Paragraph skipParagraph = null)
        {
            if (p is null) return;
            string text;
            if (textOverride != null)
                text = textOverride;
            else
                text = new TextRange(p.ContentStart, p.ContentEnd).Text;
            text ??= "";

            // Limpiar inlines existentes
            p.Inlines.Clear();
            p.Background = null;

            // Línea vacía → nada que hacer
            if (text.Length == 0) return;

            // Tokenizar línea
            TokenizeLine(p, text);
        }

        // =====================================================================
        // Tokenizer line-by-line. Emite Runs al paragraph.
        // =====================================================================
        private void TokenizeLine(Paragraph p, string text)
        {
            int n = text.Length;
            int i = 0;

            // Skip leading whitespace (preservarlo como Run sin color)
            int wsStart = i;
            while (i < n && (text[i] == ' ' || text[i] == '\t')) i++;
            if (i > wsStart)
                p.Inlines.Add(new Run(text[wsStart..i]));

            // === %% Heading? ===
            // Línea que empieza con `%%` (después de ws) es un section heading.
            if (i + 1 < n && text[i] == '%' && text[i + 1] == '%')
            {
                var run = new Run(text[i..])
                {
                    Foreground = Colors[(int)Types.Keyword],
                    FontWeight = FontWeights.Bold,
                };
                p.Inlines.Add(run);
                p.Background = BackgroundBrush;
                return;
            }

            // === Loop principal: emitir tokens ===
            while (i < n)
            {
                char c = text[i];

                // ── Whitespace ──
                if (c == ' ' || c == '\t')
                {
                    int wsBeg = i;
                    while (i < n && (text[i] == ' ' || text[i] == '\t')) i++;
                    p.Inlines.Add(new Run(text[wsBeg..i]));
                    continue;
                }

                // ── Comment % hasta EOL ──
                if (c == '%')
                {
                    p.Inlines.Add(new Run(text[i..])
                    {
                        Foreground = Colors[(int)Types.Comment],
                    });
                    return; // resto consumido
                }

                // ── Strings 'texto' o "texto" ──
                if (c == '\'' || c == '"')
                {
                    // En MATLAB, ' después de un identifier/number/) es transpose,
                    // no string. Heurística: si el char anterior es alfanum o ')',
                    // tratar como operador transpose.
                    bool isTranspose = false;
                    if (c == '\'' && p.Inlines.Count > 0 && i > 0)
                    {
                        char prev = text[i - 1];
                        if (char.IsLetterOrDigit(prev) || prev == ')' || prev == ']' || prev == '_')
                            isTranspose = true;
                    }
                    if (isTranspose)
                    {
                        p.Inlines.Add(new Run("'")
                        {
                            Foreground = Colors[(int)Types.Operator],
                        });
                        i++;
                        continue;
                    }
                    int strBeg = i;
                    char delim = c;
                    i++;
                    while (i < n && text[i] != delim)
                    {
                        // MATLAB: '' dentro de string = ' literal
                        if (text[i] == delim && i + 1 < n && text[i + 1] == delim) i += 2;
                        else i++;
                    }
                    if (i < n) i++; // consume closing delim
                    p.Inlines.Add(new Run(text[strBeg..i])
                    {
                        Foreground = Colors[(int)Types.Comment], // mismo color string/comment
                    });
                    continue;
                }

                // ── Números (incl. 25e6, 2.5e-3, .5) ──
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(text[i + 1])))
                {
                    int numBeg = i;
                    // mantisa
                    while (i < n && char.IsDigit(text[i])) i++;
                    if (i < n && text[i] == '.' && i + 1 < n && char.IsDigit(text[i + 1]))
                    {
                        i++;
                        while (i < n && char.IsDigit(text[i])) i++;
                    }
                    // exponente
                    if (i < n && (text[i] == 'e' || text[i] == 'E'))
                    {
                        int eIdx = i;
                        int j = i + 1;
                        if (j < n && (text[j] == '+' || text[j] == '-')) j++;
                        int expStart = j;
                        while (j < n && char.IsDigit(text[j])) j++;
                        if (j > expStart) i = j;
                    }
                    p.Inlines.Add(new Run(text[numBeg..i])
                    {
                        Foreground = Colors[(int)Types.Const],
                    });
                    // Sufijo de unidad opcional (mm, kg, MPa, ...) — heurística:
                    // letras inmediatamente después del número sin espacio.
                    if (i < n && char.IsLetter(text[i]))
                    {
                        int unitBeg = i;
                        while (i < n && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '^'))
                            i++;
                        p.Inlines.Add(new Run(text[unitBeg..i])
                        {
                            Foreground = Colors[(int)Types.Units],
                        });
                    }
                    continue;
                }

                // ── Identifiers (variables, keywords, functions) ──
                if (char.IsLetter(c) || c == '_')
                {
                    int idBeg = i;
                    while (i < n && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    var ident = text[idBeg..i];
                    // Clasificar (orden importa: Keywords > Constants > Functions > User > fallback):
                    Types t;
                    bool bold = false;
                    if (Keywords.Contains(ident))
                        t = Types.Keyword;
                    else if (Constants.Contains(ident))
                        t = Types.Const;
                    else if (Functions.Contains(ident))
                    {
                        t = Types.Function; bold = true;
                    }
                    else if (Defined.Functions.ContainsKey(ident))
                    {
                        t = Types.Function; bold = true;
                    }
                    else if (Defined.Variables.ContainsKey(ident))
                        t = Types.Variable;
                    else
                    {
                        // Identifier desconocido. Si va seguido de '(' es una
                        // función no declarada (tal vez un error tipográfico)
                        // o una function MATLAB que no listamos.
                        if (i < n && text[i] == '(')
                        {
                            t = Types.Function; bold = true;
                        }
                        else
                        {
                            t = Types.Variable;
                        }
                    }
                    var run = new Run(ident) { Foreground = Colors[(int)t] };
                    if (bold) run.FontWeight = FontWeights.Bold;
                    p.Inlines.Add(run);
                    continue;
                }

                // ── $Comandos (legacy de Calcpad, mantenidos por compat) ──
                if (c == '$' && i + 1 < n && char.IsLetter(text[i + 1]))
                {
                    int cmdBeg = i;
                    i++;
                    while (i < n && char.IsLetterOrDigit(text[i])) i++;
                    p.Inlines.Add(new Run(text[cmdBeg..i])
                    {
                        Foreground = Colors[(int)Types.Command],
                    });
                    continue;
                }

                // ── Brackets ──
                if (c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}')
                {
                    p.Inlines.Add(new Run(c.ToString())
                    {
                        Foreground = Colors[(int)Types.Bracket],
                    });
                    i++;
                    continue;
                }

                // ── Operadores (1 o 2 chars) ──
                if (Operators.Contains(c))
                {
                    int opBeg = i;
                    // Operadores 2-char: ==, ~=, <=, >=, .*, ./, .^, .\, .'
                    if (i + 1 < n)
                    {
                        char c2 = text[i + 1];
                        if ((c == '=' && c2 == '=') ||
                            (c == '~' && c2 == '=') ||
                            (c == '<' && c2 == '=') ||
                            (c == '>' && c2 == '=') ||
                            (c == '.' && (c2 == '*' || c2 == '/' || c2 == '^' || c2 == '\\' || c2 == '\'')))
                        {
                            i += 2;
                            p.Inlines.Add(new Run(text[opBeg..i])
                            {
                                Foreground = Colors[(int)Types.Operator],
                            });
                            continue;
                        }
                    }
                    i++;
                    p.Inlines.Add(new Run(c.ToString())
                    {
                        Foreground = Colors[(int)Types.Operator],
                    });
                    continue;
                }

                // ── Delimitadores ; , : ──
                if (c == ';' || c == ',' || c == ':')
                {
                    p.Inlines.Add(new Run(c.ToString())
                    {
                        Foreground = Colors[(int)Types.Operator],
                    });
                    i++;
                    continue;
                }

                // ── Carácter no reconocido → Error ──
                p.Inlines.Add(new Run(c.ToString())
                {
                    Foreground = Colors[(int)Types.Error],
                });
                i++;
            }
        }
    }
}
