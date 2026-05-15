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
        // MATLAB bare keywords (sin '#'). Case-sensitive.
        // =====================================================================
        internal static readonly FrozenSet<string> Keywords =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "break", "case", "catch", "continue", "do", "else", "elseif",
            "end", "for", "function", "global", "if", "otherwise", "persistent",
            "return", "switch", "try", "while",
        }.ToFrozenSet(StringComparer.Ordinal);

        // =====================================================================
        // MATLAB built-in functions. Pintadas como Types.Function (bold).
        // El preprocessor MatlabPreprocessor las mapea a las equivalentes
        // Calcpad (length→len, log→ln, log10→log, ceil→ceiling, prod→product,
        // mean→average, etc.) cuando hace falta.
        // =====================================================================
        internal static readonly FrozenSet<string> Functions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Trigonometría
            "sin", "cos", "tan", "asin", "acos", "atan", "atan2",
            "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
            "csc", "sec", "cot", "acsc", "asec", "acot",
            // Exp / log
            "exp", "log", "log2", "log10", "ln", "sqrt", "cbrt", "nthroot",
            // Abs / sign / round
            "abs", "sign", "floor", "ceil", "round", "fix", "mod", "rem",
            // Agregaciones
            "sum", "prod", "mean", "median", "min", "max", "std", "var",
            "length", "numel", "size", "ndims", "norm",
            // Matrix
            "zeros", "ones", "eye", "diag", "transpose", "inv", "pinv",
            "det", "rank", "trace", "eig", "svd", "lu", "qr", "chol",
            // Rangos / búsqueda
            "linspace", "logspace", "find", "sort", "unique", "flip", "reverse",
            "any", "all", "isempty", "isnan", "isinf", "isfinite", "true", "false",
            // I/O
            "disp", "display", "fprintf", "sprintf", "printf", "clear", "clc",
            "format", "input", "error", "warning",
            // Plot
            "plot", "plot3", "surf", "mesh", "contour", "contourf", "quiver",
            "scatter", "bar", "histogram", "title", "xlabel", "ylabel", "zlabel",
            "legend", "figure", "subplot", "grid", "hold", "axis", "colormap",
            "colorbar", "shading", "view",
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
                    // Clasificar:
                    Types t;
                    bool bold = false;
                    if (Keywords.Contains(ident))
                        t = Types.Keyword;
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
