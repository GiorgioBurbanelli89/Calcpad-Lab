#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core.Fortran;

public enum TokKind { Number, Str, Ident, Op, Eol, Eof }

public sealed record Token(TokKind Kind, string Text, double Num, int Line)
{
    public bool Is(string s) => Kind == TokKind.Op && Text == s;
    public bool IsWord(string w) => Kind == TokKind.Ident && string.Equals(Text, w, StringComparison.OrdinalIgnoreCase);
    public override string ToString() => Kind == TokKind.Eol ? "<fin de linea>" : Text;
}

public sealed class FortranError : Exception
{
    public int Line { get; }
    public FortranError(string message, int line) : base(message) => Line = line;
}

/// <summary>
/// Turns Fortran source into tokens. Fortran is case-insensitive, uses '!' for
/// comments and '&' to continue a statement on the next line.
/// </summary>
public sealed class Lexer
{
    private static readonly string[] DottedOps =
    {
        ".and.", ".or.", ".not.", ".eqv.", ".neqv.",
        ".eq.", ".ne.", ".lt.", ".le.", ".gt.", ".ge.",
        ".true.", ".false."
    };

    private readonly string _src;
    private int _i;
    private int _line = 1;

    public Lexer(string source) => _src = source.Replace("\r\n", "\n").Replace('\r', '\n');

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipSpacesAndComments();
            if (_i >= _src.Length) break;

            var c = _src[_i];

            if (c == '\n')
            {
                _i++;
                // Collapse repeated blank lines into a single statement separator.
                if (tokens.Count > 0 && tokens[^1].Kind != TokKind.Eol)
                    tokens.Add(new Token(TokKind.Eol, "\\n", 0, _line));
                _line++;
                continue;
            }

