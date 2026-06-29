// =============================================================================
// Calcpad Lab — MATLAB Tokenizer (MathParser de Calcpad reemplazado)
// =============================================================================
//   Tokeniza una expresión MATLAB-pura. Sin keywords ni operadores Calcpad.
//
//   Soportado en esta versión MVP:
//     - Números: 3, 3.14, .5, 1e10, 2.5e-3
//     - Identificadores: variables, nombres de funciones
//     - Operadores binarios: + - * / ^ .* ./ .^ \ .\
//     - Operadores comparación: < > <= >= == ~=
//     - Operadores lógicos: && || ~ & |
//     - Asignación: =
//     - Separadores: , ; (en contexto de matriz/función args)
//     - Brackets: ( ) [ ] { }
//     - Rango: :
//     - Strings: 'foo' (char), "bar" (string)
//     - Comentarios: % ... fin de línea
//     - Transposición: ' (post-operando)
//
//   NO usa nada de Calcpad.MathParser. Independiente.
// =============================================================================
using System;
using System.Collections.Generic;

namespace Calcpad.Core.Matlab
{
    public enum MatlabTokenKind
    {
        Number,           // 3, 3.14, 1e10
        ImaginaryNumber,  // 2i, 1.5j (sufijo MATLAB)
        Identifier,       // x, foo_bar, sin
        String,           // 'text' (char array)
        StringDouble,     // "text" (string scalar — MATLAB R2016b+)
        // Operadores aritméticos
        Plus,             // +
        Minus,            // -
        Star,             // *
        Slash,            // /
        Backslash,        // \  (left division)
        Caret,            // ^
        DotStar,          // .*
        DotSlash,         // ./
        DotBackslash,     // .\
        DotCaret,         // .^
        // Asignación / comparación
        Assign,           // =
        // Compuestas de Octave (solo en modo Octave)
        PlusAssign,       // +=
        MinusAssign,      // -=
        StarAssign,       // *=
        SlashAssign,      // /=
        Increment,        // ++
        Decrement,        // --
        Equal,            // ==
        NotEqual,         // ~=
        Less,             // <
        Greater,          // >
        LessEq,           // <=
        GreaterEq,        // >=
        // Lógicos
        AndShort,         // &&
        OrShort,          // ||
        AndBit,           // &
        OrBit,            // |
        Not,              // ~
        // Brackets
        LParen,           // (
        RParen,           // )
        LBracket,         // [
        RBracket,         // ]
        LBrace,           // {
        RBrace,           // }
        // Separadores
        Comma,            // ,
        Semicolon,        // ;
        Colon,            // :
        // Otros
        Transpose,        // '  (sólo cuando viene tras operando — char-array transpose)
        DotTranspose,     // .'
        Dot,              // .  (field access en structs — futuro)
        At,               // @  (function handles — futuro)
        Ellipsis,         // ...  (line continuation, ya consumido por preprocesador)
        Newline,          // fin de statement
        Comment,          // % ... fin de línea (sin el %)
        SectionHeading,   // %% ... → encabezado de sección (markdown-like)
        EndOfFile,
    }

    public readonly struct MatlabToken
    {
        public readonly MatlabTokenKind Kind;
        public readonly string Text;       // texto literal
        public readonly double NumberValue;
        public readonly int Line;
        public readonly int Column;
        public MatlabToken(MatlabTokenKind kind, string text, int line, int col, double num = 0)
        {
            Kind = kind; Text = text; Line = line; Column = col; NumberValue = num;
        }
        public override string ToString() => $"{Kind}({Text})";
    }

    /// <summary>
    /// Tokenizador MATLAB-puro. Genera una secuencia de <see cref="MatlabToken"/>
    /// desde un source string. NO depende del MathParser de Calcpad.
    /// </summary>
    public static class MatlabTokenizer
    {
        /// <summary>Modo Octave (thread-local): habilita las extensiones de sintaxis que
        /// Octave añade sobre MATLAB (comentarios <c>#</c>, <c>+= ++ --</c>,
        /// <c>endfor/endif/...</c>, <c>!</c> como <c>~</c>, continuación con <c>\</c>).
        /// Off = MATLAB estricto (Calcpad-Lab). Lo fija <see cref="MatlabPipeline"/>.</summary>
        [ThreadStatic] public static bool OctaveMode;

