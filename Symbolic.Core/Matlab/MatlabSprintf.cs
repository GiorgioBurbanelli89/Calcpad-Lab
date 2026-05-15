// =============================================================================
// Calcpad Lab — MATLAB sprintf / fprintf format implementation
// =============================================================================
//   Soporta los specifiers MATLAB más comunes:
//     %d  %i      → integer
//     %f          → fixed-point (default 6 decimals)
//     %.3f        → precision
//     %e %E       → scientific
//     %g %G       → shortest
//     %s          → string
//     %c          → char
//     %x %X       → hex
//     %o          → octal
//     %%          → literal %
//     \n \t \\    → escapes
//
//   MATLAB reuse args: si más datos que placeholders, se recorre la cadena
//   de formato múltiples veces hasta agotar inputs (vector args).
// =============================================================================
using System;
using System.Globalization;
using System.Text;

namespace Calcpad.Core.Matlab
{
    internal static class MatlabSprintf
    {
        public static string Format(string fmt, MValue[] args)
        {
            // Expandir args a una secuencia de valores escalares/strings.
            var flat = new System.Collections.Generic.List<object>();
            foreach (var a in args)
            {
                if (a == null) { flat.Add(""); continue; }
                if (a.IsString) flat.Add(a.StringValue);
                else if (a.IsScalar) flat.Add(a.Scalar);
                else
                {
                    // Matrices se aplanan column-major (MATLAB convention)
                    for (int j = 0; j < a.Cols; j++)
                        for (int i = 0; i < a.Rows; i++)
                            flat.Add(a.At(i, j));
                }
            }

            var sb = new StringBuilder();
            int argIdx = 0;
            int totalPlaceholders = CountPlaceholders(fmt);
            // MATLAB recicla format string mientras queden args.
            while (true)
            {
                int placeholdersUsedThisRound = 0;
                int i = 0;
                while (i < fmt.Length)
                {
                    char c = fmt[i];
                    if (c == '\\' && i + 1 < fmt.Length)
                    {
                        char nx = fmt[i + 1];
                        switch (nx)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case '\\': sb.Append('\\'); break;
                            case '\'': sb.Append('\''); break;
                            case '"': sb.Append('"'); break;
                            default: sb.Append(nx); break;
                        }
                        i += 2; continue;
                    }
                    if (c != '%') { sb.Append(c); i++; continue; }
                    if (i + 1 < fmt.Length && fmt[i + 1] == '%')
                    { sb.Append('%'); i += 2; continue; }

                    // Parse spec: %[flags][width][.precision]specifier
                    int specStart = i;
                    i++;
                    while (i < fmt.Length && "+-# 0".IndexOf(fmt[i]) >= 0) i++;
                    while (i < fmt.Length && char.IsDigit(fmt[i])) i++;
                    if (i < fmt.Length && fmt[i] == '.')
                    { i++; while (i < fmt.Length && char.IsDigit(fmt[i])) i++; }
                    if (i >= fmt.Length) { sb.Append(fmt[specStart..]); break; }
                    char spec = fmt[i++];
                    string fullSpec = fmt[specStart..i];
                    if (argIdx >= flat.Count) { sb.Append(fullSpec); continue; }
                    object val = flat[argIdx++];
                    placeholdersUsedThisRound++;
                    sb.Append(FormatOne(fullSpec, spec, val));
                }
                if (argIdx >= flat.Count) break;
                if (placeholdersUsedThisRound == 0) break;
            }
            return sb.ToString();
        }

        private static int CountPlaceholders(string fmt)
        {
            int count = 0;
            for (int i = 0; i < fmt.Length; i++)
            {
                if (fmt[i] != '%') continue;
                if (i + 1 < fmt.Length && fmt[i + 1] == '%') { i++; continue; }
                count++;
            }
            return count;
        }

        private static string FormatOne(string fullSpec, char spec, object val)
        {
            // Convert fullSpec (`%5.2f`, `%-10s`, `%d`, etc.) to .NET format.
            // Approach simple: parse flags/width/precision manually and apply.
            int specPos = fullSpec.Length - 1;
            // Skip the trailing specifier char to extract intermediate.
            string inner = fullSpec[1..specPos];
            // Parse: flags + width.precision
            bool leftAlign = false, plus = false, space = false, zeroPad = false;
            int idx = 0;
            while (idx < inner.Length && "+-# 0".IndexOf(inner[idx]) >= 0)
            {
                if (inner[idx] == '-') leftAlign = true;
                else if (inner[idx] == '+') plus = true;
                else if (inner[idx] == ' ') space = true;
                else if (inner[idx] == '0') zeroPad = true;
                idx++;
            }
            int width = 0;
            while (idx < inner.Length && char.IsDigit(inner[idx]))
            { width = width * 10 + (inner[idx] - '0'); idx++; }
            int precision = -1;
            if (idx < inner.Length && inner[idx] == '.')
            {
                idx++; precision = 0;
                while (idx < inner.Length && char.IsDigit(inner[idx]))
                { precision = precision * 10 + (inner[idx] - '0'); idx++; }
            }
            var inv = CultureInfo.InvariantCulture;
            string s;
            switch (spec)
            {
                case 'd': case 'i':
                    long iv = val is double d ? (long)d : Convert.ToInt64(val);
                    s = (plus && iv >= 0) ? "+" + iv.ToString(inv) : iv.ToString(inv);
                    if (space && iv >= 0 && !plus) s = " " + s;
                    break;
                case 'f': case 'F':
                    double fv = val is double d1 ? d1 : Convert.ToDouble(val);
                    int prec = precision < 0 ? 6 : precision;
                    s = fv.ToString("F" + prec, inv);
                    if (plus && fv >= 0) s = "+" + s;
                    else if (space && fv >= 0) s = " " + s;
                    break;
                case 'e': case 'E':
                    double ev = val is double d2 ? d2 : Convert.ToDouble(val);
                    int eprec = precision < 0 ? 6 : precision;
                    s = ev.ToString("0." + new string('0', eprec) + (spec == 'e' ? "e+00" : "E+00"), inv);
                    break;
                case 'g': case 'G':
                    double gv = val is double d3 ? d3 : Convert.ToDouble(val);
                    int gprec = precision < 0 ? 6 : precision;
                    s = gv.ToString("G" + gprec, inv);
                    break;
                case 's':
                    s = val?.ToString() ?? "";
                    if (precision >= 0 && s.Length > precision) s = s[..precision];
                    break;
                case 'c':
                    s = (val is double dc) ? ((char)(int)dc).ToString() : (val?.ToString() ?? "");
                    break;
                case 'x':
                    s = ((long)Convert.ToDouble(val)).ToString("x", inv);
                    break;
                case 'X':
                    s = ((long)Convert.ToDouble(val)).ToString("X", inv);
                    break;
                case 'o':
                    s = Convert.ToString((long)Convert.ToDouble(val), 8);
                    break;
                default:
                    s = val?.ToString() ?? "";
                    break;
            }
            if (width > 0 && s.Length < width)
            {
                if (leftAlign) s = s.PadRight(width);
                else if (zeroPad && (spec == 'd' || spec == 'i' || spec == 'f' || spec == 'e'))
                {
                    if (s.StartsWith("-")) s = "-" + s[1..].PadLeft(width - 1, '0');
                    else s = s.PadLeft(width, '0');
                }
                else s = s.PadLeft(width);
            }
            return s;
        }
    }
}
