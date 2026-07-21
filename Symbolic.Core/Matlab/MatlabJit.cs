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

        /// <summary>Numero de elementos de la variable, para resolver `end` como indice
        /// (MATLAB: v(end), v(end-1), v(end+1)). Se consulta en TIEMPO DE EJECUCION,
        /// asi el valor es correcto aunque el arreglo haya crecido en el bucle.</summary>
        public double MatLen(string name)
        {
            if (!Scope.TryGet(name, out var v)) return 0;
            return v.Data == null ? 0 : v.Data.Length;
        }

        // ─── Matrix indexing scalar ─────────────────────────────────────────
        public double GetMatElem1(string name, double i)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            int idx = (int)i - 1;
            // Un arreglo VACIO tiene Rows=0 y Cols=0: sin este caso se caia al
            // reparto lineal de abajo y dividia entre cero. Pasa con el patron
            // MATLAB `v = []` seguido de `v(end+1) = ...` dentro de un bucle.
            if (v.Cols == 0 || v.Rows == 0)
                throw new MatlabRuntimeException("Index exceeds matrix dimensions: " + name);
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
            // MATLAB CRECE el arreglo al asignar fuera de rango: `v(end+1) = x`.
            // El JIT no redimensionaba: con el arreglo vacio (Rows=Cols=0) dividia
            // entre cero, y con un vector se quedaba del tamano original. Se copia
            // a uno mas grande conservando lo que ya habia.
            bool vacio = v.Rows == 0 || v.Cols == 0;
            bool esCol = !vacio && v.Cols == 1 && v.Rows > 1;
            int largo = vacio ? 0 : v.Data.Length;
            if (vacio || ((v.Rows == 1 || esCol) && idx >= largo))
            {
                var nv = esCol ? new MValue(idx + 1, 1) : new MValue(1, idx + 1);
                for (int q = 0; q < largo; q++) nv.Data[q] = v.Data[q];
                nv.Data[idx] = val;
                Scope.Set(name, nv);
                return;
            }
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
        internal static readonly MethodInfo MMatLen = typeof(JitCtx).GetMethod(nameof(MatLen));
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
        internal static readonly MethodInfo MMakeMatrix2D  = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMakeMatrix2D));
        internal static readonly MethodInfo MGetMatRow     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitGetMatRow));
        internal static readonly MethodInfo MGetMatCol     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitGetMatCol));
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
            public Action<double[], int, int> WholeLoop;  // fast path puro-escalar (slots, start, end)
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
                if (scope.TryGet(c.ScalarNames[k], out var v))
                {
                    // Si la variable EXISTE como MATRIZ pero el clasificador la tomó como
                    // escalar (p.ej. `C = A*B` sin indexar el resultado, donde no hay nada
                    // que fuerce el tipo matriz), la clasificación es incorrecta: correr el
                    // JIT inicializaria su slot en 0 y al terminar lo commitearia como
                    // escalar 0, DESTRUYENDO la matriz del workspace. Se aborta el JIT y se
                    // deja que el intérprete ejecute el loop (resultado correcto).
                    if (!v.IsScalar) { Skips++; return false; }
                    slots[k] = v.Scalar;
                }
            }
            int iStart = (int)startVal;
            int iEnd   = (int)endVal;

            // Fast path: loop COMPLETO compilado con locals IL (puro escalar) — sin
            // overhead de invocación por iteración ni indexado de slots[].
            if (c.WholeLoop != null)
            {
                c.WholeLoop(slots, iStart, iEnd);
                for (int k = 0; k < c.ScalarNames.Length; k++)
                    scope.Set(c.ScalarNames[k], new MValue(slots[k]));
                Hits++;
                return true;
            }

            var ctx = new JitCtx { Slots = slots, Scope = scope, Evaluator = ev };
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
            catch (MatlabRuntimeException)
            {
                // El JIT clasificó algo como scalar que en runtime resultó
                // ser matriz (típicamente indexación dinámica de matrices
                // que el inferidor no pudo predecir). Marcamos el loop como
                // no-JIT-compatible para futuras llamadas y dejamos que el
                // intérprete ejecute el loop completo desde cero. Scope queda
                // intacto en cuanto a scalars (no commiteamos), y las
                // mutaciones de matriz vía JitCtx.SetMatElem* son idempotentes
                // para escrituras a índices disjuntos (caso típico de FEM).
                c.Failed = true;
                _cache[loop] = c;
                Skips++;
                return false;
            }

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
            // Fast path "loop completo": variables escalares como locals IL en vez de slots[].
            public bool UseLocals;
            /// <summary>Nombre del arreglo que se esta indexando: `end` dentro de sus
            /// argumentos se resuelve como la longitud de ESE arreglo.</summary>
            public string EndArray;
            public Dictionary<string, ParameterExpression> Locals = new(StringComparer.Ordinal);
        }

        /// <summary>Acceso (lectura/escritura) a una variable escalar: local IL si
        /// UseLocals (loop completo compilado), si no el slot del array (path por-iter).</summary>
        private static Expression ScalarAccess(CompileCtx cc, string name)
        {
            if (cc.UseLocals) return cc.Locals[name];
            return Expression.ArrayAccess(cc.SlotsExpr, Expression.Constant(cc.SlotIdx[name]));
        }

        /// <summary>true si el cuerpo asigna a `v(end+...)`, o sea hace CRECER un
        /// arreglo. El JIT no redimensiona, asi que esos bucles no se compilan.</summary>
        private static bool CreceArreglo(System.Collections.Generic.List<MatlabNode> body)
        {
            foreach (var st in body)
            {
                if (st is Assignment asg)
                {
                    foreach (var tg in asg.Targets)
                        if (tg is CallOrIndex ci && UsaEnd(ci.Args)) return true;
                }
                else if (st is ForLoop fl && CreceArreglo(fl.Body)) return true;
                else if (st is WhileLoop wl && CreceArreglo(wl.Body)) return true;
                else if (st is IfBlock ib)
                {
                    foreach (var br in ib.Branches)          // el else final va con Cond = null
                        if (br.Body != null && CreceArreglo(br.Body)) return true;
                }
            }
            return false;
        }

        private static bool UsaEnd(System.Collections.Generic.List<MatlabNode> args)
        {
            if (args == null) return false;
            foreach (var a in args)
                if (ContieneEnd(a)) return true;
            return false;
        }

        private static bool ContieneEnd(MatlabNode n)
        {
            switch (n)
            {
                case IdentRef ir: return ir.Name == "end";
                case BinaryOp bo: return ContieneEnd(bo.Left) || ContieneEnd(bo.Right);
                case UnaryOp uo: return ContieneEnd(uo.Operand);
                default: return false;
            }
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

                // Fast path: si el loop es PURAMENTE escalar (sin variables/ops matriz),
                // compilar el LOOP COMPLETO con variables locales IL (10-50x vs slots[]).
                Action<double[], int, int> wholeLoop = null;
                bool pureScalar = true;
                foreach (var v in cc.VarKind.Values) if (v != TKind.Scalar) { pureScalar = false; break; }
                if (pureScalar)
                {
                    try
                    {
                        var cc2 = new CompileCtx { Evaluator = ev, UseLocals = true, SlotIdx = cc.SlotIdx, VarKind = cc.VarKind };
                        foreach (var kv in cc.SlotIdx) cc2.Locals[kv.Key] = Expression.Variable(typeof(double), kv.Key);
                        var slotsP = Expression.Parameter(typeof(double[]), "slots");
                        var startP = Expression.Parameter(typeof(int), "start");
                        var endP = Expression.Parameter(typeof(int), "end");
                        var iVar = Expression.Variable(typeof(int), "i");
                        var pre = new List<Expression>();
                        foreach (var kv in cc.SlotIdx)
                            pre.Add(Expression.Assign(cc2.Locals[kv.Key], Expression.ArrayIndex(slotsP, Expression.Constant(kv.Value))));
                        pre.Add(Expression.Assign(iVar, startP));
                        var brk = Expression.Label("brk");
                        var bl = new List<Expression>
                        {
                            Expression.IfThen(Expression.GreaterThan(iVar, endP), Expression.Break(brk)),
                            Expression.Assign(cc2.Locals[loop.VarName], Expression.Convert(iVar, typeof(double)))
                        };
                        bool ok = true;
                        foreach (var stmt in loop.Body)
                        {
                            if (stmt is CommentStmt) continue;
                            var e = ConvertStmt(stmt, cc2);
                            if (e == null) { ok = false; break; }
                            bl.Add(e);
                        }
                        if (ok)
                        {
                            bl.Add(Expression.PostIncrementAssign(iVar));
                            pre.Add(Expression.Loop(Expression.Block(bl), brk));
                            foreach (var kv in cc.SlotIdx)
                                pre.Add(Expression.Assign(Expression.ArrayAccess(slotsP, Expression.Constant(kv.Value)), cc2.Locals[kv.Key]));
                            var allLocals = new List<ParameterExpression>(cc2.Locals.Values) { iVar };
                            wholeLoop = Expression.Lambda<Action<double[], int, int>>(
                                Expression.Block(allLocals, pre), slotsP, startP, endP).Compile();
                        }
                    }
                    catch { wholeLoop = null; }
                }

                return new Compiled
                {
                    Body = compiled,
                    WholeLoop = wholeLoop,
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
        // Funciones de I/O cuyo efecto es mostrar texto por iteracion. El JIT NO las
        // compila: hace bail-out al interprete para que el pipeline (InnerStmtOut)
        // pueda volcar su salida por iteracion (resultado por linea en el render).
        private static readonly System.Collections.Generic.HashSet<string> _ioFuncs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            { "fprintf", "disp", "display", "warning", "error" };

        // Funciones GRAFICAS: tienen efecto lateral por iteracion (mutan la figura/
        // malla viva). El JIT NO las compila -> bail-out al interprete, que ejecuta
        // set/drawnow de verdad y emite cada frame (animacion en vivo, modo MATLAB
        // retenido). Sin esto el JIT corre el loop y descarta los efectos graficos.
        private static readonly System.Collections.Generic.HashSet<string> _gfxFuncs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                "set", "drawnow", "pause", "patch", "figure", "plot", "plot3",
                "fill", "fill3", "surf", "mesh", "line", "text", "title",
                "xlabel", "ylabel", "zlabel", "cla", "clf", "hold", "axis",
                "caxis", "clim", "colormap", "colorbar", "quiver", "scatter",
                "image", "imagesc", "contour", "contourf", "refreshdata"
            };

        private static bool IsIoCall(MatlabNode expr) =>
            expr is CallOrIndex coi && coi.Target is IdentRef ir
            && (_ioFuncs.Contains(ir.Name) || _gfxFuncs.Contains(ir.Name));

        private static bool ClassifyStmt(MatlabNode stmt, CompileCtx cc)
        {
            switch (stmt)
            {
                case CommentStmt _: return true;
                // Cualquier llamada de I/O -> bail-out (la renderiza el interprete).
                case ExprStmt eio when IsIoCall(eio.Expr): return false;
                case Assignment aio when IsIoCall(aio.Rhs): return false;
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
                            if (!cc.SlotIdx.ContainsKey(ir.Name)) return null;
                            return Expression.Assign(ScalarAccess(cc, ir.Name), rhs);
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
                            var prevEndW = cc.EndArray; cc.EndArray = matIdent.Name;
                            var idx1 = ConvertExprAsKind(tgtCall.Args[0], cc, TKind.Scalar);
                            cc.EndArray = prevEndW;
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
                        // `end` como indice: longitud real del arreglo en ejecucion
                        if (ir.Name == "end" && cc.EndArray != null)
                            return Expression.Call(cc.CtxParam, JitCtx.MMatLen,
                                                   Expression.Constant(cc.EndArray));
                        if (!cc.VarKind.ContainsKey(ir.Name)) return null;
                        var k = cc.VarKind[ir.Name];
                        if (k == TKind.Scalar)
                        {
                            if (!cc.SlotIdx.ContainsKey(ir.Name)) return null;
                            return ScalarAccess(cc, ir.Name);
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
                if (args[0] is ColonAll) return null;   // A(:) flatten — no soportado
                var prevEnd = cc.EndArray; cc.EndArray = name;      // `end` = longitud de este arreglo
                var idx1 = ConvertExprAsKind(args[0], cc, TKind.Scalar);
                cc.EndArray = prevEnd;
                if (idx1 == null) return null;
                return Expression.Call(cc.CtxParam, JitCtx.MGetMatElem1,
                    Expression.Constant(name), idx1);
            }
            if (args.Count == 2)
            {
                bool firstColon  = args[0] is ColonAll;
                bool secondColon = args[1] is ColonAll;
                if (firstColon && secondColon) return null;   // A(:,:) → copy, no util en hot loop
                if (firstColon)
                {
                    // A(:, j) → columna j
                    var jExpr = ConvertExprAsKind(args[1], cc, TKind.Scalar);
                    if (jExpr == null) return null;
                    // Cargar la matriz como MValue y extraer columna
                    var matVar = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar,
                        Expression.Constant(name));
                    return Expression.Call(JitCtx.MGetMatCol, matVar, jExpr);
                }
                if (secondColon)
                {
                    // A(i, :) → fila i
                    var iExpr = ConvertExprAsKind(args[0], cc, TKind.Scalar);
                    if (iExpr == null) return null;
                    var matVar = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar,
                        Expression.Constant(name));
                    return Expression.Call(JitCtx.MGetMatRow, matVar, iExpr);
                }
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
            if (ml.Rows.Count == 0) return null;
            // Row vector: 1 fila → MakeRowVec
            if (ml.Rows.Count == 1)
            {
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
            // Matriz 2D: row-major flatten + MakeMatrix2D(rows, cols, flat)
            int rows = ml.Rows.Count;
            int cols = ml.Rows[0].Count;
            // Verificar consistencia
            for (int i = 0; i < rows; i++)
                if (ml.Rows[i].Count != cols) return null;
            var flat = new Expression[rows * cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    var e = ConvertExprAsKind(ml.Rows[i][j], cc, TKind.Scalar);
                    if (e == null) return null;
                    flat[i * cols + j] = e;
                }
            var arr2 = Expression.NewArrayInit(typeof(double), flat);
            return Expression.Call(JitCtx.MMakeMatrix2D,
                Expression.Constant(rows), Expression.Constant(cols), arr2);
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
