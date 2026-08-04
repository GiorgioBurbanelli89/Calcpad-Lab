using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Transpila la notación compacta de Calcpad <c>$Op{ cuerpo @ var = a : b }</c>
    /// a MATLAB puro. Hekatan Lab es MATLAB, pero esta notación es un atajo de
    /// entrada cómodo (sobre todo para integrales de matrices en FEM). Al detectar
    /// un <c>$Op{}</c> balanceado se reescribe el texto del script; el .m guardado
    /// queda MATLAB puro.
    ///
    /// Ejemplo clave (rigidez FEM, integral doble de una MATRIZ):
    ///   K_e = $Area{$Area{Bᵀ*D*B*t @ ξ = -1 : 1} @ η = -1 : 1}
    /// →  K_e = gaussint(@(η) (gaussint(@(ξ) (Bᵀ*D*B*t), -1, 1)), -1, 1)
    ///
    /// $Area/$Integral usan <c>gaussint</c> (cuadratura de Gauss-Legendre), que a
    /// diferencia de integral() es SEGURA CON MATRICES.
    /// </summary>
    public static class DollarTranspiler
    {
        // Operadores $ que SÍ transpilamos (matemáticos). Los gráficos
        // ($Plot, $Map, $Chart, $Draw, $Fem2D, $Frame, $Struct, $Table, …) se
        // dejan intactos: los renderiza el pipeline, no son valores MATLAB.
        private static readonly HashSet<string> MathOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "Sum", "Product", "Area", "Integral", "Root", "Find",
            "Slope", "Derivative", "Sup", "Inf"
        };

        /// <summary>¿La cadena contiene algún <c>$Op{</c> matemático balanceado?</summary>
        public static bool ContainsMathOp(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (s[i] != '$' || !char.IsLetter(s[i + 1])) continue;
                int j = i + 1;
                while (j < s.Length && char.IsLetter(s[j])) j++;
                if (j < s.Length && s[j] == '{' &&
                    MathOps.Contains(s.Substring(i + 1, j - (i + 1))) &&
                    FindMatchingBrace(s, j) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>Reescribe todos los <c>$Op{}</c> matemáticos de la cadena a MATLAB.
        /// Idempotente y recursivo (soporta anidados). El texto que no es un $Op
        /// se copia tal cual.</summary>
        public static string Transpile(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length + 16);
            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (c == '$' && i + 1 < input.Length && char.IsLetter(input[i + 1]))
                {
                    int j = i + 1;
                    while (j < input.Length && char.IsLetter(input[j])) j++;
                    if (j < input.Length && input[j] == '{')
                    {
                        string op = input.Substring(i + 1, j - (i + 1));
                        int close = FindMatchingBrace(input, j);
                        if (close > 0 && MathOps.Contains(op))
                        {
                            string inner = input.Substring(j + 1, close - j - 1);
                            sb.Append(EmitOp(op, inner));
                            i = close + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // ── Emisión de un operador concreto ────────────────────────────────
        private static string EmitOp(string op, string inner)
        {
            // Separar "cuerpo @ rango" por el @ de NIVEL SUPERIOR (los @ de los
            // $Op anidados están dentro de sus llaves, a profundidad > 0).
            int at = IndexOfTopLevel(inner, '@');
            string bodyRaw = at >= 0 ? inner.Substring(0, at) : inner;
            string rangeRaw = at >= 0 ? inner.Substring(at + 1) : "";

            string body = Transpile(bodyRaw).Trim();
            string var = "x", lo = "", hi = "", step = "", at0 = "";

            if (rangeRaw.Length > 0)
            {
                int eq = IndexOfTopLevel(rangeRaw, '=');
                if (eq >= 0)
                {
                    var = rangeRaw.Substring(0, eq).Trim();
                    var rhs = rangeRaw.Substring(eq + 1);
                    var parts = SplitTopLevel(rhs, ':');
                    if (parts.Count == 1) { at0 = Transpile(parts[0]).Trim(); }
                    else if (parts.Count == 2) { lo = Transpile(parts[0]).Trim(); hi = Transpile(parts[1]).Trim(); }
                    else if (parts.Count >= 3) { lo = Transpile(parts[0]).Trim(); step = Transpile(parts[1]).Trim(); hi = Transpile(parts[2]).Trim(); }
                }
            }

            string range = step.Length > 0 ? $"{lo}:{step}:{hi}" : $"{lo}:{hi}";
            string lam = $"@({var}) ({body})";

            switch (op.ToLowerInvariant())
            {
                case "sum":       return $"sum(arrayfun({lam}, {range}))";
                case "product":   return $"prod(arrayfun({lam}, {range}))";
                case "sup":       return $"max(arrayfun({lam}, {range}))";
                case "inf":       return $"min(arrayfun({lam}, {range}))";
                case "area":
                case "integral":  return $"gaussint({lam}, {lo}, {hi})";
                case "root":
                case "find":
                {
                    // $Root{f = c @ x=a:b}  o  $Root{f @ x=a:b}
                    int beq = IndexOfTopLevel(bodyRaw, '=');
                    if (beq >= 0)
                    {
                        string lhs = Transpile(bodyRaw.Substring(0, beq)).Trim();
                        string rhs = Transpile(bodyRaw.Substring(beq + 1)).Trim();
                        return $"fzero(@({var}) (({lhs})-({rhs})), [{lo} {hi}])";
                    }
                    return $"fzero({lam}, [{lo} {hi}])";
                }
                case "slope":
                case "derivative":
                    // Derivada numérica centrada en el punto at0
                    return $"((feval({lam},({at0})+1e-6))-(feval({lam},({at0})-1e-6)))/2e-6";
                default:
                    return $"${op}{{{inner}}}"; // no debería ocurrir
            }
        }

        // ── Utilidades de parseo con conteo de profundidad ─────────────────
        private static int FindMatchingBrace(string s, int openIdx)
        {
            int depth = 0;
            for (int i = openIdx; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static int IndexOfTopLevel(string s, char target)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']') depth--;
                else if (c == target && depth == 0) return i;
            }
            return -1;
        }

        private static List<string> SplitTopLevel(string s, char sep)
        {
            var res = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']') depth--;
                else if (c == sep && depth == 0) { res.Add(s.Substring(start, i - start)); start = i + 1; }
            }
            res.Add(s.Substring(start));
            return res;
        }
    }
}
