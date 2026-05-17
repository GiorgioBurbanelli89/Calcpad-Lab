// =============================================================================
// Calcpad Lab — MATLAB Symbolic Computation (MVP)
// =============================================================================
//   Soporta:
//     syms x y           — declara variables simbólicas
//     diff(expr, x)      — derivada simbólica respecto a x
//     int(expr, x)       — antiderivada (solo polinomios MVP)
//     expand(expr)       — expansión polinómica simple
//     simplify(expr)     — simplificación básica
//     subs(expr, x, val) — sustitución
//
//   Representación: árbol de expresiones simbólicas SymNode (separado del AST
//   del parser). Cada operación devuelve un nuevo SymNode.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calcpad.Core.Matlab
{
    public abstract class SymNode
    {
        public abstract SymNode Diff(string var);
        public abstract double Eval(Dictionary<string, double> vals);
        public abstract string ToInfix();
        /// <summary>Render HTML matematico (var/sup/sub/dvc) compatible con Calcpad template.</summary>
        public virtual string ToHtml() => System.Net.WebUtility.HtmlEncode(ToInfix());
        /// <summary>Convierte la expresion a codigo LaTeX (compatible con MATLAB latex()).</summary>
        public virtual string ToLatex() => ToInfix();
        /// <summary>Sustituye <c>var</c> por <c>val</c> en el árbol y devuelve nuevo nodo.</summary>
        public abstract SymNode Subs(string var, SymNode val);
        /// <summary>Simplifica recursivamente (combina constantes, elimina 0/1 neutros).</summary>
        public virtual SymNode Simplify() => this;
        /// <summary>
        /// Expande productos sobre sumas y potencias enteras positivas.
        /// (a+b)*c → a*c + b*c; (a+b)^2 → a^2 + 2*a*b + b^2; etc.
        /// Implementación en "sum-of-products" raw: una lista de términos (suma),
        /// cada término es una lista de factores (producto). Evita usar Simplify()
        /// intermedio (que re-empaqueta x*x → x^2 y rompe el bucle de expand).
        /// </summary>
        public static SymNode Expand(SymNode n)
        {
            var sop = ExpandToSop(n);
            // Reconstruir desde sum-of-products + simplify final (colecta like-terms)
            return SopToNode(sop).Simplify();
        }
        /// <summary>Sum-of-products: cada elemento exterior es un término (sumando);
        /// cada término es una lista de factores multiplicados.</summary>
        private static List<List<SymNode>> ExpandToSop(SymNode n)
        {
            switch (n)
            {
                case SymAdd a:
                {
                    var L = ExpandToSop(a.A);
                    L.AddRange(ExpandToSop(a.B));
                    return L;
                }
                case SymSub s:
                {
                    var L = ExpandToSop(s.A);
                    var R = ExpandToSop(s.B);
                    // Negar cada término de R (multiplicar por -1)
                    foreach (var term in R)
                        term.Insert(0, new SymConst(-1));
                    L.AddRange(R);
                    return L;
                }
                case SymMul m:
                {
                    // Producto cartesiano: cada término de left × cada término de right
                    var Lm = ExpandToSop(m.A);
                    var Rm = ExpandToSop(m.B);
                    var result = new List<List<SymNode>>();
                    foreach (var lt in Lm)
                        foreach (var rt in Rm)
                        {
                            var combined = new List<SymNode>(lt.Count + rt.Count);
                            combined.AddRange(lt);
                            combined.AddRange(rt);
                            result.Add(combined);
                        }
                    return result;
                }
                case SymPow p when p.Exp is SymConst pe && pe.Value > 0
                                    && pe.Value == (int)pe.Value && pe.Value <= 20:
                {
                    int k = (int)pe.Value;
                    var baseSop = ExpandToSop(p.Base);
                    // Producto repetido k veces
                    var acc = new List<List<SymNode>> { new() };  // [[]] = singleton "1"
                    for (int i = 0; i < k; i++)
                    {
                        var next = new List<List<SymNode>>();
                        foreach (var at in acc)
                            foreach (var bt in baseSop)
                            {
                                var combined = new List<SymNode>(at.Count + bt.Count);
                                combined.AddRange(at);
                                combined.AddRange(bt);
                                next.Add(combined);
                            }
                        acc = next;
                    }
                    return acc;
                }
                case SymFunc f:
                {
                    // No expandir adentro de funciones por defecto. Tratar como atom.
                    return new List<List<SymNode>> { new() { f } };
                }
                case SymDiv d:
                {
                    // Expandir numerador, denominador como atom
                    var num = ExpandToSop(d.A);
                    if (num.Count == 1 && num[0].Count == 1)
                        return new List<List<SymNode>> { new() { new SymDiv(num[0][0], d.B) } };
                    // Reconstruir num expandido como atom y dividir
                    var numNode = SopToNode(num);
                    return new List<List<SymNode>> { new() { new SymDiv(numNode, d.B) } };
                }
                default:
                    return new List<List<SymNode>> { new() { n } };
            }
        }
        private static SymNode SopToNode(List<List<SymNode>> sop)
        {
            if (sop.Count == 0) return new SymConst(0);
            // Para cada término, multiplicar factores
            var termNodes = new List<SymNode>();
            foreach (var term in sop)
            {
                if (term.Count == 0) { termNodes.Add(new SymConst(1)); continue; }
                SymNode prod = term[0];
                for (int i = 1; i < term.Count; i++)
                    prod = new SymMul(prod, term[i]);
                termNodes.Add(prod);
            }
            if (termNodes.Count == 1) return termNodes[0];
            SymNode sum = termNodes[0];
            for (int i = 1; i < termNodes.Count; i++) sum = new SymAdd(sum, termNodes[i]);
            return sum;
        }
    }

    public sealed class SymConst : SymNode
    {
        public double Value;
        public SymConst(double v) { Value = v; }
        public override SymNode Diff(string var) => new SymConst(0);
        public override double Eval(Dictionary<string, double> vals) => Value;

        /// <summary>
        /// Detecta si Value es un racional "lindo" (n/d con d <= maxDenom) y
        /// devuelve (n, d) reducido. Esto permite mostrar 0.5 como 1/2, 6.75
        /// como 27/4, 2.666... como 8/3, etc — sin tener Rational interno.
        /// </summary>
        internal static bool TryAsRational(double v, out long n, out long d, int maxDenom = 1000)
        {
            n = 0; d = 1;
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            if (Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) < 1e15)
            { n = (long)Math.Round(v); d = 1; return true; }
            const double eps = 1e-10;
            for (int den = 2; den <= maxDenom; den++)
            {
                double num = v * den;
                long rounded = (long)Math.Round(num);
                if (Math.Abs(num - rounded) < eps)
                {
                    long a = Math.Abs(rounded), b = den, g;
                    while (b != 0) { g = a % b; a = b; b = g; }
                    long gcd = a == 0 ? 1 : a;
                    n = rounded / gcd;
                    d = den / (int)gcd;
                    return true;
                }
            }
            return false;
        }

        public override string ToInfix()
        {
            if (TryAsRational(Value, out var n, out var d))
                return d == 1 ? n.ToString(CultureInfo.InvariantCulture)
                              : $"{n.ToString(CultureInfo.InvariantCulture)}/{d.ToString(CultureInfo.InvariantCulture)}";
            return Value.ToString("G", CultureInfo.InvariantCulture);
        }

        public override string ToHtml()
        {
            if (TryAsRational(Value, out var n, out var d))
            {
                if (d == 1) return n.ToString(CultureInfo.InvariantCulture);
                string sign = "";
                long absN = n;
                if (n < 0) { sign = "-"; absN = -n; }
                return $"{sign}<span class=\"dvc\"><span class=\"dvc-num\">{absN}</span>" +
                       $"<span class=\"dvl\"></span><span class=\"dvc-den\">{d}</span></span>";
            }
            return Value.ToString("G", CultureInfo.InvariantCulture);
        }

        public override string ToLatex()
        {
            if (TryAsRational(Value, out var n, out var d))
                return d == 1 ? n.ToString(CultureInfo.InvariantCulture)
                              : $"\\frac{{{n}}}{{{d}}}";
            return Value.ToString("G", CultureInfo.InvariantCulture);
        }

        public override SymNode Subs(string var, SymNode val) => this;
    }
    public sealed class SymVar : SymNode
    {
        public string Name;
        public SymVar(string n) { Name = n; }
        public override SymNode Diff(string var) => Name == var ? (SymNode)new SymConst(1) : new SymConst(0);
        public override double Eval(Dictionary<string, double> vals) =>
            vals.TryGetValue(Name, out var v) ? v : throw new MatlabRuntimeException($"Symbolic var '{Name}' not bound");
        public override string ToInfix() => Name;
        public override string ToLatex() => Name;

        // Nombres griegos comunes en notación matemática. Mapeo solo se aplica
        // en ToHtml (render Calcpad-Lab) — ToInfix sigue devolviendo el nombre
        // literal para que la sintaxis MATLAB se preserve.
        private static readonly System.Collections.Generic.Dictionary<string, string> GreekGlyph =
            new(System.StringComparer.Ordinal)
        {
            { "alpha", "α" }, { "beta", "β" }, { "gamma", "γ" }, { "delta", "δ" },
            { "epsilon", "ε" }, { "zeta", "ζ" }, { "eta", "η" }, { "theta", "θ" },
            { "kappa", "κ" }, { "lambda", "λ" }, { "mu", "μ" }, { "nu", "ν" },
            { "xi", "ξ" }, { "pi", "π" }, { "rho", "ρ" }, { "sigma", "σ" },
            { "tau", "τ" }, { "phi", "φ" }, { "chi", "χ" }, { "psi", "ψ" }, { "omega", "ω" },
            { "Alpha", "Α" }, { "Beta", "Β" }, { "Gamma", "Γ" }, { "Delta", "Δ" },
            { "Theta", "Θ" }, { "Lambda", "Λ" }, { "Xi", "Ξ" }, { "Pi", "Π" },
            { "Sigma", "Σ" }, { "Phi", "Φ" }, { "Psi", "Ψ" }, { "Omega", "Ω" }
        };

        /// <summary>Convierte una palabra a glyph griego si aplica.</summary>
        private static string MapGreek(string s) =>
            GreekGlyph.TryGetValue(s, out var g) ? g : System.Net.WebUtility.HtmlEncode(s);

        public override string ToHtml()
        {
            // Manejo de variantes "prime" comunes en MATLAB: vp -> v', thxp -> θₓ',
            // thyp -> θᵧ', thzp -> θ_z', thxpp -> θₓ''.
            // Convención: terminar en 'p' = primera derivada, 'pp' = segunda.
            string baseName = Name;
            string prime = "";
            if (baseName.EndsWith("pp") && baseName.Length > 2) { prime = "''"; baseName = baseName.Substring(0, baseName.Length - 2); }
            else if (baseName.EndsWith("p") && baseName.Length > 1 && baseName != "phi" && baseName != "psi" && baseName != "pi") { prime = "'"; baseName = baseName.Substring(0, baseName.Length - 1); }

            // Aliases comunes para variables de mecánica/ingeniería
            // th -> theta, ph -> phi (cuando vienen como prefijo de derivada)
            if (baseName == "thx") baseName = "theta_x";
            else if (baseName == "thy") baseName = "theta_y";
            else if (baseName == "thz") baseName = "theta_z";
            else if (baseName == "ph") baseName = "phi";

            // Soporte subscripts: "w_max" -> "w<sub>max</sub>", "M_max_centro" -> "M<sub>max,centro</sub>"
            // También aplica mapping griego al head.
            int us = baseName.IndexOf('_');
            if (us > 0 && us < baseName.Length - 1)
            {
                var head = baseName.Substring(0, us);
                var sub  = baseName.Substring(us + 1).Replace('_', ',');
                return $"<var>{MapGreek(head)}<sub>{MapGreek(sub)}</sub></var>{prime}";
            }
            return $"<var>{MapGreek(baseName)}</var>{prime}";
        }
        public override SymNode Subs(string var, SymNode val) => Name == var ? val : this;
    }
    public sealed class SymAdd : SymNode
    {
        public SymNode A, B;
        public SymAdd(SymNode a, SymNode b) { A = a; B = b; }
        public override SymNode Diff(string var) => new SymAdd(A.Diff(var), B.Diff(var)).Simplify();
        public override double Eval(Dictionary<string, double> vals) => A.Eval(vals) + B.Eval(vals);
        public override string ToInfix()
        {
            // Pretty-print: si B es un término negativo, mostrar como sustracción
            if (TryNegativeForm(B, out var posNode))
                return $"{A.ToInfix()} - {posNode.ToInfix()}";
            return $"{A.ToInfix()} + {B.ToInfix()}";
        }
        public override string ToHtml()
        {
            if (TryNegativeForm(B, out var posNode))
                return $"{A.ToHtml()} - {posNode.ToHtml()}";
            return $"{A.ToHtml()} + {B.ToHtml()}";
        }
        public override string ToLatex()
        {
            if (TryNegativeForm(B, out var posNode))
                return $"{A.ToLatex()}-{posNode.ToLatex()}";
            return $"{A.ToLatex()}+{B.ToLatex()}";
        }
        /// <summary>Detecta si <paramref name="n"/> es un término "negativo" (const negativo,
        /// o producto con coef negativo). Si sí, devuelve su forma positiva.</summary>
        internal static bool TryNegativeForm(SymNode n, out SymNode positive)
        {
            positive = null;
            if (n is SymConst c && c.Value < 0)
            {
                positive = new SymConst(-c.Value);
                return true;
            }
            if (n is SymMul m)
            {
                if (m.A is SymConst kA && kA.Value < 0)
                {
                    if (kA.Value == -1) positive = m.B;
                    else positive = new SymMul(new SymConst(-kA.Value), m.B);
                    return true;
                }
                if (m.B is SymConst kB && kB.Value < 0)
                {
                    if (kB.Value == -1) positive = m.A;
                    else positive = new SymMul(m.A, new SymConst(-kB.Value));
                    return true;
                }
            }
            return false;
        }
        public override SymNode Subs(string var, SymNode val) => new SymAdd(A.Subs(var, val), B.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var a = A.Simplify(); var b = B.Simplify();
            if (a is SymConst ca && b is SymConst cb) return new SymConst(ca.Value + cb.Value);
            if (a is SymConst c0 && c0.Value == 0) return b;
            if (b is SymConst c1 && c1.Value == 0) return a;
            // Identidad pitagórica: sin²(x) + cos²(x) → 1
            if (IsTrigSquare(a, "sin", out var argSin) && IsTrigSquare(b, "cos", out var argCos)
                && argSin.ToInfix() == argCos.ToInfix())
                return new SymConst(1);
            if (IsTrigSquare(a, "cos", out var argCos2) && IsTrigSquare(b, "sin", out var argSin2)
                && argCos2.ToInfix() == argSin2.ToInfix())
                return new SymConst(1);
            // ── Like-term collection: x+x→2x, 2x+3x→5x, x+2x→3x, x-x→0 ─────
            // Aplanar add chain, sumar coeficientes por base estructural
            var terms = new System.Collections.Generic.List<SymNode>();
            FlattenAdd(a, terms);
            FlattenAdd(b, terms);
            return CollectLikeTermsAdd(terms);
        }
        internal static void FlattenAdd(SymNode n, System.Collections.Generic.List<SymNode> acc)
        {
            if (n is SymAdd s) { FlattenAdd(s.A, acc); FlattenAdd(s.B, acc); }
            else if (n is SymSub sb) { FlattenAdd(sb.A, acc); acc.Add(new SymMul(new SymConst(-1), sb.B).Simplify()); }
            else acc.Add(n);
        }
        /// <summary>Extrae (coef, expr-key, expr-node) de un término. Ej: 2*x → (2, "x", x); -x → (-1, "x", x); 5 → (5, "1", null).</summary>
        internal static (double coef, string key, SymNode expr) ExtractCoef(SymNode n)
        {
            if (n is SymConst c) return (c.Value, "_const_", null);
            if (n is SymMul m)
            {
                if (m.A is SymConst kA && !(m.B is SymConst)) return (kA.Value, m.B.ToInfix(), m.B);
                if (m.B is SymConst kB && !(m.A is SymConst)) return (kB.Value, m.A.ToInfix(), m.A);
                if (m.A is SymConst kAB && m.B is SymConst kBB) return (kAB.Value * kBB.Value, "_const_", null);
            }
            return (1.0, n.ToInfix(), n);
        }
        internal static SymNode CollectLikeTermsAdd(System.Collections.Generic.List<SymNode> terms)
        {
            var groups = new System.Collections.Generic.Dictionary<string, (double Coef, SymNode Expr)>();
            double constSum = 0;
            foreach (var t in terms)
            {
                var (coef, key, expr) = ExtractCoef(t);
                if (key == "_const_") { constSum += coef; continue; }
                if (groups.TryGetValue(key, out var existing))
                    groups[key] = (existing.Coef + coef, existing.Expr);
                else
                    groups[key] = (coef, expr);
            }
            var pieces = new System.Collections.Generic.List<SymNode>();
            foreach (var kv in groups)
            {
                var (coef, expr) = kv.Value;
                if (coef == 0) continue;
                if (coef == 1) pieces.Add(expr);
                else if (coef == -1) pieces.Add(new SymMul(new SymConst(-1), expr));
                else pieces.Add(new SymMul(new SymConst(coef), expr));
            }
            if (constSum != 0) pieces.Add(new SymConst(constSum));
            if (pieces.Count == 0) return new SymConst(0);
            if (pieces.Count == 1) return pieces[0];
            // Reconstruir add chain
            SymNode acc = pieces[0];
            for (int i = 1; i < pieces.Count; i++) acc = new SymAdd(acc, pieces[i]);
            // Phase 1 de factoring: intentar extraer factor comun multiplicativo
            // (denominador racional + monomios comunes) para reducir el output.
            var factored = TryExtractCommonFactor(pieces);
            return factored ?? acc;
        }

        // ── Phase 1: Common factor extraction ──────────────────────────────
        // Para una suma `t1 + t2 + ... + tn`, descompone cada termino en
        // (coef racional p/q, dict de factores variables con potencia) y
        // extrae el factor comun: GCD de los numeradores, LCM de los
        // denominadores, intersection con min-power de los variables comunes.
        // Resultado: `common * (t1/common + t2/common + ...)` que en el caso
        // tipico de integral con limites simbolicos colapsa varios terminos
        // sueltos en una sola expresion mas legible.
        private sealed class TermDecomp
        {
            public long CoefN = 1, CoefD = 1;
            public System.Collections.Generic.Dictionary<string, (double Power, SymNode BaseExpr)> Factors
                = new(System.StringComparer.Ordinal);
        }

        private static void AddFactor(
            System.Collections.Generic.Dictionary<string, (double Power, SymNode BaseExpr)> map,
            string key, SymNode baseE, double power)
        {
            if (map.TryGetValue(key, out var ex))
                map[key] = (ex.Power + power, ex.BaseExpr);
            else
                map[key] = (power, baseE);
        }

        private static long GcdLong(long a, long b)
        {
            a = System.Math.Abs(a); b = System.Math.Abs(b);
            while (b != 0) { var t = a % b; a = b; b = t; }
            return a == 0 ? 1 : a;
        }
        private static long LcmLong(long a, long b)
        {
            if (a == 0 || b == 0) return 0;
            return System.Math.Abs(a / GcdLong(a, b) * b);
        }

        private static void DecomposeRecursive(SymNode n, TermDecomp t)
        {
            if (n is SymConst c)
            {
                if (SymConst.TryAsRational(c.Value, out var nn, out var dd))
                { t.CoefN *= nn; t.CoefD *= dd; }
                else
                { AddFactor(t.Factors, $"_c_{c.Value:R}_", c, 1); }
                return;
            }
            if (n is SymVar)
            { AddFactor(t.Factors, n.ToInfix(), n, 1); return; }
            if (n is SymPow p && p.Exp is SymConst pe)
            { AddFactor(t.Factors, p.Base.ToInfix(), p.Base, pe.Value); return; }
            if (n is SymMul m)
            { DecomposeRecursive(m.A, t); DecomposeRecursive(m.B, t); return; }
            if (n is SymDiv dv)
            {
                DecomposeRecursive(dv.A, t);
                if (dv.B is SymConst dc && SymConst.TryAsRational(dc.Value, out var dn, out var dd2))
                { t.CoefN *= dd2; t.CoefD *= dn; }
                else if (dv.B is SymVar)
                { AddFactor(t.Factors, dv.B.ToInfix(), dv.B, -1); }
                else if (dv.B is SymPow dp && dp.Exp is SymConst dpe)
                { AddFactor(t.Factors, dp.Base.ToInfix(), dp.Base, -dpe.Value); }
                else
                { AddFactor(t.Factors, dv.B.ToInfix(), dv.B, -1); }
                return;
            }
            // Otros (SymAdd, SymSub, SymFunc, SymPow con exp no-const) — opaque atom
            AddFactor(t.Factors, n.ToInfix(), n, 1);
        }

        private static SymNode BuildFromDecomp(TermDecomp t)
        {
            // Construir SymNode a partir del decomp: coef * factor1^p1 * factor2^p2 * ...
            // Reduce el coef racional por GCD primero.
            long g = GcdLong(t.CoefN, t.CoefD);
            long n = t.CoefN / g; long d = t.CoefD / g;
            // Asegurar signo en numerador
            if (d < 0) { n = -n; d = -d; }

            var parts = new System.Collections.Generic.List<SymNode>();
            // Coeficiente
            if (n != 1 || d != 1)
            {
                if (d == 1) parts.Add(new SymConst(n));
                else parts.Add(new SymConst((double)n / d));
            }
            // Factores con potencia positiva primero, luego negativos
            var keysSorted = new System.Collections.Generic.List<string>(t.Factors.Keys);
            keysSorted.Sort(System.StringComparer.Ordinal);
            foreach (var k in keysSorted)
            {
                var (pow, baseE) = t.Factors[k];
                if (pow == 0) continue;
                SymNode fexpr = pow == 1 ? baseE : new SymPow(baseE, new SymConst(pow));
                parts.Add(fexpr);
            }
            if (parts.Count == 0) return new SymConst(1);
            if (parts.Count == 1) return parts[0];
            SymNode acc = parts[0];
            for (int i = 1; i < parts.Count; i++) acc = new SymMul(acc, parts[i]);
            return acc;
        }

        internal static SymNode TryExtractCommonFactor(System.Collections.Generic.List<SymNode> terms)
        {
            if (terms.Count < 2) return null;
            var decomps = new System.Collections.Generic.List<TermDecomp>();
            foreach (var t in terms)
            {
                var d = new TermDecomp();
                DecomposeRecursive(t, d);
                decomps.Add(d);
            }

            // GCD de los abs(numeradores), LCM de los denominadores
            long commonNum = System.Math.Abs(decomps[0].CoefN);
            long commonDen = decomps[0].CoefD;
            if (commonNum == 0) return null;
            for (int i = 1; i < decomps.Count; i++)
            {
                long cn = System.Math.Abs(decomps[i].CoefN);
                if (cn == 0) return null;
                commonNum = GcdLong(commonNum, cn);
                commonDen = LcmLong(commonDen, decomps[i].CoefD);
            }

            // Factores variables comunes (interseccion con min-power, solo pow > 0)
            var commonFactors = new System.Collections.Generic.Dictionary<string, (double Power, SymNode BaseExpr)>();
            foreach (var kv in decomps[0].Factors)
            {
                if (kv.Value.Power <= 0) continue;
                bool allHave = true;
                double minP = kv.Value.Power;
                for (int i = 1; i < decomps.Count; i++)
                {
                    if (!decomps[i].Factors.TryGetValue(kv.Key, out var v) || v.Power <= 0)
                    { allHave = false; break; }
                    if (v.Power < minP) minP = v.Power;
                }
                if (allHave && minP > 0)
                    commonFactors[kv.Key] = (minP, kv.Value.BaseExpr);
            }

            // Si no hay ganancia (coef trivial 1/1 sin variables comunes), no factorizar
            bool hasNumCoef = commonNum > 1;
            bool hasDenCoef = commonDen > 1;
            bool hasVars = commonFactors.Count > 0;
            if (!hasNumCoef && !hasDenCoef && !hasVars) return null;

            // Construir factor comun
            var commonDecomp = new TermDecomp { CoefN = commonNum, CoefD = commonDen };
            foreach (var cf in commonFactors)
                commonDecomp.Factors[cf.Key] = cf.Value;
            SymNode commonExpr = BuildFromDecomp(commonDecomp);

            // Construir lista de terminos restantes: ti / common
            var remainder = new System.Collections.Generic.List<SymNode>();
            for (int i = 0; i < decomps.Count; i++)
            {
                var d = decomps[i];
                // Coef: (d.CoefN / d.CoefD) / (commonNum / commonDen) =
                //       (d.CoefN * commonDen) / (d.CoefD * commonNum)
                long signN = d.CoefN < 0 ? -1 : 1;
                long absN = System.Math.Abs(d.CoefN);
                long rNum = signN * (absN / commonNum) * commonDen;
                long rDen = d.CoefD;
                // Si commonNum no divide exactamente a absN (no deberia, porque commonNum=gcd), abort.
                if (absN % commonNum != 0) return null;
                long g = GcdLong(rNum, rDen);
                if (g > 0) { rNum /= g; rDen /= g; }
                if (rDen < 0) { rNum = -rNum; rDen = -rDen; }

                // Restar potencias comunes de los factores
                var remFactors = new System.Collections.Generic.Dictionary<string, (double Power, SymNode BaseExpr)>(d.Factors);
                foreach (var cf in commonFactors)
                {
                    if (remFactors.TryGetValue(cf.Key, out var ex))
                    {
                        double newP = ex.Power - cf.Value.Power;
                        if (newP == 0) remFactors.Remove(cf.Key);
                        else remFactors[cf.Key] = (newP, ex.BaseExpr);
                    }
                }

                var rd = new TermDecomp { CoefN = rNum, CoefD = rDen, Factors = remFactors };
                remainder.Add(BuildFromDecomp(rd));
            }

            // Construir suma remanente
            SymNode remSum = remainder[0];
            for (int i = 1; i < remainder.Count; i++) remSum = new SymAdd(remSum, remainder[i]);

            // common * (sum/common). NO llamar Simplify() para evitar recursion infinita.
            return new SymMul(commonExpr, remSum);
        }
        /// <summary>True si <paramref name="n"/> es <c>fn(x)²</c> (de la forma <c>fn(x)^2</c> o <c>fn(x)·fn(x)</c>).</summary>
        private static bool IsTrigSquare(SymNode n, string fn, out SymNode arg)
        {
            arg = null;
            if (n is SymPow pow && pow.Base is SymFunc f && f.Name == fn && pow.Exp is SymConst ce && ce.Value == 2)
            {
                arg = f.Arg; return true;
            }
            if (n is SymMul mul && mul.A is SymFunc fa && fa.Name == fn && mul.B is SymFunc fb && fb.Name == fn
                && fa.Arg.ToInfix() == fb.Arg.ToInfix())
            {
                arg = fa.Arg; return true;
            }
            return false;
        }
    }
    public sealed class SymSub : SymNode
    {
        public SymNode A, B;
        public SymSub(SymNode a, SymNode b) { A = a; B = b; }
        public override SymNode Diff(string var) => new SymSub(A.Diff(var), B.Diff(var)).Simplify();
        public override double Eval(Dictionary<string, double> vals) => A.Eval(vals) - B.Eval(vals);
        public override string ToInfix() => B is SymAdd ? $"{A.ToInfix()} - ({B.ToInfix()})" : $"{A.ToInfix()} - {B.ToInfix()}";
        public override string ToHtml() => B is SymAdd ? $"{A.ToHtml()} - ({B.ToHtml()})" : $"{A.ToHtml()} - {B.ToHtml()}";
        public override string ToLatex() => B is SymAdd ? $"{A.ToLatex()}-\\left({B.ToLatex()}\\right)" : $"{A.ToLatex()}-{B.ToLatex()}";
        public override SymNode Subs(string var, SymNode val) => new SymSub(A.Subs(var, val), B.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var a = A.Simplify(); var b = B.Simplify();
            if (a is SymConst ca && b is SymConst cb) return new SymConst(ca.Value - cb.Value);
            if (b is SymConst c0 && c0.Value == 0) return a;
            // Lift a Add con coef -1 para colectar like-terms (x - x → 0, 2x - x → x)
            var terms = new System.Collections.Generic.List<SymNode>();
            SymAdd.FlattenAdd(a, terms);
            terms.Add(new SymMul(new SymConst(-1), b).Simplify());
            return SymAdd.CollectLikeTermsAdd(terms);
        }
    }
    public sealed class SymMul : SymNode
    {
        public SymNode A, B;
        public SymMul(SymNode a, SymNode b) { A = a; B = b; }
        public override SymNode Diff(string var) =>
            new SymAdd(new SymMul(A.Diff(var), B), new SymMul(A, B.Diff(var))).Simplify();
        public override double Eval(Dictionary<string, double> vals) => A.Eval(vals) * B.Eval(vals);
        public override string ToInfix()
        {
            // Caso especial: -1 * X → -X (sin "1*")
            if (A is SymConst ca && ca.Value == -1)
            {
                string sb_ = B is SymAdd || B is SymSub ? $"({B.ToInfix()})" : B.ToInfix();
                return $"-{sb_}";
            }
            if (B is SymConst cb && cb.Value == -1)
            {
                string sa_ = A is SymAdd || A is SymSub ? $"({A.ToInfix()})" : A.ToInfix();
                return $"-{sa_}";
            }
            string sa = A is SymAdd || A is SymSub ? $"({A.ToInfix()})" : A.ToInfix();
            string sb = B is SymAdd || B is SymSub ? $"({B.ToInfix()})" : B.ToInfix();
            return $"{sa}*{sb}";
        }
        public override string ToHtml()
        {
            // -1 * X -> -X
            if (A is SymConst ca && ca.Value == -1)
            {
                string sb_ = B is SymAdd || B is SymSub ? $"({B.ToHtml()})" : B.ToHtml();
                return $"-{sb_}";
            }
            if (B is SymConst cb && cb.Value == -1)
            {
                string sa_ = A is SymAdd || A is SymSub ? $"({A.ToHtml()})" : A.ToHtml();
                return $"-{sa_}";
            }
            string saH = A is SymAdd || A is SymSub ? $"({A.ToHtml()})" : A.ToHtml();
            string sbH = B is SymAdd || B is SymSub ? $"({B.ToHtml()})" : B.ToHtml();
            // Si A es constante y B es variable/expresion (e.g. 3*x), usar middle dot.
            // Si ambos son variables (x*y), tambien usar middle dot.
            return $"{saH}&middot;{sbH}";
        }
        public override string ToLatex()
        {
            if (A is SymConst caL && caL.Value == -1)
            {
                string sb_ = B is SymAdd || B is SymSub ? $"\\left({B.ToLatex()}\\right)" : B.ToLatex();
                return $"-{sb_}";
            }
            string saL = A is SymAdd || A is SymSub ? $"\\left({A.ToLatex()}\\right)" : A.ToLatex();
            string sbL = B is SymAdd || B is SymSub ? $"\\left({B.ToLatex()}\\right)" : B.ToLatex();
            return $"{saL}\\,{sbL}";   // LaTeX usa \, (thin space) entre factores
        }
        public override SymNode Subs(string var, SymNode val) => new SymMul(A.Subs(var, val), B.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var a = A.Simplify(); var b = B.Simplify();
            if (a is SymConst ca && b is SymConst cb) return new SymConst(ca.Value * cb.Value);
            if (a is SymConst c0 && c0.Value == 0) return new SymConst(0);
            if (b is SymConst c1 && c1.Value == 0) return new SymConst(0);
            if (a is SymConst c2 && c2.Value == 1) return b;
            if (b is SymConst c3 && c3.Value == 1) return a;
            // ── Distribucion de constante sobre suma/resta ─────────────────
            // c*(A+B) -> c*A + c*B   y   c*(A-B) -> c*A - c*B
            // Critico para preservar signos cuando SymSub.Simplify hace (-1)*Fa
            // donde Fa es un SymAdd: sin esto el -1 queda como factor opaco y
            // los signos internos no propagan correctamente al like-term collect.
            if (a is SymConst && b is SymAdd bAdd)
                return new SymAdd(
                    new SymMul(a, bAdd.A).Simplify(),
                    new SymMul(a, bAdd.B).Simplify()).Simplify();
            if (a is SymConst && b is SymSub bSub)
                return new SymSub(
                    new SymMul(a, bSub.A).Simplify(),
                    new SymMul(a, bSub.B).Simplify()).Simplify();
            if (b is SymConst && a is SymAdd aAdd)
                return new SymAdd(
                    new SymMul(aAdd.A, b).Simplify(),
                    new SymMul(aAdd.B, b).Simplify()).Simplify();
            if (b is SymConst && a is SymSub aSub)
                return new SymSub(
                    new SymMul(aSub.A, b).Simplify(),
                    new SymMul(aSub.B, b).Simplify()).Simplify();
            // ── Power collection: x*x→x², x*x²→x³, x²*x³→x⁵ ─────────────────
            var factors = new System.Collections.Generic.List<SymNode>();
            FlattenMul(a, factors);
            FlattenMul(b, factors);
            return CollectLikeTermsMul(factors);
        }
        internal static void FlattenMul(SymNode n, System.Collections.Generic.List<SymNode> acc)
        {
            if (n is SymMul m) { FlattenMul(m.A, acc); FlattenMul(m.B, acc); }
            else acc.Add(n);
        }
        /// <summary>Extrae (base, exp-num) de un factor. Ej: x → (x, 1); x^3 → (x, 3); x^y → (x^y, 1) (no colectable).</summary>
        internal static (string key, SymNode baseExpr, double exp, bool collectable) ExtractBase(SymNode n)
        {
            if (n is SymConst) return ("_const_", n, 1, false);
            if (n is SymPow p && p.Exp is SymConst pe)
                return (p.Base.ToInfix(), p.Base, pe.Value, true);
            return (n.ToInfix(), n, 1, true);
        }
        internal static SymNode CollectLikeTermsMul(System.Collections.Generic.List<SymNode> factors)
        {
            var groups = new System.Collections.Generic.Dictionary<string, (double Exp, SymNode Base)>();
            double constProd = 1;
            var nonCollectable = new System.Collections.Generic.List<SymNode>();
            foreach (var f in factors)
            {
                if (f is SymConst k) { constProd *= k.Value; continue; }
                var (key, baseE, exp, collect) = ExtractBase(f);
                if (!collect) { nonCollectable.Add(f); continue; }
                if (groups.TryGetValue(key, out var existing))
                    groups[key] = (existing.Exp + exp, existing.Base);
                else
                    groups[key] = (exp, baseE);
            }
            if (constProd == 0) return new SymConst(0);
            // Construir pieces simbólicas ordenadas alfabéticamente por su key — canonicaliza x*y vs y*x
            var sortedKeys = new System.Collections.Generic.List<string>(groups.Keys);
            sortedKeys.Sort(System.StringComparer.Ordinal);
            var pieces = new System.Collections.Generic.List<SymNode>();
            foreach (var key in sortedKeys)
            {
                var (exp, baseE) = groups[key];
                if (exp == 0) continue;
                if (exp == 1) pieces.Add(baseE);
                else pieces.Add(new SymPow(baseE, new SymConst(exp)));
            }
            pieces.AddRange(nonCollectable);
            // Const al frente si != 1
            if (constProd != 1) pieces.Insert(0, new SymConst(constProd));
            if (pieces.Count == 0) return new SymConst(1);
            if (pieces.Count == 1) return pieces[0];
            SymNode acc = pieces[0];
            for (int i = 1; i < pieces.Count; i++) acc = new SymMul(acc, pieces[i]);
            return acc;
        }
    }
    public sealed class SymDiv : SymNode
    {
        public SymNode A, B;
        public SymDiv(SymNode a, SymNode b) { A = a; B = b; }
        public override SymNode Diff(string var) =>
            // (A'B - AB') / B²
            new SymDiv(
                new SymSub(new SymMul(A.Diff(var), B), new SymMul(A, B.Diff(var))),
                new SymPow(B, new SymConst(2))).Simplify();
        public override double Eval(Dictionary<string, double> vals) => A.Eval(vals) / B.Eval(vals);
        public override string ToInfix() => $"({A.ToInfix()})/({B.ToInfix()})";
        public override string ToHtml() =>
            // Fraccion estilo Calcpad: numerador arriba, denominador abajo con linea
            $"<span class=\"dvc\"><span class=\"dvc-num\">{A.ToHtml()}</span><span class=\"dvl\"></span><span class=\"dvc-den\">{B.ToHtml()}</span></span>";
        public override string ToLatex() => $"\\frac{{{A.ToLatex()}}}{{{B.ToLatex()}}}";
        public override SymNode Subs(string var, SymNode val) => new SymDiv(A.Subs(var, val), B.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var a = A.Simplify(); var b = B.Simplify();
            if (a is SymConst ca && b is SymConst cb && cb.Value != 0) return new SymConst(ca.Value / cb.Value);
            if (a is SymConst c0 && c0.Value == 0) return new SymConst(0);
            if (b is SymConst c1 && c1.Value == 1) return a;
            return new SymDiv(a, b);
        }
    }
    public sealed class SymPow : SymNode
    {
        public SymNode Base, Exp;
        public SymPow(SymNode b, SymNode e) { Base = b; Exp = e; }
        public override SymNode Diff(string var)
        {
            // Si Exp es constante n: n·x^(n-1)·x'
            if (Exp is SymConst c)
            {
                return new SymMul(new SymMul(c, new SymPow(Base, new SymConst(c.Value - 1))), Base.Diff(var)).Simplify();
            }
            // General: e^(ln base · exp) → derivada del log-rule
            // d/dx (a^b) = a^b · (b'·ln(a) + b · a'/a)
            return new SymMul(this,
                new SymAdd(
                    new SymMul(Exp.Diff(var), new SymFunc("log", Base)),
                    new SymMul(Exp, new SymDiv(Base.Diff(var), Base))
                )).Simplify();
        }
        public override double Eval(Dictionary<string, double> vals) => Math.Pow(Base.Eval(vals), Exp.Eval(vals));
        public override string ToInfix()
        {
            string sb = Base is SymAdd || Base is SymSub || Base is SymMul || Base is SymDiv ? $"({Base.ToInfix()})" : Base.ToInfix();
            string se = Exp is SymConst ? Exp.ToInfix() : $"({Exp.ToInfix()})";
            return $"{sb}^{se}";
        }
        public override string ToHtml()
        {
            // Exponente como <sup>
            string sbH = Base is SymAdd || Base is SymSub || Base is SymMul || Base is SymDiv
                ? $"({Base.ToHtml()})" : Base.ToHtml();
            string seH = Exp is SymConst ? Exp.ToHtml() : Exp.ToHtml();
            return $"{sbH}<sup>{seH}</sup>";
        }
        public override string ToLatex()
        {
            string sbL = Base is SymAdd || Base is SymSub || Base is SymMul || Base is SymDiv
                ? $"\\left({Base.ToLatex()}\\right)" : Base.ToLatex();
            return $"{{{sbL}}}^{{{Exp.ToLatex()}}}";
        }
        public override SymNode Subs(string var, SymNode val) => new SymPow(Base.Subs(var, val), Exp.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var b = Base.Simplify(); var e = Exp.Simplify();
            if (b is SymConst cb && e is SymConst ce) return new SymConst(Math.Pow(cb.Value, ce.Value));
            if (e is SymConst c0 && c0.Value == 0) return new SymConst(1);
            if (e is SymConst c1 && c1.Value == 1) return b;
            if (b is SymConst cb0 && cb0.Value == 0) return new SymConst(0);
            if (b is SymConst cb1 && cb1.Value == 1) return new SymConst(1);
            return new SymPow(b, e);
        }
    }
    public sealed class SymFunc : SymNode
    {
        public string Name;
        public SymNode Arg;
        public SymFunc(string n, SymNode a) { Name = n; Arg = a; }
        public override SymNode Diff(string var)
        {
            // Regla de la cadena: (f(g))' = f'(g)·g'
            SymNode inner = Arg.Diff(var);
            SymNode outerDeriv = Name switch
            {
                "sin" => new SymFunc("cos", Arg),
                "cos" => new SymMul(new SymConst(-1), new SymFunc("sin", Arg)),
                "tan" => new SymDiv(new SymConst(1), new SymPow(new SymFunc("cos", Arg), new SymConst(2))),
                "exp" => new SymFunc("exp", Arg),
                "log" => new SymDiv(new SymConst(1), Arg),
                "log2" => new SymDiv(new SymConst(1), new SymMul(Arg, new SymConst(Math.Log(2)))),
                "log10" => new SymDiv(new SymConst(1), new SymMul(Arg, new SymConst(Math.Log(10)))),
                "sqrt" => new SymDiv(new SymConst(0.5), new SymFunc("sqrt", Arg)),
                "sinh" => new SymFunc("cosh", Arg),
                "cosh" => new SymFunc("sinh", Arg),
                "tanh" => new SymSub(new SymConst(1), new SymPow(new SymFunc("tanh", Arg), new SymConst(2))),
                "abs" => new SymFunc("sign", Arg),
                _ => throw new MatlabRuntimeException($"diff: function '{Name}' not supported symbolically")
            };
            return new SymMul(outerDeriv, inner).Simplify();
        }
        public override double Eval(Dictionary<string, double> vals)
        {
            double x = Arg.Eval(vals);
            return Name switch
            {
                "sin" => Math.Sin(x), "cos" => Math.Cos(x), "tan" => Math.Tan(x),
                "exp" => Math.Exp(x), "log" => Math.Log(x),
                "log2" => Math.Log(x, 2), "log10" => Math.Log10(x),
                "sqrt" => Math.Sqrt(x),
                "sinh" => Math.Sinh(x), "cosh" => Math.Cosh(x), "tanh" => Math.Tanh(x),
                "abs" => Math.Abs(x), "sign" => Math.Sign(x),
                "asin" => Math.Asin(x), "acos" => Math.Acos(x), "atan" => Math.Atan(x),
                _ => throw new MatlabRuntimeException($"sym eval: function '{Name}' not supported")
            };
        }
        public override string ToInfix() => $"{Name}({Arg.ToInfix()})";
        public override string ToHtml() => $"<span style=\"font-family:'Segoe UI',sans-serif;font-weight:600;font-style:normal;color:#7c2bb2\">{System.Net.WebUtility.HtmlEncode(Name)}</span>({Arg.ToHtml()})";
        public override string ToLatex() => $"\\{Name}\\left({Arg.ToLatex()}\\right)";   // \sin(x), \cos(x), etc.
        public override SymNode Subs(string var, SymNode val) => new SymFunc(Name, Arg.Subs(var, val)).Simplify();
        public override SymNode Simplify()
        {
            var inner = Arg.Simplify();
            if (inner is SymConst c)
                return new SymConst(Eval(new Dictionary<string, double> { ["_"] = c.Value })); // hack: const arg → eval directo
            return new SymFunc(Name, inner);
        }
    }

    /// <summary>Reglas trigonométricas: expansión y reducción.</summary>
    public static class TrigRules
    {
        /// <summary>
        /// Expande funciones trig con argumentos compuestos:
        ///   sin(a+b) = sin(a)cos(b) + cos(a)sin(b)
        ///   cos(a+b) = cos(a)cos(b) - sin(a)sin(b)
        ///   sin(2x) = 2 sin(x) cos(x)
        ///   cos(2x) = cos²(x) - sin²(x) = 2cos²(x)-1 = 1-2sin²(x)
        /// </summary>
        public static SymNode ExpandTrig(SymNode node)
        {
            if (node is SymFunc f)
            {
                var arg = ExpandTrig(f.Arg);
                // sin(a+b), cos(a+b), tan(a+b)
                if (arg is SymAdd add)
                {
                    return f.Name switch
                    {
                        "sin" => new SymAdd(
                            new SymMul(new SymFunc("sin", add.A), new SymFunc("cos", add.B)),
                            new SymMul(new SymFunc("cos", add.A), new SymFunc("sin", add.B))).Simplify(),
                        "cos" => new SymSub(
                            new SymMul(new SymFunc("cos", add.A), new SymFunc("cos", add.B)),
                            new SymMul(new SymFunc("sin", add.A), new SymFunc("sin", add.B))).Simplify(),
                        "tan" => new SymDiv(
                            new SymAdd(new SymFunc("tan", add.A), new SymFunc("tan", add.B)),
                            new SymSub(new SymConst(1),
                                new SymMul(new SymFunc("tan", add.A), new SymFunc("tan", add.B)))).Simplify(),
                        _ => new SymFunc(f.Name, arg)
                    };
                }
                if (arg is SymSub sub)
                {
                    return f.Name switch
                    {
                        "sin" => new SymSub(
                            new SymMul(new SymFunc("sin", sub.A), new SymFunc("cos", sub.B)),
                            new SymMul(new SymFunc("cos", sub.A), new SymFunc("sin", sub.B))).Simplify(),
                        "cos" => new SymAdd(
                            new SymMul(new SymFunc("cos", sub.A), new SymFunc("cos", sub.B)),
                            new SymMul(new SymFunc("sin", sub.A), new SymFunc("sin", sub.B))).Simplify(),
                        _ => new SymFunc(f.Name, arg)
                    };
                }
                // sin(2x), cos(2x): detectar 2·x
                if (arg is SymMul mul && mul.A is SymConst c2 && c2.Value == 2)
                {
                    var x = mul.B;
                    return f.Name switch
                    {
                        "sin" => new SymMul(new SymConst(2),
                            new SymMul(new SymFunc("sin", x), new SymFunc("cos", x))).Simplify(),
                        "cos" => new SymSub(
                            new SymPow(new SymFunc("cos", x), new SymConst(2)),
                            new SymPow(new SymFunc("sin", x), new SymConst(2))).Simplify(),
                        _ => new SymFunc(f.Name, arg)
                    };
                }
                return new SymFunc(f.Name, arg);
            }
            // Recurse en operadores binarios
            if (node is SymAdd a) return new SymAdd(ExpandTrig(a.A), ExpandTrig(a.B)).Simplify();
            if (node is SymSub s) return new SymSub(ExpandTrig(s.A), ExpandTrig(s.B)).Simplify();
            if (node is SymMul m) return new SymMul(ExpandTrig(m.A), ExpandTrig(m.B)).Simplify();
            if (node is SymDiv d) return new SymDiv(ExpandTrig(d.A), ExpandTrig(d.B)).Simplify();
            if (node is SymPow p) return new SymPow(ExpandTrig(p.Base), ExpandTrig(p.Exp)).Simplify();
            return node;
        }
        /// <summary>
        /// Simplifica trig: 1 - sin²(x) → cos²(x), 1 - cos²(x) → sin²(x),
        /// tan(x) · cos(x) → sin(x), sin²(x) + cos²(x) → 1 (ya en SymAdd).
        /// </summary>
        public static SymNode SimplifyTrig(SymNode node)
        {
            // Recurse primero
            if (node is SymAdd a) node = new SymAdd(SimplifyTrig(a.A), SimplifyTrig(a.B)).Simplify();
            else if (node is SymSub s) node = new SymSub(SimplifyTrig(s.A), SimplifyTrig(s.B)).Simplify();
            else if (node is SymMul m) node = new SymMul(SimplifyTrig(m.A), SimplifyTrig(m.B)).Simplify();
            else if (node is SymDiv d) node = new SymDiv(SimplifyTrig(d.A), SimplifyTrig(d.B)).Simplify();
            else if (node is SymPow p) node = new SymPow(SimplifyTrig(p.Base), SimplifyTrig(p.Exp)).Simplify();
            else if (node is SymFunc f) node = new SymFunc(f.Name, SimplifyTrig(f.Arg)).Simplify();
            // Patrones específicos:
            // 1 - sin²(x) → cos²(x)
            if (node is SymSub sub && sub.A is SymConst ca && ca.Value == 1
                && sub.B is SymPow pw && pw.Base is SymFunc fb && fb.Name == "sin"
                && pw.Exp is SymConst ce && ce.Value == 2)
                return new SymPow(new SymFunc("cos", fb.Arg), new SymConst(2));
            // 1 - cos²(x) → sin²(x)
            if (node is SymSub sub2 && sub2.A is SymConst ca2 && ca2.Value == 1
                && sub2.B is SymPow pw2 && pw2.Base is SymFunc fb2 && fb2.Name == "cos"
                && pw2.Exp is SymConst ce2 && ce2.Value == 2)
                return new SymPow(new SymFunc("sin", fb2.Arg), new SymConst(2));
            return node;
        }
    }

    public static class SymOps
    {
        /// <summary>
        /// Antiderivada simbólica (integral indefinida) respecto a <paramref name="var"/>.
        /// Soporta polinomios, funciones elementales (sin/cos/exp/1/x) y combinaciones lineales.
        /// </summary>
        public static SymNode Integrate(SymNode expr, string var)
        {
            // Caso const: ∫c dx = c·x
            if (expr is SymConst c)
                return new SymMul(c, new SymVar(var)).Simplify();
            // Caso var: ∫x dx = x²/2
            if (expr is SymVar v)
            {
                if (v.Name == var) return new SymDiv(new SymPow(v, new SymConst(2)), new SymConst(2)).Simplify();
                return new SymMul(v, new SymVar(var)).Simplify();   // tratado como const
            }
            // Suma: ∫(a + b) = ∫a + ∫b
            if (expr is SymAdd add) return new SymAdd(Integrate(add.A, var), Integrate(add.B, var)).Simplify();
            if (expr is SymSub sub) return new SymSub(Integrate(sub.A, var), Integrate(sub.B, var)).Simplify();
            // ∫(c·f) = c·∫f cuando c es const respecto a var
            if (expr is SymMul mul)
            {
                if (IsConstWrt(mul.A, var)) return new SymMul(mul.A, Integrate(mul.B, var)).Simplify();
                if (IsConstWrt(mul.B, var)) return new SymMul(mul.B, Integrate(mul.A, var)).Simplify();
                // Caso especial: x·sin(x), x·cos(x), x·exp(x) — integración por partes
                // Por ahora MVP: no soportar (sería expansión grande)
                throw new MatlabRuntimeException($"int: producto general '{mul.ToInfix()}' no soportado (MVP)");
            }
            // ∫(f/c) = (∫f)/c
            if (expr is SymDiv div)
            {
                if (IsConstWrt(div.B, var))
                    return new SymDiv(Integrate(div.A, var), div.B).Simplify();
                // 1/x → log(x)
                if (div.A is SymConst ca && ca.Value == 1 && div.B is SymVar vb && vb.Name == var)
                    return new SymFunc("log", new SymVar(var));
                throw new MatlabRuntimeException($"int: cociente '{div.ToInfix()}' no soportado (MVP)");
            }
            // x^n → x^(n+1)/(n+1)  (n const, n != -1)
            if (expr is SymPow pow)
            {
                if (pow.Base is SymVar vp && vp.Name == var && pow.Exp is SymConst ce)
                {
                    if (Math.Abs(ce.Value + 1) < 1e-12)
                        return new SymFunc("log", new SymVar(var));
                    return new SymDiv(
                        new SymPow(new SymVar(var), new SymConst(ce.Value + 1)),
                        new SymConst(ce.Value + 1)).Simplify();
                }
                if (IsConstWrt(pow, var))
                    return new SymMul(pow, new SymVar(var)).Simplify();
                throw new MatlabRuntimeException($"int: potencia general '{pow.ToInfix()}' no soportada (MVP)");
            }
            // Funciones elementales con arg = x
            if (expr is SymFunc fn && fn.Arg is SymVar fv && fv.Name == var)
            {
                return fn.Name switch
                {
                    "sin" => new SymMul(new SymConst(-1), new SymFunc("cos", fn.Arg)),
                    "cos" => new SymFunc("sin", fn.Arg),
                    "exp" => new SymFunc("exp", fn.Arg),
                    "log" => new SymSub(new SymMul(fn.Arg, new SymFunc("log", fn.Arg)), fn.Arg),
                    "sqrt" => new SymDiv(
                        new SymMul(new SymConst(2.0/3), new SymPow(fn.Arg, new SymConst(1.5))),
                        new SymConst(1)),
                    "sinh" => new SymFunc("cosh", fn.Arg),
                    "cosh" => new SymFunc("sinh", fn.Arg),
                    "tan" => new SymMul(new SymConst(-1), new SymFunc("log", new SymFunc("cos", fn.Arg))),
                    _ => throw new MatlabRuntimeException($"int: function '{fn.Name}' no soportada (MVP)")
                };
            }
            // Chain rule lineal: ∫ f(a·x + b) dx = F(a·x + b) / a  (a const, a ≠ 0)
            // Detectado para sin/cos/exp/sinh/cosh
            if (expr is SymFunc fnL)
            {
                var (aLin, bLin) = TryLinearForm(fnL.Arg, var);
                if (aLin != null)
                {
                    SymNode F = fnL.Name switch
                    {
                        "sin" => new SymMul(new SymConst(-1), new SymFunc("cos", fnL.Arg)),
                        "cos" => new SymFunc("sin", fnL.Arg),
                        "exp" => new SymFunc("exp", fnL.Arg),
                        "sinh" => new SymFunc("cosh", fnL.Arg),
                        "cosh" => new SymFunc("sinh", fnL.Arg),
                        _ => null
                    };
                    if (F != null)
                        return new SymDiv(F, aLin).Simplify();
                }
            }
            if (IsConstWrt(expr, var))
                return new SymMul(expr, new SymVar(var)).Simplify();
            throw new MatlabRuntimeException($"int: expresión '{expr.ToInfix()}' no soportada (MVP)");
        }
        /// <summary>
        /// Resuelve la ODE de primer orden <c>dy/dt = rhs(t, y)</c> simbólicamente.
        /// Patrones soportados (MVP):
        ///   1. <c>rhs</c> no contiene y → <c>y = ∫rhs dt + C</c>
        ///   2. <c>rhs = k·y</c> con k const wrt y, t → <c>y = C·exp(k·t)</c>
        ///   3. <c>rhs = k·y + c</c> (lineal inhomogénea, k, c const) → <c>y = -c/k + C·exp(k·t)</c>
        ///   4. <c>rhs = y</c> → <c>y = C·exp(t)</c>
        /// Devuelve un nodo simbólico con constante <c>C</c> (SymVar).
        /// </summary>
        public static SymNode SolveOde1(SymNode rhs, string yVar, string tVar)
        {
            rhs = rhs.Simplify();
            // Caso 1: rhs no depende de y → integración directa
            if (IsConstWrt(rhs, yVar))
            {
                var antider = Integrate(rhs, tVar).Simplify();
                return new SymAdd(antider, new SymVar("C")).Simplify();
            }
            // Caso 4: rhs = y → y = C*exp(t)
            if (rhs is SymVar v && v.Name == yVar)
                return new SymMul(new SymVar("C"),
                    new SymFunc("exp", new SymVar(tVar))).Simplify();
            // Caso 2/3: rhs = k*y o rhs = k*y + c
            // Descomponer: separar términos lineales en y (coeficiente k) y constante (c)
            var terms = new System.Collections.Generic.List<SymNode>();
            SymAdd.FlattenAdd(rhs, terms);
            // En cada término, buscar factor "y"
            SymNode kAcc = null;  // coeficiente de y (suma de coefs)
            SymNode cAcc = null;  // término constante (suma)
            foreach (var t in terms)
            {
                // Buscar y en t como factor
                bool hasY = !IsConstWrt(t, yVar);
                if (!hasY)
                {
                    cAcc = cAcc == null ? t : new SymAdd(cAcc, t);
                    continue;
                }
                // Si t es y solo → coef 1
                if (t is SymVar yt && yt.Name == yVar)
                {
                    kAcc = kAcc == null ? (SymNode)new SymConst(1) : new SymAdd(kAcc, new SymConst(1));
                    continue;
                }
                // Si t es Mul con y como uno de los factores
                if (t is SymMul mt)
                {
                    SymNode kPart = null;
                    bool found = false;
                    if (mt.A is SymVar yvA && yvA.Name == yVar && IsConstWrt(mt.B, yVar))
                    { kPart = mt.B; found = true; }
                    else if (mt.B is SymVar yvB && yvB.Name == yVar && IsConstWrt(mt.A, yVar))
                    { kPart = mt.A; found = true; }
                    if (found)
                    {
                        kAcc = kAcc == null ? kPart : new SymAdd(kAcc, kPart);
                        continue;
                    }
                }
                // No es ni constante puro ni lineal en y — patrón no soportado
                throw new MatlabRuntimeException(
                    $"dsolve: término no lineal en y no soportado: '{t.ToInfix()}'");
            }
            if (kAcc == null)
                throw new MatlabRuntimeException("dsolve: no se detectó coeficiente lineal en y");
            kAcc = kAcc.Simplify();
            // Solución general:
            // y' = k*y + c → y_h = C*exp(k*t); y_p = -c/k → y = -c/k + C*exp(k*t)
            var homog = new SymMul(new SymVar("C"),
                new SymFunc("exp", new SymMul(kAcc, new SymVar(tVar))));
            if (cAcc == null) return homog.Simplify();
            var particular = new SymDiv(
                new SymMul(new SymConst(-1), cAcc.Simplify()),
                kAcc);
            return new SymAdd(particular, homog).Simplify();
        }
        /// <summary>
        /// Detecta si <paramref name="expr"/> es de la forma <c>a·var + b</c> con a, b const respecto a var.
        /// Devuelve <c>(a, b)</c> si es lineal (a ≠ 0); <c>(null, null)</c> si no.
        /// Casos cubiertos: <c>var</c>, <c>a*var</c>, <c>var*a</c>, <c>a*var + b</c>, <c>a*var - b</c>, etc.
        /// </summary>
        public static (SymNode A, SymNode B) TryLinearForm(SymNode expr, string var)
        {
            // var por sí solo
            if (expr is SymVar v && v.Name == var)
                return (new SymConst(1), new SymConst(0));
            // a·var o var·a (a const wrt var)
            if (expr is SymMul m)
            {
                if (m.A is SymVar va && va.Name == var && IsConstWrt(m.B, var))
                    return (m.B, new SymConst(0));
                if (m.B is SymVar vb && vb.Name == var && IsConstWrt(m.A, var))
                    return (m.A, new SymConst(0));
            }
            // SymAdd: linear + const, o const + linear
            if (expr is SymAdd a)
            {
                if (IsConstWrt(a.A, var))
                {
                    var (aB, bB) = TryLinearForm(a.B, var);
                    if (aB != null) return (aB, new SymAdd(a.A, bB).Simplify());
                }
                if (IsConstWrt(a.B, var))
                {
                    var (aA, bA) = TryLinearForm(a.A, var);
                    if (aA != null) return (aA, new SymAdd(a.B, bA).Simplify());
                }
            }
            // SymSub: a*var - b o a - b*var
            if (expr is SymSub s)
            {
                if (IsConstWrt(s.A, var))
                {
                    var (aB, bB) = TryLinearForm(s.B, var);
                    if (aB != null) return (new SymMul(new SymConst(-1), aB).Simplify(), new SymSub(s.A, bB).Simplify());
                }
                if (IsConstWrt(s.B, var))
                {
                    var (aA, bA) = TryLinearForm(s.A, var);
                    if (aA != null) return (aA, new SymSub(bA, s.B).Simplify());
                }
            }
            return (null, null);
        }
        /// <summary>True si la expresión no depende de la variable <paramref name="var"/>.</summary>
        public static bool IsConstWrt(SymNode expr, string var)
        {
            switch (expr)
            {
                case SymConst _: return true;
                case SymVar v: return v.Name != var;
                case SymAdd a: return IsConstWrt(a.A, var) && IsConstWrt(a.B, var);
                case SymSub s: return IsConstWrt(s.A, var) && IsConstWrt(s.B, var);
                case SymMul m: return IsConstWrt(m.A, var) && IsConstWrt(m.B, var);
                case SymDiv d: return IsConstWrt(d.A, var) && IsConstWrt(d.B, var);
                case SymPow p: return IsConstWrt(p.Base, var) && IsConstWrt(p.Exp, var);
                case SymFunc f: return IsConstWrt(f.Arg, var);
                default: return false;
            }
        }
        /// <summary>Expansión de Taylor de orden <paramref name="N"/> alrededor de <paramref name="x0"/>.</summary>
        public static SymNode TaylorExpand(SymNode f, string var, double x0, int N)
        {
            // T_N(x) = Σ_{k=0..N} f^(k)(x0)/k! · (x-x0)^k
            SymNode result = new SymConst(0);
            SymNode deriv = f;
            double factK = 1;
            for (int k = 0; k <= N; k++)
            {
                if (k > 0) factK *= k;
                var coef = deriv.Subs(var, new SymConst(x0)).Simplify();
                var term = new SymMul(
                    new SymDiv(coef, new SymConst(factK)),
                    new SymPow(new SymSub(new SymVar(var), new SymConst(x0)), new SymConst(k))
                );
                result = new SymAdd(result, term);
                deriv = deriv.Diff(var).Simplify();
            }
            return result.Simplify();
        }
        /// <summary>
        /// Resuelve <c>expr = 0</c> simbólicamente para <paramref name="var"/>.
        /// MVP: ecuaciones polinómicas hasta grado 4 (Cardano para cúbicas, Ferrari para cuárticas).
        /// Devuelve lista de raíces reales (las complejas se descartan).
        /// </summary>
        public static List<double> SolvePoly(SymNode expr, string var)
        {
            var coefs = ExtractPolyCoefs(expr.Simplify(), var);
            if (coefs == null) throw new MatlabRuntimeException("solve: expresión no polinómica (MVP)");
            // Eliminar coeficientes de mayor orden si son 0
            int deg = coefs.Length - 1;
            while (deg > 0 && Math.Abs(coefs[deg]) < 1e-15) deg--;
            var roots = new List<double>();
            if (deg == 0) return roots;   // sin variable
            if (deg == 1)
            {
                roots.Add(-coefs[0] / coefs[1]);
                return roots;
            }
            if (deg == 2)
            {
                double a = coefs[2], b = coefs[1], c = coefs[0];
                double disc = b * b - 4 * a * c;
                if (disc < 0) return roots;
                double sq = Math.Sqrt(disc);
                roots.Add((-b + sq) / (2 * a));
                roots.Add((-b - sq) / (2 * a));
                return roots;
            }
            // Grado ≥ 3: usar Durand-Kerner numérico (raíces complejas también)
            return DurandKerner(coefs, deg);
        }
        private static List<double> DurandKerner(double[] coefs, int deg)
        {
            // Durand-Kerner para raíces de polinomio. Retorna solo raíces reales (filtrado).
            var rRe = new double[deg];
            var rIm = new double[deg];
            // Inicialización en círculo (radio = bound de Cauchy)
            double bound = 0;
            for (int i = 0; i < deg; i++) bound = Math.Max(bound, Math.Abs(coefs[i] / coefs[deg]));
            bound = 1 + bound;
            double angle = 0;
            for (int k = 0; k < deg; k++)
            {
                angle = (2 * Math.PI * k) / deg + 0.4;
                rRe[k] = bound * Math.Cos(angle) / 2;
                rIm[k] = bound * Math.Sin(angle) / 2;
            }
            for (int iter = 0; iter < 200; iter++)
            {
                bool converged = true;
                for (int k = 0; k < deg; k++)
                {
                    // Eval P(rk) y derivada Π(rk - rj) para j != k
                    double pRe = 0, pIm = 0;
                    for (int i = deg; i >= 0; i--)
                    {
                        double nRe = pRe * rRe[k] - pIm * rIm[k] + coefs[i];
                        double nIm = pRe * rIm[k] + pIm * rRe[k];
                        pRe = nRe; pIm = nIm;
                    }
                    double dRe = 1, dIm = 0;
                    for (int j = 0; j < deg; j++)
                    {
                        if (j == k) continue;
                        double dx = rRe[k] - rRe[j], dy = rIm[k] - rIm[j];
                        double nRe = dRe * dx - dIm * dy;
                        double nIm = dRe * dy + dIm * dx;
                        dRe = nRe; dIm = nIm;
                    }
                    double denom = dRe * dRe + dIm * dIm;
                    if (denom < 1e-30) continue;
                    double stepRe = (pRe * dRe + pIm * dIm) / denom;
                    double stepIm = (pIm * dRe - pRe * dIm) / denom;
                    rRe[k] -= stepRe; rIm[k] -= stepIm;
                    if (Math.Abs(stepRe) > 1e-12 || Math.Abs(stepIm) > 1e-12) converged = false;
                }
                if (converged) break;
            }
            var realRoots = new List<double>();
            for (int k = 0; k < deg; k++)
                if (Math.Abs(rIm[k]) < 1e-6) realRoots.Add(rRe[k]);
            realRoots.Sort();
            return realRoots;
        }
        /// <summary>Extrae coeficientes [c0, c1, c2, ...] de un polinomio en var. Null si no es polinómico.</summary>
        private static double[] ExtractPolyCoefs(SymNode expr, string var)
        {
            // Estrategia: usar Taylor en 0 hasta encontrar grado bajo. MVP: probar grados 0..6.
            // Aprovechamos que d^n f / dx^n evaluada en 0 = c_n · n!
            int maxDeg = 8;
            var coefs = new double[maxDeg + 1];
            SymNode current = expr;
            double factK = 1;
            for (int k = 0; k <= maxDeg; k++)
            {
                if (k > 0) factK *= k;
                var atZero = current.Subs(var, new SymConst(0)).Simplify();
                if (atZero is SymConst c) coefs[k] = c.Value / factK;
                else return null;  // no es polinomio
                current = current.Diff(var).Simplify();
            }
            return coefs;
        }
    }

    /// <summary>Wrapper de SymNode dentro de un MValue.</summary>
    public static class MatlabSym
    {
        /// <summary>Parse expresión symbolic-friendly desde un string.</summary>
        public static SymNode Parse(string expr, HashSet<string> symVars)
        {
            // Reusamos el parser MATLAB para tokenizar, luego construimos SymNode desde AST.
            var tokens = MatlabTokenizer.Tokenize(expr);
            var parser = new MatlabParser(tokens);
            var astExpr = parser.ParseExpression();
            return FromAst(astExpr, symVars);
        }
        public static SymNode FromAst(MatlabNode n, HashSet<string> symVars)
        {
            switch (n)
            {
                case NumberLit num: return new SymConst(num.Value);
                case IdentRef id:
                    if (id.Name == "pi") return new SymConst(Math.PI);
                    if (id.Name == "e") return new SymConst(Math.E);
                    return new SymVar(id.Name);
                case UnaryOp u when u.Op == "-": return new SymSub(new SymConst(0), FromAst(u.Operand, symVars));
                case UnaryOp u when u.Op == "+": return FromAst(u.Operand, symVars);
                case BinaryOp b:
                    var L = FromAst(b.Left, symVars);
                    var R = FromAst(b.Right, symVars);
                    return b.Op switch
                    {
                        "+" => new SymAdd(L, R),
                        "-" => new SymSub(L, R),
                        "*" or ".*" => new SymMul(L, R),
                        "/" or "./" => new SymDiv(L, R),
                        "^" or ".^" => new SymPow(L, R),
                        _ => throw new MatlabRuntimeException($"sym: operator '{b.Op}' not supported")
                    };
                case CallOrIndex c when c.Target is IdentRef fnName && c.Args.Count == 1:
                    return new SymFunc(fnName.Name, FromAst(c.Args[0], symVars));
                default:
                    throw new MatlabRuntimeException($"sym: cannot convert {n?.GetType().Name}");
            }
        }
    }
}