        /// <summary>Cerradores de bloque de Octave que en modo Octave se normalizan a
        /// <c>end</c>, de modo que el parser MATLAB existente los acepta sin cambios.</summary>
        private static readonly HashSet<string> _octaveBlockEnders = new(StringComparer.Ordinal)
        {
            "endfor", "endif", "endwhile", "endfunction", "endswitch",
            "end_try_catch", "endparfor", "end_unwind_protect",
            "endproperties", "endmethods", "endenumeration", "endevents"
        };

        public static List<MatlabToken> Tokenize(string source)
        {
            bool octave = OctaveMode;
            int srcLen = source?.Length ?? 0;
            // ¿desde idx hasta el fin de línea sólo hay espacios/tabs? (para bloques %{ %})
            bool RestBlank(int idx)
            {
                for (int p = idx; p < srcLen && source[p] != '\n'; p++)
                    if (source[p] != ' ' && source[p] != '\t' && source[p] != '\r') return false;
                return true;
            }
            // ¿idx es la primera posición no-blanca de su línea?
            bool AtLineStart(int idx)
            {
                for (int b = idx - 1; b >= 0; b--)
                {
                    if (source[b] == '\n') break;
                    if (source[b] != ' ' && source[b] != '\t') return false;
                }
                return true;
            }
            var tokens = new List<MatlabToken>();
            if (string.IsNullOrEmpty(source))
            {
                tokens.Add(new MatlabToken(MatlabTokenKind.EndOfFile, "", 1, 1));
                return tokens;
            }
            int i = 0;
            int line = 1;
            int col = 1;
            int n = source.Length;
            // Estado: estamos esperando un operando? (en ese caso `'` abre string)
            // Si venimos de operando/cierre paren/cierre bracket, `'` es transpose.
            // MATLAB rule: si hay whitespace ENTRE el último token y el `'`,
            // se considera string (no transpose). `prevWasWhitespace` tracking
            // se usa para esa distinción.
            bool prevIsOperand = false;
            bool prevWasWhitespace = false;
            // Pila de brackets para la regla MATLAB del espacio-menos dentro de [ ] y { }:
            // un +/- con espacio ANTES y sin espacio DESPUES, tras un operando, es un
            // elemento NUEVO (unario), no resta binaria. Ej: [-g -g] = [-g, -g].
            var brackets = new System.Collections.Generic.Stack<char>();

            while (i < n)
            {
                char c = source[i];
                // Whitespace (NO newline — newline es token separador)
                if (c == ' ' || c == '\t')
                {
                    i++; col++;
                    prevWasWhitespace = true;
                    continue;
                }
                if (c == '\r') { i++; continue; }
                if (c == '\n')
                {
                    tokens.Add(new MatlabToken(MatlabTokenKind.Newline, "\n", line, col));
                    i++; line++; col = 1;
                    prevIsOperand = false;
                    prevWasWhitespace = true;
                    continue;
                }
                // Bloque de comentario %{ ... %}  (y #{ ... #} en Octave). Reglas MATLAB/Octave:
                // el abridor `%{` y el cerrador `%}` van SOLOS en su línea; los bloques anidan.
                if ((c == '%' || (octave && c == '#')) && i + 1 < n && source[i + 1] == '{'
                    && AtLineStart(i) && RestBlank(i + 2))
                {
                    int depth = 1;
                    // saltar el resto de la línea del abridor
                    while (i < n && source[i] != '\n') i++;
                    if (i < n) { i++; line++; col = 1; }
                    while (i < n && depth > 0)
                    {
                        int p = i;
                        while (p < n && (source[p] == ' ' || source[p] == '\t')) p++;
                        if (p + 1 < n && (source[p] == '%' || (octave && source[p] == '#')) && RestBlank(p + 2))
                        {
                            if (source[p + 1] == '{') depth++;
                            else if (source[p + 1] == '}') depth--;
                        }
                        while (i < n && source[i] != '\n') i++;
                        if (i < n) { i++; line++; col = 1; }
                    }
                    prevIsOperand = false;
                    prevWasWhitespace = true;
                    continue;
                }
                // Comentarios %... hasta fin de línea. %% es heading (markdown-like).
                // En modo Octave, `#` también inicia comentario y `##` es heading.
                if (c == '%' || (octave && c == '#'))
                {
                    int startCol = col;
                    bool isHeading = i + 1 < n && source[i + 1] == c;
                    int start = i + (isHeading ? 2 : 1);
                    while (start < n && source[start] == ' ') start++;
                    int end = start;
                    while (end < n && source[end] != '\n') end++;
                    var text = source[start..end];
                    tokens.Add(new MatlabToken(
                        isHeading ? MatlabTokenKind.SectionHeading : MatlabTokenKind.Comment,
                        text, line, startCol));
                    int consumed = end - i;
                    i = end;
                    col += consumed;
                    prevIsOperand = false;
                    prevWasWhitespace = true;
                    continue;
                }
                // Continuación de línea ... (eaten silently — preprocesador ya lo manejó normalmente)
                if (c == '.' && i + 2 < n && source[i + 1] == '.' && source[i + 2] == '.')
                {
                    while (i < n && source[i] != '\n') { i++; col++; }
                    if (i < n && source[i] == '\n') { i++; line++; col = 1; }
                    continue;
                }
                // Octave: continuación de línea con `\` al final de línea (solo si tras el
                // `\` no queda nada salvo espacios/comentario). Si hay operando después
                // (p.ej. `A\b`) cae al switch como Backslash (división por la izquierda).
                if (octave && c == '\\')
                {
                    int k = i + 1;
                    while (k < n && (source[k] == ' ' || source[k] == '\t')) k++;
                    if (k >= n || source[k] == '\n' || source[k] == '\r')
                    {
                        while (i < n && source[i] != '\n') { i++; col++; }
                        if (i < n && source[i] == '\n') { i++; line++; col = 1; }
                        continue;
                    }
                }
                // Strings — distinción entre `'` (texto vs transpose) y `"` (siempre string)
                if (c == '"')
                {
                    int startCol = col;
                    int j = i + 1;
                    var sb = new System.Text.StringBuilder();
                    while (j < n)
                    {
                        if (source[j] == '"')
                        {
                            // double-quote escape: ""
                            if (j + 1 < n && source[j + 1] == '"')
                            {
                                sb.Append('"');
                                j += 2;
                                continue;
                            }
                            break; // fin de string
                        }
                        // Octave: en strings con comillas dobles, `\` procesa escapes (\n, \t, \", ...).
                        // MATLAB los deja literales (off = comportamiento original).
                        if (octave && source[j] == '\\' && j + 1 < n)
                        {
                            char e = source[j + 1];
                            sb.Append(e switch
                            {
                                'n' => '\n', 't' => '\t', 'r' => '\r', 'a' => '\a',
                                'b' => '\b', 'f' => '\f', 'v' => '\v', '0' => '\0',
                                '\\' => '\\', '"' => '"', _ => '\0'
                            });
                            // escape desconocido: conservar `\` + carácter tal cual
                            if (e is not ('n' or 't' or 'r' or 'a' or 'b' or 'f' or 'v' or '0' or '\\' or '"'))
                            { sb.Length--; sb.Append('\\'); sb.Append(e); }
                            j += 2;
                            continue;
                        }
                        if (source[j] == '\n') throw new MatlabParseException("Unterminated string", line, col);
                        sb.Append(source[j]);
                        j++;
                    }
                    if (j >= n) throw new MatlabParseException("Unterminated string", line, col);
                    j++; // skip cierre
                    tokens.Add(new MatlabToken(MatlabTokenKind.StringDouble, sb.ToString(), line, startCol));
                    col += j - i;
                    i = j;
                    prevIsOperand = true;
                    continue;
                }
                if (c == '\'')
                {
                    if (prevIsOperand && !prevWasWhitespace)
                    {
                        // Transpose (sin whitespace antes)
                        tokens.Add(new MatlabToken(MatlabTokenKind.Transpose, "'", line, col));
                        i++; col++;
                        prevIsOperand = true;
                        prevWasWhitespace = false;
                        continue;
                    }
                    // Char-array string
                    int startCol = col;
                    int j = i + 1;
                    var sb = new System.Text.StringBuilder();
                    while (j < n)
                    {
                        if (source[j] == '\'')
                        {
                            // single-quote escape MATLAB: '' dentro de '...' → comilla literal
                            if (j + 1 < n && source[j + 1] == '\'')
                            {
                                sb.Append('\'');
                                j += 2;
                                continue;
                            }
                            break; // fin de string
                        }
                        if (source[j] == '\n') throw new MatlabParseException("Unterminated char-array", line, col);
                        sb.Append(source[j]);
                        j++;
                    }
                    if (j >= n) throw new MatlabParseException("Unterminated char-array", line, col);
                    j++;
                    tokens.Add(new MatlabToken(MatlabTokenKind.String, sb.ToString(), line, startCol));
                    col += j - i;
                    i = j;
                    prevIsOperand = true;
                    continue;
                }
                // Números: dígito o `.` seguido de dígito
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(source[i + 1])))
                {
                    int startCol = col;
                    int j = i;
                    bool hasDot = false, hasExp = false;
                    while (j < n)
                    {
                        char cj = source[j];
                        if (char.IsDigit(cj)) { j++; continue; }
                        if (cj == '.' && !hasDot && !hasExp)
                        {
                            // Cuidado: `.*` `./` `.^` `.\` `.'` son operadores — NO parte del número
                            if (j + 1 < n)
                            {
                                char next = source[j + 1];
                                if (next == '*' || next == '/' || next == '^' || next == '\\' || next == '\'')
                                    break;
                            }
                            hasDot = true; j++; continue;
                        }
                        if ((cj == 'e' || cj == 'E') && !hasExp)
                        {
                            hasExp = true; j++;
                            if (j < n && (source[j] == '+' || source[j] == '-')) j++;
                            continue;
                        }
                        break;
                    }
                    var numStr = source[i..j];
                    if (!double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                                          System.Globalization.CultureInfo.InvariantCulture, out double val))
                        throw new MatlabParseException($"Invalid number literal: {numStr}", line, col);
                    // Sufijo MATLAB imaginario: 2i, 1.5j (i o j al final, sin más letras)
                    bool imag = false;
                    if (j < n && (source[j] == 'i' || source[j] == 'j'))
                    {
                        // Verificar que no sea parte de un identifier más largo
                        if (j + 1 >= n || (!char.IsLetterOrDigit(source[j + 1]) && source[j + 1] != '_'))
                        {
                            imag = true;
                            j++;
                        }
                    }
                    var numKind = imag ? MatlabTokenKind.ImaginaryNumber : MatlabTokenKind.Number;
                    tokens.Add(new MatlabToken(numKind, source[i..j], line, startCol, val));
                    col += j - i;
                    i = j;
                    prevIsOperand = true;
                    prevWasWhitespace = false;
                    continue;
                }
                // Identificadores: letra o _ seguido de alfanumérico/_
                if (char.IsLetter(c) || c == '_')
                {
                    int startCol = col;
                    int j = i;
                    while (j < n && (char.IsLetterOrDigit(source[j]) || source[j] == '_')) j++;
                    var name = source[i..j];
                    // En modo Octave, endfor/endif/endwhile/... se normalizan a `end`
                    // para que el parser MATLAB existente los reconozca sin cambios.
                    if (octave && _octaveBlockEnders.Contains(name))
                        name = "end";
                    tokens.Add(new MatlabToken(MatlabTokenKind.Identifier, name, line, startCol));
                    col += j - i;
                    i = j;
                    prevIsOperand = true;
                    prevWasWhitespace = false;
                    continue;
                }
                // Operadores y puntuación
                int saveCol = col;
                MatlabTokenKind kind;
                int len = 1;
                string forcedText = null;   // texto canónico (p.ej. Octave `!=` → `~=`)
                switch (c)
                {
                    case '+':
                    case '-':
                        // Octave: asignación compuesta (+=, -=) e incremento/decremento (++, --)
                        if (octave && i + 1 < n && source[i + 1] == '=')
                        { kind = (c == '+') ? MatlabTokenKind.PlusAssign : MatlabTokenKind.MinusAssign; len = 2; prevIsOperand = false; break; }
                        if (octave && i + 1 < n && source[i + 1] == c)
                        { kind = (c == '+') ? MatlabTokenKind.Increment : MatlabTokenKind.Decrement; len = 2; prevIsOperand = false; break; }
                        // Regla MATLAB del espacio-menos: dentro de [ ] o { }, si el +/-
                        // tiene espacio ANTES y NO despues, y hay operando previo,
                        // separa un nuevo elemento (unario) -> insertamos coma virtual.
                        if (brackets.Count > 0 && (brackets.Peek() == '[' || brackets.Peek() == '{')
                            && prevWasWhitespace && prevIsOperand
                            && i + 1 < n && source[i + 1] != ' ' && source[i + 1] != '\t'
                            && source[i + 1] != '\n' && source[i + 1] != '\r')
                        {
                            tokens.Add(new MatlabToken(MatlabTokenKind.Comma, ",", line, saveCol));
                        }
                        kind = (c == '+') ? MatlabTokenKind.Plus : MatlabTokenKind.Minus;
                        prevIsOperand = false;
                        break;
                    case '*':
                        if (octave && i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.StarAssign; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Star; prevIsOperand = false; break;
                    case '/':
                        if (octave && i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.SlashAssign; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Slash; prevIsOperand = false; break;
                    case '\\': kind = MatlabTokenKind.Backslash; prevIsOperand = false; break;
                    case '^': kind = MatlabTokenKind.Caret; prevIsOperand = false; break;
                    case '(':
                        // Dentro de [ ] o { }: 'a (b)' (espacio antes del parentesis,
                        // tras operando) = DOS elementos [a, (b)], no la llamada a(b).
                        if (brackets.Count > 0 && (brackets.Peek() == '[' || brackets.Peek() == '{')
                            && prevWasWhitespace && prevIsOperand)
                            tokens.Add(new MatlabToken(MatlabTokenKind.Comma, ",", line, saveCol));
                        brackets.Push('('); kind = MatlabTokenKind.LParen; prevIsOperand = false; break;
                    case ')': if (brackets.Count > 0) brackets.Pop(); kind = MatlabTokenKind.RParen; prevIsOperand = true; break;
                    case '[': brackets.Push('['); kind = MatlabTokenKind.LBracket; prevIsOperand = false; break;
                    case ']': if (brackets.Count > 0) brackets.Pop(); kind = MatlabTokenKind.RBracket; prevIsOperand = true; break;
                    case '{': brackets.Push('{'); kind = MatlabTokenKind.LBrace; prevIsOperand = false; break;
                    case '}': if (brackets.Count > 0) brackets.Pop(); kind = MatlabTokenKind.RBrace; prevIsOperand = true; break;
                    case ',': kind = MatlabTokenKind.Comma; prevIsOperand = false; break;
                    case ';': kind = MatlabTokenKind.Semicolon; prevIsOperand = false; break;
                    case ':': kind = MatlabTokenKind.Colon; prevIsOperand = false; break;
                    case '@': kind = MatlabTokenKind.At; prevIsOperand = false; break;
                    case '.':
                        if (i + 1 < n)
                        {
                            char nx = source[i + 1];
                            if (nx == '*') { kind = MatlabTokenKind.DotStar; len = 2; prevIsOperand = false; break; }
                            if (nx == '/') { kind = MatlabTokenKind.DotSlash; len = 2; prevIsOperand = false; break; }
                            if (nx == '\\') { kind = MatlabTokenKind.DotBackslash; len = 2; prevIsOperand = false; break; }
                            if (nx == '^') { kind = MatlabTokenKind.DotCaret; len = 2; prevIsOperand = false; break; }
                            if (nx == '\'') { kind = MatlabTokenKind.DotTranspose; len = 2; prevIsOperand = true; break; }
                        }
                        kind = MatlabTokenKind.Dot; prevIsOperand = false; break;
                    case '=':
                        if (i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.Equal; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Assign; prevIsOperand = false; break;
                    case '~':
                        if (i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.NotEqual; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Not; prevIsOperand = false; break;
                    case '<':
                        if (i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.LessEq; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Less; prevIsOperand = false; break;
                    case '>':
                        if (i + 1 < n && source[i + 1] == '=')
                        { kind = MatlabTokenKind.GreaterEq; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.Greater; prevIsOperand = false; break;
                    case '&':
                        if (i + 1 < n && source[i + 1] == '&')
                        { kind = MatlabTokenKind.AndShort; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.AndBit; prevIsOperand = false; break;
                    case '|':
                        if (i + 1 < n && source[i + 1] == '|')
                        { kind = MatlabTokenKind.OrShort; len = 2; prevIsOperand = false; break; }
                        kind = MatlabTokenKind.OrBit; prevIsOperand = false; break;
                    case '!':
                        // Octave: `!` = negación lógica (alias de ~), `!=` = distinto (alias de ~=).
                        // Se canoniza el texto a `~`/`~=` para que el resto del motor lo trate igual.
                        if (octave)
                        {
                            if (i + 1 < n && source[i + 1] == '=')
                            { kind = MatlabTokenKind.NotEqual; len = 2; forcedText = "~="; prevIsOperand = false; break; }
                            kind = MatlabTokenKind.Not; forcedText = "~"; prevIsOperand = false; break;
                        }
                        goto default;
                    default:
                        throw new MatlabParseException($"Unexpected character '{c}'", line, col);
                }
                tokens.Add(new MatlabToken(kind, forcedText ?? source.Substring(i, len), line, saveCol));
                i += len;
                col += len;
                prevWasWhitespace = false;
            }
            tokens.Add(new MatlabToken(MatlabTokenKind.EndOfFile, "", line, col));
            return tokens;
        }
    }

    public class MatlabParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public MatlabParseException(string message, int line, int col)
            : base($"{message} (line {line}, col {col})")
        {
            Line = line; Column = col;
        }
    }
}