            if (c == ';')
            {
                _i++;
                tokens.Add(new Token(TokKind.Eol, ";", 0, _line));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && _i + 1 < _src.Length && char.IsDigit(_src[_i + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (c == '\'' || c == '"')
            {
                tokens.Add(ReadString(c));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            if (c == '.')
            {
                var dotted = ReadDottedOperator();
                if (dotted is not null) { tokens.Add(dotted); continue; }
            }

            tokens.Add(ReadOperator());
        }

        tokens.Add(new Token(TokKind.Eol, "\\n", 0, _line));
        tokens.Add(new Token(TokKind.Eof, "", 0, _line));
        return tokens;
    }

    private void SkipSpacesAndComments()
    {
        while (_i < _src.Length)
        {
            var c = _src[_i];
            if (c == ' ' || c == '\t') { _i++; continue; }

            if (c == '!')                       // comment to end of line
            {
                while (_i < _src.Length && _src[_i] != '\n') _i++;
                continue;
            }

            if (c == '&')                       // continuation: swallow the newline
            {
                var j = _i + 1;
                while (j < _src.Length && (_src[j] == ' ' || _src[j] == '\t')) j++;
                if (j < _src.Length && _src[j] == '!')
                    while (j < _src.Length && _src[j] != '\n') j++;
                if (j < _src.Length && _src[j] == '\n')
                {
                    _i = j + 1;
                    _line++;
                    // a leading '&' on the continued line is optional
                    while (_i < _src.Length && (_src[_i] == ' ' || _src[_i] == '\t')) _i++;
                    if (_i < _src.Length && _src[_i] == '&') _i++;
                    continue;
                }
            }
            break;
        }
    }

    private Token ReadNumber()
    {
        var start = _i;
        while (_i < _src.Length && char.IsDigit(_src[_i])) _i++;
        if (_i < _src.Length && _src[_i] == '.' && !StartsDottedOperator(_i))
        {
            _i++;
            while (_i < _src.Length && char.IsDigit(_src[_i])) _i++;
        }
        // exponent: 1.0e-3, 1.0d0 (double precision marker)
        if (_i < _src.Length && (_src[_i] is 'e' or 'E' or 'd' or 'D'))
        {
            var save = _i;
            _i++;
            if (_i < _src.Length && (_src[_i] == '+' || _src[_i] == '-')) _i++;
            if (_i < _src.Length && char.IsDigit(_src[_i]))
                while (_i < _src.Length && char.IsDigit(_src[_i])) _i++;
            else _i = save;
        }
        var text = _src[start.._i].Replace("d", "e").Replace("D", "e");
        // ignore a kind suffix such as 1.0_8
        if (_i < _src.Length && _src[_i] == '_')
        {
            _i++;
            while (_i < _src.Length && (char.IsLetterOrDigit(_src[_i]) || _src[_i] == '_')) _i++;
        }
        return new Token(TokKind.Number, text, double.Parse(text, CultureInfo.InvariantCulture), _line);
    }

    private bool StartsDottedOperator(int at)
    {
        foreach (var op in DottedOps)
            if (at + op.Length <= _src.Length &&
                _src.Substring(at, op.Length).Equals(op, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private Token ReadString(char quote)
    {
        _i++;                                    // opening quote
        var sb = new StringBuilder();
        while (_i < _src.Length)
        {
            var c = _src[_i];
            if (c == quote)
            {
                if (_i + 1 < _src.Length && _src[_i + 1] == quote) { sb.Append(quote); _i += 2; continue; }
                _i++;
                return new Token(TokKind.Str, sb.ToString(), 0, _line);
            }
            if (c == '\n') throw new FortranError("cadena de texto sin cerrar", _line);
            sb.Append(c);
            _i++;
        }
        throw new FortranError("cadena de texto sin cerrar", _line);
    }

    private Token ReadIdentifier()
    {
        var start = _i;
        while (_i < _src.Length && (char.IsLetterOrDigit(_src[_i]) || _src[_i] == '_')) _i++;
        return new Token(TokKind.Ident, _src[start.._i], 0, _line);
    }

    private Token? ReadDottedOperator()
    {
        foreach (var op in DottedOps)
        {
            if (_i + op.Length <= _src.Length &&
                _src.Substring(_i, op.Length).Equals(op, StringComparison.OrdinalIgnoreCase))
            {
                _i += op.Length;
                return new Token(TokKind.Op, op.ToLowerInvariant(), 0, _line);
            }
        }
        return null;
    }

    private Token ReadOperator()
    {
        // longest match first
        string[] twoChar = { "**", "==", "/=", "<=", ">=", "::", "//" };
        foreach (var op in twoChar)
        {
            if (_i + 2 <= _src.Length && _src.Substring(_i, 2) == op)
            {
                _i += 2;
                return new Token(TokKind.Op, op, 0, _line);
            }
        }
        var c = _src[_i];

        // El editor de Calcpad tipografia los operadores al cargar el archivo
        // (<= se vuelve ≤, >= se vuelve ≥, /= se vuelve ≠). Aceptamos ambas formas.
        switch (c)
        {
            case '≤': _i++; return new Token(TokKind.Op, "<=", 0, _line);   // ≤
            case '≥': _i++; return new Token(TokKind.Op, ">=", 0, _line);   // ≥
            case '≠': _i++; return new Token(TokKind.Op, "/=", 0, _line);   // ≠
            case '·': _i++; return new Token(TokKind.Op, "*", 0, _line);    // ·
            case '×': _i++; return new Token(TokKind.Op, "*", 0, _line);    // ×
            case '÷': _i++; return new Token(TokKind.Op, "/", 0, _line);    // ÷
            case '−': _i++; return new Token(TokKind.Op, "-", 0, _line);    // − (menos tipografico)
        }

        if ("+-*/()=<>,:%".IndexOf(c) < 0)
            throw new FortranError($"caracter no valido '{c}'", _line);
        _i++;
        return new Token(TokKind.Op, c.ToString(), 0, _line);
    }
}
