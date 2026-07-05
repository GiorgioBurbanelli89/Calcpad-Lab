using System;
using System.Runtime.InteropServices;

namespace Calcpad.Core.Matlab
{
    // Puente a giac (CAS C++ de Xcas, GPL) por P/Invoke IN-PROCESS — el mismo
    // patrón con el que MATLAB llama a MuPAD: MATLAB hace
    //     mupadmex('symobj::diff', expr, x, n)   → mupkernel.dll
    // y aquí Calcpad Lab hace
    //     GiacRunner.Eval("diff(expr, x, n)")     → giac.dll
    // Interfaz por STRINGS: se serializa el SymNode con ToInfix(), se manda el
    // comando a giac, y el string resultado se re-parsea a SymNode. giac.dll
    // (self-contained, gmp/mpfr estáticos) viaja junto al .exe.
    internal static class GiacRunner
    {
        private const string GIAC = "giac";
        // giac::caseval(const char*) — mangling MinGW/Itanium. Comando Xcas → resultado.
        [DllImport(GIAC, EntryPoint = "_ZN4giac7casevalEPKc",
                   CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr caseval(string s);

        private static readonly object _lock = new object();   // giac no es thread-safe
        private static int _available = -1;

        internal static bool IsAvailable()
        {
            if (_available >= 0) return _available == 1;
            lock (_lock)
            {
                if (_available >= 0) return _available == 1;
                try
                {
                    var p = caseval("1+1");
                    var s = Marshal.PtrToStringAnsi(p);
                    _available = (s != null && s.Trim() == "2") ? 1 : 0;
                }
                catch { _available = 0; }
                return _available == 1;
            }
        }

        // Ejecuta un comando Xcas y devuelve (ok, string). Como mupadmex('symobj::…').
        internal static (bool ok, string output) Eval(string command)
        {
            if (!IsAvailable()) return (false, "giac not available");
            try
            {
                string raw;
                lock (_lock)
                {
                    var p = caseval(command);
                    raw = Marshal.PtrToStringAnsi(p);
                }
                if (string.IsNullOrWhiteSpace(raw)) return (false, "giac empty");
                var r = raw.Trim();
                if (r.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Unable to", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
                    return (false, r);
                return (true, r);
            }
            catch (Exception ex) { return (false, "giac: " + ex.Message); }
        }

        // Convierte la sintaxis de salida de giac a la de Calcpad Lab (MATLAB):
        //  ln( → log(  (giac usa ln para log natural; Lab/MATLAB usa log)
        //  quita espacios; deja ^, *, +, - tal cual (compatibles).
        internal static string ToMatlab(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\bln\s*\(", "log(");
            return s.Trim();
        }

        // ---- Parser string→SymNode: la otra mitad del puente (deserializa la
        // respuesta de giac a la representación simbólica de Lab). Es lo que hace
        // MATLAB al recibir el resultado de MuPAD. Recursivo descendente:
        //   expr := term (('+'|'-') term)*   term := factor (('*'|'/') factor)*
        //   factor := base ('^' factor)?     base := num|var|func(expr)|(expr)|-base
        internal static SymNode ParseToSym(string s)
        {
            int pos = 0;
            var node = ParseExpr(s, ref pos);
            SkipWs(s, ref pos);
            if (pos < s.Length) throw new System.Exception("giac parse: sobra '" + s.Substring(pos) + "'");
            return node;
        }
        private static void SkipWs(string s, ref int pos) { while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++; }
        private static SymNode ParseExpr(string s, ref int pos)
        {
            var left = ParseTerm(s, ref pos);
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == '+') { pos++; left = new SymAdd(left, ParseTerm(s, ref pos)); }
                else if (pos < s.Length && s[pos] == '-') { pos++; left = new SymSub(left, ParseTerm(s, ref pos)); }
                else break;
            }
            return left;
        }
        private static SymNode ParseTerm(string s, ref int pos)
        {
            var left = ParseFactor(s, ref pos);
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == '*') { pos++; left = new SymMul(left, ParseFactor(s, ref pos)); }
                else if (pos < s.Length && s[pos] == '/') { pos++; left = new SymDiv(left, ParseFactor(s, ref pos)); }
                else break;
            }
            return left;
        }
        private static SymNode ParseFactor(string s, ref int pos)
        {
            var b = ParseBase(s, ref pos);
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '^') { pos++; return new SymPow(b, ParseFactor(s, ref pos)); }
            return b;
        }
        private static SymNode ParseBase(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '-') { pos++; return new SymSub(new SymConst(0), ParseBase(s, ref pos)); }
            if (pos < s.Length && s[pos] == '+') { pos++; return ParseBase(s, ref pos); }
            if (pos < s.Length && s[pos] == '(') { pos++; var e = ParseExpr(s, ref pos); SkipWs(s, ref pos); if (pos < s.Length && s[pos] == ')') pos++; return e; }
            if (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
            {
                int start = pos;
                while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
                return new SymConst(double.Parse(s.Substring(start, pos - start), System.Globalization.CultureInfo.InvariantCulture));
            }
            if (pos < s.Length && (char.IsLetter(s[pos]) || s[pos] == '_'))
            {
                int start = pos;
                while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_')) pos++;
                string name = s.Substring(start, pos - start);
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == '(')
                {
                    pos++; var arg = ParseExpr(s, ref pos); SkipWs(s, ref pos); if (pos < s.Length && s[pos] == ')') pos++;
                    return new SymFunc(name, arg);
                }
                if (name == "pi") return new SymConst(System.Math.PI);
                return new SymVar(name);
            }
            throw new System.Exception("giac parse: caracter inesperado en pos " + pos + " de '" + s + "'");
        }
    }
}
