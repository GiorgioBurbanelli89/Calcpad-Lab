// =============================================================================
// Calcpad Lab — JIT Phase 3 (Expression Trees + matrix arithmetic + matrix literals)
// =============================================================================
//   Compila for-loops a IL nativo via System.Linq.Expressions con soporte para:
//
//   Phase 1:  scalar arithmetic                     (a = b + c * d)
//   Phase 2:  matrix indexing scalar                (A(i,j) = x, x = A(i,j))
//             + function calls returning scalar     (x = f(a, b))
//   Phase 3:  + matrix literals                     (pa = [a, b, c, d])
//             + matrix arithmetic                   (C = A * B, C = -A, C = A')
//             + function calls returning matrix     (Bm = B_mat(...))
//             + scalar/matrix mixed                 (s * M, M * s)
//
//   Cualquier nodo no soportado → bail-out al intérprete.
//
//   Diseño:
//   - Pre-pass clasifica cada variable como scalar (double slot) o matrix
//     (MValue lookup en scope). El tipo se infiere de la RHS de la 1a
//     asignación o del uso (LHS de indexing → matrix).
//   - ConvertExpr produce Expression con .Type = double o MValue según
//     la clasificación. Conversiones explícitas via JitMatToScalar /
//     new MValue(scalar) cuando hay mismatch.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Calcpad.Core.Matlab
{
    public sealed class JitCtx
    {
        public double[] Slots;           // scalar slots
        public MatlabScope Scope;        // matrix vars + scope para function dispatch
        public MatlabEvaluator Evaluator;

        // ─── Matrix indexing scalar ─────────────────────────────────────────
        public double GetMatElem1(string name, double i)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            int idx = (int)i - 1;
            if (v.Rows == 1) return v.At(0, idx);
            if (v.Cols == 1) return v.At(idx, 0);
            return v.At(idx / v.Cols, idx % v.Cols);
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

        // ─── Function call ──────────────────────────────────────────────────
        public double CallScalar(string name, double[] args)
        {
            var mArgs = new MValue[args.Length];
            for (int i = 0; i < args.Length; i++) mArgs[i] = new MValue(args[i]);
            return MatlabEvaluator.JitMatToScalar(Evaluator.JitCall(name, mArgs));
        }
        public MValue CallMatrix(string name, double[] args)
        {
            var mArgs = new MValue[args.Length];
            for (int i = 0; i < args.Length; i++) mArgs[i] = new MValue(args[i]);
            return Evaluator.JitCall(name, mArgs);
        }

        // ─── Matrix variable access ────────────────────────────────────────
        public MValue GetMatrixVar(string name)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            return v;
        }
        public void SetMatrixVar(string name, MValue val) => Scope.Set(name, val);

        // ─── MethodInfo handles (pre-resueltos) ───────────────────────────
        internal static readonly MethodInfo MGetMatElem1 = typeof(JitCtx).GetMethod(nameof(GetMatElem1));
        internal static readonly MethodInfo MGetMatElem2 = typeof(JitCtx).GetMethod(nameof(GetMatElem2));
        internal static readonly MethodInfo MSetMatElem1 = typeof(JitCtx).GetMethod(nameof(SetMatElem1));
        internal static readonly MethodInfo MSetMatElem2 = typeof(JitCtx).GetMethod(nameof(SetMatElem2));
        internal static readonly MethodInfo MCallScalar  = typeof(JitCtx).GetMethod(nameof(CallScalar));
        internal static readonly MethodInfo MCallMatrix  = typeof(JitCtx).GetMethod(nameof(CallMatrix));
        internal static readonly MethodInfo MGetMatVar   = typeof(JitCtx).GetMethod(nameof(GetMatrixVar));
        internal static readonly MethodInfo MSetMatVar   = typeof(JitCtx).GetMethod(nameof(SetMatrixVar));
        internal static readonly FieldInfo  FSlots       = typeof(JitCtx).GetField(nameof(Slots));

        // ─── Matrix ops (static methods en MatlabEvaluator) ───────────────
        internal static readonly MethodInfo MMatMul        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatMul));
        internal static readonly MethodInfo MMatAdd        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatAdd));
        internal static readonly MethodInfo MMatSub        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatSub));
        internal static readonly MethodInfo MMatTrans      = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatTrans));
        internal static readonly MethodInfo MMatNeg        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatNeg));
        internal static readonly MethodInfo MMatScalarMul  = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatScalarMul));
        internal static readonly MethodInfo MMakeRowVec    = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMakeRowVec));
        internal static readonly MethodInfo MMatToScalar   = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatToScalar));
        internal static readonly ConstructorInfo CMValueScalar = typeof(MValue).GetConstructor(new[] { typeof(double) });
    }

    public static class MatlabJit
    {
        public static bool Enabled =
            System.Environment.GetEnvironmentVariable("CALCPAD_LAB_JIT") != "0";
        public static long Hits, Compiles, Skips;

        private struct Compiled
        {
            public Action<JitCtx> Body;
            public string[] ScalarNames;
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

            var slots = new double[c.ScalarNames.Length];
            for (int k = 0; k < c.ScalarNames.Length; k++)
            {
                if (k == c.IterIdx) continue;
                if (scope.TryGet(c.ScalarNames[k], out var v) && v.IsScalar)
                    slots[k] = v.Scalar;
            }
            var ctx = new JitCtx { Slots = slots, Scope = scope, Evaluator = ev };

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
            catch (ContinueSignal) { }
            catch (BreakSignal) { }

            for (int k = 0; k < c.ScalarNames.Length; k++)
                scope.Set(c.ScalarNames[k], new MValue(slots[k]));

            Hits++;
            return true;
        }

        // ─── Compile context con clasificación de tipos ─────────────────
        /// <summary>Tipo inferido de una expresión / variable.</summary>
        private enum TKind { Scalar, Matrix }

        private sealed class CompileCtx
        {
            public MatlabEvaluator Evaluator;
            public Dictionary<string, int> SlotIdx = new(StringComparer.Ordinal);
            public Dictionary<string, TKind> VarKind = new(StringComparer.Ordinal);
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

                // Pass 1: clasificar variables
                cc.VarKind[loop.VarName] = TKind.Scalar;
                if (!ClassifyBody(loop.Body, cc)) return new Compiled { Failed = true };

                // Asignar slot index a las scalar vars
                foreach (var kv in cc.VarKind)
                    if (kv.Value == TKind.Scalar && !cc.SlotIdx.ContainsKey(kv.Key))
                        cc.SlotIdx[kv.Key] = cc.SlotIdx.Count;

                // Pass 2: emitir Expressions
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

        // ─── Pass 1: classification ──────────────────────────────────────
        private static bool ClassifyBody(IEnumerable<MatlabNode> stmts, CompileCtx cc)
        {
            foreach (var s in stmts) if (!ClassifyStmt(s, cc)) return false;
            return true;
        }
        private static bool ClassifyStmt(MatlabNode stmt, CompileCtx cc)
        {
            switch (stmt)
            {
                case CommentStmt _: return true;
                case Assignment a when a.Targets.Count == 1:
                    var rhsKind = InferKind(a.Rhs, cc);
                    if (rhsKind == null) return false;
                    var tgt = a.Targets[0];
                    if (tgt is IdentRef ir)
                        return SetKind(cc, ir.Name, rhsKind.Value);
                    if (tgt is CallOrIndex tgtCall && tgtCall.Target is IdentRef matRef)
                    {
                        // A(i,j) = x : A es matrix var
                        if (!SetKind(cc, matRef.Name, TKind.Matrix)) return false;
                        foreach (var arg in tgtCall.Args)
                            if (InferKind(arg, cc) == null) return false;
                        return true;
                    }
                    return false;
                case ExprStmt es:
                    return InferKind(es.Expr, cc) != null;
                default:
                    return false;
            }
        }

        private static bool SetKind(CompileCtx cc, string name, TKind kind)
        {
            if (cc.VarKind.TryGetValue(name, out var existing))
                return existing == kind;   // conflict si cambia
            cc.VarKind[name] = kind;
            return true;
        }

        private static TKind? InferKind(MatlabNode node, CompileCtx cc)
        {
            switch (node)
            {
                case NumberLit _: return TKind.Scalar;
                case IdentRef ir:
                    if (cc.VarKind.TryGetValue(ir.Name, out var k)) return k;
                    // Variable no asignada antes — asumimos scalar (live-in)
                    cc.VarKind[ir.Name] = TKind.Scalar;
                    return TKind.Scalar;
                case UnaryOp u when u.Op == "-" || u.Op == "+":
                    return InferKind(u.Operand, cc);
                case UnaryOp u when u.Op == "'" || u.Op == ".'":
                    var t = InferKind(u.Operand, cc);
                    return t == null ? null : TKind.Matrix;
                case BinaryOp b:
                    var L = InferKind(b.Left, cc);
                    var R = InferKind(b.Right, cc);
                    if (L == null || R == null) return null;
                    // Mixed scalar/matrix → matrix
                    if (L == TKind.Matrix || R == TKind.Matrix) return TKind.Matrix;
                    return TKind.Scalar;
                case CallOrIndex coi when coi.Target is IdentRef ident:
                    if (cc.Evaluator.JitIsFunction(ident.Name))
                    {
                        // Función: heurística — si retorna matrix detectaremos a runtime.
                        // Para clasificacion, miramos al primer arg / uso. Default: scalar
                        // si se asigna a un scalar slot conocido; matrix si vemos matrix lit
                        // o B_mat / N_vec naming. Para simplicidad: matrix si el nombre
                        // empieza con mayuscula O contiene "mat"/"vec"/"_vec". Sino scalar.
                        return GuessFnKind(ident.Name);
                    }
                    // Indexing de matriz
                    if (!SetKind(cc, ident.Name, TKind.Matrix)) return null;
                    foreach (var arg in coi.Args)
                        if (InferKind(arg, cc) == null) return null;
                    // M(i,j) o M(i) → scalar (asumimos índices escalares = single element)
                    return TKind.Scalar;
                case MatrixLit ml:
                    foreach (var row in ml.Rows)
                        foreach (var el in row)
                            if (InferKind(el, cc) == null) return null;
                    return TKind.Matrix;
                default:
                    return null;
            }
        }

        private static TKind GuessFnKind(string name)
        {
            // Heurística simple: si el nombre sugiere matrix → matrix; sino scalar.
            // Lista común: B_mat, N_vec, K_mat, zeros, ones, eye, transpose, etc.
            // Cualquier función con args múltiples y nombre tipo "*_mat" o "*_vec" → matrix.
            string lo = name.ToLowerInvariant();
            if (lo.EndsWith("_mat") || lo.EndsWith("_vec") || lo == "zeros" || lo == "ones"
                || lo == "eye" || lo == "transpose" || lo == "inv" || lo == "diag")
                return TKind.Matrix;
            return TKind.Scalar;
        }

        // ─── Pass 2: statement → Expression ──────────────────────────────
        private static Expression ConvertStmt(MatlabNode stmt, CompileCtx cc)
        {
            switch (stmt)
            {
                case Assignment a when a.Targets.Count == 1:
                    var tgt = a.Targets[0];
                    if (tgt is IdentRef ir)
                    {
                        var rhsKind = cc.VarKind[ir.Name];
                        var rhs = ConvertExprAsKind(a.Rhs, cc, rhsKind);
                        if (rhs == null) return null;
                        if (rhsKind == TKind.Scalar)
                        {
                            if (!cc.SlotIdx.TryGetValue(ir.Name, out var idx)) return null;
                            var slot = Expression.ArrayAccess(cc.SlotsExpr, Expression.Constant(idx));
                            return Expression.Assign(slot, rhs);
                        }
                        else
                        {
                            return Expression.Call(cc.CtxParam, JitCtx.MSetMatVar,
                                Expression.Constant(ir.Name), rhs);
                        }
                    }
                    if (tgt is CallOrIndex tgtCall && tgtCall.Target is IdentRef matIdent)
                    {
                        var rhs = ConvertExprAsKind(a.Rhs, cc, TKind.Scalar);
                        if (rhs == null) return null;
                        if (tgtCall.Args.Count == 1)
                        {
                            var idx1 = ConvertExprAsKind(tgtCall.Args[0], cc, TKind.Scalar);
                            if (idx1 == null) return null;
                            return Expression.Call(cc.CtxParam, JitCtx.MSetMatElem1,
                                Expression.Constant(matIdent.Name), idx1, rhs);
                        }
                        if (tgtCall.Args.Count == 2)
                        {
                            var idx1 = ConvertExprAsKind(tgtCall.Args[0], cc, TKind.Scalar);
                            var idx2 = ConvertExprAsKind(tgtCall.Args[1], cc, TKind.Scalar);
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

        // ─── Convert con coerción de tipo ───────────────────────────────
        private static Expression ConvertExprAsKind(MatlabNode node, CompileCtx cc, TKind want)
        {
            var have = InferKind(node, cc);
            if (have == null) return null;
            var e = ConvertExpr(node, cc);
            if (e == null) return null;
            // Coerción
            if (have == TKind.Scalar && want == TKind.Matrix)
                return Expression.New(JitCtx.CMValueScalar, e);
            if (have == TKind.Matrix && want == TKind.Scalar)
                return Expression.Call(JitCtx.MMatToScalar, e);
            return e;
        }

        private static Expression ConvertExpr(MatlabNode node, CompileCtx cc)
        {
            switch (node)
            {
                case NumberLit nl:
                    return Expression.Constant(nl.Value, typeof(double));
                case IdentRef ir:
                    {
                        var k = cc.VarKind[ir.Name];
                        if (k == TKind.Scalar)
                        {
                            if (!cc.SlotIdx.TryGetValue(ir.Name, out var idx)) return null;
                            return Expression.ArrayAccess(cc.SlotsExpr, Expression.Constant(idx));
                        }
                        return Expression.Call(cc.CtxParam, JitCtx.MGetMatVar,
                            Expression.Constant(ir.Name));
                    }
                case UnaryOp u when u.Op == "-":
                    {
                        var k = InferKind(u.Operand, cc);
                        var op = ConvertExpr(u.Operand, cc);
                        if (op == null) return null;
                        if (k == TKind.Scalar) return Expression.Negate(op);
                        return Expression.Call(JitCtx.MMatNeg, op);
                    }
                case UnaryOp u when u.Op == "+":
                    return ConvertExpr(u.Operand, cc);
                case UnaryOp u when u.Op == "'" || u.Op == ".'":
                    {
                        var op = ConvertExprAsKind(u.Operand, cc, TKind.Matrix);
                        if (op == null) return null;
                        return Expression.Call(JitCtx.MMatTrans, op);
                    }
                case BinaryOp b:
                    {
                        var kL = InferKind(b.Left, cc);
                        var kR = InferKind(b.Right, cc);
                        if (kL == null || kR == null) return null;
                        bool bothScalar = kL == TKind.Scalar && kR == TKind.Scalar;
                        if (bothScalar)
                        {
                            var Le = ConvertExpr(b.Left, cc);
                            var Re = ConvertExpr(b.Right, cc);
                            if (Le == null || Re == null) return null;
                            return b.Op switch
                            {
                                "+"          => Expression.Add(Le, Re),
                                "-"          => Expression.Subtract(Le, Re),
                                "*"  or ".*" => Expression.Multiply(Le, Re),
                                "/"  or "./" => Expression.Divide(Le, Re),
                                "^"  or ".^" => Expression.Power(Le, Re),
                                _            => null,
                            };
                        }
                        // Matrix arith — al menos un operando es matrix.
                        // Scalar * matrix: usar JitMatScalarMul
                        if (b.Op == "*" || b.Op == ".*")
                        {
                            if (kL == TKind.Scalar)
                            {
                                var sc = ConvertExpr(b.Left, cc);
                                var mt = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                                if (sc == null || mt == null) return null;
                                return Expression.Call(JitCtx.MMatScalarMul, mt, sc);
                            }
                            if (kR == TKind.Scalar)
                            {
                                var mt = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                                var sc = ConvertExpr(b.Right, cc);
                                if (mt == null || sc == null) return null;
                                return Expression.Call(JitCtx.MMatScalarMul, mt, sc);
                            }
                            // matrix * matrix
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(JitCtx.MMatMul, Lm, Rm);
                        }
                        if (b.Op == "+" || b.Op == "-")
                        {
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(b.Op == "+" ? JitCtx.MMatAdd : JitCtx.MMatSub, Lm, Rm);
                        }
                        return null;
                    }
                case CallOrIndex coi when coi.Target is IdentRef ident:
                    return ConvertCallOrIndex(ident.Name, coi.Args, cc);
                case MatrixLit ml:
                    return ConvertMatrixLit(ml, cc);
                default:
                    return null;
            }
        }

        private static Expression ConvertCallOrIndex(string name, List<MatlabNode> args, CompileCtx cc)
        {
            bool isFn = cc.Evaluator.JitIsFunction(name);
            if (isFn)
            {
                // Function call. Decidir scalar/matrix por heurística (= GuessFnKind).
                var fnKind = GuessFnKind(name);
                var argExprs = new Expression[args.Count];
                for (int i = 0; i < args.Count; i++)
                {
                    var e = ConvertExprAsKind(args[i], cc, TKind.Scalar);
                    if (e == null) return null;
                    argExprs[i] = e;
                }
                var arr = Expression.NewArrayInit(typeof(double), argExprs);
                var method = (fnKind == TKind.Matrix) ? JitCtx.MCallMatrix : JitCtx.MCallScalar;
                return Expression.Call(cc.CtxParam, method, Expression.Constant(name), arr);
            }
            // Matrix indexing
            if (args.Count == 1)
            {
                var idx1 = ConvertExprAsKind(args[0], cc, TKind.Scalar);
                if (idx1 == null) return null;
                return Expression.Call(cc.CtxParam, JitCtx.MGetMatElem1,
                    Expression.Constant(name), idx1);
            }
            if (args.Count == 2)
            {
                var idx1 = ConvertExprAsKind(args[0], cc, TKind.Scalar);
                var idx2 = ConvertExprAsKind(args[1], cc, TKind.Scalar);
                if (idx1 == null || idx2 == null) return null;
                return Expression.Call(cc.CtxParam, JitCtx.MGetMatElem2,
                    Expression.Constant(name), idx1, idx2);
            }
            return null;
        }

        private static Expression ConvertMatrixLit(MatrixLit ml, CompileCtx cc)
        {
            // PoC: solo row vector (1 fila) con elementos escalares
            if (ml.Rows.Count != 1) return null;
            var elems = ml.Rows[0];
            var exprs = new Expression[elems.Count];
            for (int i = 0; i < elems.Count; i++)
            {
                var e = ConvertExprAsKind(elems[i], cc, TKind.Scalar);
                if (e == null) return null;
                exprs[i] = e;
            }
            var arr = Expression.NewArrayInit(typeof(double), exprs);
            return Expression.Call(JitCtx.MMakeRowVec, arr);
        }

        // ─── Eval scalar para los limites del range ───────────────────────
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
