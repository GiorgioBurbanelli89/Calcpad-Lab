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
        /// <summary>Llamada a función de usuario con args MValue (mezcla matriz/escalar) que
        /// devuelve UN output (matriz). Para sub-llamadas del return-map: mc_stress no la usa,
        /// pero completa el par con CallMultiMV.</summary>
        public MValue CallMV(string name, MValue[] args) => Evaluator.JitCall(name, args);
        /// <summary>Llamada MULTI-OUTPUT con args MValue: [a,b,...]=f(args). Devuelve MValue[]
        /// (cada salida opaca: matriz, escalar o STRING — el consumidor decide). Así `reg` (string)
        /// del return-map fluye sin soporte de strings en el JIT.</summary>
        public MValue[] CallMultiMV(string name, MValue[] args) => Evaluator.JitCallMulti(name, args);

        // ─── Funciones matematicas INLINE (sin MValue ni dispatch) ─────────
        // MATLAB mod(a,b) = a - b*floor(a/b);  mod(a,0) = a.
        public static double JMod(double a, double b) => b == 0 ? a : a - b * Math.Floor(a / b);
        // MATLAB rem(a,b) = a - b*fix(a/b);  rem(a,0) = NaN.
        public static double JRem(double a, double b) => b == 0 ? double.NaN : a - b * Math.Truncate(a / b);
        // MATLAB round: mitad HACIA AFUERA (2.5->3), no banquero.
        public static double JRound(double a) => Math.Round(a, MidpointRounding.AwayFromZero);
        public static double JSign(double a) => Math.Sign(a);
        public static double JFix(double a) => Math.Truncate(a);
        public static double JLog2(double a) => Math.Log(a) / 0.6931471805599453;

        // ─── Matrix variable access ────────────────────────────────────────
        public MValue GetMatrixVar(string name)
        {
            if (!Scope.TryGet(name, out var v))
                throw new MatlabRuntimeException("Undefined: " + name);
            return v;
        }
        public void SetMatrixVar(string name, MValue val) => Scope.Set(name, val);

        /// <summary>C si existe como matriz densa real con la forma dada (para reusar su
        /// buffer en la fusion element-wise); null si no existe o no encaja.</summary>
        public MValue GetMatrixOrNull(string name)
        {
            if (Scope.TryGet(name, out var v) && v != null && !v.IsSparseReal
                && !v.IsComplex && !v.IsString && v.CellData == null && v.Data != null)
                return v;
            return null;
        }

        /// <summary>Lee un elemento de un cell array por indices escalares: c{i} o c{i,j}.
        /// Devuelve el MValue de la celda (una matriz en el FEM: Bc{e,q}).</summary>
        public MValue GetCellElem(string name, double i, double j)
        {
            if (!Scope.TryGet(name, out var v) || v.CellData == null)
                throw new MatlabRuntimeException("Undefined cell: " + name);
            int r = (int)i - 1, c = (int)j - 1;
            var cd = v.CellData;
            // c{i} lineal (col-major MATLAB) si es 1-D; c{i,j} 2-D
            if (j == 0)   // marca de indice unico (llamador pasa j=0)
            {
                int rows = cd.GetLength(0), cols = cd.GetLength(1);
                if (rows == 1) { c = r; r = 0; }
                else if (cols == 1) { c = 0; }
                else { c = r / rows; r = r % rows; }
            }
            return cd[r, c];
        }

        // ─── MethodInfo handles (pre-resueltos) ───────────────────────────
        internal static readonly MethodInfo MMatLen = typeof(JitCtx).GetMethod(nameof(MatLen));
        internal static readonly MethodInfo MGetMatElem1 = typeof(JitCtx).GetMethod(nameof(GetMatElem1));
        internal static readonly MethodInfo MGetMatElem2 = typeof(JitCtx).GetMethod(nameof(GetMatElem2));
        internal static readonly MethodInfo MSetMatElem1 = typeof(JitCtx).GetMethod(nameof(SetMatElem1));
        internal static readonly MethodInfo MSetMatElem2 = typeof(JitCtx).GetMethod(nameof(SetMatElem2));
        internal static readonly MethodInfo MCallScalar  = typeof(JitCtx).GetMethod(nameof(CallScalar));
        internal static readonly MethodInfo MCallMatrix  = typeof(JitCtx).GetMethod(nameof(CallMatrix));
        internal static readonly MethodInfo MCallMV      = typeof(JitCtx).GetMethod(nameof(CallMV));
        internal static readonly MethodInfo MCallMultiMV = typeof(JitCtx).GetMethod(nameof(CallMultiMV));
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
        internal static readonly MethodInfo MMakeEmpty     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMakeEmpty));
        internal static readonly MethodInfo MIsEmpty       = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitIsEmpty));
        internal static readonly MethodInfo MMatDiv        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatDiv));
        internal static readonly MethodInfo MMatEwDiv      = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatEwDiv));
        internal static readonly MethodInfo MMatLDiv       = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatLDiv));
        internal static readonly MethodInfo MMakeRange     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMakeRange));
        internal static readonly MethodInfo MMatColon      = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatColon));
        internal static readonly MethodInfo MHorzCat       = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitHorzCat));
        internal static readonly MethodInfo MVertCat       = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitVertCat));
        internal static readonly MethodInfo MMatEwPow      = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatEwPow));
        internal static readonly MethodInfo MMatPow        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatPow));
        internal static readonly MethodInfo MMatCmp        = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatCmp));
        internal static readonly MethodInfo MGetMatRow     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitGetMatRow));
        internal static readonly MethodInfo MGetMatCol     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitGetMatCol));
        internal static readonly MethodInfo MMatToScalar   = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitMatToScalar));
        internal static readonly MethodInfo MGetCellElem   = typeof(JitCtx).GetMethod(nameof(GetCellElem));
        internal static readonly MethodInfo MGetMatrixOrNull = typeof(JitCtx).GetMethod(nameof(GetMatrixOrNull));
        internal static readonly MethodInfo MEwFusedRun    = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.EwFusedRun));
        internal static readonly System.Reflection.ConstructorInfo CVecFromScalar =
            typeof(System.Numerics.Vector<double>).GetConstructor(new[] { typeof(double) });
        internal static readonly System.Reflection.ConstructorInfo CVecFromArray =
            typeof(System.Numerics.Vector<double>).GetConstructor(new[] { typeof(double[]), typeof(int) });
        internal static readonly MethodInfo MJitGather     = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitGather));
        internal static readonly MethodInfo MJitScatterAdd = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitScatterAdd));
        internal static readonly MethodInfo MJitScatterAssign = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitScatterAssign));
        internal static readonly MethodInfo MJitScatterAddInPlace = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitScatterAddInPlace));
        internal static readonly MethodInfo MJitColSlice   = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitColSlice));
        internal static readonly MethodInfo MJitRowSlice   = typeof(MatlabEvaluator).GetMethod(nameof(MatlabEvaluator.JitRowSlice));
        internal static readonly ConstructorInfo CMValueScalar = typeof(MValue).GetConstructor(new[] { typeof(double) });

        // ─── Mapa de funciones matematicas escalares que el JIT INLINEA como
        // llamada estatica nativa (Math.* o JitCtx.J*), evitando el dispatch
        // generico con MValue+diccionario (mod() en un loop FEM pasa de ~2us a ~ns).
        private static MethodInfo M1(string n) => typeof(Math).GetMethod(n, new[] { typeof(double) });
        private static MethodInfo M2(string n) => typeof(Math).GetMethod(n, new[] { typeof(double), typeof(double) });
        private static MethodInfo J(string n) => typeof(JitCtx).GetMethod(n);
        internal static readonly Dictionary<string, (int Argc, MethodInfo M)> InlineMath = new(StringComparer.Ordinal)
        {
            ["sqrt"]=(1,M1("Sqrt")), ["abs"]=(1,M1("Abs")), ["floor"]=(1,M1("Floor")),
            ["ceil"]=(1,M1("Ceiling")), ["sin"]=(1,M1("Sin")), ["cos"]=(1,M1("Cos")),
            ["tan"]=(1,M1("Tan")), ["asin"]=(1,M1("Asin")), ["acos"]=(1,M1("Acos")),
            ["atan"]=(1,M1("Atan")), ["sinh"]=(1,M1("Sinh")), ["cosh"]=(1,M1("Cosh")),
            ["tanh"]=(1,M1("Tanh")), ["exp"]=(1,M1("Exp")), ["log"]=(1,M1("Log")),
            ["log10"]=(1,M1("Log10")), ["log2"]=(1,J(nameof(JLog2))), ["round"]=(1,J(nameof(JRound))),
            ["sign"]=(1,J(nameof(JSign))), ["fix"]=(1,J(nameof(JFix))),
            ["mod"]=(2,J(nameof(JMod))), ["rem"]=(2,J(nameof(JRem))),
            ["power"]=(2,M2("Pow")), ["atan2"]=(2,M2("Atan2")),
            ["min"]=(2,M2("Min")), ["max"]=(2,M2("Max")),
        };
    }

    public static class MatlabJit
    {
        public static bool Enabled =
            System.Environment.GetEnvironmentVariable("CALCPAD_LAB_JIT") != "0";
        public static long Hits, Compiles, Skips;
        public static long EwEmitOk, EwEmitFail;   // diagnostico fusion element-wise

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
                c = TryCompile(loop, ev, scope);
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
        private enum TKind { Scalar, Matrix, Cell }

        private sealed class CompileCtx
        {
            public MatlabEvaluator Evaluator;
            public MatlabScope Scope;   // scope real: para inferir el tipo de las vars live-in
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
            /// <summary>Label de salida de la FUNCIÓN (JIT de funciones): `return` hace Goto aquí.
            /// null en contexto de bucle (el `return` no se soporta ahí → bailout).</summary>
            public LabelTarget ReturnLabel;
            /// <summary>Labels del bucle MÁS INTERNO para `break`/`continue`. null fuera de un bucle.</summary>
            public LabelTarget BreakLabel;
            public LabelTarget ContinueLabel;
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

        private static Compiled TryCompile(ForLoop loop, MatlabEvaluator ev, MatlabScope scope)
        {
            if (loop.Iter is not Range range || range.Step != null)
                return new Compiled { Failed = true };

            try
            {
                var cc = new CompileCtx { Evaluator = ev, Scope = scope };
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
        /// <summary>Phase 0 del JIT de FUNCIONES: compila una función cuyos params, locals y
        /// outputs son TODOS escalares y cuyo cuerpo es compatible con el JIT (aritmética,
        /// if/for, math inline, llamadas escalares). Devuelve un delegado in[]→out[] o null
        /// (fallback al intérprete, sin riesgo). NO soporta: matrices, cells, sort, strings,
        /// return temprano, multi-construcciones no escalares → esas devuelven null.</summary>
        public static Func<double[], double[]> TryCompileScalarFunction(FunctionDef def, MatlabEvaluator ev)
        {
            try
            {
                if (def.ClosureScope != null) return null;
                if (def.OutputNames.Count == 0) return null;
                foreach (var p in def.ParamNames) if (p == "varargin") return null;
                foreach (var o in def.OutputNames) if (o == "varargout") return null;
                var cc = new CompileCtx { Evaluator = ev, UseLocals = true };
                cc.ReturnLabel = Expression.Label("ret");   // Phase 1: `return` -> Goto aquí
                foreach (var p in def.ParamNames) cc.VarKind[p] = TKind.Scalar;
                foreach (var o in def.OutputNames) cc.VarKind[o] = TKind.Scalar;
                if (!ClassifyBody(def.Body, cc)) return null;
                foreach (var kv in cc.VarKind) if (kv.Value != TKind.Scalar) return null;   // algo no-escalar → fallback
                foreach (var kv in cc.VarKind)
                    if (!cc.Locals.ContainsKey(kv.Key)) cc.Locals[kv.Key] = Expression.Variable(typeof(double), kv.Key);

                var inP = Expression.Parameter(typeof(double[]), "in");
                var body = new List<Expression>();
                for (int i = 0; i < def.ParamNames.Count; i++)      // bind params: local = in[i]
                    body.Add(Expression.Assign(cc.Locals[def.ParamNames[i]], Expression.ArrayIndex(inP, Expression.Constant(i))));
                foreach (var o in def.OutputNames)                  // outputs = 0 por defecto
                    body.Add(Expression.Assign(cc.Locals[o], Expression.Constant(0.0)));
                foreach (var st in def.Body)
                {
                    if (st is CommentStmt) continue;
                    var e = ConvertStmt(st, cc);
                    if (e == null) return null;                     // construcción no soportada → fallback
                    body.Add(e);
                }
                body.Add(Expression.Label(cc.ReturnLabel));         // destino de los `return` tempranos
                var outArr = Expression.Variable(typeof(double[]), "out");
                body.Add(Expression.Assign(outArr, Expression.NewArrayBounds(typeof(double), Expression.Constant(def.OutputNames.Count))));
                for (int i = 0; i < def.OutputNames.Count; i++)
                    body.Add(Expression.Assign(Expression.ArrayAccess(outArr, Expression.Constant(i)), cc.Locals[def.OutputNames[i]]));
                body.Add(outArr);
                var allLocals = new List<ParameterExpression>(cc.Locals.Values) { outArr };
                var block = Expression.Block(typeof(double[]), allLocals, body);
                return Expression.Lambda<Func<double[], double[]>>(block, inP).Compile();
            }
            catch { return null; }
        }

        /// <summary>JIT de función GENERAL (matrices + escalares): resultado compilado + metadatos
        /// para enlazar params y extraer outputs en runtime. Especializado a los KINDS de los args
        /// de la 1ª llamada (ParamKinds); si una llamada trae otros kinds → intérprete.</summary>
        public sealed class CompiledFnMV
        {
            public Action<JitCtx> Body;
            public Dictionary<string, int> SlotIdx;
            public string[] ParamNames;
            public TKindPub[] ParamKinds;
            public string[] OutputNames;
            public TKindPub[] OutputKinds;
        }
        public enum TKindPub { Scalar, Matrix }   // espejo público de TKind (Cell no se soporta aquí)

        /// <summary>Compila una función con params/locals ESCALARES o MATRIZ (Phase 3). Reusa
        /// ConvertStmt/ConvertExpr (matrices via el Scope del JitCtx). Devuelve null si algo no
        /// se soporta (cells, sort, strings, multi-output aún) → intérprete (fallback seguro).
        /// paramKinds: true=matriz, false=escalar, uno por parámetro.</summary>
        /// <summary>Toggle A/B (solo para benchmark): env HEK_NO_MVJIT=1 desactiva el JIT Phase 3
        /// para medir su aporte sin recompilar. Por defecto activo.</summary>
        public static readonly bool MVEnabled =
            System.Environment.GetEnvironmentVariable("HEK_NO_MVJIT") != "1";

        public static CompiledFnMV TryCompileFunctionMV(FunctionDef def, MatlabEvaluator ev, bool[] paramIsMatrix)
        {
            try
            {
                if (!MVEnabled) return null;
                if (def.ClosureScope != null || def.OutputNames.Count == 0) return null;
                foreach (var p in def.ParamNames) if (p == "varargin") return null;
                foreach (var o in def.OutputNames) if (o == "varargout") return null;
                if (paramIsMatrix.Length != def.ParamNames.Count) return null;
                var cc = new CompileCtx { Evaluator = ev };   // NO UseLocals -> slots[] + Scope
                cc.CtxParam = Expression.Parameter(typeof(JitCtx), "ctx");
                cc.SlotsExpr = Expression.Field(cc.CtxParam, JitCtx.FSlots);
                cc.ReturnLabel = Expression.Label("ret");
                for (int i = 0; i < def.ParamNames.Count; i++)
                    cc.VarKind[def.ParamNames[i]] = paramIsMatrix[i] ? TKind.Matrix : TKind.Scalar;
                if (!ClassifyBody(def.Body, cc)) return null;
                foreach (var kv in cc.VarKind) if (kv.Value == TKind.Cell) return null;
                foreach (var kv in cc.VarKind)
                    if (kv.Value == TKind.Scalar && !cc.SlotIdx.ContainsKey(kv.Key)) cc.SlotIdx[kv.Key] = cc.SlotIdx.Count;
                var body = new List<Expression>();
                foreach (var st in def.Body)
                {
                    if (st is CommentStmt) continue;
                    var e = ConvertStmt(st, cc);
                    if (e == null) return null;
                    body.Add(e);
                }
                body.Add(Expression.Label(cc.ReturnLabel));
                if (body.Count == 0) return null;
                var lambda = Expression.Lambda<Action<JitCtx>>(Expression.Block(body), cc.CtxParam).Compile();
                var okinds = new TKindPub[def.OutputNames.Count];
                for (int i = 0; i < def.OutputNames.Count; i++)
                {
                    if (!cc.VarKind.TryGetValue(def.OutputNames[i], out var ok)) return null;
                    if (ok == TKind.Cell) return null;
                    okinds[i] = ok == TKind.Matrix ? TKindPub.Matrix : TKindPub.Scalar;
                }
                var pkinds = new TKindPub[paramIsMatrix.Length];
                for (int i = 0; i < paramIsMatrix.Length; i++) pkinds[i] = paramIsMatrix[i] ? TKindPub.Matrix : TKindPub.Scalar;
                return new CompiledFnMV
                {
                    Body = lambda, SlotIdx = cc.SlotIdx,
                    ParamNames = def.ParamNames.ToArray(), ParamKinds = pkinds,
                    OutputNames = def.OutputNames.ToArray(), OutputKinds = okinds
                };
            }
            catch { return null; }
        }

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
                case Assignment amo when amo.Targets.Count > 1:
                    {
                        // [a,b,...]=userfn(args): solo funciones de usuario; args deben inferirse;
                        // cada target real (no `~`) es MATRIZ OPACA (matriz/escalar/string en el Scope).
                        if (amo.Rhs is not CallOrIndex mc || mc.Target is not IdentRef mcId) return false;
                        if (!cc.Evaluator.JitIsUserFunction(mcId.Name)) return false;
                        foreach (var arg in mc.Args) if (InferKind(arg, cc) == null) return false;
                        foreach (var t in amo.Targets)
                        {
                            if (t is not IdentRef tv) return false;
                            if (tv.Name == "~") continue;
                            if (!SetKind(cc, tv.Name, TKind.Matrix)) return false;
                        }
                        return true;
                    }
                case IfBlock ib:
                    foreach (var (cond, cbody) in ib.Branches)
                    {
                        if (cond != null && !ClassifyCond(cond, cc)) return false;
                        if (!ClassifyBody(cbody, cc)) return false;
                    }
                    return true;
                case ForLoop fl:
                    // Bucle anidado (el for q dentro del for e de fint_c). Rango simple.
                    if (fl.Iter is not Range fr || fr.Step != null) return false;
                    if (!SetKind(cc, fl.VarName, TKind.Scalar)) return false;
                    if (InferKind(fr.Start, cc) == null || InferKind(fr.End, cc) == null) return false;
                    return ClassifyBody(fl.Body, cc);
                case WhileLoop wl:
                    if (!ClassifyCond(wl.Cond, cc)) return false;
                    return ClassifyBody(wl.Body, cc);
                case BreakStmt:
                case ContinueStmt:
                    return true;               // no clasifican variables
                case ExprStmt es:
                    return InferKind(es.Expr, cc) != null;
                case ReturnStmt:
                    return true;               // `return` no clasifica variables
                default:
                    return false;
            }
        }

        /// <summary>Clasifica (registra vars de) una condicion de if. Comparaciones/AND/OR
        /// se recorren recursivamente; el resto se infiere como expresion.</summary>
        private static bool ClassifyCond(MatlabNode node, CompileCtx cc)
        {
            if (node is BinaryOp b)
            {
                if (b.Op is ">" or "<" or ">=" or "<=" or "==" or "~=" or "!=")
                    return InferKind(b.Left, cc) != null && InferKind(b.Right, cc) != null;
                if (b.Op is "&&" or "&" or "||" or "|")
                    return ClassifyCond(b.Left, cc) && ClassifyCond(b.Right, cc);
            }
            return InferKind(node, cc) != null;
        }

        private static bool SetKind(CompileCtx cc, string name, TKind kind)
        {
            if (cc.VarKind.TryGetValue(name, out var existing))
                return existing == kind;   // conflict si cambia
            cc.VarKind[name] = kind;
            return true;
        }

        /// <summary>Nombres que el evaluador resuelve como CONSTANTES (no viven en el scope).
        /// El JIT debe rendirse al verlos: si los trata como variables live-in, su slot
        /// se inicializa en 0 y el resultado sale mal en silencio.</summary>
        private static readonly HashSet<string> BuiltinConsts = new(StringComparer.Ordinal)
        {
            "pi", "e", "eps", "Inf", "inf", "NaN", "nan", "realmax", "realmin", "intmax", "intmin"
        };

        private static TKind? InferKind(MatlabNode node, CompileCtx cc)
        {
            switch (node)
            {
                case NumberLit _: return TKind.Scalar;
                case IdentRef ir:
                    if (cc.VarKind.TryGetValue(ir.Name, out var k)) return k;
                    // CONSTANTES BUILTIN (pi, e, eps, Inf, NaN...): NO son variables del
                    // workspace, las resuelve el evaluador. Si se dejan pasar como "live-in",
                    // al sembrar los slots `scope.TryGet` falla y el slot queda en 0 -> el JIT
                    // calcula con pi=0 SIN AVISAR. Eso anulaba el angulo de friccion en un FEM
                    // (phi*pi/180 = 0) y el talud se desmoronaba. Bail-out: que lo corra el
                    // interprete, mas lento pero correcto.
                    if (BuiltinConsts.Contains(ir.Name)) return null;
                    // Variable no asignada antes en el loop → es LIVE-IN: consultar su tipo
                    // REAL en el scope. Antes se asumia Scalar; una matriz live-in (P en
                    // C=P.*Q) se tomaba escalar y luego el sembrado la detectaba no-escalar y
                    // hacia bail-out. Con el tipo real, el loop compila (y habilita la fusion).
                    if (cc.Scope != null && cc.Scope.TryGet(ir.Name, out var lv) && lv != null
                        && !lv.IsScalar && !lv.IsString && lv.CellData == null && lv.Fields == null
                        && lv.Symbolic == null && lv.SymCells == null && lv.MapData == null && !lv.Is3D
                        && (lv.Data != null || lv.IsSparseReal))
                    {
                        cc.VarKind[ir.Name] = TKind.Matrix;
                        return TKind.Matrix;
                    }
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
                        // Clasificar los ARGUMENTOS (registra las vars que usan). Sin esto,
                        // mod(a*7+e, n) con a,e,n de loops EXTERNOS no las registraba y el emit
                        // no las podia resolver -> el loop anidado con mod() bailaba al interprete.
                        var aks = new TKind[coi.Args.Count];
                        for (int ai = 0; ai < coi.Args.Count; ai++)
                        {
                            var akk = InferKind(coi.Args[ai], cc);
                            if (akk == null) return null;
                            aks[ai] = akk.Value;
                        }
                        // Función de USUARIO: inferir el kind REAL de su 1ª salida (clasificando su
                        // cuerpo). Corrige `Dep=depcont(...)` matriz que la heurística daba escalar.
                        if (cc.Evaluator.JitIsUserFunction(ident.Name))
                        {
                            var uk = InferUserFnOutKind(ident.Name, aks, cc);
                            if (uk != null) return uk;
                        }
                        // BUILTIN con algun arg MATRIZ: norm/det/... SIEMPRE escalar (se coacciona);
                        // el resto (sum/max/min...) da escalar O vector segun forma → OPACO (Matrix).
                        foreach (var a2 in aks) if (a2 == TKind.Matrix)
                            return AlwaysScalarBuiltins.Contains(ident.Name) ? TKind.Scalar : TKind.Matrix;
                        // Builtin con args escalares: heurística por nombre.
                        return GuessFnKind(ident.Name);
                    }
                    // Indexing de matriz
                    if (!SetKind(cc, ident.Name, TKind.Matrix)) return null;
                    bool anyColon = false, anyVec = false;
                    foreach (var arg in coi.Args)
                    {
                        if (arg is ColonAll) { anyColon = true; continue; }
                        var ak = InferKind(arg, cc);
                        if (ak == null) return null;
                        if (ak == TKind.Matrix) anyVec = true;   // indice vectorial → gather
                    }
                    // slice A(:,j)/A(i,:) o gather A(vec) → Matrix; todos escalares → Scalar
                    return (anyColon || anyVec) ? TKind.Matrix : TKind.Scalar;
                case CellIndex cix when cix.Target is IdentRef cellId:
                    // c{i} / c{i,j}: la var es un cell, el ELEMENTO es una matriz (Bc{e,q})
                    if (!SetKind(cc, cellId.Name, TKind.Cell)) return null;
                    foreach (var arg in cix.Args)
                        if (InferKind(arg, cc) == null) return null;
                    return TKind.Matrix;
                case MatrixLit ml:
                    foreach (var row in ml.Rows)
                        foreach (var el in row)
                            if (InferKind(el, cc) == null) return null;
                    return TKind.Matrix;
                case Range rg:                                   // a:b / a:s:b como VECTOR (v=1:n)
                    if (InferKind(rg.Start, cc) == null) return null;
                    if (InferKind(rg.End, cc) == null) return null;
                    if (rg.Step != null && InferKind(rg.Step, cc) == null) return null;
                    return TKind.Matrix;
                default:
                    return null;
            }
        }

        // Caché del KIND de la 1ª salida de funciones de usuario, por (nombre + firma de kinds de args).
        private static readonly Dictionary<string, TKind> _fnOutKind = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _fnOutKindBusy = new(StringComparer.Ordinal);
        /// <summary>Infiere el KIND (escalar/matriz) de la 1ª salida de una función de USUARIO
        /// clasificando su cuerpo (recursivo, cacheado). Devuelve null si no se puede — el llamador
        /// cae a GuessFnKind. Corrige `Dep=depcont(...)` (matriz) que la heurística daba escalar.</summary>
        private static TKind? InferUserFnOutKind(string name, TKind[] argKinds, CompileCtx cc)
        {
            var def = cc.Evaluator.JitGetUserFn(name);
            if (def == null || def.OutputNames.Count == 0 || def.ClosureScope != null) return null;
            if (def.ParamNames.Count != argKinds.Length) return null;
            foreach (var p in def.ParamNames) if (p == "varargin") return null;
            var sb = new System.Text.StringBuilder(name); sb.Append(':');
            foreach (var k in argKinds) sb.Append(k == TKind.Matrix ? 'M' : (k == TKind.Cell ? 'C' : 'S'));
            string key = sb.ToString();
            if (_fnOutKind.TryGetValue(key, out var cached)) return cached;
            if (!_fnOutKindBusy.Add(key)) return null;   // RECURSION: kind desconocido → el llamador usa GuessFnKind
            try
            {
                var cc2 = new CompileCtx { Evaluator = cc.Evaluator };
                for (int i = 0; i < def.ParamNames.Count; i++) cc2.VarKind[def.ParamNames[i]] = argKinds[i];
                if (!ClassifyBody(def.Body, cc2) || !cc2.VarKind.TryGetValue(def.OutputNames[0], out var ok))
                    return null;
                _fnOutKind[key] = ok;
                return ok;
            }
            finally { _fnOutKindBusy.Remove(key); }
        }

        // Builtins que SIEMPRE devuelven escalar aunque el arg sea matriz (norm(v), det(A)...).
        // Se coaccionan a escalar. El resto de reductores (sum/max/min/prod) dependen de la FORMA
        // (sum(v)=escalar pero sum(A,1)=vector) → salida opaca Matrix.
        private static readonly HashSet<string> AlwaysScalarBuiltins = new(StringComparer.OrdinalIgnoreCase)
        { "norm","det","trace","numel","length","rank","cond","nnz","dot","isempty","isscalar","isvector","isrow","iscolumn" };

        private static TKind GuessFnKind(string name)
        {
            // Heurística simple: si el nombre sugiere matrix → matrix; sino scalar.
            // Lista común: B_mat, N_vec, K_mat, zeros, ones, eye, transpose, etc.
            // Cualquier función con args múltiples y nombre tipo "*_mat" o "*_vec" → matrix.
            string lo = name.ToLowerInvariant();
            if (lo.EndsWith("_mat") || lo.EndsWith("_vec") || lo == "zeros" || lo == "ones"
                || lo == "eye" || lo == "transpose" || lo == "inv" || lo == "diag"
                || lo == "linspace" || lo == "logspace" || lo == "repmat" || lo == "reshape"
                || lo == "colon" || lo == "sort" || lo == "cumsum" || lo == "cumprod" || lo == "find")
                return TKind.Matrix;   // builtins que devuelven vector/matriz aun con args escalares
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
                        // FUSION element-wise: C = <cadena +,-,.*,./ de matrices> en un solo
                        // pase SIMD sin temporales, reusando el buffer de C. Mata el alloc/GC
                        // por operacion (el cuello real de A.*B en matrices grandes).
                        if (rhsKind == TKind.Matrix)
                        {
                            var fused = TryEmitEwFused(ir.Name, a.Rhs, cc);
                            if (fused != null) return fused;
                        }
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
                        // SCATTER: V(vec) = RHS con indice VECTORIAL (Fint(d)=Fint(d)+...).
                        // El RHS ya trae el gather+suma; scatter-assign equivale a += porque
                        // los dofs de un elemento no se repiten.
                        if (tgtCall.Args.Count == 1 && InferKind(tgtCall.Args[0], cc) == TKind.Matrix)
                        {
                            var idxV = ConvertExprAsKind(tgtCall.Args[0], cc, TKind.Matrix);
                            if (idxV == null) return null;
                            // PATRON ACUMULACION: V(idx) = V(idx) + X  →  scatter-ADD in-place de X.
                            // Evita clonar el vector entero (ndof) y el gather redundante: el cuello
                            // de fint_c (~167M copias/corrida se vuelven 0).
                            if (a.Rhs is BinaryOp badd && badd.Op == "+")
                            {
                                MatlabNode other = null;
                                if (IsSameIndexed(badd.Left, matIdent.Name, tgtCall.Args)) other = badd.Right;
                                else if (IsSameIndexed(badd.Right, matIdent.Name, tgtCall.Args)) other = badd.Left;
                                if (other != null)
                                {
                                    var addE = ConvertExprAsKind(other, cc, TKind.Matrix);
                                    if (addE != null)
                                    {
                                        var dstA = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar, Expression.Constant(matIdent.Name));
                                        var acc = Expression.Call(JitCtx.MJitScatterAddInPlace, dstA, idxV, addE);
                                        return Expression.Call(cc.CtxParam, JitCtx.MSetMatVar,
                                            Expression.Constant(matIdent.Name), acc);
                                    }
                                }
                            }
                            // general: scatter-assign del RHS evaluado (clona)
                            var valsV = ConvertExprAsKind(a.Rhs, cc, TKind.Matrix);
                            if (valsV == null) return null;
                            var dstV = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar, Expression.Constant(matIdent.Name));
                            var scattered = Expression.Call(JitCtx.MJitScatterAssign, dstV, idxV, valsV);
                            return Expression.Call(cc.CtxParam, JitCtx.MSetMatVar,
                                Expression.Constant(matIdent.Name), scattered);
                        }
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
                case Assignment amo when amo.Targets.Count > 1:
                    {
                        // MULTI-OUTPUT: [a, b, ...] = userfn(args)  (el return-map: [sr,reg]=mc_return(...)).
                        // Los outputs son MValue OPACOS (matriz/escalar/string) en el Scope: `reg` (string)
                        // fluye sin soporte de strings en el JIT. Solo funciones de USUARIO.
                        if (amo.Rhs is not CallOrIndex mc || mc.Target is not IdentRef mcId) return null;
                        if (!cc.Evaluator.JitIsUserFunction(mcId.Name)) return null;
                        var argArr = BuildMValueArgs(mc.Args, cc);
                        if (argArr == null) return null;
                        var outsVar = Expression.Variable(typeof(MValue[]), "outs");
                        var callE = Expression.Call(cc.CtxParam, JitCtx.MCallMultiMV,
                            Expression.Constant(mcId.Name), argArr);
                        var stmts = new List<Expression> { Expression.Assign(outsVar, callE) };
                        for (int k = 0; k < amo.Targets.Count; k++)
                        {
                            if (amo.Targets[k] is not IdentRef tv || tv.Name == "~") continue;   // ignore
                            var elem = Expression.ArrayIndex(outsVar, Expression.Constant(k));   // MValue
                            var kind = cc.VarKind[tv.Name];
                            if (kind == TKind.Scalar)
                                stmts.Add(Expression.Assign(ScalarAccess(cc, tv.Name),
                                    Expression.Call(JitCtx.MMatToScalar, elem)));
                            else
                                stmts.Add(Expression.Call(cc.CtxParam, JitCtx.MSetMatVar,
                                    Expression.Constant(tv.Name), elem));
                        }
                        return Expression.Block(new[] { outsVar }, stmts);
                    }
                case IfBlock ib:
                    {
                        // Construir IfThenElse anidado desde el final (el else va con Cond=null).
                        Expression elseE = Expression.Empty();
                        for (int bi = ib.Branches.Count - 1; bi >= 0; bi--)
                        {
                            var (cond, cbody) = ib.Branches[bi];
                            var bodyExprs = new List<Expression>();
                            foreach (var st in cbody)
                            {
                                if (st is CommentStmt) continue;
                                var se = ConvertStmt(st, cc);
                                if (se == null) return null;
                                bodyExprs.Add(se);
                            }
                            Expression bodyBlock = bodyExprs.Count > 0
                                ? Expression.Block(bodyExprs) : Expression.Empty();
                            if (cond == null) elseE = bodyBlock;
                            else
                            {
                                var condE = ConvertCond(cond, cc);
                                if (condE == null) return null;
                                elseE = Expression.IfThenElse(condE, bodyBlock, elseE);
                            }
                        }
                        return elseE;
                    }
                case ReturnStmt:
                    // `return` temprano (JIT de funciones): Goto al label de salida. En contexto
                    // de bucle (ReturnLabel null) no se soporta -> bailout.
                    return cc.ReturnLabel != null ? Expression.Goto(cc.ReturnLabel) : null;
                case ForLoop fl:
                    {
                        // Bucle anidado: for v = start:end  (step 1). Emite un Loop IL con
                        // contador double y break cuando v>end.
                        if (fl.Iter is not Range r2 || r2.Step != null) return null;
                        if (!cc.SlotIdx.ContainsKey(fl.VarName)) return null;
                        var startE = ConvertExprAsKind(r2.Start, cc, TKind.Scalar);
                        var endE = ConvertExprAsKind(r2.End, cc, TKind.Scalar);
                        if (startE == null || endE == null) return null;
                        var lv = ScalarAccess(cc, fl.VarName);
                        var endLocal = Expression.Variable(typeof(double), "for_end");
                        var brk = Expression.Label("for_brk");
                        var cont = Expression.Label("for_cont");   // `continue` salta AQUI (antes del incremento)
                        var innerBody = new List<Expression>
                        {
                            Expression.IfThen(Expression.GreaterThan(lv, endLocal), Expression.Break(brk))
                        };
                        var savedBrkF = cc.BreakLabel; var savedContF = cc.ContinueLabel;
                        cc.BreakLabel = brk; cc.ContinueLabel = cont;
                        foreach (var st in fl.Body)
                        {
                            if (st is CommentStmt) continue;
                            var se = ConvertStmt(st, cc);
                            if (se == null) { cc.BreakLabel = savedBrkF; cc.ContinueLabel = savedContF; return null; }
                            innerBody.Add(se);
                        }
                        cc.BreakLabel = savedBrkF; cc.ContinueLabel = savedContF;
                        innerBody.Add(Expression.Label(cont));   // destino de continue
                        innerBody.Add(Expression.Assign(lv, Expression.Add(lv, Expression.Constant(1.0))));
                        var loop = Expression.Loop(Expression.Block(innerBody), brk);
                        return Expression.Block(
                            new[] { endLocal },
                            Expression.Assign(endLocal, endE),
                            Expression.Assign(lv, startE),
                            loop);
                    }
                case WhileLoop wl:
                    {
                        // while cond ... end : Loop IL que revalua la condicion cada vuelta.
                        var wbrk = Expression.Label("while_brk");
                        var wcont = Expression.Label("while_cont");   // continue → revalua condicion
                        var savedBrkW = cc.BreakLabel; var savedContW = cc.ContinueLabel;
                        cc.BreakLabel = wbrk; cc.ContinueLabel = wcont;
                        var wbody = new List<Expression> { Expression.Label(wcont) };
                        var wcondE = ConvertCond(wl.Cond, cc);
                        if (wcondE == null) { cc.BreakLabel = savedBrkW; cc.ContinueLabel = savedContW; return null; }
                        wbody.Add(Expression.IfThen(Expression.Not(wcondE), Expression.Break(wbrk)));
                        foreach (var st in wl.Body)
                        {
                            if (st is CommentStmt) continue;
                            var se = ConvertStmt(st, cc);
                            if (se == null) { cc.BreakLabel = savedBrkW; cc.ContinueLabel = savedContW; return null; }
                            wbody.Add(se);
                        }
                        cc.BreakLabel = savedBrkW; cc.ContinueLabel = savedContW;
                        return Expression.Loop(Expression.Block(wbody), wbrk);
                    }
                case BreakStmt:
                    return cc.BreakLabel != null ? Expression.Break(cc.BreakLabel) : null;
                case ContinueStmt:
                    return cc.ContinueLabel != null ? Expression.Goto(cc.ContinueLabel) : null;
                case ExprStmt _:
                    return Expression.Empty();
                default:
                    return null;
            }
        }

        /// <summary>true si `node` es V(args) con el mismo nombre e indices que el target —
        /// para reconocer la acumulacion V(idx)=V(idx)+X.</summary>
        private static bool IsSameIndexed(MatlabNode node, string name, List<MatlabNode> args)
        {
            if (node is not CallOrIndex ci || ci.Target is not IdentRef id || id.Name != name) return false;
            if (ci.Args.Count != args.Count) return false;
            for (int i = 0; i < args.Count; i++) if (!NodeEq(ci.Args[i], args[i])) return false;
            return true;
        }
        /// <summary>Igualdad estructural minima de indices (IdentRef/NumberLit). Conservador:
        /// cualquier otra cosa → no igual (cae al camino general con clon).</summary>
        private static bool NodeEq(MatlabNode a, MatlabNode b) => (a, b) switch
        {
            (IdentRef x, IdentRef y) => x.Name == y.Name,
            (NumberLit x, NumberLit y) => x.Value == y.Value,
            _ => false
        };

        // ─── Fusion element-wise: C = P.*Q+P → un solo loop SIMD sin temporales ───────
        /// <summary>true si `node` es una expresion element-wise PURA (solo +,-,.*,./ y
        /// unario -/+ sobre matrices densas o literales numericos). Recolecta los nombres
        /// de las matrices hoja (dedup, en orden) en `leaves`. Conservador: escalares
        /// variables, '*'/'/'/funciones → NO fusiona (return false).</summary>
        private static bool CollectEw(MatlabNode node, CompileCtx cc, System.Collections.Generic.List<string> leaves)
        {
            switch (node)
            {
                case NumberLit _: return true;
                case IdentRef ir:
                    if (!cc.VarKind.TryGetValue(ir.Name, out var k) || k != TKind.Matrix) return false;
                    if (!leaves.Contains(ir.Name)) leaves.Add(ir.Name);
                    return true;
                case UnaryOp u when u.Op == "-" || u.Op == "+":
                    return CollectEw(u.Operand, cc, leaves);
                case BinaryOp b when b.Op is "+" or "-" or ".*" or "./":
                    return CollectEw(b.Left, cc, leaves) && CollectEw(b.Right, cc, leaves);
                default: return false;
            }
        }

        /// <summary>Genera el cuerpo del kernel. vec=true → sobre Vector&lt;double&gt; (bloque
        /// SIMD); vec=false → sobre double (resto escalar). datas = double[][] (Data de las
        /// hojas), pos = offset SIMD o indice escalar.</summary>
        private static Expression GenEw(MatlabNode node, System.Collections.Generic.List<string> leaves,
            ParameterExpression datas, ParameterExpression pos, bool vec)
        {
            switch (node)
            {
                case NumberLit nl:
                    return vec ? Expression.New(JitCtx.CVecFromScalar, Expression.Constant(nl.Value))
                               : (Expression)Expression.Constant(nl.Value, typeof(double));
                case IdentRef ir:
                    {
                        int k = leaves.IndexOf(ir.Name);
                        var arrK = Expression.ArrayIndex(datas, Expression.Constant(k));   // datas[k] → double[]
                        return vec ? Expression.New(JitCtx.CVecFromArray, arrK, pos)        // new Vector(datas[k], off)
                                   : Expression.ArrayIndex(arrK, pos);                      // datas[k][i]
                    }
                case UnaryOp u when u.Op == "-":
                    return Expression.Negate(GenEw(u.Operand, leaves, datas, pos, vec));
                case UnaryOp u when u.Op == "+":
                    return GenEw(u.Operand, leaves, datas, pos, vec);
                case BinaryOp b:
                    var L = GenEw(b.Left, leaves, datas, pos, vec);
                    var R = GenEw(b.Right, leaves, datas, pos, vec);
                    return b.Op switch
                    {
                        "+" => Expression.Add(L, R),
                        "-" => Expression.Subtract(L, R),
                        ".*" => Expression.Multiply(L, R),
                        "./" => Expression.Divide(L, R),
                        _ => null
                    };
                default: return null;
            }
        }

        /// <summary>Si `rhs` es element-wise puro con ≥1 matriz hoja, emite la asignacion
        /// `name = &lt;fusion&gt;`: un solo pase SIMD sin temporales, reusando el buffer de
        /// name. Devuelve null si no aplica (→ camino normal).</summary>
        private static Expression TryEmitEwFused(string name, MatlabNode rhs, CompileCtx cc)
        {
            var leaves = new System.Collections.Generic.List<string>();
            if (!CollectEw(rhs, cc, leaves) || leaves.Count == 0) return null;
            // solo vale si hay al menos una operacion (si es solo `C = P`, que lo haga el camino normal)
            if (rhs is IdentRef) return null;
            try
            {
                var datasP = Expression.Parameter(typeof(double[][]), "datas");
                var offP = Expression.Parameter(typeof(int), "off");
                var vecBody = GenEw(rhs, leaves, datasP, offP, true);
                if (vecBody == null) return null;
                var vk = Expression.Lambda<Func<double[][], int, System.Numerics.Vector<double>>>(vecBody, datasP, offP).Compile();

                var idxP = Expression.Parameter(typeof(int), "i");
                var sclBody = GenEw(rhs, leaves, datasP, idxP, false);
                if (sclBody == null) return null;
                var sk = Expression.Lambda<Func<double[][], int, double>>(sclBody, datasP, idxP).Compile();

                var leafElems = new Expression[leaves.Count];
                for (int i = 0; i < leaves.Count; i++)
                    leafElems[i] = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar, Expression.Constant(leaves[i]));
                var leavesArr = Expression.NewArrayInit(typeof(MValue), leafElems);
                var reuseE = Expression.Call(cc.CtxParam, JitCtx.MGetMatrixOrNull, Expression.Constant(name));
                var fused = Expression.Call(JitCtx.MEwFusedRun, leavesArr, reuseE,
                    Expression.Constant(vk), Expression.Constant(sk));
                EwEmitOk++;
                return Expression.Call(cc.CtxParam, JitCtx.MSetMatVar, Expression.Constant(name), fused);
            }
            catch { EwEmitFail++; return null; }   // cualquier problema generando el kernel → camino normal
        }

        /// <summary>Condicion booleana (para if): comparaciones/AND/OR sobre escalares.</summary>
        private static Expression ConvertCond(MatlabNode node, CompileCtx cc)
        {
            if (node is BinaryOp b)
            {
                if (b.Op is ">" or "<" or ">=" or "<=" or "==" or "~=" or "!=")
                {
                    var L = ConvertExprAsKind(b.Left, cc, TKind.Scalar);
                    var R = ConvertExprAsKind(b.Right, cc, TKind.Scalar);
                    if (L == null || R == null) return null;
                    return b.Op switch
                    {
                        ">" => Expression.GreaterThan(L, R),
                        "<" => Expression.LessThan(L, R),
                        ">=" => Expression.GreaterThanOrEqual(L, R),
                        "<=" => Expression.LessThanOrEqual(L, R),
                        "==" => Expression.Equal(L, R),
                        _ => Expression.NotEqual(L, R),
                    };
                }
                if (b.Op is "&&" or "&")
                {
                    var L = ConvertCond(b.Left, cc); var R = ConvertCond(b.Right, cc);
                    if (L == null || R == null) return null;
                    return Expression.AndAlso(L, R);
                }
                if (b.Op is "||" or "|")
                {
                    var L = ConvertCond(b.Left, cc); var R = ConvertCond(b.Right, cc);
                    if (L == null || R == null) return null;
                    return Expression.OrElse(L, R);
                }
            }
            // condicion escalar suelta: cond != 0
            var s = ConvertExprAsKind(node, cc, TKind.Scalar);
            if (s == null) return null;
            return Expression.NotEqual(s, Expression.Constant(0.0, typeof(double)));
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
                        if (b.Op == "/" || b.Op == "./")
                        {
                            // A/B (B 1×1 → escala; si no → mrdivide) y A./B (element-wise). Misma
                            // semantica que el interprete. Necesario para el return-map: /(2*sj), /den.
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(b.Op == "/" ? JitCtx.MMatDiv : JitCtx.MMatEwDiv, Lm, Rm);
                        }
                        if (b.Op == "\\")   // A\b : mldivide (resolver A·x=b)
                        {
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(JitCtx.MMatLDiv, Lm, Rm);
                        }
                        if (b.Op == ".^" || b.Op == "^")   // A.^B element-wise / A^B mpower
                        {
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(b.Op == ".^" ? JitCtx.MMatEwPow : JitCtx.MMatPow, Lm, Rm);
                        }
                        int cmp = b.Op switch { "<" => 0, ">" => 1, "<=" => 2, ">=" => 3, "==" => 4, "~=" => 5, "!=" => 5, _ => -1 };
                        if (cmp >= 0)   // comparacion element-wise → matriz logica (v>5, A==B)
                        {
                            var Lm = ConvertExprAsKind(b.Left, cc, TKind.Matrix);
                            var Rm = ConvertExprAsKind(b.Right, cc, TKind.Matrix);
                            if (Lm == null || Rm == null) return null;
                            return Expression.Call(JitCtx.MMatCmp, Lm, Rm, Expression.Constant(cmp));
                        }
                        return null;
                    }
                case CallOrIndex coi when coi.Target is IdentRef ident:
                    return ConvertCallOrIndex(ident.Name, coi.Args, cc);
                case CellIndex cix when cix.Target is IdentRef cellId:
                    {
                        // c{i} o c{i,j} → GetCellElem(name, i, j). j=0 marca indice unico.
                        Expression iE, jE;
                        if (cix.Args.Count == 1)
                        {
                            iE = ConvertExprAsKind(cix.Args[0], cc, TKind.Scalar);
                            jE = Expression.Constant(0.0, typeof(double));
                        }
                        else if (cix.Args.Count == 2)
                        {
                            iE = ConvertExprAsKind(cix.Args[0], cc, TKind.Scalar);
                            jE = ConvertExprAsKind(cix.Args[1], cc, TKind.Scalar);
                        }
                        else return null;
                        if (iE == null || jE == null) return null;
                        return Expression.Call(cc.CtxParam, JitCtx.MGetCellElem,
                            Expression.Constant(cellId.Name), iE, jE);
                    }
                case MatrixLit ml:
                    return ConvertMatrixLit(ml, cc);
                case Range rg:                                   // a:b / a:s:b → vector fila
                    {
                        var s = ConvertExprAsKind(rg.Start, cc, TKind.Scalar);
                        var e = ConvertExprAsKind(rg.End, cc, TKind.Scalar);
                        var st = rg.Step != null ? ConvertExprAsKind(rg.Step, cc, TKind.Scalar)
                                                 : Expression.Constant(1.0, typeof(double));
                        if (s == null || e == null || st == null) return null;
                        return Expression.Call(JitCtx.MMakeRange, s, st, e);
                    }
                default:
                    return null;
            }
        }

        private static Expression ConvertCallOrIndex(string name, List<MatlabNode> args, CompileCtx cc)
        {
            bool isFn = cc.Evaluator.JitIsFunction(name);
            // isempty(x): logico escalar (1.0/0.0). El arg suele ser MATRIZ (n, D del return-map),
            // asi que NO pasa por el path double[] (que exige escalar). Emitimos MIsEmpty directo.
            if (name == "isempty" && args.Count == 1 && !cc.Evaluator.JitIsUserFunction(name))
            {
                var mk = InferKind(args[0], cc);
                if (mk == TKind.Matrix)
                {
                    var mv = ConvertExprAsKind(args[0], cc, TKind.Matrix);
                    if (mv == null) return null;
                    return Expression.Call(JitCtx.MIsEmpty, mv);
                }
                if (mk == TKind.Scalar) return Expression.Constant(0.0, typeof(double));  // escalar nunca vacio
            }
            // FAST-PATH: funcion matematica escalar builtin (mod, floor, sqrt, sin, ...) que el
            // usuario NO redefinio -> llamada estatica nativa, sin MValue ni diccionario.
            // Sin esto, un mod() dentro de un loop FEM tiraba TODO el loop al dispatch lento.
            // ¿Todos los args son escalares? Si alguno es matriz, la vía inline (que fuerza escalar)
            // NO aplica: sqrt(v)/abs(v)/exp(-v) son element-wise sobre el vector -> caen a MCallMV.
            bool allScalarArgs = true;
            foreach (var a in args) { var ak = InferKind(a, cc); if (ak == null || ak == TKind.Matrix) { allScalarArgs = false; break; } }
            if (isFn && allScalarArgs && JitCtx.InlineMath.TryGetValue(name, out var im)
                     && im.Argc == args.Count && !cc.Evaluator.JitIsUserFunction(name))
            {
                var iargs = new Expression[args.Count];
                for (int i = 0; i < args.Count; i++)
                {
                    var e = ConvertExprAsKind(args[i], cc, TKind.Scalar);
                    if (e == null) return null;
                    iargs[i] = e;
                }
                return Expression.Call(im.M, iargs);
            }
            // Funcion (usuario O builtin) que hay que llamar por MValue[] (MCallMV, 1ª salida) en vez
            // del path double[]: cuando algun arg es MATRIZ (norm(v), yieldparts(sig,...)) O cuando la
            // SALIDA es matriz aunque los args sean escalares (r=maybe(flag) que devuelve un vector).
            // El KIND de salida se infiere del cuerpo (usuario) o por heurística (builtin); el tipo del
            // Expression coincide (double si escalar via MMatToScalar, MValue si matriz).
            // La vía rápida inline ya se probó arriba; solo la excluimos si REALMENTE aplica a esta
            // llamada (nombre+aridad+no-redefinida). Así `max(v)`/`min(v)` de 1 arg-matriz (reduccion)
            // caen aquí en vez de forzarse a escalar.
            bool inlineApplies = allScalarArgs && JitCtx.InlineMath.TryGetValue(name, out var _im3)
                && _im3.Argc == args.Count && !cc.Evaluator.JitIsUserFunction(name);
            if (isFn && !inlineApplies)
            {
                var aks = new TKind[args.Count];
                bool anyMat = false;
                for (int i = 0; i < args.Count; i++)
                {
                    var ak = InferKind(args[i], cc);
                    if (ak == null) return null;
                    aks[i] = ak.Value; if (ak == TKind.Matrix) anyMat = true;
                }
                // KIND de salida: usuario→inferido del cuerpo; builtin con arg matriz→norm/det.. escalar,
                // resto opaco Matrix (sum(A,1) da vector); builtin con args escalares→heurística.
                TKind ok = cc.Evaluator.JitIsUserFunction(name)
                    ? (InferUserFnOutKind(name, aks, cc) ?? TKind.Matrix)
                    : (anyMat ? (AlwaysScalarBuiltins.Contains(name) ? TKind.Scalar : TKind.Matrix)
                              : GuessFnKind(name));
                if (anyMat || ok == TKind.Matrix)
                {
                    var argArr = BuildMValueArgs(args, cc);
                    if (argArr == null) return null;
                    var callMV = Expression.Call(cc.CtxParam, JitCtx.MCallMV, Expression.Constant(name), argArr);
                    return ok == TKind.Scalar ? (Expression)Expression.Call(JitCtx.MMatToScalar, callMV) : callMV;
                }
            }
            if (isFn)
            {
                // Function call. Decidir scalar/matrix por heurística (= GuessFnKind).
                var fnKind = GuessFnKind(name);
                // El path double[] solo admite args ESCALARES. Si algún arg es matriz
                // (p.ej. interp1(xg,yg,xq) con xg,yg vectores), forzarlo a escalar dispara
                // MMatToScalar("Expected scalar, got matrix"). Claudicamos -> el interprete
                // ejecuta la funcion (correcto). Las funciones del hot loop son anidadas y no
                // llegan aca, asi que no hay costo de rendimiento relevante.
                for (int i = 0; i < args.Count; i++)
                    if (InferKind(args[i], cc) != TKind.Scalar) return null;
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
                if (args[0] is ColonAll)                 // A(:) → vector columna column-major
                {
                    var matC = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar, Expression.Constant(name));
                    return Expression.Call(JitCtx.MMatColon, matC);
                }
                // GATHER: A(vec) con indice vectorial (el u(d') del FEM)
                if (InferKind(args[0], cc) == TKind.Matrix)
                {
                    var idxV = ConvertExprAsKind(args[0], cc, TKind.Matrix);
                    if (idxV == null) return null;
                    var matV = Expression.Call(cc.CtxParam, JitCtx.MGetMatVar, Expression.Constant(name));
                    return Expression.Call(JitCtx.MJitGather, matV, idxV);
                }
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

        /// <summary>Arma un <c>MValue[]</c> para una llamada, convirtiendo cada arg por su kind:
        /// matriz → MValue directo; escalar → <c>new MValue(double)</c>. null si algún arg no se puede.</summary>
        private static Expression BuildMValueArgs(List<MatlabNode> args, CompileCtx cc)
        {
            var elems = new Expression[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                var k = InferKind(args[i], cc);
                if (k == null || k == TKind.Cell) return null;
                if (k == TKind.Matrix)
                {
                    var m = ConvertExprAsKind(args[i], cc, TKind.Matrix);
                    if (m == null) return null;
                    elems[i] = m;
                }
                else
                {
                    var s = ConvertExprAsKind(args[i], cc, TKind.Scalar);
                    if (s == null) return null;
                    elems[i] = Expression.New(JitCtx.CMValueScalar, s);   // new MValue(double)
                }
            }
            return Expression.NewArrayInit(typeof(MValue), elems);
        }

        private static Expression ConvertMatrixLit(MatrixLit ml, CompileCtx cc)
        {
            // Literal VACIO []  →  MValue 0×0 (para `n=[]` del return-map). Tambien filas todas vacias.
            bool allEmpty = ml.Rows.Count == 0;
            if (!allEmpty) { allEmpty = true; foreach (var row in ml.Rows) if (row.Count > 0) { allEmpty = false; break; } }
            if (allEmpty) return Expression.Call(JitCtx.MMakeEmpty);
            // Convertir cada entrada; ¿son TODAS escalares (double)? -> vía rápida flatten.
            // (numeros, vars escalares, aritmetica escalar, indexado A(i)). Si alguna compila a un
            // BLOQUE matriz (MValue: A*b, A(:,j), otra matriz) -> vía CONCAT.
            int rows = ml.Rows.Count;
            int cols = ml.Rows[0].Count;
            for (int i = 0; i < rows; i++) if (ml.Rows[i].Count != cols) cols = -1;   // filas desiguales: no flatten
            bool allScalar = cols >= 0;
            var conv = new Expression[rows][];
            for (int i = 0; i < rows && allScalar; i++)
            {
                conv[i] = new Expression[ml.Rows[i].Count];
                for (int j = 0; j < ml.Rows[i].Count; j++)
                {
                    var e = ConvertExpr(ml.Rows[i][j], cc);
                    if (e == null) return null;
                    if (e.Type != typeof(double)) { allScalar = false; break; }
                    conv[i][j] = e;
                }
            }
            if (allScalar)
            {
                if (rows == 1)
                    return Expression.Call(JitCtx.MMakeRowVec, Expression.NewArrayInit(typeof(double), conv[0]));
                var flat = new Expression[rows * cols];
                for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) flat[i * cols + j] = conv[i][j];
                return Expression.Call(JitCtx.MMakeMatrix2D,
                    Expression.Constant(rows), Expression.Constant(cols), Expression.NewArrayInit(typeof(double), flat));
            }
            // CONCAT: cada fila = HorzCat de sus bloques (escalar→1×1, matriz tal cual); luego VertCat.
            var rowExprs = new Expression[rows];
            for (int i = 0; i < rows; i++)
            {
                var pieces = new Expression[ml.Rows[i].Count];
                for (int j = 0; j < ml.Rows[i].Count; j++)
                {
                    var m = ConvertExprAsKind(ml.Rows[i][j], cc, TKind.Matrix);
                    if (m == null) return null;
                    pieces[j] = m;
                }
                rowExprs[i] = Expression.Call(JitCtx.MHorzCat, Expression.NewArrayInit(typeof(MValue), pieces));
            }
            if (rows == 1) return rowExprs[0];
            return Expression.Call(JitCtx.MVertCat, Expression.NewArrayInit(typeof(MValue), rowExprs));
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
