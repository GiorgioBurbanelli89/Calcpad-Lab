using System;
using System.Collections.Generic;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        private sealed class Token
        {
            internal string Value { get; set; }
            internal TokenTypes Type;
            internal int CacheID = -1;
            internal Token(string value, TokenTypes type)
            {
                Value = value;
                Type = type;
            }
            public override string ToString() => Value;
        }

        private enum TokenTypes
        {
            Expression,
            Heading,
            Text,
            Html,
            Error
        }

        private List<Token> GetTokens(ReadOnlySpan<char> s)
        {
            var tokens = new List<Token>();

            // ── MATLAB-style '%' comments (Calcpad Lab es MATLAB-only) ─────
            {
                int lead = 0;
                while (lead < s.Length && (s[lead] == ' ' || s[lead] == '\t')) lead++;
                if (lead < s.Length && s[lead] == '%')
                {
                    // Toda la línea es comment
                    int cs = lead + 1;
                    if (cs < s.Length && s[cs] == ' ') cs++; // skip un espacio leading
                    var commentText = cs < s.Length ? s[cs..].ToString() : "";
                    tokens.Add(new Token(commentText, TokenTypes.Text));
                    return tokens;
                }
                // Buscar '%' inline fuera de strings (separa expr de comment)
                bool insideSq = false, insideDq = false;
                int hashIdx = -1;
                for (int i = 0; i < s.Length; i++)
                {
                    var c = s[i];
                    if (c == '\'' && !insideDq) insideSq = !insideSq;
                    else if (c == '\"' && !insideSq) insideDq = !insideDq;
                    else if (c == '%' && !insideSq && !insideDq)
                    {
                        hashIdx = i;
                        break;
                    }
                }
                if (hashIdx > 0)
                {
                    // Hay expr antes del '%' — tokenize la expr y agrega el comment después
                    var exprSpan = s[..hashIdx].TrimEnd();
                    var commentStart = hashIdx + 1;
                    if (commentStart < s.Length && s[commentStart] == ' ') commentStart++;
                    var comment = commentStart < s.Length ? s[commentStart..].ToString() : "";
                    // Recursivamente tokenizar la expresión (sin el '%...')
                    var subTokens = GetTokens(exprSpan);
                    tokens.AddRange(subTokens);
                    if (!string.IsNullOrWhiteSpace(comment))
                        tokens.Add(new Token(comment, TokenTypes.Text));
                    return tokens;
                }
            }

            var ts = new TextSpan(s);
            var currentSeparator = ' ';
            for (int i = 0, len = s.Length; i < len; ++i)
            {
                var c = s[i];
                if (c == '\'' || c == '\"')
                {
                    var i1 = i + 1;
                    var isPair = i1 < len && s[i1] == c;
                    if (currentSeparator == c)
                    {
                        // INSIDE a c-region (text). `''` is a literal escaped quote
                        // — emit both chars into the text content, don't toggle.
                        if (isPair)
                        {
                            ts.Expand();
                            ts.Expand();
                            i = i1;
                            continue;
                        }
                        // Single `'` → close text, switch to expression.
                        if (!ts.IsEmpty)
                            AddToken(tokens, ts.Cut(), currentSeparator);
                        ts.Reset(i + 1);
                        currentSeparator = ' ';
                    }
                    else if (currentSeparator == ' ')
                    {
                        // OUTSIDE any region (default expression mode). A normal
                        // single `'` toggles to text mode. But `''` here is
                        // typically a typo where the user wrote two quotes meaning
                        // "close expression and re-enter text mode". So we skip
                        // both chars and ENTER text mode for the content that
                        // follows. Net behaviour: `'expr''text'` ≡ `'expr' 'text'`.
                        if (!ts.IsEmpty)
                            AddToken(tokens, ts.Cut(), currentSeparator);
                        ts.Reset(isPair ? i + 2 : i + 1);
                        currentSeparator = c;
                        if (isPair) i = i1;
                    }
                    else
                    {
                        // Inside a different quote-type region — emit as literal.
                        ts.Expand();
                    }
                }
                else
                    ts.Expand();
            }
            if (!ts.IsEmpty)
                AddToken(tokens, ts.Cut(), currentSeparator);

            return tokens;
        }

        private void AddToken(List<Token> tokens, ReadOnlySpan<char> value, char separator)
        {
            var tokenValue = value.ToString().Replace("\"\"", "&quot;").Replace("''", "&apos;");
            var tokenType = GetTokenType(separator);
            if (tokenType == TokenTypes.Expression)
            {
                if (value.IsWhiteSpace())
                    return;
            }
            else if (_isVal < 1)
            {
                if (tokens.Count == 0)
                    tokenValue += " ";
                else
                    tokenValue = string.Concat(" ", tokenValue," ");
            }

            var token = new Token(tokenValue, tokenType);
            if (token.Type == TokenTypes.Text)
            {
                tokenValue = tokenValue.TrimStart();
                if (tokenValue.Length > 0 && tokenValue[0] == '<')
                    token.Type = TokenTypes.Html;
            }
            tokens.Add(token);
        }

        private static TokenTypes GetTokenType(char separator)
        {
            return separator switch
            {
                ' ' => TokenTypes.Expression,
                '\"' => TokenTypes.Heading,
                '\'' => TokenTypes.Text,
                _ => TokenTypes.Error,
            };
        }

        /// <summary>
        /// Post-process heading tokens: if a Heading contains single quotes,
        /// split into Heading + Expression + Heading sub-tokens so inline
        /// expressions like '#deq expr' work inside titles.
        /// </summary>
        private static void SplitHeadingExpressions(List<Token> tokens)
        {
            for (int t = 0; t < tokens.Count; t++)
            {
                if (tokens[t].Type != TokenTypes.Heading) continue;
                var val = tokens[t].Value;
                if (!val.Contains('\'')) continue;

                // Split by single quotes
                var parts = new List<Token>();
                int i = 0;
                bool inExpr = false;
                int start = 0;
                while (i < val.Length)
                {
                    if (val[i] == '\'')
                    {
                        if (i > start)
                        {
                            var chunk = val[start..i];
                            parts.Add(new Token(inExpr ? chunk : " " + chunk + " ",
                                inExpr ? TokenTypes.Expression : TokenTypes.Heading));
                        }
                        inExpr = !inExpr;
                        start = i + 1;
                    }
                    i++;
                }
                if (start < val.Length)
                {
                    var chunk = val[start..];
                    parts.Add(new Token(inExpr ? chunk : " " + chunk + " ",
                        inExpr ? TokenTypes.Expression : TokenTypes.Heading));
                }

                if (parts.Count > 1)
                {
                    tokens.RemoveAt(t);
                    tokens.InsertRange(t, parts);
                }
            }
        }
    }
}