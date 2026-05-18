// =============================================================================
// Calcpad Lab — JIT Phase 1 (Expression Trees)
// =============================================================================
//   Compila for-loops escalares a IL nativo via System.Linq.Expressions.
//   Cachea por identidad de AST. Fallback al intérprete si el patrón no es
//   soportado.
//
//   Patrón soportado (PoC inicial):
//     for var = start:end
//         a = b + c*d - e^f      ← Assignment con IdentRef target
//         x = sin(y)              ← (futuro: builtin scalar dispatch)
//     end
//
//   Reglas:
//     - Iter debe ser Range con Step null (default 1)
//     - start/end deben ser escalares evaluables (NumberLit, IdentRef escalar)
//     - Body: solo Assignment con IdentRef target. RHS: NumberLit, IdentRef,
//       BinaryOp con + - * / ^ .* ./ .^ , UnaryOp con + -
//     - NO break/continue, NO if/while/for anidados, NO matrices, NO strings
//
//   Cualquier cosa fuera de esto = bail-out a intérprete normal.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Calcpad.Core.Matlab
{
    public static class MatlabJit
    {
        /// <summary>Master switch del JIT. False = todos los loops via intérprete.
        /// Override por env var: CALCPAD_LAB_JIT=0 desactiva.</summary>
        public static bool Enabled =
            System.Environment.GetEnvironmentVariable("CALCPAD_LAB_JIT") != "0";

        /// <summary>Métricas para profiling.</summary>
        public static long Hits;
        public static long Compiles;
        public static long Skips;

        private struct Compiled
        {
            public Action<double[]> Body;   // IL compilado
            public string[] VarNames;       // slot[i] ↔ var name
            public int IterIdx;             // posición del iter var en slots
            public bool Failed;             // marca "no compilable" para evitar reintentos
        }

        // Cache por identidad de AST (ForLoop instance es persistente entre runs)
        private static readonly Dictionary<ForLoop, Compiled> _cache = new();

        public static bool TryExecute(ForLoop loop, MatlabScope scope)
        {
            if (!Enabled) return false;

            if (!_cache.TryGetValue(loop, out var c))
            {
                c = TryCompile(loop);
                _cache[loop] = c;
                if (!c.Failed) Compiles++;
            }
            if (c.Failed) { Skips++; return false; }

            // Resolver start/end del rango
            var range = (Range)loop.Iter;
            if (!TryEvalScalar(range.Start, scope, out double startVal)) { Skips++; return false; }
            if (!TryEvalScalar(range.End,   scope, out double endVal))   { Skips++; return false; }

            // Slot array para el hot loop
            var slots = new double[c.VarNames.Length];
            for (int k = 0; k < c.VarNames.Length; k++)
            {
                if (k == c.IterIdx) continue;
                if (scope.TryGet(c.VarNames[k], out var v) && v.IsScalar)
                    slots[k] = v.Scalar;
            }

            // ─── HOT LOOP — IL nativo (no interpretado) ─────────────────────
            int iStart = (int)startVal;
            int iEnd   = (int)endVal;
            for (int i = iStart; i <= iEnd; i++)
            {
                slots[c.IterIdx] = i;
                c.Body(slots);
            }

            // Sync de vuelta al scope
            for (int k = 0; k < c.VarNames.Length; k++)
                scope.Set(c.VarNames[k], new MValue(slots[k]));

            Hits++;
            return true;
        }

        // ─── Compile ─────────────────────────────────────────────────────────
        private static Compiled TryCompile(ForLoop loop)
        {
            // Requisito: iter = Range, step null (default 1)
            if (loop.Iter is not Range range || range.Step != null)
                return new Compiled { Failed = true };

            try
            {
                // 1. Recoger todos los identifiers usados en el body
                var names = new List<string> { loop.VarName };
                CollectNames(loop.Body, names);

                // 2. Mapa name → slot index
                var idx = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int k = 0; k < names.Count; k++) idx[names[k]] = k;

                // 3. Construir el Expression body
                var slotsParam = Expression.Parameter(typeof(double[]), "slots");
                var body = new List<Expression>();
                foreach (var stmt in loop.Body)
                {
                    var e = ConvertStmt(stmt, slotsParam, idx);
                    if (e == null) return new Compiled { Failed = true };
                    body.Add(e);
                }
                if (body.Count == 0) return new Compiled { Failed = true };

                var block = Expression.Block(body);
                var lambda = Expression.Lambda<Action<double[]>>(block, slotsParam);
                var compiled = lambda.Compile();

                return new Compiled
                {
                    Body = compiled,
                    VarNames = names.ToArray(),
                    IterIdx = idx[loop.VarName],
                    Failed = false,
                };
            }
            catch
            {
                return new Compiled { Failed = true };
            }
        }

        // ─── AST helpers ─────────────────────────────────────────────────────
        private static void CollectNames(IEnumerable<MatlabNode> stmts, List<string> names)
        {
            foreach (var s in stmts) CollectNamesIn(s, names);
        }
        private static void CollectNamesIn(MatlabNode node, List<string> names)
        {
            switch (node)
            {
                case Assignment a:
                    foreach (var t in a.Targets)
                        if (t is IdentRef tgt) AddName(names, tgt.Name);
                        else throw new NotSupportedException();
                    CollectNamesIn(a.Rhs, names);
                    break;
                case ExprStmt es:
                    CollectNamesIn(es.Expr, names);
                    break;
                case IdentRef ir:
                    AddName(names, ir.Name);
                    break;
                case BinaryOp b:
                    CollectNamesIn(b.Left, names);
                    CollectNamesIn(b.Right, names);
                    break;
                case UnaryOp u when u.Op == "-" || u.Op == "+":
                    CollectNamesIn(u.Operand, names);
                    break;
                case NumberLit _:
                    break;
                default:
                    throw new NotSupportedException();   // node desconocido → bail-out
            }
        }
        private static void AddName(List<string> names, string n)
        {
            if (!names.Contains(n)) names.Add(n);
        }

        // ─── Convert AST node → Expression ───────────────────────────────────
        private static Expression ConvertStmt(MatlabNode stmt, ParameterExpression slots, Dictionary<string,int> idx)
        {
            switch (stmt)
            {
                case Assignment a when a.Targets.Count == 1 && a.Targets[0] is IdentRef tgt:
                    var rhs = ConvertExpr(a.Rhs, slots, idx);
                    if (rhs == null) return null;
                    var slot = Expression.ArrayAccess(slots, Expression.Constant(idx[tgt.Name]));
                    return Expression.Assign(slot, rhs);
                case ExprStmt _:
                    return Expression.Empty();   // no side effect en escalar
                default:
                    return null;
            }
        }

        private static Expression ConvertExpr(MatlabNode node, ParameterExpression slots, Dictionary<string,int> idx)
        {
            switch (node)
            {
                case NumberLit nl:
                    return Expression.Constant(nl.Value, typeof(double));
                case IdentRef ir:
                    if (!idx.TryGetValue(ir.Name, out var k)) return null;
                    return Expression.ArrayAccess(slots, Expression.Constant(k));
                case UnaryOp u when u.Op == "-":
                    var op = ConvertExpr(u.Operand, slots, idx);
                    return op == null ? null : Expression.Negate(op);
                case UnaryOp u when u.Op == "+":
                    return ConvertExpr(u.Operand, slots, idx);
                case BinaryOp b:
                    var L = ConvertExpr(b.Left,  slots, idx);
                    var R = ConvertExpr(b.Right, slots, idx);
                    if (L == null || R == null) return null;
                    return b.Op switch
                    {
                        "+"          => Expression.Add(L, R),
                        "-"          => Expression.Subtract(L, R),
                        "*"  or ".*" => Expression.Multiply(L, R),
                        "/"  or "./" => Expression.Divide(L, R),
                        "^"  or ".^" => Expression.Power(L, R),
                        _            => null,
                    };
                default:
                    return null;
            }
        }

        // ─── Scalar eval para los límites del rango ──────────────────────────
        private static bool TryEvalScalar(MatlabNode node, MatlabScope scope, out double val)
        {
            val = 0;
            switch (node)
            {
                case NumberLit nl: val = nl.Value; return true;
                case IdentRef ir:
                    if (scope.TryGet(ir.Name, out var v) && v.IsScalar) { val = v.Scalar; return true; }
                    return false;
                case UnaryOp u when u.Op == "-":
                    if (TryEvalScalar(u.Operand, scope, out var inner)) { val = -inner; return true; }
                    return false;
                case BinaryOp b:
                    if (!TryEvalScalar(b.Left, scope, out var lv)) return false;
                    if (!TryEvalScalar(b.Right, scope, out var rv)) return false;
                    val = b.Op switch
                    {
                        "+" => lv + rv,
                        "-" => lv - rv,
                        "*" or ".*" => lv * rv,
                        "/" or "./" => lv / rv,
                        _ => double.NaN,
                    };
                    return !double.IsNaN(val);
                default:
                    return false;
            }
        }
    }
}
