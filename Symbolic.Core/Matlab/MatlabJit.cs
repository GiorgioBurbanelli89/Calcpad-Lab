// =============================================================================
// Calcpad Lab — JIT Phase 2 (Expression Trees + matrix indexing + function calls)
// =============================================================================
//   Compila for-loops a IL nativo via System.Linq.Expressions.
//
//   Patrones soportados en Phase 2:
//     for var = start:end
//         x      = a + b*c              ← scalar arithmetic (Phase 1)
//         A(i)   = scalar               ← matrix indexed write 1D
//         A(i,j) = scalar               ← matrix indexed write 2D
//         x      = A(i)                 ← matrix indexed read 1D
//         x      = A(i,j)               ← matrix indexed read 2D
//         x      = f(a, b, ...)         ← function call retornando escalar
//     end
//
//   Cualquier nodo no soportado (matrix arithmetic, strings, if/while
//   nested, break/continue) hace bail-out al intérprete.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Calcpad.Core.Matlab
{
    /// <summary>Contexto runtime del JIT: arrays de scalars + acceso a scope/evaluator.</summary>
    public sealed class JitCtx
    {
        public double[] Slots;
        public MatlabScope Scope;
        public MatlabEvaluator Evaluator;

        // ─── Métodos invocados desde el IL compilado ──────────────────────
        public double GetMatElem1(string name, double i)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            int idx = (int)i - 1;
            if (v.Rows == 1) return v.At(0, idx);
            if (v.Cols == 1) return v.At(idx, 0);
            return v.At(idx / v.Cols, idx % v.Cols);  // linear flat (column-major MATLAB convention)
        }

        public double GetMatElem2(string name, double i, double j)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            return v.At((int)i - 1, (int)j - 1);
        }

        public void SetMatElem1(string name, double i, double val)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            int idx = (int)i - 1;
            if (v.Rows == 1) v.Set(0, idx, val);
            else if (v.Cols == 1) v.Set(idx, 0, val);
            else v.Set(idx / v.Cols, idx % v.Cols, val);
        }

        public void SetMatElem2(string name, double i, double j, double val)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            v.Set((int)i - 1, (int)j - 1, val);
        }

        public double CallScalar(string name, double[] args)
        {
            var mArgs = new MValue[args.Length];
            for (int i = 0; i < args.Length; i++) mArgs[i] = new MValue(args[i]);
            var r = Evaluator.JitCall(name, mArgs);
            return r.IsScalar ? r.Scalar : 0;
        }

        // ─── MethodInfo handles (preresueltos en static ctor) ─────────────
        internal static readonly MethodInfo MGetMatElem1 = typeof(JitCtx).GetMethod(nameof(GetMatElem1));
        internal static readonly MethodInfo MGetMatElem2 = typeof(JitCtx).GetMethod(nameof(GetMatElem2));
        internal static readonly MethodInfo MSetMatElem1 = typeof(JitCtx).GetMethod(nameof(SetMatElem1));
        internal static readonly MethodInfo MSetMatElem2 = typeof(JitCtx).GetMethod(nameof(SetMatElem2));
        internal static readonly MethodInfo MCallScalar  = typeof(JitCtx).GetMethod(nameof(CallScalar));
        internal static readonly FieldInfo  FSlots       = typeof(JitCtx).GetField(nameof(Slots));
    }

    public static class MatlabJit
    {
        public static bool Enabled =
            System.Environment.GetEnvironmentVariable("CALCPAD_LAB_JIT") != "0";

        public static long Hits, Compiles, Skips;

        private struct Compiled
        {
            public Action<JitCtx> Body;
            public string[] ScalarNames;     // slot[i] ↔ scalar var name
            public int IterIdx;
            public bool Failed;
        }

        private static readonly Dictionary<ForLoop, Compiled> _cache = new();

        public static bool TryExecute(ForLoop loop, MatlabScope scope, MatlabEvaluator ev)
        {
            if (!Enabled) return false;

            if (!_cache.TryGetValue(loop, out var c))
            {
                c = TryCompile(loop, ev);
                _cache[loop] = c;
                if (!c.Failed) Compiles++;
            }
            if (c.Failed) { Skips++; return false; }

            var range = (Range)loop.Iter;
            if (!TryEvalScalar(range.Start, scope, out double startVal)) { Skips++; return false; }
            if (!TryEvalScalar(range.End,   scope, out double endVal))   { Skips++; return false; }

            // Construir contexto + sync de scalares desde scope
            var slots = new double[c.ScalarNames.Length];
            for (int k = 0; k < c.ScalarNames.Length; k++)
            {
                if (k == c.IterIdx) continue;
                if (scope.TryGet(c.ScalarNames[k], out var v) && v.IsScalar)
                    slots[k] = v.Scalar;
            }
            var ctx = new JitCtx { Slots = slots, Scope = scope, Evaluator = ev };

            // ─── HOT LOOP — IL nativo ───────────────────────────────────
            int iStart = (int)startVal;
            int iEnd   = (int)endVal;
            try
            {
                for (int i = iStart; i <= iEnd; i++)
                {
                    slots[c.IterIdx] = i;
                    c.Body(ctx);
                }
            }
            catch (ContinueSignal) { /* no soportado en JIT por ahora */ }
            catch (BreakSignal) { /* idem */ }
            // Cualquier otra excepcion (Undefined, indices OOB) la dejamos burbujear

            // Sync scalares de vuelta al scope
            for (int k = 0; k < c.ScalarNames.Length; k++)
                scope.Set(c.ScalarNames[k], new MValue(slots[k]));

            Hits++;
            return true;
        }

        // ─── Compile ─────────────────────────────────────────────────────
        private sealed class CompileCtx
        {
            public MatlabEvaluator Evaluator;
            public Dictionary<string, int> SlotIdx = new(StringComparer.Ordinal);
            public HashSet<string> MatrixVars = new(StringComparer.Ordinal);
            public ParameterExpression CtxParam;
            public MemberExpression SlotsExpr;
        }

        private static Compiled TryCompile(ForLoop loop, MatlabEvaluator ev)
        {
            if (loop.Iter is not Range range || range.Step != null)
                return new Compiled { Failed = true };

            try
            {
                var cc = new CompileCtx { Evaluator = ev };
                cc.CtxParam = Expression.Parameter(typeof(JitCtx), "ctx");
                cc.SlotsExpr = Expression.Field(cc.CtxParam, JitCtx.FSlots);

                // 1. Pre-scan: collect scalar names (LHS de assignments con IdentRef)
                //    + identifiers usados como scalars en RHS.
                AddScalar(cc, loop.VarName);   // iter var siempre es scalar
                if (!ScanBody(loop.Body, cc)) return new Compiled { Failed = true };

                // 2. Build Expression body
                var body = new List<Expression>();
                foreach (var stmt in loop.Body)
                {
                    if (stmt is CommentStmt) continue;
                    var e = ConvertStmt(stmt, cc);
                    if (e == null) return new Compiled { Failed = true };
                    body.Add(e);
                }
                if (body.Count == 0) return new Compiled { Failed = true };

                var block = Expression.Block(body);
                var lambda = Expression.Lambda<Action<JitCtx>>(block, cc.CtxParam);
                var compiled = lambda.Compile();

                var names = new string[cc.SlotIdx.Count];
                foreach (var kv in cc.SlotIdx) names[kv.Value] = kv.Key;

                return new Compiled
                {
                    Body = compiled,
                    ScalarNames = names,
                    IterIdx = cc.SlotIdx[loop.VarName],
                    Failed = false,
                };
            }
            catch
            {
                return new Compiled { Failed = true };
            }
        }

        // ─── Scan pass: clasifica cada IdentRef como scalar o matrix ──────
        private static bool ScanBody(IEnumerable<MatlabNode> stmts, CompileCtx cc)
        {
            foreach (var s in stmts) if (!ScanNode(s, cc, lhs: false)) return false;
            return true;
        }
        private static bool ScanNode(MatlabNode node, CompileCtx cc, bool lhs)
        {
            switch (node)
            {
                case Assignment a:
                    if (a.Targets.Count != 1) return false;
                    if (!ScanNode(a.Targets[0], cc, lhs: true)) return false;
                    return ScanNode(a.Rhs, cc, lhs: false);
                case ExprStmt es:
                    return ScanNode(es.Expr, cc, lhs: false);
                case IdentRef ir:
                    if (lhs) AddScalar(cc, ir.Name);
                    else
                    {
                        // Si ya es matrix o el evaluator no la tiene como fn,
                        // asumimos scalar. Si la usan tambien como matrix indirectamente
                        // se descubre cuando se procesan CallOrIndex.
                        if (!cc.MatrixVars.Contains(ir.Name)) AddScalar(cc, ir.Name);
                    }
                    return true;
                case CallOrIndex coi:
                    if (coi.Target is not IdentRef tgt) return false;
                    // Si es user function o builtin → es call (no es matrix var)
                    bool isFn = IsFunction(cc, tgt.Name);
                    if (!isFn) cc.MatrixVars.Add(tgt.Name);
                    foreach (var arg in coi.Args)
                        if (!ScanNode(arg, cc, lhs: false)) return false;
                    return true;
                case BinaryOp b:
                    return ScanNode(b.Left, cc, lhs: false) && ScanNode(b.Right, cc, lhs: false);
                case UnaryOp u when u.Op == "-" || u.Op == "+":
                    return ScanNode(u.Operand, cc, lhs: false);
                case NumberLit _:
                    return true;
                case CommentStmt _:
                    return true;
                default:
                    return false;
            }
        }
        private static bool IsFunction(CompileCtx cc, string name)
            => cc.Evaluator.JitIsFunction(name);
        private static void AddScalar(CompileCtx cc, string name)
        {
            if (cc.MatrixVars.Contains(name)) return;
            if (cc.SlotIdx.ContainsKey(name)) return;
            cc.SlotIdx[name] = cc.SlotIdx.Count;
        }

        // ─── Statement convert ───────────────────────────────────────────
        private static Expression ConvertStmt(MatlabNode stmt, CompileCtx cc)
        {
            switch (stmt)
            {
                case Assignment a when a.Targets.Count == 1:
                    var tgt = a.Targets[0];
                    var rhs = ConvertExpr(a.Rhs, cc);
                    if (rhs == null) return null;
                    if (rhs.Type != typeof(double)) rhs = Expression.Convert(rhs, typeof(double));

                    if (tgt is IdentRef ir)
                    {
                        if (!cc.SlotIdx.TryGetValue(ir.Name, out var idx)) return null;
                        var slot = Expression.ArrayAccess(cc.SlotsExpr, Expression.Constant(idx));
                        return Expression.Assign(slot, rhs);
                    }
                    if (tgt is CallOrIndex tgtCall && tgtCall.Target is IdentRef matIdent)
                    {
                        cc.MatrixVars.Add(matIdent.Name);
                        if (tgtCall.Args.Count == 1)
                        {
                            var idx1 = ConvertExpr(tgtCall.Args[0], cc);
                            if (idx1 == null) return null;
                            return Expression.Call(cc.CtxParam, JitCtx.MSetMatElem1,
                                Expression.Constant(matIdent.Name), idx1, rhs);
                        }
                        if (tgtCall.Args.Count == 2)
                        {
                            var idx1 = ConvertExpr(tgtCall.Args[0], cc);
                            var idx2 = ConvertExpr(tgtCall.Args[1], cc);
                            if (idx1 == null || idx2 == null) return null;
                            return Expression.Call(cc.CtxParam, JitCtx.MSetMatElem2,
                                Expression.Constant(matIdent.Name), idx1, idx2, rhs);
                        }
                        return null;
                    }
                    return null;
                case ExprStmt _:
                    return Expression.Empty();
                default:
                    return null;
            }
        }

        // ─── Expression convert ──────────────────────────────────────────
        private static Expression ConvertExpr(MatlabNode node, CompileCtx cc)
        {
            switch (node)
            {
                case NumberLit nl:
                    return Expression.Constant(nl.Value, typeof(double));
                case IdentRef ir:
                    if (!cc.SlotIdx.TryGetValue(ir.Name, out var k)) return null;
                    return Expression.ArrayAccess(cc.SlotsExpr, Expression.Constant(k));
                case CallOrIndex coi when coi.Target is IdentRef ident:
                    return ConvertCallOrIndex(ident.Name, coi.Args, cc);
                case UnaryOp u when u.Op == "-":
                    var op = ConvertExpr(u.Operand, cc);
                    return op == null ? null : Expression.Negate(op);
                case UnaryOp u when u.Op == "+":
                    return ConvertExpr(u.Operand, cc);
                case BinaryOp b:
                    var L = ConvertExpr(b.Left, cc);
                    var R = ConvertExpr(b.Right, cc);
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

        private static Expression ConvertCallOrIndex(string name, List<MatlabNode> args, CompileCtx cc)
        {
            // ¿Es función o indexing de matriz?
            bool isFn = IsFunction(cc, name);
            if (isFn)
            {
                // call f(a1, a2, ...) — todos los args deben ser scalars convertibles
                var argExprs = new Expression[args.Count];
                for (int i = 0; i < args.Count; i++)
                {
                    var e = ConvertExpr(args[i], cc);
                    if (e == null) return null;
                    if (e.Type != typeof(double)) e = Expression.Convert(e, typeof(double));
                    argExprs[i] = e;
                }
                var arr = Expression.NewArrayInit(typeof(double), argExprs);
                return Expression.Call(cc.CtxParam, JitCtx.MCallScalar,
                    Expression.Constant(name), arr);
            }
            else
            {
                // Matrix indexing
                cc.MatrixVars.Add(name);
                if (args.Count == 1)
                {
                    var idx1 = ConvertExpr(args[0], cc);
                    if (idx1 == null) return null;
                    return Expression.Call(cc.CtxParam, JitCtx.MGetMatElem1,
                        Expression.Constant(name), idx1);
                }
                if (args.Count == 2)
                {
                    var idx1 = ConvertExpr(args[0], cc);
                    var idx2 = ConvertExpr(args[1], cc);
                    if (idx1 == null || idx2 == null) return null;
                    return Expression.Call(cc.CtxParam, JitCtx.MGetMatElem2,
                        Expression.Constant(name), idx1, idx2);
                }
                return null;
            }
        }

        // ─── Scalar eval para los límites del rango ───────────────────────
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
                    if (!TryEvalScalar(b.Left,  scope, out var lv)) return false;
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
