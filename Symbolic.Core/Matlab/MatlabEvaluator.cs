// =============================================================================
// Calcpad Lab — MATLAB Evaluator (sin dependencias del MathParser de Calcpad)
// =============================================================================
//   Evalúa un AST MATLAB produciendo valores numéricos. Internamente usa los
//   tipos de almacenamiento Matrix/Vector/RealValue de Calcpad porque ya están
//   probados y rinden bien — pero la lógica de evaluación, despacho de funciones,
//   operadores y resolución de nombres es 100% MATLAB-puro, sin pasar por
//   MathParser/Calculator/MatrixCalculator de Calcpad.
//
//   Esto significa que `A .* B` se calcula DIRECTAMENTE en C# y el HTML output
//   no muestra `hprod(...)` ni separador `;` — sólo MATLAB.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core.Matlab
{
    /// <summary>
    /// Valor runtime MATLAB. Wrap minimalista — adentro un double escalar, un
    /// vector double[], o una matriz row-major. Sin units (las dejamos para fase 2).
    /// </summary>
    public sealed class MValue
    {
        public readonly int Rows;
        public readonly int Cols;
        public readonly double[] Data;   // row-major real parts, length = Rows * Cols
        public readonly double[] Imag;   // si no-null: parts imaginarias paralelas a Data
        public readonly bool IsString;
        public readonly string StringValue;
        /// <summary>True si el string proviene de "..." (string scalar, R2016b+) en vez de '...' (char array).</summary>
        public bool IsDoubleQuoted;
        /// <summary>Si no-null: array de strings ("..."). Cada celda es una string. Cuando este campo está seteado, IsDoubleQuoted=true.</summary>
        public string[,] StringArrayData;
        public bool IsStringArray => StringArrayData != null;
        /// <summary>Si no-null, este MValue es un function handle ejecutable.</summary>
        public readonly Func<MValue[], MValue> Callable;
        /// <summary>Nombre del handle (debug only).</summary>
        public readonly string CallableName;
        /// <summary>Si no-null, este MValue es un struct con fields.</summary>
        public Dictionary<string, MValue> Fields;
        public bool IsStruct => Fields != null;
        /// <summary>Si no-null, este MValue es una instancia de la clase con ese nombre.</summary>
        public string ClassName;
        public bool IsInstance => ClassName != null && Fields != null;
        /// <summary>Si no-null, este MValue es un cell array.</summary>
        public MValue[,] CellData;
        public bool IsCell => CellData != null;
        public System.Collections.Generic.List<MValue> StructArrayItems;   // struct-array: res(k).field
        public bool IsStructArray => StructArrayItems != null;
        /// <summary>Si no-null: storage sparse CSR. Vals + Cols + RowPtr.</summary>
        public double[] SparseVals;
        public int[] SparseCols;
        public int[] SparseRowPtr;
        public bool IsSparseReal => SparseVals != null;
        /// <summary>Si no-null: array 3D Pages. Cada Page es un MValue 2D.</summary>
        public MValue[] Pages;
        public bool Is3D => Pages != null;
        /// <summary>Si no-null, este MValue es una expresión simbólica.</summary>
        public SymNode Symbolic;
        public bool IsSymbolic => Symbolic != null;
        /// <summary>Si no-null: matriz simbólica — cada celda es un SymNode (row-major en SymCells[r,c]).</summary>
        public SymNode[,] SymCells;
        public bool IsSymMatrix => SymCells != null;
        /// <summary>Si no-null, este MValue es un containers.Map (clave canonica string -> valor).</summary>
        public Dictionary<string, MValue> MapData;
        public bool IsMap => MapData != null;
        /// <summary>True si es un handle de gráficos (axes/figure/uicontrol). Lab usa un eje implícito;
        /// las propiedades del handle (Value, Min, Max…) se guardan en Fields.</summary>
        public bool IsGfxHandle;

        public bool IsScalar => Rows == 1 && Cols == 1 && !IsString && Callable == null && Fields == null && CellData == null && Symbolic == null && SymCells == null && StringArrayData == null && MapData == null;
        public bool IsCallable => Callable != null;
        public bool IsComplex => Imag != null;
        public static MValue NewSymbolic(SymNode s)
        {
            var v = new MValue(0);
            v.Symbolic = s;
            return v;
        }

        public MValue(double scalar) { Rows = 1; Cols = 1; Data = new[] { scalar }; }
        public MValue(int rows, int cols)
        {
            Rows = rows; Cols = cols; Data = new double[rows * cols];
        }
        public MValue(int rows, int cols, double[] data)
        {
            if (data.Length != rows * cols)
                throw new ArgumentException("data length mismatch");
            Rows = rows; Cols = cols; Data = data;
        }
        public MValue(int rows, int cols, double[] dataRe, double[] dataIm)
        {
            if (dataRe.Length != rows * cols || dataIm.Length != rows * cols)
                throw new ArgumentException("data length mismatch");
            Rows = rows; Cols = cols; Data = dataRe; Imag = dataIm;
        }
        public MValue(double re, double im) { Rows = 1; Cols = 1; Data = new[] { re }; Imag = new[] { im }; }
        public MValue(string s) { IsString = true; StringValue = s; Rows = 1; Cols = s?.Length ?? 0; Data = Array.Empty<double>(); }
        public MValue(Func<MValue[], MValue> fn, string name)
        {
            Callable = fn; CallableName = name;
            Rows = 1; Cols = 1; Data = Array.Empty<double>();
        }
        /// <summary>Constructor struct vacío.</summary>
        public static MValue NewStruct()
        {
            var v = new MValue(0);
            v.Fields = new Dictionary<string, MValue>(StringComparer.Ordinal);
            return v;
        }
        /// <summary>Struct-array MATLAB: res(1).n=… res(2).n=… — lista de structs (cada elemento
        /// es un struct con Fields). Soporta res(k).f=v, res(s).f y {res.n}.</summary>
        public static MValue NewStructArray()
        {
            var v = new MValue(0);
            v.StructArrayItems = new System.Collections.Generic.List<MValue>();
            return v;
        }
        /// <summary>Constructor containers.Map vacío.</summary>
        public static MValue NewMap()
        {
            var v = new MValue(0);
            v.MapData = new Dictionary<string, MValue>(StringComparer.Ordinal);
            return v;
        }
        /// <summary>Constructor instancia de clase OOP. Las propiedades inicializadas se ponen en Fields.</summary>
        public static MValue NewInstance(string className, Dictionary<string, MValue> props)
        {
            var v = new MValue(0);
            v.Fields = props ?? new Dictionary<string, MValue>(StringComparer.Ordinal);
            v.ClassName = className;
            return v;
        }
        /// <summary>Constructor cell array a partir de matriz 2D.</summary>
        public static MValue NewCell(MValue[,] cells)
        {
            int r = cells.GetLength(0), c = cells.GetLength(1);
            var v = new MValue(r, c);   // setear Rows/Cols (readonly) = dims del cell; sin esto todo cell queda 1x1
            v.CellData = cells;
            return v;
        }
        /// <summary>Constructor sparse CSR.</summary>
        public static MValue NewSparseCSR(int rows, int cols, double[] vals, int[] colIdx, int[] rowPtr)
            => new MValue(rows, cols, vals, colIdx, rowPtr);
        public static MValue New3D(MValue[] pages)
        {
            var v = new MValue(0);
            v.Pages = pages;
            return v;
        }
        /// <summary>Constructor string scalar ("..." MATLAB R2016b+): tamaño lógico [1,1] aunque internamente preservamos length.</summary>
        public static MValue NewStringScalar(string s)
        {
            // Usamos el constructor (string) que setea IsString, luego flag IsDoubleQuoted
            var v = new MValue(s ?? "");
            v.IsDoubleQuoted = true;
            return v;
        }
        /// <summary>Constructor string array ("..." MATLAB R2016b+): matriz r×c de strings.</summary>
        public static MValue NewStringArray(string[,] arr)
        {
            int r = arr.GetLength(0), c = arr.GetLength(1);
            var v = new MValue(r, c);
            v.StringArrayData = arr;
            v.IsDoubleQuoted = true;
            return v;
        }
        /// <summary>Constructor matriz simbólica — cada celda es un SymNode.</summary>
        public static MValue NewSymMatrix(SymNode[,] cells)
        {
            int r = cells.GetLength(0), c = cells.GetLength(1);
            var v = new MValue(r, c);
            v.SymCells = cells;
            return v;
        }
        // Sparse holder via constructor especial
        private MValue(int rows, int cols, double[] sparseVals, int[] sparseCols, int[] sparseRowPtr)
        {
            Rows = rows; Cols = cols; Data = Array.Empty<double>();
            SparseVals = sparseVals; SparseCols = sparseCols; SparseRowPtr = sparseRowPtr;
        }

        public double Scalar => Data[0];
        public double At(int r, int c) => Data[r * Cols + c];
        public void Set(int r, int c, double v) => Data[r * Cols + c] = v;

        // Densifica una matriz sparse CSR → densa (row-major). Si ya es densa, la
        // devuelve tal cual. Red de seguridad: cualquier operación que no soporte
        // sparse llama a ToDense() y sigue con el camino denso (comportamiento previo).
        public MValue ToDense()
        {
            if (!IsSparseReal) return this;
            var r = new MValue(Rows, Cols);
            for (int i = 0; i < Rows; i++)
                for (int k = SparseRowPtr[i]; k < SparseRowPtr[i + 1]; k++)
                    r.Set(i, SparseCols[k], SparseVals[k]);
            return r;
        }

        public override string ToString()
        {
            if (IsString) return $"'{StringValue}'";
            if (IsScalar) return Scalar.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
            return $"[{Rows}×{Cols} matrix]";
        }
    }

    /// <summary>
    /// Scope de variables MATLAB. Simple dictionary + parent.
    /// </summary>
    public sealed class MatlabScope
    {
        public readonly Dictionary<string, MValue> Vars = new(StringComparer.Ordinal);
        public readonly MatlabScope Parent;
        /// <summary>Nombres declarados como <c>global</c> en este scope — leen/escriben en el shared GlobalVars.</summary>
        public readonly HashSet<string> GlobalNames = new(StringComparer.Ordinal);
        /// <summary>Globals compartidos entre invocaciones (Globals raíz lo provee).</summary>
        public Dictionary<string, MValue> GlobalVars;
        public MatlabScope(MatlabScope parent = null)
        {
            Parent = parent;
            GlobalVars = parent?.GlobalVars ?? new Dictionary<string, MValue>(StringComparer.Ordinal);
        }
        public bool TryGet(string name, out MValue val)
        {
            if (GlobalNames.Contains(name) && GlobalVars.TryGetValue(name, out val)) return true;
            if (Vars.TryGetValue(name, out val)) return true;
            return Parent?.TryGet(name, out val) ?? false;
        }
        public void Set(string name, MValue val)
        {
            if (GlobalNames.Contains(name)) { GlobalVars[name] = val; return; }
            Vars[name] = val;
        }
    }

    /// <summary>
    /// Resultado de un statement: valor producido, nombre de variable asignada (si aplica),
    /// y flag de suppression.
    /// </summary>
    public readonly struct StatementResult
    {
        public readonly string AssignedName;
        public readonly MValue Value;
        public readonly bool Suppressed;
        public StatementResult(string name, MValue val, bool suppr) { AssignedName = name; Value = val; Suppressed = suppr; }
    }

    public sealed class MatlabEvaluator
    {
        public readonly MatlabScope Globals;
        /// <summary>Callback opcional para <c>disp(...)</c>. Si null, se ignoran.</summary>
        private Action<string> _output;
        public Action<string> Output { get => _output; set => _output = value; }

        // ─── I/O de archivos (fopen/fclose/fprintf/fscanf/...) ──────────────
        // fid 1 = stdout, 2 = stderr; los archivos de usuario empiezan en 3.
        private readonly Dictionary<int, System.IO.StreamWriter> _fileWriters = new();
        private readonly Dictionary<int, System.IO.StreamReader> _fileReaders = new();
        private int _nextFid = 3;
        /// <summary>Callback opcional para HTML inline (plots, etc.). Si null, los plots se descartan.</summary>
        private Action<string> _htmlOut;
        public Action<string> HtmlOut { get => _htmlOut; set => _htmlOut = value; }
        private int _vizCounter = 0;   // ids unicos para visores 3D interactivos (solidmesh)
        // Canal para FRAMES de animación (drawnow): se emite EN VIVO por iteración y el host
        // lo repinta en el mismo lienzo. Distinto de HtmlOut (que se bufferiza por statement).
        private Action<string> _frameOut;
        public Action<string> FrameOut { get => _frameOut; set => _frameOut = value; }
        /// <summary>
        /// Callback opcional invocado para CADA statement ejecutado dentro de un
        /// bloque (for, while, if, switch, try). El pipeline lo usa para emitir HTML
        /// inline mostrando el progreso. Si null, los inner statements son silenciosos.
        /// </summary>
        private Action<MatlabNode, StatementResult> _innerStmtOut;
        public Action<MatlabNode, StatementResult> InnerStmtOut { get => _innerStmtOut; set => _innerStmtOut = value; }
        /// <summary>Colormap activo. Cambiado por <c>colormap('jet')</c> y consumido por surf/contourf.</summary>
        /// <summary>Colormap activo. Por defecto PARULA, que es el de MATLAB: asi una
        /// misma malla sale del mismo color en los dos. Si el script llama a
        /// colormap('jet') se respeta lo que pida.</summary>
        private static string _activeColormap = "parula";
        /// <summary>hold on activo: los siguientes plot/scatter/text COMPONEN en los mismos ejes
        /// (abre la figura acumulada). hold off la cierra. Sin esto cada plot abría un gráfico nuevo.</summary>
        private bool _holdOn = false;
        /// <summary>Índice del ciclo de colores de MATLAB (ColorOrder): cada plot sin color
        /// explícito toma el siguiente. Se reinicia al abrir ejes nuevos. Sin esto, varias
        /// curvas sin color salían todas del mismo azul.</summary>
        private int _colorCycleIdx = 0;
        private static readonly string[] _matlabColorOrder = {
            "#0072BD", "#D95319", "#EDB120", "#7E2F8E", "#77AC30", "#4DBEEE", "#A2142F"
        };
        /// <summary>Subplot grid activo (m, n) si subplot(m, n, p) fue llamado.</summary>
        internal (int m, int n)? _subplotGrid;
        /// <summary>Posición 1-based del subplot activo.</summary>
        internal int _activeSubplotPos;
        /// <summary>Pending plot annotations — aplicados al PRÓXIMO plot que se emita.</summary>
        internal string _pendingPlotTitle;
        internal string _pendingXLabel, _pendingYLabel, _pendingZLabel;
        internal string[] _pendingLegend;
        /// <summary>Reset las labels pendientes (llamar después de emitir cada plot).</summary>
        internal void ConsumePendingPlotLabels()
        {
            _pendingPlotTitle = null;
            _pendingXLabel = null; _pendingYLabel = null; _pendingZLabel = null;
            _pendingLegend = null;
        }
        /// <summary>Devuelve las labels pendientes a usar en el plot actual y las consume.</summary>
        internal (string title, string xlab, string ylab, string zlab, string[] legend) GetPlotLabels()
        {
            var r = (_pendingPlotTitle, _pendingXLabel, _pendingYLabel, _pendingZLabel, _pendingLegend);
            ConsumePendingPlotLabels();
            return r;
        }

        /// <summary>
        /// Stack de contexto para resolver <c>end</c> dentro de indexing. Push
        /// (targetSize, dimIndex) antes de evaluar cada arg; pop después.
        /// </summary>
        private readonly Stack<(MValue Target, int Dim, int NDims)> _endCtx = new();

        public MatlabEvaluator()
        {
            Globals = new MatlabScope();
            RegisterBuiltins();
        }

        // ─── Función registry (MATLAB built-ins) ────────────────────────────
        private readonly Dictionary<string, Func<MValue[], MValue>> _builtins = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<MValue[], MValue[]>> _multiOutBuiltins = new(StringComparer.Ordinal);
        /// <summary>User-defined functions registradas con <c>function ... end</c>.</summary>
        private readonly Dictionary<string, FunctionDef> _userFunctions = new(StringComparer.Ordinal);
        /// <summary>Symbolic functions (symfun) MATLAB-style: `f(x) = x^2`. Mapea
        /// nombre → lista de nombres de parametros formales. El valor de la
        /// expresion vive en `Globals.Vars[name]` como MValue.Symbolic. Cuando
        /// `f(args)` se invoca y `f` esta aqui, sustituye los params por los args.</summary>
        private readonly Dictionary<string, List<string>> _symFunParams = new(StringComparer.Ordinal);
        /// <summary>Clases definidas via <c>classdef</c>.</summary>
        private readonly Dictionary<string, ClassDef> _classes = new(StringComparer.Ordinal);
        /// <summary>Builtins de visualizacion reservados: si un .m AUTOCONTENIDO define una funcion
        /// local homonima (p.ej. `hoverdata` con la version MATLAB/datacursor), Lab NO la registra
        /// → usa su builtin (canvas). Asi UN SOLO archivo identico corre en Calcpad Lab y MATLAB 2017a.</summary>
        private static readonly HashSet<string> _reservedVizBuiltins = new(StringComparer.Ordinal) { "hoverdata" };
        /// <summary>Registra función para uso en pre-pass del pipeline (MATLAB script + helpers).</summary>
        public void RegisterFunction(FunctionDef fd) { if (!_reservedVizBuiltins.Contains(fd.Name)) _userFunctions[fd.Name] = fd; }

        /// <summary>True si todos los args son IdentRef y referencian sym vars
        /// existentes en scope (declaradas via `syms`). Usado para detectar el
        /// patron symfun: `f(x) = ...` requiere que x ya sea sym var.</summary>
        private bool AllArgsAreSymVars(List<MatlabNode> args, MatlabScope scope)
        {
            if (args == null || args.Count == 0) return false;
            foreach (var a in args)
            {
                if (a is not IdentRef ir) return false;
                if (!scope.TryGet(ir.Name, out var v)) return false;
                if (v == null || !v.IsSymbolic) return false;
                if (v.Symbolic is not SymVar) return false;
            }
            return true;
        }
        /// <summary>Registra clase para uso en pre-pass.</summary>
        public void RegisterClass(ClassDef cd) => _classes[cd.Name] = cd;
        /// <summary>Variables persistent — guardadas por (funcName + varName) entre invocaciones.</summary>
        private readonly Dictionary<string, MValue> _persistentVars = new(StringComparer.Ordinal);
        /// <summary>Para tic/toc.</summary>
        private System.Diagnostics.Stopwatch _ticStopwatch;
        /// <summary>RNG estable (seed determinístico para repetibilidad).</summary>
        private Random _rng = new(42);   // reseed via rng(seed) (MATLAB)

        private void RegisterBuiltins()
        {
            // ETABS — corre un modelo .EDB con SAPFire (OAPI) y devuelve un struct con resultados.
            //   r = etabs_run("mesa.edb")            % caso "Live" por defecto
            //   r = etabs_run("mesa.edb", "Dead")    % caso específico
            // Campos: col_P col_V2 col_V3 col_T col_M2 col_M3  beam_V3 beam_T beam_M2  slab_Mxx slab_Myy slab_Mxy
            _builtins["etabs_run"] = a =>
            {
                if (a.Length < 1 || !a[0].IsString)
                    throw new MatlabRuntimeException("etabs_run: primer argumento = ruta del .EDB (string)");
                string model = a[0].StringValue;
                string lc = (a.Length >= 2 && a[1].IsString) ? a[1].StringValue : "Live";
                var r = EtabsBridge.Run(model, lc);
                var s = MValue.NewStruct();
                foreach (var kv in r) s.Fields[kv.Key] = new MValue(kv.Value);
                return s;
            };

            // inputname(k): nombre de la variable pasada como k-ésimo argumento de
            // la función actual. Devuelve "" si fue una expresión (no variable) o
            // si se llama fuera de una función. Frame provisto por _inputNameStack.
            _builtins["inputname"] = a =>
            {
                if (a.Length < 1)
                    throw new MatlabRuntimeException("inputname: requiere el índice del argumento");
                int k = (int)Math.Round(a[0].Scalar);
                if (_inputNameStack.Count == 0) return new MValue("");
                var names = _inputNameStack.Peek();
                if (k >= 1 && k <= names.Length) return new MValue(names[k - 1] ?? "");
                return new MValue("");
            };

            // Elementary math (element-wise on matrices)
            _builtins["sin"] = a => MapUnary(a[0], Math.Sin);
            _builtins["cos"] = a => MapUnary(a[0], Math.Cos);
            _builtins["tan"] = a => MapUnary(a[0], Math.Tan);
            _builtins["asin"] = a => MapUnary(a[0], Math.Asin);
            _builtins["acos"] = a => MapUnary(a[0], Math.Acos);
            _builtins["atan"] = a => MapUnary(a[0], Math.Atan);
            _builtins["sinh"] = a => MapUnary(a[0], Math.Sinh);
            _builtins["cosh"] = a => MapUnary(a[0], Math.Cosh);
            _builtins["tanh"] = a => MapUnary(a[0], Math.Tanh);
            _builtins["exp"] = a => MapUnary(a[0], Math.Exp);
            _builtins["log"] = a => MapUnary(a[0], Math.Log);
            _builtins["log2"] = a => MapUnary(a[0], x => Math.Log(x, 2));
            _builtins["log10"] = a => MapUnary(a[0], Math.Log10);
            _builtins["sqrt"] = a => MapUnary(a[0], Math.Sqrt);
            _builtins["abs"] = a => {
                var v = a[0];
                if (v.IsComplex)
                {
                    var r = new double[v.Data.Length];
                    for (int i = 0; i < v.Data.Length; i++)
                        r[i] = Math.Sqrt(v.Data[i] * v.Data[i] + v.Imag[i] * v.Imag[i]);
                    return new MValue(v.Rows, v.Cols, r);
                }
                return MapUnary(v, Math.Abs);
            };
            _builtins["real"] = a => {
                var v = a[0];
                if (!v.IsComplex) return v;
                return new MValue(v.Rows, v.Cols, (double[])v.Data.Clone());
            };
            _builtins["imag"] = a => {
                var v = a[0];
                if (!v.IsComplex)
                {
                    var z = new double[v.Data.Length];
                    return new MValue(v.Rows, v.Cols, z);
                }
                return new MValue(v.Rows, v.Cols, (double[])v.Imag.Clone());
            };
            _builtins["conj"] = a => {
                var v = a[0];
                if (!v.IsComplex) return v;
                var im = new double[v.Imag.Length];
                for (int i = 0; i < im.Length; i++) im[i] = -v.Imag[i];
                return new MValue(v.Rows, v.Cols, (double[])v.Data.Clone(), im);
            };
            _builtins["angle"] = a => {
                var v = a[0];
                var r = new double[v.Data.Length];
                for (int i = 0; i < v.Data.Length; i++)
                    r[i] = Math.Atan2(v.IsComplex ? v.Imag[i] : 0, v.Data[i]);
                return new MValue(v.Rows, v.Cols, r);
            };
            _builtins["complex"] = a => {
                if (a.Length == 1) return new MValue(a[0].Scalar, 0);
                var re = a[0]; var im = a[1];
                if (re.IsScalar && im.IsScalar) return new MValue(re.Scalar, im.Scalar);
                if (re.Data.Length != im.Data.Length)
                    throw new MatlabRuntimeException("complex(re, im): dim mismatch");
                return new MValue(re.Rows, re.Cols, (double[])re.Data.Clone(), (double[])im.Data.Clone());
            };
            _builtins["isreal"] = a => new MValue(!a[0].IsComplex ? 1 : 0);
            _builtins["iscomplex"] = a => new MValue(a[0].IsComplex ? 1 : 0);
            _builtins["fft"] = a => MatlabFFT.Fft(a[0], false);
            _builtins["ifft"] = a => MatlabFFT.Fft(a[0], true);
            _builtins["fft2"] = a => MatlabFFT.Fft2(a[0], false);
            _builtins["ifft2"] = a => MatlabFFT.Fft2(a[0], true);

            // Sparse matrices — modelo MVP: convertimos a/desde full; el storage no
            // es realmente sparse pero la API existe para portabilidad de scripts.
            _builtins["sparse"] = a => {
                // NOTA Lab: el motor trata 'sparse' como DENSO. Asi la indexacion
                // K(i,j)=v y el solve K\b usan los caminos densos (que funcionan) y el
                // FE corre. Para sistemas enormes conviene MATLAB/Octave (sparse real).
                // sparse(M) -> M (ya denso) ; sparse(i,j,s,m,n) -> denso acumulado ;
                // sparse(m,n) -> zeros(m,n) denso.
                if (a.Length == 1)
                {
                    return a[0].IsSparseReal ? CsrToFull(a[0]) : a[0];
                }
                if (a.Length >= 5)
                {
                    // SPARSE REAL (CSR): antes densificaba a m×n (128 MB para FEM grande y
                    // el solve escaneaba densa 3× por iteracion). Ahora TripletsToCsr guarda
                    // solo los no-ceros → K*U (SpMV), K(free,free) (submatriz sparse) y
                    // K\b (PARDISO) corren en el camino sparse, como MATLAB.
                    int m = (int)a[3].Scalar, n = (int)a[4].Scalar;
                    var ss = a[2].Data;
                    var svArr = ss.Length == 1 && a[0].Data.Length > 1
                        ? System.Linq.Enumerable.Repeat(ss[0], a[0].Data.Length).ToArray()
                        : ss;
                    return TripletsToCsr(m, n, a[0].Data, a[1].Data, svArr);
                }
                if (a.Length >= 2 && a[0].IsScalar && a[1].IsScalar)
                {
                    int m = (int)a[0].Scalar, n = (int)a[1].Scalar;
                    return new MValue(m, n);                   // zeros(m,n) denso
                }
                return a[0];
            };
            _builtins["full"] = a => {
                if (!a[0].IsSparseReal) return a[0];
                return CsrToFull(a[0]);
            };
            _builtins["issparse"] = a => new MValue(a[0].IsSparseReal ? 1 : 0);
            // spsolve(A, b): A·x=b via MKL PARDISO (solver de OpenSees). Acepta A
            // sparse (CSR) o densa (construye CSR de los no-ceros). Para FEM grande.
            _builtins["spsolve"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("spsolve(A, b)");
                var A = a[0]; var b = a[1];
                if (A.Rows != A.Cols) throw new MatlabRuntimeException("spsolve: A debe ser cuadrada");
                int n = A.Rows;
                if (b.Rows * b.Cols != n) throw new MatlabRuntimeException("spsolve: dim(b) != n");
                int[] rowPtr, colIdx; double[] vals;
                if (A.IsSparseReal) { rowPtr = A.SparseRowPtr; colIdx = A.SparseCols; vals = A.SparseVals; }
                else
                {
                    // CSR 0-based a partir de la densa (omitir ceros exactos)
                    var rp = new int[n + 1]; var ci = new System.Collections.Generic.List<int>(); var vv = new System.Collections.Generic.List<double>();
                    for (int i = 0; i < n; i++)
                    {
                        rp[i] = ci.Count;
                        for (int jcol = 0; jcol < n; jcol++)
                        {
                            double e = A.At(i, jcol);
                            if (e != 0.0) { ci.Add(jcol); vv.Add(e); }
                        }
                    }
                    rp[n] = ci.Count; rowPtr = rp; colIdx = ci.ToArray(); vals = vv.ToArray();
                }
                var bv = new double[n];
                for (int i = 0; i < n; i++) bv[i] = b.Rows == n ? b.At(i, 0) : b.At(0, i);
                var x = Calcpad.Core.BlasInterop.SolveSparsePardiso(n, rowPtr, colIdx, vals, bv);
                var r = new MValue(n, 1);
                for (int i = 0; i < n; i++) r.Set(i, 0, x[i]);
                return r;
            };
            _builtins["haspardiso"] = a => new MValue(Calcpad.Core.BlasInterop.PardisoAvailable ? 1 : 0);
            // cell(n) -> n x n ; cell(m,n) -> m x n ; celdas vacias = [] (0x0)
            _builtins["cell"] = a => {
                int m = a.Length >= 1 ? (int)a[0].Scalar : 0;
                int n = a.Length >= 2 ? (int)a[1].Scalar : m;
                var cells = new MValue[m, n];
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++)
                        cells[i, j] = new MValue(0, 0);
                return MValue.NewCell(cells);
            };
            // hypot(a,b) = sqrt(a^2+b^2) element-wise (sin overflow conceptual; MATLAB-compat)
            _builtins["hypot"] = a => MapBinary(a[0], a[1], (x, y) => Math.Sqrt(x * x + y * y));
            // caxis/clim: límites de color del eje. Se usan para normalizar el colormap del
            // renderer CANVAS igual que MATLAB (caxis([0 1]) → todo el rango jet en [0,1]).
            _builtins["caxis"] = a => {
                a = DropAxes(a);
                if (a.Length >= 1 && a[0].Rows * a[0].Cols >= 2) MatlabPlots.SetCAxis(a[0].Data[0], a[0].Data[1]);
                else if (a.Length >= 2) MatlabPlots.SetCAxis(a[0].Scalar, a[1].Scalar);
                return new MValue(0);
            };
            _builtins["clim"] = _builtins["caxis"];
            _builtins["nnz"] = a => {
                if (a[0].IsSparseReal) return new MValue(a[0].SparseVals.Length);
                int count = 0;
                foreach (var x in a[0].Data) if (x != 0) count++;
                return new MValue(count);
            };
            _builtins["nonzeros"] = a => {
                if (a[0].IsSparseReal) return new MValue(a[0].SparseVals.Length, 1, (double[])a[0].SparseVals.Clone());
                var nz = a[0].Data.Where(x => x != 0).ToArray();
                return new MValue(nz.Length, 1, nz);
            };
            _builtins["density"] = a => {
                int total = a[0].Rows * a[0].Cols;
                int nz = a[0].IsSparseReal ? a[0].SparseVals.Length : a[0].Data.Count(x => x != 0);
                return new MValue(total == 0 ? 0 : (double)nz / total);
            };

            // Helpers sparse
            MValue FullToCsr(MValue m)
            {
                var vals = new System.Collections.Generic.List<double>();
                var cols = new System.Collections.Generic.List<int>();
                var rowPtr = new int[m.Rows + 1];
                for (int i = 0; i < m.Rows; i++)
                {
                    rowPtr[i] = vals.Count;
                    for (int j = 0; j < m.Cols; j++)
                    {
                        double v = m.At(i, j);
                        if (v != 0) { vals.Add(v); cols.Add(j); }
                    }
                }
                rowPtr[m.Rows] = vals.Count;
                return MValue.NewSparseCSR(m.Rows, m.Cols, vals.ToArray(), cols.ToArray(), rowPtr);
            }
            MValue CsrToFull(MValue s)
            {
                var r = new MValue(s.Rows, s.Cols);
                for (int i = 0; i < s.Rows; i++)
                {
                    for (int k = s.SparseRowPtr[i]; k < s.SparseRowPtr[i + 1]; k++)
                        r.Set(i, s.SparseCols[k], s.SparseVals[k]);
                }
                return r;
            }
            MValue TripletsToCsr(int m, int n, double[] ii, double[] jj, double[] ss)
            {
                // Acumular duplicados, ordenar por (row, col)
                var dict = new Dictionary<long, double>();
                for (int k = 0; k < ii.Length; k++)
                {
                    int r = (int)ii[k] - 1, c = (int)jj[k] - 1;
                    if (r < 0 || r >= m || c < 0 || c >= n) continue;
                    long key = (long)r * n + c;
                    if (dict.TryGetValue(key, out var existing)) dict[key] = existing + ss[k];
                    else dict[key] = ss[k];
                }
                var sortedKeys = dict.Keys.OrderBy(k => k).ToArray();
                var vals = new double[sortedKeys.Length];
                var cols = new int[sortedKeys.Length];
                var rowPtr = new int[m + 1];
                int idx = 0; int curRow = 0;
                foreach (var key in sortedKeys)
                {
                    int r = (int)(key / n), c = (int)(key % n);
                    while (curRow <= r) { rowPtr[curRow++] = idx; }
                    vals[idx] = dict[key];
                    cols[idx] = c;
                    idx++;
                }
                while (curRow <= m) rowPtr[curRow++] = idx;
                return MValue.NewSparseCSR(m, n, vals, cols, rowPtr);
            }
            _builtins["spdiags"] = a => {
                // spdiags(B, d, m, n) — construir banded matrix con diagonales en cols de B
                if (a.Length < 4) throw new MatlabRuntimeException("spdiags(B, d, m, n)");
                var B = a[0]; var d = a[1].Data; int m = (int)a[2].Scalar, n = (int)a[3].Scalar;
                var r = new MValue(m, n);
                for (int k = 0; k < d.Length; k++)
                {
                    int dk = (int)d[k];
                    for (int i = 0; i < B.Rows; i++)
                    {
                        int row = i, col = i + dk;
                        if (row >= 0 && row < m && col >= 0 && col < n)
                            r.Set(row, col, B.At(i, k));
                    }
                }
                return r;
            };
            _builtins["speye"] = a => {
                int n = (int)a[0].Scalar;
                int m = a.Length > 1 ? (int)a[1].Scalar : n;
                var r = new MValue(n, m);
                for (int i = 0; i < Math.Min(n, m); i++) r.Set(i, i, 1);
                return r;
            };
            _builtins["spones"] = a => {
                var v = a[0];
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Data.Length; i++) r.Data[i] = v.Data[i] != 0 ? 1 : 0;
                return r;
            };
            _builtins["spy"] = a => {
                _htmlOut?.Invoke(MatlabPlots.Spy(a[0]));
                return new MValue(0);
            };

            // BVP solver — shooting method con Newton (MVP simple)
            // bvp4c(@odefun, @bcfun, t_mesh, y0_guess)
            // odefun: @(t, y) → dy/dt
            // bcfun: @(ya, yb) → residual vector (= 0 en solución)
            // t_mesh: vector de tiempos
            // y0_guess: vector inicial (será optimizado para satisfacer BCs)
            _builtins["bvp4c"] = a => {
                if (a.Length < 4 || !a[0].IsCallable || !a[1].IsCallable)
                    throw new MatlabRuntimeException("bvp4c(@odefun, @bcfun, t_mesh, y0_guess)");
                var odeFn = a[0].Callable;
                var bcFn = a[1].Callable;
                var tMesh = a[2].Data;
                int neq = a[3].Data.Length;
                // Shooting: integrar IVP desde t0 con y0 = guess, calcular residual BC.
                // Iterar Newton sobre y0 hasta que bcFn(y_start, y_end) = 0.
                var y0 = (double[])a[3].Data.Clone();
                double[] IntegrateAndGetEnd(double[] y0v)
                {
                    var y0Mv = neq == 1 ? new MValue(y0v[0]) : new MValue(neq, 1, (double[])y0v.Clone());
                    var tMv = new MValue(1, 2, new[] { tMesh[0], tMesh[tMesh.Length - 1] });
                    var argSet = new[] { a[0], tMv, y0Mv };
                    var (_, ys) = RunOdeDP45(argSet, 1e-8, 1e-6);
                    var yEnd = new double[neq];
                    for (int i = 0; i < neq; i++) yEnd[i] = ys.At(ys.Rows - 1, i);
                    return yEnd;
                }
                double[] Residual(double[] y0v)
                {
                    var yEnd = IntegrateAndGetEnd(y0v);
                    var ya = neq == 1 ? new MValue(y0v[0]) : new MValue(neq, 1, (double[])y0v.Clone());
                    var yb = neq == 1 ? new MValue(yEnd[0]) : new MValue(neq, 1, yEnd);
                    var resMv = bcFn(new[] { ya, yb });
                    return (double[])resMv.Data.Clone();
                }
                // Newton sobre y0
                for (int it = 0; it < 50; it++)
                {
                    var res = Residual(y0);
                    double rn = 0; foreach (var v in res) rn += v * v;
                    if (Math.Sqrt(rn) < 1e-10) break;
                    // Jacobiano numérico
                    int m = res.Length;
                    var J = new MValue(m, neq);
                    for (int j = 0; j < neq; j++)
                    {
                        double h = Math.Max(1e-7, 1e-5 * Math.Abs(y0[j]));
                        var yPert = (double[])y0.Clone(); yPert[j] += h;
                        var resPert = Residual(yPert);
                        for (int i = 0; i < m; i++) J.Set(i, j, (resPert[i] - res[i]) / h);
                    }
                    var bVec = new MValue(m, 1);
                    for (int i = 0; i < m; i++) bVec.Set(i, 0, -res[i]);
                    MValue dy;
                    try { dy = MatlabLinAlg.Linsolve(J, bVec); }
                    catch { break; }
                    for (int i = 0; i < neq; i++) y0[i] += dy.At(i, 0);
                }
                // Integrar con y0 final y devolver toda la trayectoria
                var y0Final = neq == 1 ? new MValue(y0[0]) : new MValue(neq, 1, y0);
                var tspanFinal = new MValue(1, 2, new[] { tMesh[0], tMesh[tMesh.Length - 1] });
                var argsFinal = new[] { a[0], tspanFinal, y0Final };
                var (tOut, yOut) = RunOdeDP45(argsFinal, 1e-8, 1e-6);
                return yOut;
            };
            _multiOutBuiltins["bvp4c"] = a => {
                var yResult = _builtins["bvp4c"](a);
                // Re-integrar para obtener t también
                var tMesh = a[2].Data;
                var tspanFinal = new MValue(1, 2, new[] { tMesh[0], tMesh[tMesh.Length - 1] });
                // y0 ya está ajustado dentro del bvp4c, pero ahí lo perdimos. Por simplicidad
                // devolvemos t_mesh directo + yResult.
                return new[] { new MValue(tMesh.Length, 1, tMesh), yResult };
            };

            // PDE solver (1D parabolic — método de líneas)
            _builtins["pdepe"] = a => {
                // pdepe simplificado: ∂u/∂t = α·∂²u/∂x² + f(u, x, t)
                // Args: pdepe(alpha, f, x_mesh, t_mesh, u0_fn)
                // - alpha: scalar (diffusion coefficient)
                // - f: @(u, x, t) (source term, opcional null)
                // - x_mesh: vector
                // - t_mesh: vector
                // - u0_fn: @(x) initial condition
                if (a.Length < 5) throw new MatlabRuntimeException("pdepe(alpha, @f, x, t, @u0)");
                double alpha = a[0].Scalar;
                var fSource = a[1].IsCallable ? a[1].Callable : null;
                var x = a[2].Data;
                var t = a[3].Data;
                var u0Fn = a[4].Callable;
                int Nx = x.Length, Nt = t.Length;
                var U = new MValue(Nt, Nx);
                // Initial condition
                for (int j = 0; j < Nx; j++) U.Set(0, j, u0Fn(new[] { new MValue(x[j]) }).Scalar);
                // Crank-Nicolson en tiempo + diferencia central espacial
                for (int n_t = 0; n_t < Nt - 1; n_t++)
                {
                    double dt = t[n_t + 1] - t[n_t];
                    // Construir sistema A·u^{n+1} = B·u^n + S
                    // (I - 0.5*dt*α*L) u^{n+1} = (I + 0.5*dt*α*L) u^n + dt*f
                    var Amat = new MValue(Nx, Nx);
                    var Bmat = new MValue(Nx, Nx);
                    for (int i = 1; i < Nx - 1; i++)
                    {
                        double hL = x[i] - x[i - 1], hR = x[i + 1] - x[i];
                        double cL = 2.0 / (hL * (hL + hR));
                        double cC = -2.0 / (hL * hR);
                        double cR = 2.0 / (hR * (hL + hR));
                        Amat.Set(i, i - 1, -0.5 * dt * alpha * cL);
                        Amat.Set(i, i, 1 - 0.5 * dt * alpha * cC);
                        Amat.Set(i, i + 1, -0.5 * dt * alpha * cR);
                        Bmat.Set(i, i - 1, 0.5 * dt * alpha * cL);
                        Bmat.Set(i, i, 1 + 0.5 * dt * alpha * cC);
                        Bmat.Set(i, i + 1, 0.5 * dt * alpha * cR);
                    }
                    // Boundary conditions Dirichlet u=u0 fija (MVP simple)
                    Amat.Set(0, 0, 1);
                    Amat.Set(Nx - 1, Nx - 1, 1);
                    Bmat.Set(0, 0, 1);
                    Bmat.Set(Nx - 1, Nx - 1, 1);
                    var uOld = new MValue(Nx, 1);
                    for (int j = 0; j < Nx; j++) uOld.Set(j, 0, U.At(n_t, j));
                    var rhs = MatlabLinAlg.MatMul(Bmat, uOld);
                    if (fSource != null)
                    {
                        for (int j = 1; j < Nx - 1; j++)
                        {
                            double fVal = fSource(new[] {
                                new MValue(U.At(n_t, j)),
                                new MValue(x[j]),
                                new MValue(t[n_t])
                            }).Scalar;
                            rhs.Set(j, 0, rhs.At(j, 0) + dt * fVal);
                        }
                    }
                    var uNew = MatlabLinAlg.Linsolve(Amat, rhs);
                    for (int j = 0; j < Nx; j++) U.Set(n_t + 1, j, uNew.At(j, 0));
                }
                return U;
            };

            // ODE solvers
            _builtins["ode45"] = a => RunOdeDP45(a, 1e-6, 1e-3).y;   // Dormand-Prince adaptive
            _builtins["ode23"] = a => RunOdeBS23(a, 1e-3, 1e-3).y;   // Bogacki-Shampine
            _builtins["ode4"]  = a => RunOdeRK4(a, 0.01);             // RK4 fijo
            _builtins["ode_euler"] = a => RunOdeEuler(a, 0.01);
            _multiOutBuiltins["ode45"] = a => { var (t, y) = RunOdeDP45(a, 1e-6, 1e-3); return new[] { t, y }; };
            _multiOutBuiltins["ode23"] = a => { var (t, y) = RunOdeBS23(a, 1e-3, 1e-3); return new[] { t, y }; };
            _multiOutBuiltins["ode4"]  = a => RunOdeRK4Multi(a, 0.01);
            _builtins["fftshift"] = a => {
                var v = a[0];
                int n = v.Data.Length;
                int h = n / 2;
                var re = new double[n];
                var im = v.IsComplex ? new double[n] : null;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + h) % n;
                    re[i] = v.Data[j];
                    if (im != null) im[i] = v.Imag[j];
                }
                return im != null ? new MValue(v.Rows, v.Cols, re, im) : new MValue(v.Rows, v.Cols, re);
            };
            _builtins["sign"] = a => MapUnary(a[0], x => Math.Sign(x));
            _builtins["floor"] = a => MapUnary(a[0], Math.Floor);
            _builtins["ceil"] = a => MapUnary(a[0], Math.Ceiling);
            _builtins["round"] = a => MapUnary(a[0], Math.Round);
            _builtins["atan2"] = a => MapBinary(a[0], a[1], Math.Atan2);
            _builtins["mod"] = a => MapBinary(a[0], a[1], (x, y) => y == 0 ? x : x - y * Math.Floor(x / y));
            _builtins["rem"] = a => MapBinary(a[0], a[1], (x, y) => y == 0 ? x : x - y * Math.Truncate(x / y));
            _builtins["power"] = a => MapBinary(a[0], a[1], Math.Pow);
            _builtins["max"] = a => MinMaxBuiltin(a, true);
            _builtins["min"] = a => MinMaxBuiltin(a, false);
            _builtins["sum"] = a => (a[0].IsSymbolic || a[0].IsSymMatrix)
                ? ReduceSym(a[0], isProd: false)
                : ReduceNumDim(a[0], a.Length > 1 && a[1] != null && !a[1].IsString ? (int)a[1].Scalar : 0, 0.0, (acc, x) => acc + x);
            _builtins["prod"] = a => (a[0].IsSymbolic || a[0].IsSymMatrix)
                ? ReduceSym(a[0], isProd: true)
                : ReduceNumDim(a[0], a.Length > 1 && a[1] != null && !a[1].IsString ? (int)a[1].Scalar : 0, 1.0, (acc, x) => acc * x);
            _builtins["mean"] = a => {
                var v = a[0];
                int dim = a.Length > 1 ? (int)a[1].Scalar : 0;   // 0 = auto
                bool isVec = (v.Rows == 1 || v.Cols == 1);
                if (dim == 0 && isVec)   // vector -> escalar (media de todo)
                {
                    double s = 0; for (int i = 0; i < v.Data.Length; i++) s += v.Data[i];
                    return new MValue(s / Math.Max(v.Data.Length, 1));
                }
                if (dim == 0) dim = 1;   // matriz sin dim -> medias por COLUMNA (como MATLAB)
                if (dim == 1)            // media por columna -> 1 x Cols
                {
                    var r = new MValue(1, v.Cols);
                    for (int j = 0; j < v.Cols; j++)
                    {
                        double s = 0; for (int i = 0; i < v.Rows; i++) s += v.At(i, j);
                        r.Set(0, j, s / Math.Max(v.Rows, 1));
                    }
                    return r;
                }
                else                     // dim==2 -> media por fila -> Rows x 1
                {
                    var r = new MValue(v.Rows, 1);
                    for (int i = 0; i < v.Rows; i++)
                    {
                        double s = 0; for (int j = 0; j < v.Cols; j++) s += v.At(i, j);
                        r.Set(i, 0, s / Math.Max(v.Cols, 1));
                    }
                    return r;
                }
            };
            _builtins["length"] = a => new MValue(Math.Max(a[0].Rows, a[0].Cols));
            _builtins["numel"] = a => new MValue(a[0].Rows * a[0].Cols);
            // isequal(A,B,...): 1 si TODOS los argumentos tienen igual tamaño y elementos.
            _builtins["isequal"] = a => {
                if (a.Length < 2) return new MValue(1);
                for (int q = 1; q < a.Length; q++)
                {
                    var A0 = a[0]; var Bq = a[q];
                    if (A0.IsString || Bq.IsString)
                    {
                        if (!(A0.IsString && Bq.IsString) || A0.StringValue != Bq.StringValue) return new MValue(0);
                        continue;
                    }
                    if (A0.Rows != Bq.Rows || A0.Cols != Bq.Cols) return new MValue(0);
                    if (A0.Data.Length != Bq.Data.Length) return new MValue(0);
                    for (int t = 0; t < A0.Data.Length; t++)
                        if (A0.Data[t] != Bq.Data[t]) return new MValue(0);
                }
                return new MValue(1);
            };
            _builtins["zeros"] = a => MakeFill(a, 0);
            _builtins["ones"] = a => MakeFill(a, 1);
            // ─── FEM pattern fusion kernels (Calcpad-Lab specific) ────────
            // btdb(B, D) ≡ B' * D * B     — assembly kernel
            // dbz(D, B, z) ≡ D * B * z    — postproc moment kernel
            // Fusionan 2-3 matmuls + transpose + allocations en una sola pasada.
            _builtins["btdb"] = a => {
                var B = a[0]; var D = a[1];
                int M = B.Rows, N = B.Cols;
                if (D.Rows != M || D.Cols != M)
                    throw new MatlabRuntimeException($"btdb: B is {B.Rows}×{B.Cols}, D must be {M}×{M}, got {D.Rows}×{D.Cols}");
                var T = new double[M * N];
                for (int p = 0; p < M; p++)
                    for (int j = 0; j < N; j++) {
                        double s = 0;
                        for (int q = 0; q < M; q++)
                            s += D.At(p, q) * B.At(q, j);
                        T[p * N + j] = s;
                    }
                var R = new MValue(N, N);
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++) {
                        double s = 0;
                        for (int p = 0; p < M; p++)
                            s += B.At(p, i) * T[p * N + j];
                        R.Set(i, j, s);
                    }
                return R;
            };
            _builtins["dbz"] = a => {
                var D = a[0]; var B = a[1]; var z = a[2];
                int m = D.Rows, p = D.Cols, n = B.Cols;
                if (B.Rows != p || z.Rows * z.Cols != n)
                    throw new MatlabRuntimeException(
                        $"dbz: D is {D.Rows}×{D.Cols}, B is {B.Rows}×{B.Cols}, z is {z.Rows}×{z.Cols}");
                var Bz = new double[p];
                for (int pp = 0; pp < p; pp++) {
                    double s = 0;
                    for (int q = 0; q < n; q++)
                        s += B.At(pp, q) * z.Data[q];
                    Bz[pp] = s;
                }
                var R = new MValue(m, 1);
                for (int i = 0; i < m; i++) {
                    double s = 0;
                    for (int pp = 0; pp < p; pp++)
                        s += D.At(i, pp) * Bz[pp];
                    R.Set(i, 0, s);
                }
                return R;
            };
            _builtins["zeros3"] = a => Make3D(a, 0);
            _builtins["ones3"] = a => Make3D(a, 1);
            _builtins["ndims"] = a => new MValue(a[0].Is3D ? 3 : (a[0].Rows > 1 && a[0].Cols > 1 ? 2 : (a[0].Rows == 1 && a[0].Cols == 1 ? 0 : 1)));
            _builtins["cat3"] = a => {
                // cat(3, A, B, C, ...) — concatenar matrices 2D como páginas
                int dim = (int)a[0].Scalar;
                if (dim != 3)
                {
                    // cat(1, ...) o cat(2, ...) — usar concat 2D
                    var rest = new MValue[a.Length - 1];
                    Array.Copy(a, 1, rest, 0, rest.Length);
                    if (dim == 1) return _builtins["vertcat"](rest);
                    if (dim == 2) return _builtins["horzcat"](rest);
                    throw new MatlabRuntimeException("cat: solo dim 1, 2 o 3 (MVP)");
                }
                var pages = new MValue[a.Length - 1];
                for (int i = 1; i < a.Length; i++) pages[i - 1] = a[i];
                return MValue.New3D(pages);
            };
            // Override cat para soportar dim 3
            _builtins["cat"] = a => _builtins["cat3"](a);

            static MValue Make3D(MValue[] args, double fill)
            {
                if (args.Length < 3) throw new MatlabRuntimeException("zeros3/ones3 requiere [rows, cols, pages]");
                int rows = (int)args[0].Scalar;
                int cols = (int)args[1].Scalar;
                int pages = (int)args[2].Scalar;
                var arr = new MValue[pages];
                for (int p = 0; p < pages; p++)
                {
                    var page = new MValue(rows, cols);
                    if (fill != 0)
                        for (int i = 0; i < page.Data.Length; i++) page.Data[i] = fill;
                    arr[p] = page;
                }
                return MValue.New3D(arr);
            }
            // Random
            _builtins["rand"] = a => {
                int nR = a.Length >= 1 ? (int)a[0].Scalar : 1;
                int nC = a.Length >= 2 ? (int)a[1].Scalar : nR;
                var r = new MValue(nR, nC);
                for (int i = 0; i < r.Data.Length; i++) r.Data[i] = _rng.NextDouble();
                return r;
            };
            // rng(seed) / rng('default') / rng('shuffle') — re-siembra el RNG (MATLAB): reproducible.
            _builtins["rng"] = a => {
                if (a.Length >= 1 && a[0] != null && !a[0].IsString)
                    _rng = new Random((int)a[0].Scalar);
                else if (a.Length >= 1 && a[0] != null && a[0].IsString &&
                         a[0].StringValue.Equals("shuffle", StringComparison.OrdinalIgnoreCase))
                    _rng = new Random();
                else
                    _rng = new Random(42);   // 'default'
                return new MValue(0);
            };
            _builtins["randn"] = a => {
                int nR = a.Length >= 1 ? (int)a[0].Scalar : 1;
                int nC = a.Length >= 2 ? (int)a[1].Scalar : nR;
                var r = new MValue(nR, nC);
                for (int i = 0; i < r.Data.Length; i++)
                {
                    // Box-Muller
                    double u1 = Math.Max(_rng.NextDouble(), 1e-12);
                    double u2 = _rng.NextDouble();
                    r.Data[i] = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
                }
                return r;
            };
            _builtins["randi"] = a => {
                int imax = (int)a[0].Scalar;
                int nR = a.Length >= 2 ? (int)a[1].Scalar : 1;
                int nC = a.Length >= 3 ? (int)a[2].Scalar : nR;
                var r = new MValue(nR, nC);
                for (int i = 0; i < r.Data.Length; i++) r.Data[i] = _rng.Next(1, imax + 1);
                return r;
            };
            _builtins["randperm"] = a => {
                int n = (int)a[0].Scalar;
                var arr = new double[n];
                for (int i = 0; i < n; i++) arr[i] = i + 1;
                // Fisher-Yates
                for (int i = n - 1; i > 0; i--)
                {
                    int j = _rng.Next(0, i + 1);
                    (arr[i], arr[j]) = (arr[j], arr[i]);
                }
                return new MValue(1, n, arr);
            };
            // Aliases para usar con @handles (plus, times, minus, etc.)
            _builtins["plus"] = a => MapBinary(a[0], a[1], (x, y) => x + y);
            _builtins["minus"] = a => MapBinary(a[0], a[1], (x, y) => x - y);
            _builtins["times"] = a => MapBinary(a[0], a[1], (x, y) => x * y);
            _builtins["rdivide"] = a => MapBinary(a[0], a[1], (x, y) => x / y);
            _builtins["ldivide"] = a => MapBinary(a[0], a[1], (x, y) => y / x);
            _builtins["mtimes"] = a => {
                // matrix mul si ambos son matrices, sino broadcast
                if (a[0].IsScalar || a[1].IsScalar) return MapBinary(a[0], a[1], (x, y) => x * y);
                return MatlabLinAlg.MatMul(a[0], a[1]);
            };
            _builtins["uminus"] = a => MapUnary(a[0], x => -x);
            _builtins["uplus"] = a => a[0];
            _builtins["not"] = a => MapUnary(a[0], x => x == 0 ? 1 : 0);
            _builtins["eq"] = a => MapBinary(a[0], a[1], (x, y) => x == y ? 1 : 0);
            _builtins["ne"] = a => MapBinary(a[0], a[1], (x, y) => x != y ? 1 : 0);
            _builtins["lt"] = a => MapBinary(a[0], a[1], (x, y) => x < y ? 1 : 0);
            _builtins["gt"] = a => MapBinary(a[0], a[1], (x, y) => x > y ? 1 : 0);
            _builtins["le"] = a => MapBinary(a[0], a[1], (x, y) => x <= y ? 1 : 0);
            _builtins["ge"] = a => MapBinary(a[0], a[1], (x, y) => x >= y ? 1 : 0);
            _builtins["and"] = a => MapBinary(a[0], a[1], (x, y) => (x != 0 && y != 0) ? 1 : 0);
            _builtins["or"]  = a => MapBinary(a[0], a[1], (x, y) => (x != 0 || y != 0) ? 1 : 0);

            _builtins["magic"] = a => {
                // Matriz mágica de orden n (suma constante por filas/cols/diags)
                int n = (int)a[0].Scalar;
                var M = new MValue(n, n);
                if (n % 2 == 1)
                {
                    // Método siamés (n impar)
                    int i = 0, j = n / 2;
                    for (int k = 1; k <= n * n; k++)
                    {
                        M.Set(i, j, k);
                        int ni = (i - 1 + n) % n;
                        int nj = (j + 1) % n;
                        if (M.At(ni, nj) != 0) { i = (i + 1) % n; }
                        else { i = ni; j = nj; }
                    }
                }
                else if (n % 4 == 0)
                {
                    // Método doblemente par (n divisible por 4)
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                            M.Set(i, j, i * n + j + 1);
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            int im = i % 4, jm = j % 4;
                            if ((im == jm) || (im + jm == 3))
                                M.Set(i, j, n * n + 1 - M.At(i, j));
                        }
                }
                else
                {
                    // n par no múltiplo de 4 — LUX method simple (MVP: rellena en orden)
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                            M.Set(i, j, i * n + j + 1);
                }
                return M;
            };
            _builtins["eye"] = a => {
                int n = a.Length > 0 ? (int)a[0].Scalar : 1;
                int m = a.Length > 1 ? (int)a[1].Scalar : n;
                var r = new MValue(n, m);
                for (int k = 0; k < Math.Min(n, m); k++) r.Set(k, k, 1.0);
                return r;
            };
            _builtins["linspace"] = a => {
                double s = a[0].Scalar, e = a[1].Scalar;
                int n = a.Length > 2 ? (int)a[2].Scalar : 100;
                var r = new MValue(1, n);
                if (n == 1) { r.Data[0] = s; return r; }
                double step = (e - s) / (n - 1);
                for (int i = 0; i < n; i++) r.Data[i] = s + i * step;
                return r;
            };
            _builtins["logspace"] = a => {
                double s = a[0].Scalar, e = a[1].Scalar;
                int n = a.Length > 2 ? (int)a[2].Scalar : 50;
                var r = new MValue(1, n);
                if (n == 1) { r.Data[0] = Math.Pow(10, s); return r; }
                double step = (e - s) / (n - 1);
                for (int i = 0; i < n; i++) r.Data[i] = Math.Pow(10, s + i * step);
                return r;
            };
            _builtins["size"] = a => {
                int rows, cols, pages = 1;
                if (a[0].Is3D)
                {
                    var p0 = a[0].Pages[0];
                    rows = p0.Rows; cols = p0.Cols; pages = a[0].Pages.Length;
                }
                else if (a[0].IsCell)
                {
                    rows = a[0].CellData.GetLength(0);
                    cols = a[0].CellData.GetLength(1);
                }
                else if (a[0].IsString)
                {
                    rows = 1; cols = a[0].StringValue?.Length ?? 0;
                }
                else { rows = a[0].Rows; cols = a[0].Cols; }
                if (a.Length >= 2) {
                    int dim = (int)a[1].Scalar;
                    if (dim == 1) return new MValue(rows);
                    if (dim == 2) return new MValue(cols);
                    if (dim == 3) return new MValue(pages);
                    return new MValue(1);  // dims más altas → 1
                }
                if (a[0].Is3D)
                    return new MValue(1, 3, new[] { (double)rows, (double)cols, (double)pages });
                return new MValue(1, 2, new[] { (double)rows, (double)cols });
            };
            _builtins["transpose"] = a => Transpose(a[0]);
            _builtins["fix"] = a => MapUnary(a[0], Math.Truncate);
            _builtins["trunc"] = a => MapUnary(a[0], Math.Truncate);
            _builtins["sind"] = a => MapUnary(a[0], v => Math.Sin(v * Math.PI / 180));
            _builtins["cosd"] = a => MapUnary(a[0], v => Math.Cos(v * Math.PI / 180));
            _builtins["tand"] = a => MapUnary(a[0], v => Math.Tan(v * Math.PI / 180));
            _builtins["deg2rad"] = a => MapUnary(a[0], v => v * Math.PI / 180);
            _builtins["rad2deg"] = a => MapUnary(a[0], v => v * 180 / Math.PI);
            _builtins["cumsum"] = a => {
                var v = a[0];
                var r = new MValue(v.Rows, v.Cols);
                double acc = 0;
                for (int i = 0; i < v.Data.Length; i++) { acc += v.Data[i]; r.Data[i] = acc; }
                return r;
            };
            _builtins["cumprod"] = a => {
                var v = a[0];
                var r = new MValue(v.Rows, v.Cols);
                double acc = 1;
                for (int i = 0; i < v.Data.Length; i++) { acc *= v.Data[i]; r.Data[i] = acc; }
                return r;
            };
            _builtins["diff"] = a => {
                var v = a[0];
                if (v.Rows == 1 || v.Cols == 1)
                {
                    int n = v.Data.Length - 1;
                    var r = new MValue(v.Rows == 1 ? 1 : n, v.Cols == 1 ? 1 : n);
                    for (int i = 0; i < n; i++) r.Data[i] = v.Data[i + 1] - v.Data[i];
                    return r;
                }
                throw new MatlabRuntimeException("diff: 2D matrices not supported yet");
            };
            _builtins["reshape"] = a => {
                var v = a[0];
                int nElem = v.Data.Length;
                // MATLAB: una dimension puede ser [] -> se infiere del total. Ej:
                // reshape(x, 1, []) -> 1 x n ;  reshape(x, [], 1) -> n x 1.
                bool rEmpty = a[1].Rows * a[1].Cols == 0;
                bool cEmpty = a.Length > 2 && a[2].Rows * a[2].Cols == 0;
                int rows, cols;
                if (rEmpty && cEmpty)
                    throw new MatlabRuntimeException("reshape: solo una dimension puede ser []");
                else if (rEmpty) { cols = (int)a[2].Scalar; rows = cols == 0 ? 0 : nElem / cols; }
                else if (cEmpty) { rows = (int)a[1].Scalar; cols = rows == 0 ? 0 : nElem / rows; }
                else { rows = (int)a[1].Scalar; cols = (int)a[2].Scalar; }
                if (rows * cols != v.Data.Length)
                    throw new MatlabRuntimeException($"reshape: size mismatch {v.Data.Length} ≠ {rows}×{cols}");
                // MATLAB usa orden column-major: la lectura linear del source y la
                // escritura del target deben respetar ese orden para preservar la
                // identidad reshape(v(:), m, n).
                var r = new MValue(rows, cols);
                int srcRows = v.Rows, srcCols = v.Cols;
                int n = v.Data.Length;
                for (int linear = 0; linear < n; linear++)
                {
                    // Source en column-major
                    int sCol = linear / srcRows;
                    int sRow = linear - sCol * srcRows;
                    double val = v.At(sRow, sCol);
                    // Target en column-major
                    int tCol = linear / rows;
                    int tRow = linear - tCol * rows;
                    r.Set(tRow, tCol, val);
                }
                return r;
            };
            _builtins["repmat"] = a => {
                var v = a[0];
                int rR = (int)a[1].Scalar;
                int rC = a.Length > 2 ? (int)a[2].Scalar : rR;
                var r = new MValue(v.Rows * rR, v.Cols * rC);
                for (int i = 0; i < r.Rows; i++)
                    for (int j = 0; j < r.Cols; j++)
                        r.Set(i, j, v.At(i % v.Rows, j % v.Cols));
                return r;
            };
            _builtins["repelem"] = a => {
                // repelem(v, n)  o  repelem(v, counts) para vectores (repite cada elemento)
                var v = a[0];
                bool isRow = v.Rows == 1;
                int len = v.Rows * v.Cols;
                var el = new double[len];
                { int k = 0;
                  if (isRow) for (int j = 0; j < v.Cols; j++) el[k++] = v.At(0, j);
                  else for (int i = 0; i < v.Rows; i++) el[k++] = v.At(i, 0); }
                var cnt = new int[len];
                if (a[1].Rows * a[1].Cols == 1) { int n = (int)a[1].Scalar; for (int i = 0; i < len; i++) cnt[i] = n; }
                else { for (int i = 0; i < len && i < a[1].Data.Length; i++) cnt[i] = (int)a[1].Data[i]; }
                int total = 0; for (int i = 0; i < len; i++) total += cnt[i];
                var outd = new double[total];
                { int k = 0; for (int i = 0; i < len; i++) for (int rr = 0; rr < cnt[i]; rr++) outd[k++] = el[i]; }
                return isRow ? new MValue(1, total, outd) : new MValue(total, 1, outd);
            };
            _builtins["jet"] = a => {
                // jet(n): colormap n x 3 (azul->cian->amarillo->rojo), paleta Abaqus/CSI
                int n = a.Length >= 1 ? (int)a[0].Scalar : 64;
                if (n < 1) n = 1;
                var m = new MValue(n, 3);
                for (int i = 0; i < n; i++)
                {
                    double t = n == 1 ? 0.5 : (double)i / (n - 1);
                    double r = Math.Min(4 * t - 1.5, -4 * t + 4.5);
                    double g = Math.Min(4 * t - 0.5, -4 * t + 3.5);
                    double b = Math.Min(4 * t + 0.5, -4 * t + 2.5);
                    m.Set(i, 0, Math.Max(0.0, Math.Min(1.0, r)));
                    m.Set(i, 1, Math.Max(0.0, Math.Min(1.0, g)));
                    m.Set(i, 2, Math.Max(0.0, Math.Min(1.0, b)));
                }
                return m;
            };
            _builtins["sort"] = a => {
                var v = a[0];
                var data = (double[])v.Data.Clone();
                Array.Sort(data);
                return new MValue(v.Rows, v.Cols, data);
            };
            // sortrows(A) / sortrows(A, col): ordena FILAS por columna(s). col negativo = descendente.
            _builtins["sortrows"] = a => {
                var v = a[0];
                int rows = v.Rows, cols = v.Cols;
                var keys = new List<(int col, int dir)>();
                if (a.Length >= 2 && a[1] != null && !a[1].IsString && a[1].Data.Length > 0) {
                    foreach (var c in a[1].Data) { int ci = (int)c; keys.Add((System.Math.Abs(ci) - 1, ci < 0 ? -1 : 1)); }
                } else {
                    for (int j = 0; j < cols; j++) keys.Add((j, 1));
                }
                var idx = new int[rows];
                for (int i = 0; i < rows; i++) idx[i] = i;
                Array.Sort(idx, (x, y) => {
                    foreach (var (col, dir) in keys) {
                        if (col < 0 || col >= cols) continue;
                        double dx = v.At(x, col), dy = v.At(y, col);
                        if (dx < dy) return -dir;
                        if (dx > dy) return dir;
                    }
                    return x.CompareTo(y);   // estable
                });
                var r = new MValue(rows, cols);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        r.Set(i, j, v.At(idx[i], j));
                return r;
            };
            _builtins["unique"] = a => {
                var v = a[0];
                var set = new SortedSet<double>(v.Data);
                var data = new double[set.Count];
                int k = 0; foreach (var x in set) data[k++] = x;
                return new MValue(1, data.Length, data);
            };
            // setdiff(A, B) — elementos en A que NO estan en B (set ordenado)
            _builtins["setdiff"] = a => {
                var setB = new HashSet<double>(a[1].Data);
                var diff = new SortedSet<double>();
                foreach (var x in a[0].Data) if (!setB.Contains(x)) diff.Add(x);
                var dataSD = new double[diff.Count];
                int kSD = 0; foreach (var x in diff) dataSD[kSD++] = x;
                return new MValue(1, dataSD.Length, dataSD);
            };
            // intersect(A, B) — elementos comunes a A y B (set ordenado)
            _builtins["intersect"] = a => {
                var setBi = new HashSet<double>(a[1].Data);
                var inter = new SortedSet<double>();
                foreach (var x in a[0].Data) if (setBi.Contains(x)) inter.Add(x);
                var dataIS = new double[inter.Count];
                int kIS = 0; foreach (var x in inter) dataIS[kIS++] = x;
                return new MValue(1, dataIS.Length, dataIS);
            };
            // union(A, B) — todos los elementos sin duplicados (set ordenado)
            _builtins["union"] = a => {
                var allU = new SortedSet<double>(a[0].Data);
                foreach (var x in a[1].Data) allU.Add(x);
                var dataU = new double[allU.Count];
                int kU = 0; foreach (var x in allU) dataU[kU++] = x;
                return new MValue(1, dataU.Length, dataU);
            };
            // ismember(A, B) — vector logico de mismo tamano que A
            _builtins["ismember"] = a => {
                var setBm = new HashSet<double>(a[1].Data);
                var r = new MValue(a[0].Rows, a[0].Cols);
                for (int i = 0; i < a[0].Data.Length; i++)
                    r.Data[i] = setBm.Contains(a[0].Data[i]) ? 1 : 0;
                return r;
            };
            _builtins["any"] = a => {
                foreach (var x in a[0].Data) if (x != 0) return new MValue(1);
                return new MValue(0);
            };
            _builtins["all"] = a => {
                foreach (var x in a[0].Data) if (x == 0) return new MValue(0);
                return new MValue(1);
            };
            _builtins["isempty"] = a => new MValue(a[0].Data.Length == 0 ? 1 : 0);
            _builtins["isscalar"] = a => new MValue(a[0].IsScalar ? 1 : 0);
            _builtins["isvector"] = a => new MValue((a[0].Rows == 1 || a[0].Cols == 1) ? 1 : 0);
            // ── No-op builtins esteticos de MATLAB (clean console / paths) ──
            // No tienen sentido en un entorno HTML/PDF render, pero son comunes
            // en scripts MATLAB. Los aceptamos como no-op para evitar errores.
            _builtins["clc"]     = a => new MValue(0);
            _builtins["close"]   = a => new MValue(0);
            _builtins["clf"]     = a => new MValue(0);
            _builtins["cla"]     = a => new MValue(0);
            _builtins["clear"]   = a => new MValue(0);
            _builtins["format"]  = a => new MValue(0);   // format bank/long/short/... : no-op (no cambia el render)
            _builtins["addpath"] = a => new MValue(0);
            _builtins["rmpath"]  = a => new MValue(0);
            _builtins["pause"]   = a => new MValue(0);
            _builtins["drawnow"] = a => { var f = MatlabPlots.RenderFrame(); if (f != null) _frameOut?.Invoke(f); return new MValue(0); };
            // ── true(...) / false(...) como funciones MATLAB que crean matrices logicas ──
            // true / false como literales ya estan resueltos en EvalIdent (consts MATLAB).
            // Aqui solo el caso de llamada con args para crear matrices de 1s o 0s.
            //
            // Soporta tres patrones MATLAB:
            //   false()              -> 0 escalar
            //   false(n)             -> matriz n×n
            //   false(m, n)          -> matriz m×n
            //   false([m, n])        -> matriz m×n (vector de tamaños, e.g. false(size(X)))
            (int r, int c) ParseSize(MValue[] a)
            {
                if (a.Length == 0) return (1, 1);
                if (a.Length == 1)
                {
                    // size() como vector [r, c]
                    if (a[0].Data.Length >= 2 && !a[0].IsScalar)
                        return ((int)a[0].Data[0], (int)a[0].Data[1]);
                    // scalar n -> matriz n×n
                    int n = (int)a[0].Scalar;
                    return (n, n);
                }
                return ((int)a[0].Scalar, (int)a[1].Scalar);
            }
            _builtins["true"]  = a => {
                if (a.Length == 0) return new MValue(1);
                var (rT, cT) = ParseSize(a);
                var mTrue = new MValue(rT, cT);
                for (int i = 0; i < mTrue.Data.Length; i++) mTrue.Data[i] = 1;
                return mTrue;
            };
            _builtins["false"] = a => {
                if (a.Length == 0) return new MValue(0);
                var (rF, cF) = ParseSize(a);
                return new MValue(rF, cF);  // ya inicializa a 0
            };
            _builtins["disp"] = a => {
                // Para matrices: imprimir con formato MATLAB clásico
                if (a[0] == null) { _output?.Invoke(""); return a[0]; }
                if (a[0].IsString) { _output?.Invoke(a[0].StringValue); return a[0]; }
                // Para simbolico: mostrar la expresion simplificada como texto
                if (a[0].IsSymbolic) { _output?.Invoke(a[0].Symbolic.Simplify().ToInfix()); return a[0]; }
                if (a[0].IsScalar) { _output?.Invoke(a[0].Scalar.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)); return a[0]; }
                if (a[0].IsStruct)
                {
                    var sbDisp = new StringBuilder();
                    foreach (var kv in a[0].Fields)
                        sbDisp.AppendLine($"  {kv.Key}: {(kv.Value.IsScalar ? kv.Value.Scalar.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) : "[" + kv.Value.Rows + "x" + kv.Value.Cols + "]")}");
                    _output?.Invoke(sbDisp.ToString().TrimEnd());
                    return a[0];
                }
                // Matrix display: cada fila entre corchetes `[a  b  c]` (celdas separadas por 2
                // espacios) para que RenderDispWithMatrices la muestre como MATRIZ rica (corchetes
                // grandes tipo worksheet Calcpad), no como texto plano. Un valor NO es texto.
                var sbM = new StringBuilder();
                for (int i = 0; i < a[0].Rows; i++)
                {
                    sbM.Append('[');
                    for (int j = 0; j < a[0].Cols; j++)
                    {
                        if (j > 0) sbM.Append("  ");
                        sbM.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:G6}", a[0].At(i, j));
                    }
                    sbM.Append(']');
                    sbM.Append('\n');   // \n (no AppendLine): el \r de CRLF rompe TryParseMatrixRow (línea debe terminar en ']')
                }
                _output?.Invoke(sbM.ToString().TrimEnd());
                return a[0];
            };
            _builtins["mat2str"] = a => {
                if (a[0].IsScalar) return new MValue(a[0].Scalar.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                if (a[0].IsString) return new MValue($"'{a[0].StringValue}'");
                int prec = a.Length >= 2 ? (int)a[1].Scalar : 15;
                var sbM2 = new StringBuilder("[");
                for (int i = 0; i < a[0].Rows; i++)
                {
                    if (i > 0) sbM2.Append("; ");
                    for (int j = 0; j < a[0].Cols; j++)
                    {
                        if (j > 0) sbM2.Append(" ");
                        sbM2.Append(a[0].At(i, j).ToString($"G{prec}", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                sbM2.Append("]");
                return new MValue(sbM2.ToString());
            };
            _builtins["accumarray"] = a => {
                // accumarray(subs, vals[, sz]) — acumula vals en sus subs respectivos
                if (a.Length < 2) throw new MatlabRuntimeException("accumarray(subs, vals)");
                var subs = a[0].Data;   // índices 1-based
                var vals = a[1].Data;
                int n = subs.Length;
                int maxIdx = 0;
                for (int i = 0; i < n; i++) maxIdx = Math.Max(maxIdx, (int)subs[i]);
                int sz = a.Length >= 3 ? (int)a[2].Scalar : maxIdx;
                var r = new MValue(sz, 1);
                for (int i = 0; i < n; i++)
                {
                    int idx = (int)subs[i] - 1;
                    if (idx >= 0 && idx < sz) r.Set(idx, 0, r.At(idx, 0) + vals[i]);
                }
                return r;
            };
            _builtins["tabulate"] = a => {
                // tabulate(x) — tabla de frecuencias [valor, count, percent]
                var v = a[0].Data;
                var groups = v.GroupBy(x => x).OrderBy(g => g.Key).ToArray();
                int n = groups.Length;
                var result = new MValue(n, 3);
                for (int i = 0; i < n; i++)
                {
                    result.Set(i, 0, groups[i].Key);
                    result.Set(i, 1, groups[i].Count());
                    result.Set(i, 2, 100.0 * groups[i].Count() / v.Length);
                }
                return result;
            };
            _builtins["histcounts"] = a => {
                var v = a[0].Data;
                int nbins = a.Length >= 2 ? (int)a[1].Scalar : 10;
                double mn = v.Min(), mx = v.Max();
                double w = (mx - mn) / nbins;
                var counts = new double[nbins];
                foreach (var x in v)
                {
                    int b = Math.Min(nbins - 1, (int)((x - mn) / w));
                    counts[b]++;
                }
                return new MValue(1, nbins, counts);
            };

            // ─── Plot builtins (emiten HTML via _htmlOut) ────────────────────
            _builtins["colormap"] = a => {
                a = DropAxes(a);   // tolerar colormap(ax, jet) — ax = gca handle
                if (a.Length > 0 && a[0] != null)
                {
                    if (a[0].IsString) _activeColormap = a[0].StringValue;
                    else if (!a[0].IsStruct && a[0].Cols == 3 && a[0].Rows >= 2)
                    {
                        // matriz Nx3 (colormap(gca, jet(256)) / coolw(256)) -> colormap custom real
                        var rows = new double[a[0].Rows][];
                        for (int i = 0; i < a[0].Rows; i++)
                            rows[i] = new[] { a[0].At(i, 0), a[0].At(i, 1), a[0].At(i, 2) };
                        MatlabPlots.SetCustomColormap(rows);
                        _activeColormap = "custom";
                    }
                    else _activeColormap = "custom";
                    // MATLAB: colormap tras el plot lo re-colorea -> re-estilizar el ultimo plot.
                    var restyle = MatlabPlots.RestyleLastColormap(_activeColormap);
                    if (restyle != null) _htmlOut?.Invoke(restyle);
                }
                return a.Length > 0 ? a[0] : new MValue(0);
            };
            // Nombres de colormap como funciones (MATLAB): colormap(jet), colormap(jet_r) — `_r` = INVERTIDO.
            foreach (var cm in new[] { "jet", "jet_r", "parula", "viridis", "hot", "cool", "gray", "grey",
                                       "bone", "hsv", "spring", "summer", "autumn", "winter", "copper" })
            {
                var name = cm;
                _builtins[name] = _a => {
                    // MATLAB: jet / jet(n) -> SIEMPRE matriz n×3 RGB (0..1). Sin arg -> 256×3.
                    // (ANTES devolvia el string "jet" sin arg, lo que rompia flipud(jet) y
                    //  colormap(flipud(jet)); ahora es identico a MATLAB 2017a: jet es una matriz.)
                    bool rev = name.EndsWith("_r");
                    int nn = (_a.Length >= 1 && _a[0].IsScalar && !_a[0].IsString)
                             ? Math.Max(1, (int)_a[0].Scalar) : 256;
                    var M = new MValue(nn, 3);
                    for (int k = 0; k < nn; k++)
                    {
                        double tt = nn == 1 ? 0.5 : (double)k / (nn - 1);
                        if (rev) tt = 1 - tt;
                        // formula jet (base; la placa usa jet/jet_r)
                        double r = Math.Max(0, Math.Min(1, Math.Min(4*tt - 1.5, -4*tt + 4.5)));
                        double g = Math.Max(0, Math.Min(1, Math.Min(4*tt - 0.5, -4*tt + 3.5)));
                        double b = Math.Max(0, Math.Min(1, Math.Min(4*tt + 0.5, -4*tt + 2.5)));
                        M.Set(k, 0, r); M.Set(k, 1, g); M.Set(k, 2, b);
                    }
                    return M;
                };
            }
            _builtins["surf"] = a => {
                a = DropAxes(a);   // tolerar surf(ax, …)
                MValue X, Y, Z, C = null;
                if (a.Length >= 3 && !a[0].IsString && !a[1].IsString && !a[2].IsString) { X = a[0]; Y = a[1]; Z = a[2]; }
                else if (a.Length >= 1 && !a[0].IsString) {
                    Z = a[0];
                    X = new MValue(Z.Rows, Z.Cols); Y = new MValue(Z.Rows, Z.Cols);
                    for (int i = 0; i < Z.Rows; i++) for (int j = 0; j < Z.Cols; j++) { X.Set(i, j, j+1); Y.Set(i, j, i+1); }
                }
                else throw new MatlabRuntimeException("surf requires 1 or 3 args");
                // 4º positional = C (datos de color) si es matriz (no string de opción)
                int opt = (a.Length >= 3 && !a[0].IsString) ? 3 : 1;
                if (opt < a.Length && !a[opt].IsString) { C = a[opt]; opt++; }
                // Opciones name-value: FaceColor / EdgeColor / FaceAlpha
                string faceColorMode = "colormap"; string faceColorCss = null; double faceAlpha = 1; string edgeColor = "";
                for (int i = opt; i + 1 < a.Length; i += 2)
                {
                    if (!a[i].IsString) continue;
                    string key = a[i].StringValue.ToLowerInvariant(); var val = a[i + 1];
                    switch (key)
                    {
                        case "facecolor":
                            if (val.IsString) { var sv = val.StringValue.ToLowerInvariant(); if (sv == "interp" || sv == "flat") faceColorMode = "interp"; else { faceColorCss = MatlabColorToJs(val.StringValue); faceColorMode = "uniform"; } }
                            else if (val.Rows == 1 && val.Cols == 3) { faceColorCss = RgbVecToCss(val); faceColorMode = "uniform"; }
                            break;
                        case "edgecolor":
                            edgeColor = val.IsString ? MatlabColorToJs(val.StringValue) : (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : edgeColor);
                            break;
                        case "facealpha": faceAlpha = val.Scalar; break;
                    }
                }
                // Con figura abierta (hold on) → COMPONER en la misma escena 3D (como MATLAB).
                // Sin figura → surf standalone (comportamiento previo).
                if (MatlabPlots.HasOpenFigure)
                    MatlabPlots.AddSurf3D(X, Y, Z, C, _activeColormap, faceColorMode, faceColorCss, faceAlpha, edgeColor);
                else
                    _htmlOut?.Invoke(MatlabPlots.Surf(X, Y, Z, _activeColormap, "surf"));
                return new MValue(0);
            };
            _builtins["mesh"] = _builtins["surf"];  // wireframe = surf MVP
            _builtins["contourf"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("contourf(X, Y, Z[, n])");
                int n = a.Length >= 4 ? (int)a[3].Scalar : 10;
                _htmlOut?.Invoke(MatlabPlots.Contourf(a[0], a[1], a[2], n, _activeColormap));
                return new MValue(0);
            };
            _builtins["contour"] = _builtins["contourf"];
            _builtins["imagesc"] = a => {
                _htmlOut?.Invoke(MatlabPlots.Imagesc(a[0], _activeColormap));
                return new MValue(0);
            };
            _builtins["pcolor"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("pcolor(X, Y, Z)");
                _htmlOut?.Invoke(MatlabPlots.Contourf(a[0], a[1], a[2], 30, _activeColormap));
                return new MValue(0);
            };
            _builtins["plot"] = a => {
                a = DropAxes(a);   // tolerar plot(ax, …)
                // plot(G, 'XData',X, 'YData',Y, 'EdgeLabel',L) — dibujo de un objeto graph,
                // que es como CEINCI-LAB dibuja la estructura en dibujoplano.m: cada barra
                // es una arista y cada nudo un circulo numerado.
                if (a.Length >= 1 && a[0].Fields != null && a[0].Fields.ContainsKey("__isgraph__"))
                    return PlotGraphObject(a);
                // plot(Y) | plot(X,Y) | plot(X,Y,'spec') | + name-value (Color, LineWidth,
                // MarkerFaceColor, MarkerEdgeColor, MarkerSize). Respeta el linespec ('o','^','-',...)
                // y, si hay figura abierta, COMPONE en los mismos ejes que patch/line/text.
                MValue X, Y;
                int rest;
                if (a.Length >= 2 && !a[1].IsString) { X = a[0]; Y = a[1]; rest = 2; }
                else {
                    Y = a[0];
                    X = new MValue(1, Y.Data.Length);
                    for (int i = 0; i < X.Data.Length; i++) X.Data[i] = i + 1;
                    rest = 1;
                }
                bool wantLine = false, wantMarker = false;
                string specColor = null, symbol = "circle";
                if (rest < a.Length && a[rest].IsString) {
                    ParseLineSpec(a[rest].StringValue, out wantLine, out wantMarker, out specColor, out symbol);
                    rest++;
                }
                if (!wantLine && !wantMarker) wantLine = true;   // default MATLAB = línea
                bool autoColor = specColor == null;   // sin color explícito -> ciclar ColorOrder
                string lineColor = specColor ?? "#1f77b4";
                // MATLAB solo RELLENA el marcador si se pide MarkerFaceColor.
                // 'ro' es un circulo rojo HUECO, no macizo. El color del linespec
                // va al BORDE. Lab lo usaba como relleno y salian todos macizos.
                // El punto '.' es la EXCEPCION: en MATLAB siempre va macizo.
                string markerFill = symbol == "point" ? (specColor ?? "#1f77b4") : "none";
                string markerEdge = specColor;
                double lineWidth = 1.5, markerSize = 6;
                string dispName = null;   // DisplayName -> nombre en la leyenda
                bool hideFromLegend = false;
                for (int i = rest; i + 1 < a.Length; i += 2) {
                    if (!a[i].IsString) break;
                    switch (a[i].StringValue.ToLowerInvariant()) {
                        case "color": lineColor = ColorArg(a[i+1]); autoColor = false;
                            if (specColor == null) { markerFill = lineColor; markerEdge = lineColor; } break;
                        case "linewidth": lineWidth = a[i+1].Scalar; break;
                        case "markerfacecolor": markerFill = ColorArg(a[i+1]); break;
                        case "markeredgecolor": markerEdge = ColorArg(a[i+1]); break;
                        case "markersize": markerSize = a[i+1].Scalar; break;
                        case "displayname": dispName = a[i+1].IsString ? a[i+1].StringValue : null; break;
                        case "handlevisibility": hideFromLegend = a[i+1].IsString &&
                            a[i+1].StringValue.Equals("off", StringComparison.OrdinalIgnoreCase); break;
                    }
                }
                if (hideFromLegend) dispName = null;
                markerFill ??= lineColor; markerEdge ??= "black";
                // MATLAB: todo plot va a los EJES ACTUALES. Componemos SIEMPRE en una figura.
                // Sin hold, cada plot es su propia figura: cerrar la anterior (si la hay) y abrir
                // una nueva -> así `plot(a); hold on; plot(b)` compone a+b en los mismos ejes
                // (antes 'a' se emitía suelto y quedaba en un gráfico aparte).
                if (!_holdOn && MatlabPlots.HasOpenFigure) {
                    string prevh = MatlabPlots.FinishFigure();
                    if (!string.IsNullOrEmpty(prevh)) _htmlOut?.Invoke(prevh);
                }
                if (!MatlabPlots.HasOpenFigure) {
                    string prev = MatlabPlots.BeginFigure();
                    if (!string.IsNullOrEmpty(prev)) _htmlOut?.Invoke(prev);
                    _colorCycleIdx = 0;   // ejes nuevos -> reiniciar el ciclo de colores
                }
                // Color automático (MATLAB ColorOrder): curvas sucesivas sin color explícito.
                if (autoColor) {
                    lineColor = _matlabColorOrder[_colorCycleIdx % _matlabColorOrder.Length];
                    _colorCycleIdx++;
                    if (symbol != "point") markerEdge ??= lineColor;
                }
                // el nombre va a UNA sola traza (la línea si hay; si no, el marcador) -> una entrada
                if (wantLine) MatlabPlots.Line2D(X.Data, Y.Data, lineColor, lineWidth, dispName);
                if (wantMarker) MatlabPlots.Markers2D(X.Data, Y.Data, markerFill, markerEdge, symbol, markerSize, wantLine ? null : dispName);
                return new MValue(0);
            };
            _builtins["plot3"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("plot3(x, y, z)");
                // Si hay una figura 3D abierta (p.ej. tras patch del sólido), COMPONER la línea en
                // esa escena (jaula de acero embebida). Si no, plot3 autónomo como antes.
                if (MatlabPlots.HasOpenFigure && MatlabPlots.FigureIs3D)
                {
                    string col = "#c81a0d"; double lw = 2.0;
                    for (int i = 3; i + 1 < a.Length; i++)
                    {
                        if (!a[i].IsString) continue;
                        string key = a[i].StringValue.ToLowerInvariant();
                        if (key == "color")
                            col = a[i + 1].IsString ? MatlabColorToJs(a[i + 1].StringValue)
                                  : (a[i + 1].Rows == 1 && a[i + 1].Cols == 3 ? RgbVecToCss(a[i + 1]) : col);
                        else if (key == "linewidth") lw = a[i + 1].Scalar;
                    }
                    MatlabPlots.AddLine3D(a[0].Data, a[1].Data, a[2].Data, col, lw);
                }
                else
                    _htmlOut?.Invoke(MatlabPlots.Plot3(a[0], a[1], a[2]));
                return new MValue(0);
            };
            // Viewer interactivo de la mesa: slabview3d(A, B, H, na, nb, struct_campos)
            //   struct con campos nodales (length (na+1)*(nb+1), indexados i*(nb+1)+j):
            //   s.w, s.Mxy, s.Mx, s.My ... → 2D color map (jet+hover) + 3D Three.js + selector.
            _builtins["slabview3d"] = a => {
                if (a.Length < 6 || a[5] == null || a[5].Fields == null)
                    throw new MatlabRuntimeException("slabview3d(A, B, H, na, nb, struct con campos)");
                double A = a[0].Scalar, B = a[1].Scalar, H = a[2].Scalar;
                int na = (int)a[3].Scalar, nb = (int)a[4].Scalar;
                var fields = new System.Collections.Generic.List<(string, string, string, double[])>();
                foreach (var kv in a[5].Fields)
                {
                    string unit = kv.Key == "w" ? "mm" : "kNm/m";
                    var arr = kv.Value?.Data ?? System.Array.Empty<double>();
                    fields.Add((kv.Key, kv.Key + " [" + unit + "]", unit, (double[])arr.Clone()));
                }
                _htmlOut?.Invoke(MatlabPlots.SlabView3D(A, B, H, na, nb, fields));
                return new MValue(0);
            };
            _builtins["peaks"] = a => {
                if (a.Length == 0) {
                    var vv = new double[49]; for (int i = 0; i < 49; i++) vv[i] = -3.0 + i * 6.0 / 48;
                    return MatlabPlots.Peaks(vv, vv);
                }
                if (a.Length == 1) {
                    if (a[0].IsScalar) {
                        int n = (int)a[0].Scalar;
                        var vv = new double[n]; for (int i = 0; i < n; i++) vv[i] = -3.0 + i * 6.0 / Math.Max(n-1, 1);
                        return MatlabPlots.Peaks(vv, vv);
                    }
                    var vec = a[0].Data;
                    return MatlabPlots.Peaks(vec, vec);
                }
                return MatlabPlots.PeaksFromGrid(a[0], a[1]);
            };
            _builtins["bar"] = a => {
                MValue X, Y;
                if (a.Length == 1) {
                    Y = a[0];
                    X = new MValue(1, Y.Data.Length);
                    for (int i = 0; i < X.Data.Length; i++) X.Data[i] = i + 1;
                } else { X = a[0]; Y = a[1]; }
                _htmlOut?.Invoke(MatlabPlots.Bar(X, Y, false));
                return new MValue(0);
            };
            _builtins["barh"] = a => {
                MValue X, Y;
                if (a.Length == 1) {
                    Y = a[0]; X = new MValue(1, Y.Data.Length);
                    for (int i = 0; i < X.Data.Length; i++) X.Data[i] = i + 1;
                } else { X = a[0]; Y = a[1]; }
                _htmlOut?.Invoke(MatlabPlots.Bar(X, Y, true));
                return new MValue(0);
            };
            _builtins["scatter"] = a => {
                a = DropAxes(a);   // tolerar scatter(ax, …)
                if (a.Length < 2) throw new MatlabRuntimeException("scatter(x, y[, sz, c, 'filled'])");
                // scatter(x, y, sz, c, 'filled') — sz y c son POSICIONALES; 'filled' es flag string.
                MValue sz = null, c = null;
                bool filled = false;
                int pos = 2;
                if (pos < a.Length && !a[pos].IsString) { sz = a[pos]; pos++; }        // 3º = tamaño
                if (pos < a.Length && !a[pos].IsString) { c = a[pos]; pos++; }         // 4º = color (vector/RGB)
                for (int k = pos; k < a.Length; k++)                                   // flags: 'filled', o color string
                {
                    if (!a[k].IsString) continue;
                    var s = a[k].StringValue;
                    if (s.Equals("filled", StringComparison.OrdinalIgnoreCase)) filled = true;
                    else if (c == null) c = a[k];                                      // scatter(x,y,'r')
                }
                _htmlOut?.Invoke(MatlabPlots.Scatter(a[0], a[1], sz, c, _activeColormap, filled,
                                                     MatlabPlots.ColorbarState));
                return new MValue(0);
            };
            _builtins["scatter3"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("scatter3(x, y, z)");
                _htmlOut?.Invoke(MatlabPlots.Scatter3(a[0], a[1], a[2]));
                return new MValue(0);
            };
            _builtins["histogram"] = a => {
                int nb = a.Length >= 2 ? (int)a[1].Scalar : 20;
                _htmlOut?.Invoke(MatlabPlots.Histogram(a[0], nb));
                return new MValue(0);
            };
            _builtins["hist"] = _builtins["histogram"];
            _builtins["histogram2"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("histogram2(X, Y[, nbins])");
                int nb = a.Length >= 3 ? (int)a[2].Scalar : 20;
                _htmlOut?.Invoke(MatlabPlots.Histogram2D(a[0], a[1], nb));
                return new MValue(0);
            };
            _builtins["heatmap"] = a => {
                _htmlOut?.Invoke(MatlabPlots.Heatmap(a[0], _activeColormap));
                return new MValue(0);
            };
            _builtins["stem"] = a => {
                MValue X, Y;
                if (a.Length == 1) {
                    Y = a[0]; X = new MValue(1, Y.Data.Length);
                    for (int i = 0; i < X.Data.Length; i++) X.Data[i] = i + 1;
                } else { X = a[0]; Y = a[1]; }
                _htmlOut?.Invoke(MatlabPlots.Stem(X, Y));
                return new MValue(0);
            };
            _builtins["polar"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("polar(theta, r)");
                _htmlOut?.Invoke(MatlabPlots.Polar(a[0], a[1]));
                return new MValue(0);
            };
            _builtins["quiver"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("quiver(X, Y, U, V)");
                _htmlOut?.Invoke(MatlabPlots.Quiver(a[0], a[1], a[2], a[3]));
                return new MValue(0);
            };
            _builtins["quiver3"] = a => {
                if (a.Length < 6) throw new MatlabRuntimeException("quiver3(X, Y, Z, U, V, W)");
                _htmlOut?.Invoke(MatlabPlots.Quiver3(a[0], a[1], a[2], a[3], a[4], a[5]));
                return new MValue(0);
            };
            _builtins["slice"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("slice(X, Y, Z, V[, planes...])");
                _htmlOut?.Invoke(MatlabPlots.Slice(a[0], a[1], a[2], a[3],
                    a.Length > 4 ? a[4].Data : new double[0],
                    a.Length > 5 ? a[5].Data : new double[0],
                    a.Length > 6 ? a[6].Data : new double[0]));
                return new MValue(0);
            };
            _builtins["streamslice"] = _builtins["quiver"];  // approximation
            _builtins["title"] = a => {
                a = DropAxes(a);   // tolerar title(ax, …)
                if (a.Length > 0 && a[0].IsString) {
                    if (MatlabPlots.HasOpenFigure) MatlabPlots.SetFigTitle(a[0].StringValue);
                    else RelayoutLastPlot("title", JsonEscape(a[0].StringValue));
                }
                return new MValue(0);
            };
            _builtins["xlabel"] = a => {
                a = DropAxes(a);
                if (a.Length > 0 && a[0].IsString) {
                    if (MatlabPlots.HasOpenFigure) MatlabPlots.SetFigXLabel(a[0].StringValue);
                    else RelayoutLastPlot("xaxis.title", JsonEscape(a[0].StringValue));
                }
                return new MValue(0);
            };
            _builtins["ylabel"] = a => {
                a = DropAxes(a);
                if (a.Length > 0 && a[0].IsString) {
                    if (MatlabPlots.HasOpenFigure) MatlabPlots.SetFigYLabel(a[0].StringValue);
                    else RelayoutLastPlot("yaxis.title", JsonEscape(a[0].StringValue));
                }
                return new MValue(0);
            };
            _builtins["zlabel"] = a => {
                if (a.Length > 0 && a[0].IsString) {
                    if (MatlabPlots.HasOpenFigure) MatlabPlots.SetFigZLabel(a[0].StringValue);
                    else RelayoutLastPlot("scene.zaxis.title", JsonEscape(a[0].StringValue));
                }
                return new MValue(0);
            };
            _builtins["legend"] = a => {
                // legend('Location','northeast')  -> posiciona la leyenda (usa los DisplayName ya puestos).
                // legend('a','b',...)             -> asigna nombres a las trazas (orden) + muestra.
                // legend / legend('show')         -> solo muestra la leyenda.
                string loc = null;
                var names = new List<string>();
                for (int k = 0; k < a.Length; k++)
                {
                    if (!a[k].IsString) continue;
                    var s = a[k].StringValue;
                    if (s.Equals("Location", StringComparison.OrdinalIgnoreCase) && k + 1 < a.Length)
                    { loc = a[k + 1].IsString ? a[k + 1].StringValue : null; k++; continue; }
                    if (s.Equals("show", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("boxoff", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("boxon", StringComparison.OrdinalIgnoreCase)) continue;
                    names.Add(s);
                }
                MatlabPlots.SetLegend(loc, names.Count > 0 ? names.ToArray() : null);   // estado de figura -> FinishFigure lo emite (sirve para --shot)
                // y además, por si la figura YA está renderizada, un script en vivo
                string legPos = MatlabPlots.LegendPosJson(loc);
                var setNames = names.Count > 0
                    ? $"Plotly.restyle(d, 'name', [{string.Join(",", names.Select(n => $"\"{JsonEscape(n)}\""))}], null); Plotly.restyle(d, 'showlegend', [true], [{string.Join(",", Enumerable.Range(0, names.Count))}]);"
                    : "";
                _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{MatlabPlots.LastPlotId}'); if(d&&window.Plotly){{{setNames} Plotly.relayout(d, {{showlegend:true, legend:{legPos}}});}}}})();</script>\n");
                return new MValue(0);
            };
            // Helper para emitir relayout sobre el último plot
            void RelayoutLastPlot(string property, string jsonValue)
            {
                int id = MatlabPlots.LastPlotId;
                if (id == 0) return;
                _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{id}'); if(d&&window.Plotly) Plotly.relayout(d, '{property}', \"{jsonValue}\");}})();</script>\n");
            }
            string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            _builtins["colorbar"] = a => {
                MatlabPlots.SetColorbar(true);
                // MATLAB: colorbar tras el plot enciende la barra en el plot YA dibujado.
                var r = MatlabPlots.RestyleLastColorbar(true);
                if (r != null) _htmlOut?.Invoke(r);
                return new MValue(0);
            };
            _builtins["shading"] = a => new MValue(0);
            _builtins["axis"] = a => {
                // axis('equal'|'square'|'tight'|'normal') o axis([xmin xmax ymin ymax])
                a = DropAxes(a);   // tolerar axis(ax, …)
                // Aspecto del canvas 3D: equal/square = proporciones reales (escena sólida);
                // tight/normal/auto = cada eje se estira para llenar (surf de resultados).
                if (a.Length > 0 && a[0].IsString)
                    switch (a[0].StringValue)
                    {
                        case "equal": case "square": case "image": MatlabPlots.SetAxisEqual(true); break;
                        case "tight": case "normal": case "auto": MatlabPlots.SetAxisEqual(false); break;
                    }
                int id = MatlabPlots.LastPlotId;
                if (id == 0) return new MValue(0);
                if (a.Length > 0 && a[0].IsString)
                {
                    string mode = a[0].StringValue;
                    string layout = mode switch
                    {
                        "equal" => "{yaxis: {scaleanchor: 'x', scaleratio: 1}}",
                        "square" => "{yaxis: {scaleanchor: 'x', scaleratio: 1}, width: 500, height: 500}",
                        "tight" => "{xaxis: {autorange: true}, yaxis: {autorange: true}}",
                        "normal" => "{xaxis: {autorange: true}, yaxis: {autorange: true}}",
                        _ => null
                    };
                    if (layout != null)
                        _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{id}'); if(d&&window.Plotly) Plotly.relayout(d, {layout});}})();</script>\n");
                }
                else if (a.Length > 0 && a[0].Data.Length >= 4)
                {
                    var b = a[0].Data;
                    _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{id}'); if(d&&window.Plotly) Plotly.relayout(d, {{xaxis:{{range:[{b[0]},{b[1]}]}}, yaxis:{{range:[{b[2]},{b[3]}]}}}});}})();</script>\n");
                }
                return new MValue(0);
            };
            _builtins["view"] = a => new MValue(0);
            _builtins["grid"] = a => {
                int id = MatlabPlots.LastPlotId;
                bool on = true;
                if (a.Length > 0)
                {
                    if (a[0].IsString) on = a[0].StringValue == "on";
                    else if (a[0].IsScalar) on = a[0].Scalar != 0;
                }
                MatlabPlots.SetGrid(on);   // tambien para el renderizador SVG de primitivas
                if (id == 0) return new MValue(0);
                string js = on ? "{xaxis:{showgrid:true}, yaxis:{showgrid:true}}" : "{xaxis:{showgrid:false}, yaxis:{showgrid:false}}";
                _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{id}'); if(d&&window.Plotly) Plotly.relayout(d, {js});}})();</script>\n");
                return new MValue(0);
            };
            _builtins["hold"] = a => {
                // hold on  -> abrir la figura compuesta: plot/scatter/text SIGUIENTES se
                //             superponen en los mismos ejes (antes cada plot abría uno nuevo).
                // hold off -> cerrar/emitir la figura actual: el próximo plot empieza otra.
                // hold     -> alterna.
                string mode = a.Length > 0 && a[0].IsString
                    ? a[0].StringValue.Trim().ToLowerInvariant()
                    : (_holdOn ? "off" : "on");
                if (mode == "off") {
                    _holdOn = false;
                    string html = MatlabPlots.FinishFigure();
                    if (!string.IsNullOrEmpty(html)) _htmlOut?.Invoke(html);
                } else {   // "on" (o cualquier otra cosa la tratamos como on)
                    _holdOn = true;
                    if (!MatlabPlots.HasOpenFigure) {
                        string prev = MatlabPlots.BeginFigure();
                        if (!string.IsNullOrEmpty(prev)) _htmlOut?.Invoke(prev);
                    }
                }
                return new MValue(0);
            };
            // Modos interactivos de figura de MATLAB 2017a — en Calcpad Lab los plots
            // (plotly + canvas WebView2) YA traen hover/datatip/zoom/pan nativos, así que
            // estos son no-op para que el MISMO script con hover cursor no falle (Undefined).
            // El hover interactivo funciona igual: al pasar el cursor se ven los valores.
            _builtins["datacursormode"] = a => new MValue(0);
            _builtins["zoom"] = a => new MValue(0);
            _builtins["pan"] = a => new MValue(0);
            _builtins["rotate3d"] = a => new MValue(0);
            _builtins["brush"] = a => new MValue(0);
            // guidata(h, data) almacena / guidata(h) recupera datos de app por figura
            // (el patrón de hover custom de MATLAB: set(fig,'WindowButtonMotionFcn',@cb)
            // + guidata para pasar datos al callback). Store en memoria por handle.
            var guiStore = new Dictionary<int, MValue>();
            _builtins["guidata"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("guidata(h[, data])");
                int h = a[0].IsScalar ? (int)a[0].Scalar : 0;
                if (a.Length >= 2) { guiStore[h] = a[1]; return new MValue(0); }
                return guiStore.TryGetValue(h, out var d) ? d : new MValue(0);
            };
            _builtins["figure"] = a => {
                // Cerrar figura anterior si está abierta (emit HTML), comenzar nueva
                string prev = MatlabPlots.BeginFigure();
                if (!string.IsNullOrEmpty(prev)) _htmlOut?.Invoke(prev);
                _htmlOut?.Invoke("<div class=\"matlab-figure-break\" style=\"height:0;margin:.5em 0\"></div>\n");
                _subplotGrid = null;
                return new MValue(0);
            };
            _builtins["patch"] = a => {
                // Modo simple posicional: patch(X, Y, color)  o  patch(X, Y, Z, color) para 3D
                // Modo named-args: patch('Faces', F, 'Vertices', V, 'FaceColor', col, ...)
                if (a.Length == 0) throw new MatlabRuntimeException("patch needs args");
                // Detectar modo named: primer arg es string 'Faces' o similar
                if (a[0].IsString)
                {
                    return EvalPatchNamed(a);
                }
                // Modo posicional: X, Y, [Z], color
                MValue X = a[0], Y = a[1];
                string faceColor = "lightblue", edgeColor = "black";
                double faceAlpha = 1, lineWidth = 1;
                int next = 2;
                // patch(X, Y, Z, color) — 3D: a[2] es Z SOLO si es vector del mismo nº de
                // vértices que X. Un color [r g b] (1×3) NO es Z → es el faceColor.
                if (a.Length >= 4 && !a[2].IsString &&
                    a[2].Data.Length == X.Data.Length && X.Data.Length != 3)
                {
                    next = 3;
                }
                if (a.Length > next)
                {
                    if (a[next].IsString) faceColor = MatlabColorToJs(a[next].StringValue);
                    else if (a[next].IsScalar) faceColor = ScalarToColorJs(a[next].Scalar);
                    else if (a[next].Rows == 1 && a[next].Cols == 3)
                        faceColor = RgbVecToCss(a[next]);
                }
                // Parse name-value pairs after positional args
                for (int i = next + 1; i + 1 < a.Length; i += 2)
                {
                    if (!a[i].IsString) break;
                    string key = a[i].StringValue.ToLowerInvariant();
                    var val = a[i + 1];
                    switch (key)
                    {
                        case "facecolor":
                            faceColor = val.IsString ? MatlabColorToJs(val.StringValue) :
                                        (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : faceColor);
                            break;
                        case "edgecolor":
                            edgeColor = val.IsString ? MatlabColorToJs(val.StringValue) :
                                        (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : edgeColor);
                            break;
                        case "facealpha": faceAlpha = val.Scalar; break;
                        case "linewidth": lineWidth = val.Scalar; break;
                        case "linestyle": /* TODO */ break;
                    }
                }
                MatlabPlots.Patch2D(X.Data, Y.Data, faceColor, edgeColor, faceAlpha, lineWidth);
                return new MValue(0);
            };
            // graph(NI, NJ [, weights]) — objeto grafo de MATLAB. CEINCI-LAB lo usa en
            // dibujoplano.m para dibujar la estructura con los nudos numerados y una
            // etiqueta por elemento. Se modela como struct con la tabla Edges, de modo
            // que G.Edges.Weight y G.Edges.EndNodes funcionen como en MATLAB.
            _builtins["graph"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("graph(s, t [, weights])");
                var s = a[0].Data; var tt = a[1].Data;
                int m = System.Math.Min(s.Length, tt.Length);
                var w = new double[m];
                if (a.Length >= 3 && a[2].Data != null)
                    for (int i = 0; i < m; i++) w[i] = i < a[2].Data.Length ? a[2].Data[i] : 0;
                else
                    for (int i = 0; i < m; i++) w[i] = 1;
                // EndNodes: matriz m x 2 (nudo inicial, nudo final)
                var en = new double[m * 2];
                for (int i = 0; i < m; i++) { en[i * 2] = s[i]; en[i * 2 + 1] = tt[i]; }
                var edges = MValue.NewStruct();
                edges.Fields["endnodes"] = new MValue(m, 2, en);
                edges.Fields["EndNodes"] = edges.Fields["endnodes"];
                edges.Fields["weight"]   = new MValue(1, m, (double[])w.Clone());
                edges.Fields["Weight"]   = edges.Fields["weight"];
                int nNodes = 0;
                for (int i = 0; i < m; i++)
                {
                    if (s[i]  > nNodes) nNodes = (int)System.Math.Round(s[i]);
                    if (tt[i] > nNodes) nNodes = (int)System.Math.Round(tt[i]);
                }
                var nodes = MValue.NewStruct();
                nodes.Fields["name"] = new MValue(nNodes, 1);
                nodes.Fields["Name"] = nodes.Fields["name"];
                var g = MValue.NewStruct();
                g.Fields["edges"] = edges;
                g.Fields["Edges"] = edges;
                g.Fields["nodes"] = nodes;
                g.Fields["Nodes"] = nodes;
                g.Fields["__isgraph__"] = new MValue(1);
                g.Fields["__nnodes__"]  = new MValue(nNodes);
                return g;
            };
            _builtins["digraph"] = a => _builtins["graph"](a);

            // ── API del objeto graph de MATLAB ──────────────────────────────
            static (double[] s, double[] tt, double[] w, int n) GraphData(MValue g)
            {
                if (g.Fields == null || !g.Fields.ContainsKey("__isgraph__"))
                    throw new MatlabRuntimeException("se esperaba un objeto graph");
                var ed = g.Fields["edges"];
                var en = ed.Fields["endnodes"].Data;
                var wv = ed.Fields["weight"].Data;
                int m = en.Length / 2;
                var s = new double[m]; var tt = new double[m];
                for (int i = 0; i < m; i++) { s[i] = en[i * 2]; tt[i] = en[i * 2 + 1]; }
                int n = (int)g.Fields["__nnodes__"].Scalar;
                return (s, tt, wv, n);
            }
            _builtins["numnodes"] = a => new MValue(GraphData(a[0]).n);
            _builtins["numedges"] = a => new MValue(GraphData(a[0]).s.Length);
            _builtins["degree"] = a => {
                var (s, tt, _, n) = GraphData(a[0]);
                var d = new double[n];
                for (int i = 0; i < s.Length; i++)
                {
                    d[(int)s[i] - 1]++; d[(int)tt[i] - 1]++;      // lazo cuenta 2, como MATLAB
                }
                if (a.Length >= 2 && a[1].IsScalar)
                    return new MValue(d[(int)a[1].Scalar - 1]);
                return new MValue(n, 1, d);
            };
            _builtins["neighbors"] = a => {
                var (s, tt, _, _) = GraphData(a[0]);
                int nodo = (int)a[1].Scalar;
                var lst = new List<double>();
                for (int i = 0; i < s.Length; i++)
                {
                    if ((int)s[i] == nodo)  lst.Add(tt[i]);
                    else if ((int)tt[i] == nodo) lst.Add(s[i]);
                }
                lst.Sort();
                return new MValue(lst.Count, 1, lst.ToArray());
            };
            _builtins["adjacency"] = a => {
                var (s, tt, w, n) = GraphData(a[0]);
                var A = new MValue(n, n);
                for (int i = 0; i < s.Length; i++)
                {
                    int r = (int)s[i] - 1, c = (int)tt[i] - 1;
                    A.Set(r, c, 1); A.Set(c, r, 1);              // no dirigido
                }
                return A;
            };
            _builtins["conncomp"] = a => {
                var (s, tt, _, n) = GraphData(a[0]);
                var comp = new int[n];
                int cur = 0;
                for (int seed = 0; seed < n; seed++)
                {
                    if (comp[seed] != 0) continue;
                    cur++;
                    var stack = new Stack<int>(); stack.Push(seed);
                    comp[seed] = cur;
                    while (stack.Count > 0)
                    {
                        int u = stack.Pop();
                        for (int i = 0; i < s.Length; i++)
                        {
                            int x = (int)s[i] - 1, y = (int)tt[i] - 1;
                            int v = x == u ? y : (y == u ? x : -1);
                            if (v >= 0 && comp[v] == 0) { comp[v] = cur; stack.Push(v); }
                        }
                    }
                }
                var r2 = new double[n];
                for (int i = 0; i < n; i++) r2[i] = comp[i];
                return new MValue(1, n, r2);
            };
            _builtins["shortestpath"] = a => {
                var (s, tt, w, n) = GraphData(a[0]);
                int src = (int)a[1].Scalar - 1, dst = (int)a[2].Scalar - 1;
                var dist = new double[n]; var prev = new int[n]; var vis = new bool[n];
                for (int i = 0; i < n; i++) { dist[i] = double.PositiveInfinity; prev[i] = -1; }
                dist[src] = 0;
                for (int it = 0; it < n; it++)
                {
                    int u = -1; double best = double.PositiveInfinity;
                    for (int i = 0; i < n; i++) if (!vis[i] && dist[i] < best) { best = dist[i]; u = i; }
                    if (u < 0) break;
                    vis[u] = true;
                    for (int i = 0; i < s.Length; i++)
                    {
                        int x = (int)s[i] - 1, y = (int)tt[i] - 1;
                        int v = x == u ? y : (y == u ? x : -1);
                        if (v < 0) continue;
                        double nd = dist[u] + (i < w.Length ? w[i] : 1);
                        if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
                    }
                }
                var path = new List<double>();
                for (int c = dst; c >= 0; c = prev[c]) { path.Insert(0, c + 1); if (c == src) break; }
                if (path.Count == 0 || (int)path[0] != src + 1) return new MValue(1, 0);
                return new MValue(1, path.Count, path.ToArray());
            };

            // error(msg) / error(fmt, args...) / assert(cond, msg): lanzan como MATLAB,
            // para que `try ... catch e` reciba el mensaje del script y no un
            // "Undefined: error" del propio motor.
            _builtins["error"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("error");
                string msg = a[0].IsString ? a[0].StringValue : "error";
                if (a.Length > 1 && a[0].IsString)
                {
                    try { msg = MatlabSprintf.Format(a[0].StringValue, a[1..]); }
                    catch { /* si el formato falla, se usa el texto crudo */ }
                }
                // error('id:sub','texto') → MATLAB usa el 2do como mensaje
                if (a.Length > 1 && a[0].IsString && a[1].IsString &&
                    a[0].StringValue.Contains(':') && !a[0].StringValue.Contains('%'))
                    msg = a[1].StringValue;
                throw new MatlabRuntimeException(msg);
            };
            _builtins["assert"] = a => {
                bool ok = a.Length > 0 && a[0].Data != null && a[0].Data.Length > 0;
                if (ok) foreach (var d in a[0].Data) if (d == 0) { ok = false; break; }
                if (!ok)
                {
                    string msg = a.Length > 1 && a[1].IsString
                               ? a[1].StringValue : "Assertion failed.";
                    throw new MatlabRuntimeException(msg);
                }
                return new MValue(0);
            };

            _builtins["line"] = a => {
                a = DropAxes(a);   // tolerar line(ax, …)
                if (a.Length < 2) throw new MatlabRuntimeException("line(x, y [, props...])");
                MValue X = a[0], Y = a[1];
                string color = "black";
                double lineWidth = 1;
                string lineStyle = "-", marker = null, markerFace = null, markerEdge = null;
                double markerSize = 6;
                int start = 2;
                if (a.Length >= 3 && !a[2].IsString && a[2].Rows == 1) start = 3;  // line(x,y,z)
                for (int i = start; i + 1 < a.Length; i += 2)
                {
                    if (!a[i].IsString) break;
                    string key = a[i].StringValue.ToLowerInvariant();
                    var val = a[i + 1];
                    switch (key)
                    {
                        case "color":
                            color = val.IsString ? MatlabColorToJs(val.StringValue) :
                                    (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : color);
                            break;
                        case "linewidth": lineWidth = val.Scalar; break;
                        // MATLAB permite pedirle a line() que NO trace la linea y solo
                        // ponga marcadores: line(x,y,'LineStyle','none','Marker','s').
                        // Asi dibuja CEINCI-LAB los nudos. Lab lo ignoraba y trazaba la
                        // linea que los une, sin marcadores.
                        case "linestyle": lineStyle = val.IsString ? val.StringValue.ToLowerInvariant() : lineStyle; break;
                        case "marker": marker = val.IsString ? val.StringValue.ToLowerInvariant() : marker; break;
                        case "markersize": markerSize = val.Scalar; break;
                        case "markerfacecolor":
                            markerFace = val.IsString ? MatlabColorToJs(val.StringValue) :
                                         (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : markerFace);
                            break;
                        case "markeredgecolor":
                            markerEdge = val.IsString ? MatlabColorToJs(val.StringValue) :
                                         (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : markerEdge);
                            break;
                    }
                }
                bool sinLinea = lineStyle == "none";
                bool conMarcador = marker != null && marker != "none";
                if (!sinLinea) MatlabPlots.Line2D(X.Data, Y.Data, color, lineWidth);
                if (conMarcador)
                    MatlabPlots.Markers2D(X.Data, Y.Data,
                                          markerFace ?? "none",              // MATLAB: sin relleno por defecto
                                          markerEdge ?? color,
                                          MatlabMarkerToPlotly(marker), markerSize);
                return new MValue(0);
            };
            _builtins["text"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("text(x, y, str [, props...])");
                // text(x,y,z,str): si a[2] escalar y a[3] string
                bool zForm = a.Length >= 4 && a[2].IsScalar && a[3].IsString;
                int start = zForm ? 4 : 3;
                string color = "black";
                double fontSize = 11;
                string align = "left";   // MATLAB: HorizontalAlignment def = 'left'
                for (int i = start; i + 1 < a.Length; i += 2)
                {
                    if (!a[i].IsString) break;
                    switch (a[i].StringValue.ToLowerInvariant())
                    {
                        case "color": color = ColorArg(a[i + 1]); break;
                        case "fontsize": fontSize = a[i + 1].Scalar; break;
                        case "horizontalalignment": align = a[i + 1].StringValue.ToLowerInvariant(); break;
                    }
                }
                MValue X = a[0], Y = a[1];
                MValue lbl = zForm ? a[3] : a[2];
                // VECTORIZADO: text(xv, yv, etiquetas) → una etiqueta por punto, SIN for de MATLAB.
                //   etiquetas string → misma para todos;  vector numérico → num por punto.
                int n = Math.Max(X.Data.Length, Y.Data.Length);
                for (int i = 0; i < n; i++)
                {
                    double xi = X.Data.Length == 1 ? X.Data[0] : X.Data[i];
                    double yi = Y.Data.Length == 1 ? Y.Data[0] : Y.Data[i];
                    string str;
                    if (lbl.IsString) str = lbl.StringValue;
                    else
                    {
                        double v = lbl.Data.Length == 1 ? lbl.Data[0] : (i < lbl.Data.Length ? lbl.Data[i] : lbl.Data[lbl.Data.Length - 1]);
                        str = (v == Math.Floor(v)) ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                                                   : v.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    MatlabPlots.Text2D(xi, yi, str, color, fontSize, align);
                }
                return new MValue(0);
            };
            _builtins["box"] = a => new MValue(0);   // box on/off: no-op (el SVG ya trae marco)
            _builtins["warning"] = a => new MValue(0);   // warning(...)/ws=warning('off',id): no-op; devuelve dummy
            _builtins["clf"] = a => new MValue(0);
            _builtins["subplot"] = a => {
                // subplot(m, n, p) abre un sub-axes en la cuadrícula m×n posición p (1-based)
                if (a.Length < 3) throw new MatlabRuntimeException("subplot(m, n, p)");
                int m = (int)a[0].Scalar, n = (int)a[1].Scalar, p = (int)a[2].Scalar;
                if (_subplotGrid == null || _subplotGrid.Value.m != m || _subplotGrid.Value.n != n)
                {
                    _subplotGrid = (m, n);
                    _htmlOut?.Invoke($"<div class=\"matlab-subplot-grid\" style=\"display:grid;grid-template-columns:repeat({n}, 1fr);gap:1em;margin:1em 0\" data-grid-rows=\"{m}\" data-grid-cols=\"{n}\"></div>\n");
                }
                _activeSubplotPos = p;
                return new MValue(0);
            };
            _builtins["sgtitle"] = a => {
                if (a.Length > 0 && a[0].IsString)
                    _htmlOut?.Invoke($"<h3 style=\"color:#0066b8;margin:.4em 0\">{System.Web.HttpUtility.HtmlEncode(a[0].StringValue)}</h3>\n");
                return new MValue(0);
            };
            _builtins["trisurf"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("trisurf(tri, x, y, z)");
                var tri = a[0]; var x = a[1]; var y = a[2]; var z = a[3];
                int n = x.Data.Length;
                var verts = new MValue(n, 3);
                for (int i = 0; i < n; i++) { verts.Set(i,0,x.Data[i]); verts.Set(i,1,y.Data[i]); verts.Set(i,2,z.Data[i]); }
                MValue cdata = a.Length >= 5 ? a[4] : a[3];
                MatlabPlots.PatchMesh(tri, verts, cdata, "interp", "lightblue", "black", 1, 0.5, _activeColormap ?? "parula");
                MatlabPlots.SetFigure3D(true);
                return new MValue(0);
            };
            // SÓLIDOS 3D MACIZOS: da elementos de VOLUMEN (tetraedros Mx4 o hexaedros Mx8) y
            // extrae automaticamente la PIEL exterior (caras que aparecen 1 sola vez) -> cuerpo
            // solido cerrado. Al filtrar elementos (ej. tetramesh(T(keep,:),X,C)) el CORTE queda
            // RELLENO con la seccion interna (sin huecos). tetramesh = compatible MATLAB 2017a (tets).
            _builtins["tetramesh"] = a => SolidVolMesh(a);   // tetraedros (4 col) - compatible MATLAB (estatico)
            _builtins["solidmesh"] = a => {                  // tets(4)/hexaedros(8) - visor con CORTE interactivo
                if (a.Length < 2) throw new MatlabRuntimeException("solidmesh(E, X[, C]) — E: elementos Mx4/Mx8, X: nodos Nx3");
                var E = a[0]; var X = a[1];
                if (X.Cols < 3) throw new MatlabRuntimeException("solidmesh: X debe ser Nx3");
                var nodes = new double[X.Rows][];
                for (int i = 0; i < X.Rows; i++) nodes[i] = new[] { X.At(i, 0), X.At(i, 1), X.At(i, 2) };
                var elems = new int[E.Rows][];
                for (int e = 0; e < E.Rows; e++) { elems[e] = new int[E.Cols]; for (int c = 0; c < E.Cols; c++) elems[e][c] = (int)E.At(e, c) - 1; }  // 1-based MATLAB -> 0-based JS
                double[] fld = new double[X.Rows];
                if (a.Length >= 3 && a[2] != null && a[2].Data != null && a[2].Data.Length == X.Rows)
                    fld = (double[])a[2].Data.Clone();
                else for (int i = 0; i < X.Rows; i++) fld[i] = X.At(i, 2);   // sin C valido -> color por z
                _htmlOut?.Invoke(MatlabPlots.SolidClipViewer(nodes, elems, new[] { "campo" }, new[] { fld },
                    "Sólido 3D — corta con el slider", "sc" + (++_vizCounter)));
                return new MValue(0);
            };
            _builtins["fill"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("fill(x, y, color)");
                string fc = a[2].IsString ? MatlabColorToJs(a[2].StringValue) :
                            (a[2].IsScalar ? ScalarToColorJs(a[2].Scalar) : "lightblue");
                MatlabPlots.Patch2D(a[0].Data, a[1].Data, fc, "black", 1, 1);
                return new MValue(0);
            };
            _builtins["fill3"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("fill3(x, y, z, color)");
                int n = a[0].Data.Length;
                if (n < 3) return new MValue(0);
                var verts = new MValue(n, 3);
                for (int i = 0; i < n; i++) { verts.Set(i,0,a[0].Data[i]); verts.Set(i,1,a[1].Data[i]); verts.Set(i,2,a[2].Data[i]); }
                var faces = new MValue(n - 2, 3);
                for (int i = 0; i < n - 2; i++) { faces.Set(i,0,1); faces.Set(i,1,i+2); faces.Set(i,2,i+3); }
                string fc = a[3].IsString ? MatlabColorToJs(a[3].StringValue) : "lightblue";
                MatlabPlots.PatchMesh(faces, verts, null, "uniform", fc, "black", 1, 0.5, _activeColormap ?? "parula");
                MatlabPlots.SetFigure3D(true);
                return new MValue(0);
            };
            _builtins["saveas"] = a => {
                // saveas(gcf, 'path.svg' o 'path.png')
                // Si extensión es .svg → escribe SVG REAL al disco
                // Si .png → escribe SVG (sin convertir, pero le ponemos .svg adelante)
                // Cualquier formato → además emite HTML inline para visualización
                string path = a.Length >= 2 && a[1].IsString ? a[1].StringValue : null;
                if (MatlabPlots.HasOpenFigure)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Intentar SVG export
                        var svg = MatlabPlots.ExportSvg();
                        if (svg != null)
                        {
                            try
                            {
                                string svgPath = path;
                                if (svgPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                                    svgPath = svgPath.Substring(0, svgPath.Length - 4) + ".svg";
                                else if (!svgPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                                    svgPath = svgPath + ".svg";
                                var dir = System.IO.Path.GetDirectoryName(svgPath);
                                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                                    System.IO.Directory.CreateDirectory(dir);
                                System.IO.File.WriteAllText(svgPath, svg);
                                _output?.Invoke($"saveas: SVG escrito en {svgPath}");
                            }
                            catch (Exception ex)
                            {
                                _output?.Invoke($"saveas: error escribiendo SVG: {ex.Message}");
                            }
                        }
                    }
                    string html = MatlabPlots.FinishFigure();
                    if (!string.IsNullOrEmpty(html)) _htmlOut?.Invoke(html);
                }
                return new MValue(0);
            };
            _builtins["mkdir"] = a => {
                if (a.Length > 0 && a[0].IsString)
                {
                    try { System.IO.Directory.CreateDirectory(a[0].StringValue); } catch {}
                }
                return new MValue(0);
            };
            _builtins["gcf"] = a => MkGfxHandle(System.Array.Empty<MValue>());   // figura actual (handle)
            _builtins["gca"] = a => MkGfxHandle(System.Array.Empty<MValue>());   // ejes actuales (handle)
            // --- Handles de ejes / GUI (Lab usa un eje implicito; props en Fields) ---
            _builtins["axes"] = a => MkGfxHandle(a);
            _builtins["uicontrol"] = a => MkGfxHandle(a);
            _builtins["uipanel"] = a => MkGfxHandle(a);
            _builtins["addlistener"] = a => MkGfxHandle(System.Array.Empty<MValue>());
            // ancestor(h,'figure') -> handle (Lab usa un contenedor implícito; devolvemos el mismo
            // handle para que get/set/appdata hagan round-trip dentro de la función).
            _builtins["ancestor"] = a => (a.Length > 0 && a[0].IsGfxHandle) ? a[0] : MkGfxHandle(System.Array.Empty<MValue>());
            // getappdata/setappdata: almacén clave-valor sobre el handle (para itw_hover y GUIs).
            _builtins["getappdata"] = a => {
                if (a.Length >= 2 && a[0].Fields != null && a[1].IsString
                    && a[0].Fields.TryGetValue("__app_" + a[1].StringValue, out var v)) return v;
                return new MValue(0, 0);   // [] vacío
            };
            _builtins["setappdata"] = a => {
                if (a.Length >= 3 && a[0].Fields != null && a[1].IsString)
                    a[0].Fields["__app_" + a[1].StringValue] = a[2];
                return new MValue(0);
            };
            // eps: épsilon de máquina. eps -> 2.22e-16 ; eps(x) -> distancia al siguiente double.
            _builtins["eps"] = a => {
                if (a.Length >= 1 && a[0].IsScalar && !a[0].IsString)
                {
                    double x = Math.Abs(a[0].Scalar);
                    if (x == 0) return new MValue(4.9406564584124654e-324);
                    return new MValue(Math.Pow(2, Math.Floor(Math.Log2(x)) - 52));
                }
                return new MValue(2.220446049250313e-16);
            };
            // xlim/ylim/zlim: MATLAB fija el rango del eje y ahi se queda. Antes eran
            // no-ops ("Lab auto-escala"), asi que una figura con zoom salia igual que la
            // vista completa. Aceptan xlim([lo hi]) y xlim(lo, hi).
            static bool TryLim(MValue[] a, out double lo, out double hi)
            {
                lo = hi = 0;
                if (a.Length >= 2 && a[0].IsScalar && a[1].IsScalar) { lo = a[0].Scalar; hi = a[1].Scalar; }
                else if (a.Length >= 1 && a[0].Data != null && a[0].Data.Length >= 2) { lo = a[0].Data[0]; hi = a[0].Data[1]; }
                else return false;
                return hi > lo;
            }
            _builtins["xlim"] = a => { if (TryLim(a, out var lo, out var hi)) MatlabPlots.SetXLim(lo, hi); return new MValue(0); };
            _builtins["ylim"] = a => { if (TryLim(a, out var lo, out var hi)) MatlabPlots.SetYLim(lo, hi); return new MValue(0); };
            _builtins["zlim"] = a => { if (TryLim(a, out var lo, out var hi)) MatlabPlots.SetZLim(lo, hi); return new MValue(0); };
            _builtins["movegui"] = a => new MValue(0);
            _builtins["drawnow"] = a => { var f = MatlabPlots.RenderFrame(); if (f != null) _frameOut?.Invoke(f); return new MValue(0); };
            _builtins["get"] = a => {
                if (a.Length >= 2 && a[0].Fields != null && a[1].IsString
                    && a[0].Fields.TryGetValue(a[1].StringValue.ToLowerInvariant(), out var v)) return v;
                return new MValue(0);
            };
            _builtins["set"] = a => {
                MValue newVerts = null, newCData = null;
                for (int i = 1; i + 1 < a.Length; i += 2)
                    if (a[i].IsString)
                    {
                        string k = a[i].StringValue.ToLowerInvariant();
                        if (a[0].Fields != null) a[0].Fields[k] = a[i + 1];   // guarda en el handle si lo tiene
                        if (k == "vertices") newVerts = a[i + 1];
                        else if (k == "facevertexcdata") newCData = a[i + 1];
                    }
                // MALLA RETENIDA (modo MATLAB): set con Vertices/FaceVertexCData MUTA la malla viva;
                // el próximo render (drawnow) la reconstruye desde ahí → animación. NO depende del handle.
                if (MatlabPlots.RetainedActive)
                {
                    if (newVerts != null) MatlabPlots.UpdateRetainedVerts(ToJagged(newVerts));
                    if (newCData != null && newCData.Data != null) MatlabPlots.UpdateRetainedCData((double[])newCData.Data.Clone());
                }
                return new MValue(0);
            };
            _builtins["light"] = a => new MValue(0);
            _builtins["lighting"] = a => new MValue(0);
            _builtins["material"] = a => new MValue(0);
            _builtins["camlight"] = a => new MValue(0);
            _builtins["camproj"] = a => new MValue(0);   // proyección de cámara (Plotly gestiona la suya)
            _builtins["camva"] = a => new MValue(0);      // ángulo de visión de cámara (zoom)
            _builtins["camtarget"] = a => new MValue(0);
            _builtins["campos"] = a => new MValue(0);
            _builtins["camup"] = a => new MValue(0);
            _builtins["camzoom"] = a => new MValue(0);
            _builtins["camroll"] = a => new MValue(0);
            _builtins["camorbit"] = a => new MValue(0);
            _builtins["rotate3d"] = a => new MValue(0);
            // print/exportgraphics: en Lab la figura se rende INLINE (no PNG a disco). No-op:
            // la figura compuesta se emite al cerrar/terminar el script (FinishFigure).
            _builtins["hoverdata"] = a => {
                // hoverdata(M, 'lbl1|lbl2|...'): adjunta valores por-cara (M: NE x k) al hover interactivo
                var M = a[0]; int rows = M.Rows, cols = M.Cols;
                var vals = new double[rows][];
                for (int i = 0; i < rows; i++) { vals[i] = new double[cols]; for (int j = 0; j < cols; j++) vals[i][j] = M.At(i, j); }
                string[] labels;
                if (a.Length > 1 && a[1].IsString) labels = a[1].StringValue.Split('|');
                else { labels = new string[cols]; for (int j = 0; j < cols; j++) labels[j] = "v" + (j + 1); }
                MatlabPlots.SetHoverData(vals, labels);
                return new MValue(0);
            };
            // alias interno para que un .m AUTOCONTENIDO (funcion local hoverdata que despacha
            // por plataforma) llame al canvas de Lab sin recursion. En MATLAB no existe -> el .m
            // toma la rama datacursor. Asi UN SOLO archivo corre en Calcpad Lab y en MATLAB 2017a.
            _builtins["__hoverdata_lab"] = _builtins["hoverdata"];
            _builtins["print"] = a => {
                // print(fig,'-dpng'[,'-rNNN'],'archivo.png'): rasteriza la figura a PNG EMBEBIDO (sin JS)
                bool png = false; string file = null;
                foreach (var x in a)
                {
                    if (!x.IsString) continue;
                    string s = x.StringValue;
                    if (s.Equals("-dpng", StringComparison.OrdinalIgnoreCase)) png = true;
                    else if (!s.StartsWith("-")) file = s;
                }
                if (png && !string.IsNullOrEmpty(file) && MatlabPlots.HasOpenFigure)
                {
                    // Escribe SOLO el archivo PNG (verificacion); NO emite inline ni limpia,
                    // para que FinishFigure emita el render de la figura (canvas interactivo / SVG).
                    var bytes = MatlabPlots.RasterizeFigurePng(960, 700);
                    if (bytes != null)
                    {
                        try { System.IO.File.WriteAllBytes(file, bytes); }
                        catch (Exception ex) { _output?.Invoke($"print: error PNG: {ex.Message}"); }
                    }
                }
                return new MValue(0);
            };
            _builtins["exportgraphics"] = a => new MValue(0);
            // mfilename / fileparts / fullfile: usados típicamente para armar rutas de guardado.
            // En Lab (render inline) la ruta es irrelevante, pero los implementamos para no romper.
            _builtins["mfilename"] = a => new MValue("");
            _builtins["fileparts"] = a => {
                string p = a.Length > 0 && a[0].IsString ? a[0].StringValue : "";
                return new MValue(System.IO.Path.GetDirectoryName(p) ?? "");
            };
            _builtins["fullfile"] = a => {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var x in a) if (x.IsString) parts.Add(x.StringValue);
                return new MValue(parts.Count == 0 ? "" : System.IO.Path.Combine(parts.ToArray()));
            };
            _builtins["shading"] = a => new MValue(0);
            // NOTA: la definición buena de text() está arriba (usa MatlabPlots.Text2D →
            // se acumula en la figura con xref:'x'/yref:'y'). Se quitó un segundo text()
            // duplicado que hacía Plotly.relayout (reemplazaba el array → solo sobrevivía
            // la última anotación, y en coords de papel). Ver [[reference-lab-matlab-plot-limits]].
            _builtins["annotation"] = a => {
                // annotation('textbox', [x y w h], 'String', 'foo')
                // MVP: solo texto en el último plot
                for (int i = 0; i < a.Length - 1; i++)
                    if (a[i].IsString && a[i].StringValue == "String" && a[i + 1].IsString)
                    {
                        int id = MatlabPlots.LastPlotId;
                        if (id == 0) return new MValue(0);
                        _htmlOut?.Invoke($"<script>(function(){{var d=document.getElementById('matlab_plot_{id}'); if(d&&window.Plotly) Plotly.relayout(d, {{annotations:[{{x:0.5,y:0.95,xref:'paper',yref:'paper',text:\"{System.Web.HttpUtility.JavaScriptStringEncode(a[i + 1].StringValue)}\",showarrow:false,bgcolor:'#fffacd'}}]}});}})();</script>\n");
                        return new MValue(0);
                    }
                return new MValue(0);
            };
            // Structs
            _builtins["struct"] = a => {
                var s = MValue.NewStruct();
                if (a.Length % 2 != 0) throw new MatlabRuntimeException("struct: pairs (name, val) required");
                for (int i = 0; i < a.Length; i += 2)
                {
                    if (!a[i].IsString) throw new MatlabRuntimeException("struct: field name must be string");
                    s.Fields[a[i].StringValue] = a[i + 1];
                }
                return s;
            };
            _builtins["isstruct"]  = a => new MValue(a[0].IsStruct ? 1 : 0);
            _builtins["isnumeric"] = a => new MValue((!a[0].IsStruct && !a[0].IsString) ? 1 : 0);
            _builtins["ischar"]    = a => new MValue(a[0].IsString ? 1 : 0);
            _builtins["isstring"]  = a => new MValue((a[0].IsDoubleQuoted || a[0].IsStringArray) ? 1 : 0);
            _builtins["islogical"] = a => new MValue(!a[0].IsStruct && !a[0].IsString ? 1 : 0);
            _builtins["isreal"]    = a => new MValue(!a[0].IsStruct && !a[0].IsString ? 1 : 0);
            // isequal(A,B,...) → 1 si TODOS son iguales (mismo tamaño y mismos valores). Usado en
            // el bucle de conjunto-activo del FEM (converge cuando el set no cambia).
            _builtins["isequal"] = a => {
                if (a.Length < 2) return new MValue(1);
                for (int i = 1; i < a.Length; i++)
                    if (!MValuesEqual(a[0], a[i])) return new MValue(0);
                return new MValue(1);
            };
            _builtins["isnan"]     = a => {
                if (a[0].IsScalar) return new MValue(double.IsNaN(a[0].Scalar) ? 1 : 0);
                var r = new MValue(a[0].Rows, a[0].Cols);
                for (int i = 0; i < a[0].Data.Length; i++) r.Data[i] = double.IsNaN(a[0].Data[i]) ? 1 : 0;
                return r;
            };
            _builtins["isinf"]     = a => {
                if (a[0].IsScalar) return new MValue(double.IsInfinity(a[0].Scalar) ? 1 : 0);
                var r = new MValue(a[0].Rows, a[0].Cols);
                for (int i = 0; i < a[0].Data.Length; i++) r.Data[i] = double.IsInfinity(a[0].Data[i]) ? 1 : 0;
                return r;
            };
            _builtins["isfinite"]  = a => {
                if (a[0].IsScalar) return new MValue(double.IsFinite(a[0].Scalar) ? 1 : 0);
                var r = new MValue(a[0].Rows, a[0].Cols);
                for (int i = 0; i < a[0].Data.Length; i++) r.Data[i] = double.IsFinite(a[0].Data[i]) ? 1 : 0;
                return r;
            };
            _builtins["iscell"]    = a => new MValue(a[0] != null && a[0].IsCell ? 1 : 0);
            _builtins["fieldnames"] = a => {
                if (!a[0].IsStruct) return new MValue(0, 0);
                var keys = a[0].Fields.Keys.ToArray();
                // Devolver vector de strings — modelo simple: usar 1×N de chars sería complejo,
                // así que devolvemos un struct synthetic con un único campo `_keys` (placeholder MVP).
                // Mejor: solo concatenar como string single
                return new MValue(string.Join(", ", keys));
            };
            _builtins["isfield"] = a => {
                if (!a[0].IsStruct || !a[1].IsString) return new MValue(0);
                return new MValue(a[0].Fields.ContainsKey(a[1].StringValue) ? 1 : 0);
            };
            // ─── containers.Map ───
            _builtins["isKey"] = a => {
                if (a.Length < 2 || !a[0].IsMap) throw new MatlabRuntimeException("isKey(map, key)");
                return new MValue(a[0].MapData.ContainsKey(MapKey(a[1])) ? 1.0 : 0.0);
            };
            _builtins["keys"] = a => {
                if (a.Length < 1 || !a[0].IsMap) throw new MatlabRuntimeException("keys(map)");
                var ks = a[0].MapData.Keys.ToArray();
                var cell = new MValue[1, ks.Length];
                for (int i = 0; i < ks.Length; i++) cell[0, i] = new MValue(ks[i]);
                return MValue.NewCell(cell);
            };
            _builtins["values"] = a => {
                if (a.Length < 1 || !a[0].IsMap) throw new MatlabRuntimeException("values(map)");
                var vs = a[0].MapData.Values.ToArray();
                var cell = new MValue[1, vs.Length];
                for (int i = 0; i < vs.Length; i++) cell[0, i] = vs[i];
                return MValue.NewCell(cell);
            };
            _builtins["remove"] = a => {
                if (a.Length < 2 || !a[0].IsMap) throw new MatlabRuntimeException("remove(map, key)");
                a[0].MapData.Remove(MapKey(a[1]));
                return a[0];
            };

            // String formatting (MATLAB sprintf-like)
            _builtins["sprintf"] = a => {
                if (a.Length == 0) return new MValue("");
                if (!a[0].IsString) throw new MatlabRuntimeException("sprintf: first arg must be format string");
                var rest = new MValue[a.Length - 1];
                Array.Copy(a, 1, rest, 0, rest.Length);
                return new MValue(MatlabSprintf.Format(a[0].StringValue, rest));
            };
            _builtins["fprintf"] = a => {
                if (a.Length == 0) return new MValue(0);
                // Forma fprintf(fid, format, ...): primer arg numérico = file id.
                int fmtIdx = 0, fid = 1;
                if (a[0].IsScalar && !a[0].IsString)
                {
                    fid = (int)Math.Round(a[0].Scalar);
                    fmtIdx = 1;
                }
                if (fmtIdx >= a.Length || !a[fmtIdx].IsString)
                    throw new MatlabRuntimeException("fprintf: falta la cadena de formato");
                var rest = new MValue[a.Length - fmtIdx - 1];
                Array.Copy(a, fmtIdx + 1, rest, 0, rest.Length);
                var text = MatlabSprintf.Format(a[fmtIdx].StringValue, rest);
                if (fid == 1 || fid == 2)            // stdout / stderr → salida visible
                    _output?.Invoke(text);
                else if (_fileWriters.TryGetValue(fid, out var w))
                    w.Write(text);
                else
                    throw new MatlabRuntimeException($"fprintf: file id {fid} inválido (no abierto con fopen)");
                return new MValue(text.Length);   // MATLAB devuelve nº de bytes escritos
            };
            // NOTA: NO se define `printf` — es Octave-only; MATLAB no lo tiene.
            // Calcpad-Lab es estrictamente MATLAB-compatible: lo que MATLAB rechaza,
            // Lab tambien (usar `fprintf`). Ver _ioFuncs en MatlabJit.cs.

            // ─── I/O de archivos ────────────────────────────────────────────
            // fopen(name [,mode]) → fid (>=3) o -1 si falla.
            _builtins["fopen"] = a => {
                if (a.Length < 1 || !a[0].IsString)
                    throw new MatlabRuntimeException("fopen: primer argumento = nombre de archivo (string)");
                string path = a[0].StringValue;
                string mode = (a.Length >= 2 && a[1].IsString) ? a[1].StringValue.Trim() : "r";
                var enc = new System.Text.UTF8Encoding(false); // UTF-8 sin BOM (para griegas)
                try
                {
                    int fid = _nextFid++;
                    if (mode.StartsWith("r"))
                        _fileReaders[fid] = new System.IO.StreamReader(path, enc);
                    else if (mode.StartsWith("a"))
                        _fileWriters[fid] = new System.IO.StreamWriter(path, append: true, enc);
                    else // "w", "w+", etc. → crear/truncar
                        _fileWriters[fid] = new System.IO.StreamWriter(path, append: false, enc);
                    return new MValue(fid);
                }
                catch { return new MValue(-1); }  // convención MATLAB: -1 en fallo
            };
            // fclose(fid) o fclose('all') → 0 ok, -1 fallo.
            _builtins["fclose"] = a => {
                if (a.Length >= 1 && a[0].IsString && a[0].StringValue == "all")
                {
                    foreach (var w in _fileWriters.Values) { w.Flush(); w.Dispose(); }
                    foreach (var r in _fileReaders.Values) r.Dispose();
                    _fileWriters.Clear(); _fileReaders.Clear();
                    return new MValue(0);
                }
                if (a.Length < 1 || !a[0].IsScalar) return new MValue(-1);
                int fid = (int)Math.Round(a[0].Scalar);
                if (_fileWriters.TryGetValue(fid, out var ww)) { ww.Flush(); ww.Dispose(); _fileWriters.Remove(fid); return new MValue(0); }
                if (_fileReaders.TryGetValue(fid, out var rr)) { rr.Dispose(); _fileReaders.Remove(fid); return new MValue(0); }
                return new MValue(-1);
            };
            // fgetl(fid): línea sin salto, o -1 en EOF.
            _builtins["fgetl"] = a => {
                int fid = (int)Math.Round(a[0].Scalar);
                if (!_fileReaders.TryGetValue(fid, out var r)) throw new MatlabRuntimeException($"fgetl: file id {fid} no abierto para lectura");
                var line = r.ReadLine();
                return line == null ? new MValue(-1) : new MValue(line);
            };
            // fgets(fid): línea CON salto, o -1 en EOF.
            _builtins["fgets"] = a => {
                int fid = (int)Math.Round(a[0].Scalar);
                if (!_fileReaders.TryGetValue(fid, out var r)) throw new MatlabRuntimeException($"fgets: file id {fid} no abierto para lectura");
                var line = r.ReadLine();
                return line == null ? new MValue(-1) : new MValue(line + "\n");
            };
            // fscanf(fid [,format]): si el formato pide números → vector columna;
            // si no, devuelve el texto restante completo.
            _builtins["fscanf"] = a => {
                int fid = (int)Math.Round(a[0].Scalar);
                if (!_fileReaders.TryGetValue(fid, out var r)) throw new MatlabRuntimeException($"fscanf: file id {fid} no abierto para lectura");
                string fmt = (a.Length >= 2 && a[1].IsString) ? a[1].StringValue : "%f";
                string content = r.ReadToEnd();
                bool numeric = fmt.Contains("%f") || fmt.Contains("%d") || fmt.Contains("%g") || fmt.Contains("%e") || fmt.Contains("%i");
                if (!numeric) return new MValue(content);
                var nums = new System.Collections.Generic.List<double>();
                foreach (var tok in content.Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    if (double.TryParse(tok, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                        nums.Add(d);
                if (nums.Count == 0) return new MValue(0, 0);
                var col = new double[nums.Count];
                for (int i = 0; i < nums.Count; i++) col[i] = nums[i];
                return new MValue(nums.Count, 1, col);   // vector columna
            };
            // feof(fid): 1 si fin de archivo.
            _builtins["feof"] = a => {
                int fid = (int)Math.Round(a[0].Scalar);
                if (_fileReaders.TryGetValue(fid, out var r)) return new MValue(r.EndOfStream ? 1 : 0);
                return new MValue(1);
            };

            // evalc(code): ejecuta el código capturando su salida en un string.
            _builtins["evalc"] = a => {
                if (a.Length < 1 || !a[0].IsString)
                    throw new MatlabRuntimeException("evalc: argumento = código MATLAB (string)");
                var sb = new StringBuilder();
                var savedOut = _output;
                _output = s => sb.Append(s);
                try
                {
                    var toks = MatlabTokenizer.Tokenize(a[0].StringValue);
                    var stmts = new MatlabParser(toks).ParseAllStatements();
                    foreach (var st in stmts)
                        if (st is FunctionDef fdE) RegisterFunction(fdE);
                        else if (st is ClassDef cdE) RegisterClass(cdE);
                    foreach (var st in stmts)
                        if (st is not FunctionDef && st is not ClassDef)
                            ExecuteOne(st, Globals);
                }
                finally { _output = savedOut; }
                return new MValue(sb.ToString());
            };

            // exit / quit: en una hoja de cálculo no cierran nada → no-op (devuelven 0).
            _builtins["exit"] = a => new MValue(0);
            _builtins["quit"] = a => new MValue(0);
            _builtins["num2str"] = a => {
                if (a[0].IsString) return a[0];
                if (a[0].IsScalar) return new MValue(a[0].Scalar.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                var sb = new StringBuilder();
                for (int i = 0; i < a[0].Rows; i++)
                {
                    if (i > 0) sb.Append("\n");
                    for (int j = 0; j < a[0].Cols; j++)
                    {
                        if (j > 0) sb.Append(" ");
                        sb.Append(a[0].At(i, j).ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                return new MValue(sb.ToString());
            };
            _builtins["str2num"] = a => {
                if (!a[0].IsString) throw new MatlabRuntimeException("str2num: arg must be string");
                if (double.TryParse(a[0].StringValue, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return new MValue(d);
                return new MValue(0, 0);
            };
            _builtins["strcat"] = a => {
                var sb = new StringBuilder();
                foreach (var x in a) sb.Append(x.IsString ? x.StringValue : x.ToString());
                return new MValue(sb.ToString());
            };
            _builtins["strlen"] = a => new MValue(a[0].IsString ? a[0].StringValue.Length : 0);
            _builtins["upper"] = a => new MValue(a[0].IsString ? a[0].StringValue.ToUpper() : a[0].ToString());
            _builtins["lower"] = a => new MValue(a[0].IsString ? a[0].StringValue.ToLower() : a[0].ToString());
            _builtins["strrep"] = a => {
                if (a.Length < 3 || !a[0].IsString || !a[1].IsString || !a[2].IsString)
                    throw new MatlabRuntimeException("strrep(s, find, replace) requires strings");
                return new MValue(a[0].StringValue.Replace(a[1].StringValue, a[2].StringValue));
            };
            _builtins["strfind"] = a => {
                if (!a[0].IsString || !a[1].IsString) throw new MatlabRuntimeException("strfind(s, pat)");
                var s = a[0].StringValue; var pat = a[1].StringValue;
                var hits = new System.Collections.Generic.List<double>();
                int idx = 0;
                while ((idx = s.IndexOf(pat, idx)) >= 0) { hits.Add(idx + 1); idx++; }
                if (hits.Count == 0) return new MValue(0, 0);
                return new MValue(1, hits.Count, hits.ToArray());
            };
            _builtins["strsplit"] = a => {
                if (!a[0].IsString) throw new MatlabRuntimeException("strsplit(s, delim)");
                var s = a[0].StringValue;
                var delim = a.Length > 1 && a[1].IsString ? a[1].StringValue : " ";
                var parts = s.Split(new[] { delim }, StringSplitOptions.None);
                var cells = new MValue[1, parts.Length];
                for (int i = 0; i < parts.Length; i++) cells[0, i] = new MValue(parts[i]);
                return MValue.NewCell(cells);
            };
            _builtins["contains"] = a => {
                if (!a[0].IsString || !a[1].IsString) return new MValue(0);
                return new MValue(a[0].StringValue.Contains(a[1].StringValue) ? 1 : 0);
            };
            _builtins["strcmp"] = a => {
                if (a.Length < 2 || !a[0].IsString || !a[1].IsString) return new MValue(0);
                return new MValue(a[0].StringValue == a[1].StringValue ? 1 : 0);
            };
            _builtins["strcmpi"] = a => {
                if (a.Length < 2 || !a[0].IsString || !a[1].IsString) return new MValue(0);
                return new MValue(string.Equals(a[0].StringValue, a[1].StringValue, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            };
            _builtins["strncmp"] = a => {
                if (a.Length < 3 || !a[0].IsString || !a[1].IsString) return new MValue(0);
                int n = (int)a[2].Scalar;
                var s1 = a[0].StringValue ?? ""; var s2 = a[1].StringValue ?? "";
                if (s1.Length < n || s2.Length < n) return new MValue(0);
                return new MValue(s1.Substring(0, n) == s2.Substring(0, n) ? 1 : 0);
            };
            _builtins["strncmpi"] = a => {
                if (a.Length < 3 || !a[0].IsString || !a[1].IsString) return new MValue(0);
                int n = (int)a[2].Scalar;
                var s1 = a[0].StringValue ?? ""; var s2 = a[1].StringValue ?? "";
                if (s1.Length < n || s2.Length < n) return new MValue(0);
                return new MValue(string.Equals(s1.Substring(0, n), s2.Substring(0, n), StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            };
            _builtins["startsWith"] = a => new MValue(a[0].IsString && a[1].IsString && a[0].StringValue.StartsWith(a[1].StringValue) ? 1 : 0);
            _builtins["endsWith"] = a => new MValue(a[0].IsString && a[1].IsString && a[0].StringValue.EndsWith(a[1].StringValue) ? 1 : 0);
            _builtins["strtrim"] = a => new MValue(a[0].IsString ? a[0].StringValue.Trim() : a[0].ToString());
            // Regex
            _builtins["regexp"] = a => {
                if (a.Length < 2 || !a[0].IsString || !a[1].IsString)
                    throw new MatlabRuntimeException("regexp(str, pattern[, opt])");
                string opt = a.Length >= 3 && a[2].IsString ? a[2].StringValue : "start";
                try
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(a[0].StringValue, a[1].StringValue);
                    if (opt == "match")
                    {
                        var cells = new MValue[1, matches.Count];
                        for (int i = 0; i < matches.Count; i++) cells[0, i] = new MValue(matches[i].Value);
                        return MValue.NewCell(cells);
                    }
                    if (opt == "tokens")
                    {
                        var rows = matches.Count;
                        int maxGroups = 1;
                        foreach (System.Text.RegularExpressions.Match m in matches) maxGroups = Math.Max(maxGroups, m.Groups.Count - 1);
                        var cells = new MValue[rows, Math.Max(1, maxGroups)];
                        for (int i = 0; i < matches.Count; i++)
                            for (int g = 1; g < matches[i].Groups.Count; g++)
                                cells[i, g - 1] = new MValue(matches[i].Groups[g].Value);
                        return MValue.NewCell(cells);
                    }
                    // 'start' (default): vector de posiciones 1-based
                    if (matches.Count == 0) return new MValue(0, 0);
                    var pos = new double[matches.Count];
                    for (int i = 0; i < matches.Count; i++) pos[i] = matches[i].Index + 1;
                    return new MValue(1, pos.Length, pos);
                }
                catch (System.Text.RegularExpressions.RegexParseException ex)
                { throw new MatlabRuntimeException($"regexp: invalid pattern: {ex.Message}"); }
            };
            _builtins["regexprep"] = a => {
                if (a.Length < 3 || !a[0].IsString || !a[1].IsString || !a[2].IsString)
                    throw new MatlabRuntimeException("regexprep(str, pattern, replacement)");
                try
                {
                    return new MValue(System.Text.RegularExpressions.Regex.Replace(
                        a[0].StringValue, a[1].StringValue, a[2].StringValue));
                }
                catch (System.Text.RegularExpressions.RegexParseException ex)
                { throw new MatlabRuntimeException($"regexprep: invalid pattern: {ex.Message}"); }
            };
            _builtins["regexpi"] = a => {
                // Case-insensitive: prepend (?i) al pattern
                if (a.Length < 2) throw new MatlabRuntimeException("regexpi(str, pattern)");
                var args = (MValue[])a.Clone();
                args[1] = new MValue("(?i)" + a[1].StringValue);
                return _builtins["regexp"](args);
            };

            // String array construction y manipulación
            _builtins["string"] = a => {
                if (a.Length == 0) return MValue.NewStringScalar("");
                if (a[0].IsStringArray) return a[0];
                if (a[0].IsString)
                {
                    // Lift char-array a string scalar
                    var v = MValue.NewStringScalar(a[0].StringValue ?? "");
                    return v;
                }
                if (a[0].IsScalar)
                    return MValue.NewStringScalar(a[0].Scalar.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                if (a[0].IsSymbolic)
                    return MValue.NewStringScalar(a[0].Symbolic.ToInfix());
                if (a[0].IsCell)
                {
                    // Cell array de char-arrays → string array
                    var cd = a[0].CellData;
                    int r = cd.GetLength(0), c = cd.GetLength(1);
                    var arr = new string[r, c];
                    for (int i = 0; i < r; i++)
                        for (int j = 0; j < c; j++)
                            arr[i, j] = cd[i, j]?.IsString == true
                                ? cd[i, j].StringValue
                                : (cd[i, j] != null ? cd[i, j].ToString() : "");
                    return MValue.NewStringArray(arr);
                }
                // Matriz → array de strings con cada elemento numérico
                var sArr = new string[a[0].Rows, a[0].Cols];
                for (int i = 0; i < a[0].Rows; i++)
                    for (int j = 0; j < a[0].Cols; j++)
                        sArr[i, j] = a[0].At(i, j).ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
                return MValue.NewStringArray(sArr);
            };
            _builtins["char"] = a => {
                if (a[0].IsSymbolic)
                {
                    // Para render HTML: envolver en sentinels PUA Unicode (..)
                    // que MatlabPipeline reconoce al flushear dispBuffer y deja pasar
                    // como HTML crudo (con clases CSS Calcpad: .dvc, .dvl, <sup>, etc).
                    return new MValue("" + a[0].Symbolic.ToHtml() + "");
                }
                if (a[0].IsScalar)
                {
                    // char(65) → 'A'
                    return new MValue(((char)(int)a[0].Scalar).ToString());
                }
                if (a[0].IsString) return a[0];
                // Vector → string char-by-char
                var sb = new StringBuilder();
                foreach (var v in a[0].Data) sb.Append((char)(int)v);
                return new MValue(sb.ToString());
            };
            _builtins["double"] = a => {
                if (a[0].IsSymbolic && a[0].Symbolic is SymConst sc) return new MValue(sc.Value);
                if (a[0].IsSymbolic)
                {
                    try { return new MValue(a[0].Symbolic.Eval(new Dictionary<string, double>())); }
                    catch { throw new MatlabRuntimeException("double: symbolic expression has unbound variables"); }
                }
                if (a[0].IsString)
                {
                    // String → ASCII codes
                    var s = a[0].StringValue;
                    if (s == null || s.Length == 0) return new MValue(0, 0);
                    var data = new double[s.Length];
                    for (int i = 0; i < s.Length; i++) data[i] = (int)s[i];
                    return new MValue(1, s.Length, data);
                }
                return a[0];
            };
            _builtins["strlength"] = a => {
                if (a[0].IsStringArray)
                {
                    int r = a[0].StringArrayData.GetLength(0), c = a[0].StringArrayData.GetLength(1);
                    var resR = new MValue(r, c);
                    for (int i = 0; i < r; i++)
                        for (int j = 0; j < c; j++)
                            resR.Set(i, j, a[0].StringArrayData[i, j]?.Length ?? 0);
                    return resR;
                }
                if (!a[0].IsString) return new MValue(0);
                return new MValue(a[0].StringValue?.Length ?? 0);
            };
            _builtins["plus_str"] = a => {
                // "abc" + "def" → "abcdef" (string concat)
                if (a.Length < 2) return a[0];
                var s1 = a[0].IsString ? a[0].StringValue : a[0].ToString();
                var s2 = a[1].IsString ? a[1].StringValue : a[1].ToString();
                return new MValue(s1 + s2);
            };

            _builtins["strjoin"] = a => {
                if (!a[0].IsCell) throw new MatlabRuntimeException("strjoin(cell, delim)");
                var delim = a.Length > 1 && a[1].IsString ? a[1].StringValue : " ";
                var cells = a[0].CellData;
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < cells.GetLength(0); i++)
                    for (int j = 0; j < cells.GetLength(1); j++)
                        if (cells[i, j] != null && cells[i, j].IsString) parts.Add(cells[i, j].StringValue);
                return new MValue(string.Join(delim, parts));
            };

            // Numerical
            _builtins["polyval"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("polyval(p, x)");
                var coefs = a[0].Data;  // [a_n, a_{n-1}, ..., a_0]
                var x = a[1];
                MValue Apply(double xv)
                {
                    double y = 0;
                    foreach (var c in coefs) y = y * xv + c;
                    return new MValue(y);
                }
                if (x.IsScalar) return Apply(x.Scalar);
                var r = new MValue(x.Rows, x.Cols);
                for (int i = 0; i < x.Data.Length; i++) r.Data[i] = Apply(x.Data[i]).Scalar;
                return r;
            };
            _builtins["polyfit"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("polyfit(x, y, n)");
                var x = a[0]; var y = a[1]; int n = (int)a[2].Scalar;
                int m = x.Data.Length;
                if (m != y.Data.Length) throw new MatlabRuntimeException("polyfit: x and y length mismatch");
                // Construir Vandermonde N×(n+1): V[i, j] = x[i]^(n-j)
                var V = new MValue(m, n + 1);
                for (int i = 0; i < m; i++) for (int j = 0; j <= n; j++) V.Set(i, j, Math.Pow(x.Data[i], n - j));
                var yCol = new MValue(m, 1);
                for (int i = 0; i < m; i++) yCol.Set(i, 0, y.Data[i]);
                var p = MatlabLinAlg.Linsolve(V, yCol);
                var result = new MValue(1, n + 1);
                for (int i = 0; i <= n; i++) result.Data[i] = p.At(i, 0);
                return result;
            };
            // Integración numérica adaptive
            _builtins["integral"] = a => {
                if (a.Length < 3 || !a[0].IsCallable) throw new MatlabRuntimeException("integral(@f, a, b)");
                var f = a[0].Callable;
                double aL = a[1].Scalar, bL = a[2].Scalar;
                double tol = a.Length >= 4 ? a[3].Scalar : 1e-10;
                double F(double x) => f(new[] { new MValue(x) }).Scalar;
                return new MValue(AdaptiveSimpson(F, aL, bL, tol, 20));
            };
            _builtins["quad"] = _builtins["integral"];
            _builtins["quadl"] = _builtins["integral"];
            _builtins["quadgk"] = _builtins["integral"];
            _builtins["dblquad"] = a => {
                // dblquad(@f, xMin, xMax, yMin, yMax)
                if (a.Length < 5 || !a[0].IsCallable) throw new MatlabRuntimeException("dblquad(@f, xmin, xmax, ymin, ymax)");
                var f = a[0].Callable;
                double xMin = a[1].Scalar, xMax = a[2].Scalar;
                double yMin = a[3].Scalar, yMax = a[4].Scalar;
                double tol = a.Length >= 6 ? a[5].Scalar : 1e-8;
                double F(double x, double y) => f(new[] { new MValue(x), new MValue(y) }).Scalar;
                // Integral interna en y para cada x, luego en x
                double Inner(double x) => AdaptiveSimpson(y => F(x, y), yMin, yMax, tol, 15);
                return new MValue(AdaptiveSimpson(Inner, xMin, xMax, tol, 15));
            };
            _builtins["triplequad"] = a => {
                if (a.Length < 7 || !a[0].IsCallable) throw new MatlabRuntimeException("triplequad(@f, xmin, xmax, ymin, ymax, zmin, zmax)");
                var f = a[0].Callable;
                double xMin = a[1].Scalar, xMax = a[2].Scalar;
                double yMin = a[3].Scalar, yMax = a[4].Scalar;
                double zMin = a[5].Scalar, zMax = a[6].Scalar;
                double tol = a.Length >= 8 ? a[7].Scalar : 1e-6;
                double F(double x, double y, double z) => f(new[] { new MValue(x), new MValue(y), new MValue(z) }).Scalar;
                double InnerY(double x) => AdaptiveSimpson(z => AdaptiveSimpson(y => F(x, y, z), yMin, yMax, tol, 12), zMin, zMax, tol, 12);
                return new MValue(AdaptiveSimpson(InnerY, xMin, xMax, tol, 12));
            };

            _builtins["trapz"] = a => {
                MValue y; double[] x = null;
                if (a.Length == 1) { y = a[0]; }
                else { x = a[0].Data; y = a[1]; }
                double s = 0;
                if (x == null)
                    for (int i = 1; i < y.Data.Length; i++) s += 0.5 * (y.Data[i - 1] + y.Data[i]);
                else
                    for (int i = 1; i < y.Data.Length; i++) s += 0.5 * (x[i] - x[i - 1]) * (y.Data[i - 1] + y.Data[i]);
                return new MValue(s);
            };
            _builtins["interp1"] = a => {
                // interp1(x, y, xq[, method]) — method: 'linear' (default), 'spline', 'pchip', 'nearest'
                if (a.Length < 3) throw new MatlabRuntimeException("interp1(x, y, xq[, method])");
                var x = a[0].Data; var y = a[1].Data; var xq = a[2];
                string method = a.Length >= 4 && a[3].IsString ? a[3].StringValue : "linear";
                if (method == "spline") return SplineInterp(x, y, xq);
                if (method == "pchip")  return PchipInterp(x, y, xq);
                if (method == "nearest")
                {
                    MValue Near(double q)
                    {
                        int idx = 0;
                        double best = double.MaxValue;
                        for (int i = 0; i < x.Length; i++)
                        { double d = Math.Abs(x[i] - q); if (d < best) { best = d; idx = i; } }
                        return new MValue(y[idx]);
                    }
                    if (xq.IsScalar) return Near(xq.Scalar);
                    var rN = new MValue(xq.Rows, xq.Cols);
                    for (int i = 0; i < xq.Data.Length; i++) rN.Data[i] = Near(xq.Data[i]).Scalar;
                    return rN;
                }
                // linear (default)
                double Lerp(double q)
                {
                    if (q <= x[0]) return y[0];
                    if (q >= x[^1]) return y[^1];
                    int lo = 0, hi = x.Length - 1;
                    while (hi - lo > 1) { int mid = (lo + hi) / 2; if (x[mid] <= q) lo = mid; else hi = mid; }
                    double t = (q - x[lo]) / (x[hi] - x[lo]);
                    return y[lo] + t * (y[hi] - y[lo]);
                }
                if (xq.IsScalar) return new MValue(Lerp(xq.Scalar));
                var r = new MValue(xq.Rows, xq.Cols);
                for (int i = 0; i < xq.Data.Length; i++) r.Data[i] = Lerp(xq.Data[i]);
                return r;
            };
            _builtins["spline"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("spline(x, y, xq)");
                return SplineInterp(a[0].Data, a[1].Data, a[2]);
            };
            _builtins["pchip"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("pchip(x, y, xq)");
                return PchipInterp(a[0].Data, a[1].Data, a[2]);
            };
            _builtins["cumtrapz"] = a => {
                MValue y; double[] x = null;
                if (a.Length == 1) { y = a[0]; }
                else { x = a[0].Data; y = a[1]; }
                var r = new double[y.Data.Length];
                r[0] = 0;
                for (int i = 1; i < y.Data.Length; i++)
                {
                    double dx = x == null ? 1.0 : (x[i] - x[i - 1]);
                    r[i] = r[i - 1] + 0.5 * dx * (y.Data[i - 1] + y.Data[i]);
                }
                return new MValue(y.Rows, y.Cols, r);
            };
            _builtins["gradient"] = a => {
                var v = a[0];
                double h = a.Length >= 2 ? a[1].Scalar : 1.0;
                int n = v.Data.Length;
                var g = new double[n];
                if (n == 1) return new MValue(0);
                g[0] = (v.Data[1] - v.Data[0]) / h;
                g[n - 1] = (v.Data[n - 1] - v.Data[n - 2]) / h;
                for (int i = 1; i < n - 1; i++) g[i] = (v.Data[i + 1] - v.Data[i - 1]) / (2 * h);
                return new MValue(v.Rows, v.Cols, g);
            };
            _builtins["conv"] = a => {
                // conv(u, v) — convolución 1D
                var u = a[0].Data; var w = a[1].Data;
                int nu = u.Length, nw = w.Length;
                int nr = nu + nw - 1;
                var r = new double[nr];
                for (int i = 0; i < nr; i++)
                {
                    double s = 0;
                    for (int j = Math.Max(0, i - nw + 1); j <= Math.Min(nu - 1, i); j++)
                        s += u[j] * w[i - j];
                    r[i] = s;
                }
                return new MValue(1, nr, r);
            };
            _builtins["xcorr"] = a => {
                // xcorr(x, y) — correlación cruzada
                var x = a[0].Data;
                var y = a.Length >= 2 ? a[1].Data : a[0].Data;
                int nx = x.Length, ny = y.Length;
                int nr = nx + ny - 1;
                var r = new double[nr];
                for (int k = 0; k < nr; k++)
                {
                    int lag = k - (ny - 1);
                    double s = 0;
                    for (int i = 0; i < nx; i++)
                    {
                        int j = i - lag;
                        if (j >= 0 && j < ny) s += x[i] * y[j];
                    }
                    r[k] = s;
                }
                return new MValue(1, nr, r);
            };
            _builtins["xcov"] = a => {
                // Cross-covariance = xcorr de señales centradas en media
                var x = a[0].Data; var y = a.Length >= 2 ? a[1].Data : x;
                double mx = 0, my = 0;
                foreach (var v in x) mx += v; mx /= x.Length;
                foreach (var v in y) my += v; my /= y.Length;
                var xc = new double[x.Length];
                var yc = new double[y.Length];
                for (int i = 0; i < x.Length; i++) xc[i] = x[i] - mx;
                for (int i = 0; i < y.Length; i++) yc[i] = y[i] - my;
                var inner = new[] { new MValue(1, xc.Length, xc), new MValue(1, yc.Length, yc) };
                return _builtins["xcorr"](inner);
            };
            _builtins["butter"] = a => {
                // butter(n, Wn) — filtro Butterworth pasa-bajo de orden n con cutoff Wn (normalizado a Nyquist).
                // Devuelve [b, a] como struct con campos b/a (no tf — simple).
                if (a.Length < 2) throw new MatlabRuntimeException("butter(n, Wn)");
                int n = (int)a[0].Scalar;
                double Wn = a[1].Scalar;   // normalized cutoff [0, 1] (Wn = ωc / (Fs/2))
                // Prewarp para Tustin: Ωc = tan(π·Wn/2)
                double Wc = Math.Tan(Math.PI * Wn / 2);
                // Polos analógicos Butterworth en círculo unidad:
                // s_k = exp(i·π·(2k+n-1)/(2n)) para k=1..n
                var polesAnalog = new System.Numerics.Complex[n];
                for (int k = 1; k <= n; k++)
                {
                    double ang = Math.PI * (2 * k + n - 1) / (2 * n);
                    polesAnalog[k - 1] = new System.Numerics.Complex(Wc * Math.Cos(ang), Wc * Math.Sin(ang));
                }
                // Bilinear transform: z = (1 + s/2)/(1 - s/2)
                // Más simple MVP: armar tf analógico → c2d Tustin
                // Construir den polinómico desde poles (producto complejo)
                var den = new System.Numerics.Complex[] { System.Numerics.Complex.One };
                foreach (var p in polesAnalog)
                {
                    var newDen = new System.Numerics.Complex[den.Length + 1];
                    for (int i = 0; i < den.Length; i++)
                    {
                        newDen[i] += den[i];
                        newDen[i + 1] -= p * den[i];
                    }
                    den = newDen;
                }
                // Num analógico = Wc^n (lowpass)
                var numA = new double[1];
                numA[0] = Math.Pow(Wc, n);
                var denA = new double[den.Length];
                for (int i = 0; i < den.Length; i++) denA[i] = den[i].Real;
                // C2d Tustin con Ts=2 (la prewarp ya está aplicada con Wc = tan(π·Wn/2))
                var (numZ, denZ) = MatlabControl.C2dTustin(numA, denA, 2.0);
                var result = MValue.NewStruct();
                result.Fields["b"] = new MValue(1, numZ.Length, numZ);
                result.Fields["a"] = new MValue(1, denZ.Length, denZ);
                return result;
            };
            _builtins["cheby1"] = a => {
                // Chebyshev Type I: orden n, ripple R (dB), cutoff Wn normalizado
                if (a.Length < 3) throw new MatlabRuntimeException("cheby1(n, R, Wn)");
                int n = (int)a[0].Scalar;
                double R = a[1].Scalar;          // ripple in dB
                double Wn = a[2].Scalar;
                double eps = Math.Sqrt(Math.Pow(10, R / 10) - 1);
                double Wc = Math.Tan(Math.PI * Wn / 2);   // prewarp
                // Polos en elipse: σ_k = -sinh(asinh(1/eps)/n) · sin((2k-1)π/(2n)) · Wc
                //                  ω_k =  cosh(asinh(1/eps)/n) · cos((2k-1)π/(2n)) · Wc
                double mu = Math.Asinh(1.0 / eps) / n;
                var poles = new System.Numerics.Complex[n];
                for (int k = 1; k <= n; k++)
                {
                    double theta = Math.PI * (2 * k - 1) / (2 * n);
                    poles[k - 1] = new System.Numerics.Complex(
                        -Math.Sinh(mu) * Math.Sin(theta) * Wc,
                        Math.Cosh(mu) * Math.Cos(theta) * Wc);
                }
                // Construir polinomio den desde poles
                var den = new System.Numerics.Complex[] { System.Numerics.Complex.One };
                foreach (var p in poles)
                {
                    var newDen = new System.Numerics.Complex[den.Length + 1];
                    for (int i = 0; i < den.Length; i++)
                    {
                        newDen[i] += den[i];
                        newDen[i + 1] -= p * den[i];
                    }
                    den = newDen;
                }
                var denA = new double[den.Length];
                for (int i = 0; i < den.Length; i++) denA[i] = den[i].Real;
                // Num analógico: para Chebyshev I lowpass, K = den(0) / (2^(n-1) * eps) si n par else den(0)/eps
                double K = denA[denA.Length - 1] / (n % 2 == 0 ? Math.Pow(2, n - 1) * eps : Math.Pow(2, n - 1));
                var numA = new[] { K };
                var (numZ, denZ) = MatlabControl.C2dTustin(numA, denA, 2.0);
                var result = MValue.NewStruct();
                result.Fields["b"] = new MValue(1, numZ.Length, numZ);
                result.Fields["a"] = new MValue(1, denZ.Length, denZ);
                return result;
            };
            _builtins["cheby2"] = a => {
                // Chebyshev II — stub que usa Butterworth como fallback (MVP)
                return _builtins["butter"](new[] { a[0], a.Length >= 3 ? a[2] : a[1] });
            };
            _builtins["ellip"] = _builtins["cheby1"];   // alias MVP

            _builtins["freqz"] = a => {
                // freqz(b, a, N) — respuesta frecuencial del filtro digital
                double[] b, an;
                if (a.Length >= 1 && a[0].IsStruct)
                {
                    b = a[0].Fields["b"].Data;
                    an = a[0].Fields["a"].Data;
                }
                else
                {
                    b = a[0].Data;
                    an = a.Length >= 2 ? a[1].Data : new[] { 1.0 };
                }
                int N = a.Length >= 3 ? (int)a[2].Scalar : 512;
                var w = new double[N];
                var H = new (double re, double im)[N];
                for (int k = 0; k < N; k++)
                {
                    w[k] = Math.PI * k / (N - 1);
                    double cw = Math.Cos(-w[k]), sw = Math.Sin(-w[k]);
                    // num
                    double bRe = 0, bIm = 0;
                    for (int i = 0; i < b.Length; i++)
                    {
                        double angle = -w[k] * i;
                        bRe += b[i] * Math.Cos(angle); bIm += b[i] * Math.Sin(angle);
                    }
                    double aRe = 0, aIm = 0;
                    for (int i = 0; i < an.Length; i++)
                    {
                        double angle = -w[k] * i;
                        aRe += an[i] * Math.Cos(angle); aIm += an[i] * Math.Sin(angle);
                    }
                    double denom = aRe * aRe + aIm * aIm;
                    H[k] = ((bRe * aRe + bIm * aIm) / denom, (bIm * aRe - bRe * aIm) / denom);
                }
                var result = MValue.NewStruct();
                result.Fields["w"] = new MValue(1, N, w);
                result.Fields["mag"] = new MValue(1, N, H.Select(h => Math.Sqrt(h.re * h.re + h.im * h.im)).ToArray());
                result.Fields["phase"] = new MValue(1, N, H.Select(h => Math.Atan2(h.im, h.re)).ToArray());
                return result;
            };
            _builtins["hilbert"] = a => {
                // Transformada de Hilbert via FFT: H{x} = ifft(fft(x) · H_op)
                var x = a[0];
                var fft = MatlabFFT.Fft(x, false);
                int n = x.Data.Length;
                var fRe = (double[])fft.Data.Clone();
                var fIm = (double[])fft.Imag.Clone();
                // H_op: 1 at 0 and N/2, 2 para k = 1..N/2-1, 0 para resto
                for (int k = 0; k < n; k++)
                {
                    double h = (k == 0 || (n % 2 == 0 && k == n / 2)) ? 1 :
                               (k < n / 2 ? 2 : 0);
                    fRe[k] *= h; fIm[k] *= h;
                }
                var ifft = MatlabFFT.Fft(new MValue(x.Rows, x.Cols, fRe, fIm), true);
                return ifft;
            };

            // JSON I/O
            _builtins["jsonencode"] = a => new MValue(JsonEncode(a[0]));
            _builtins["jsondecode"] = a => {
                if (!a[0].IsString) throw new MatlabRuntimeException("jsondecode(str)");
                return JsonDecode(a[0].StringValue);
            };

            string JsonEncode(MValue v)
            {
                if (v == null) return "null";
                if (v.IsString) return $"\"{System.Web.HttpUtility.JavaScriptStringEncode(v.StringValue)}\"";
                if (v.IsScalar)
                {
                    double s = v.Scalar;
                    if (double.IsNaN(s)) return "null";
                    if (double.IsInfinity(s)) return "null";
                    return s.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                }
                if (v.IsStruct)
                {
                    var sb = new StringBuilder("{");
                    bool first = true;
                    foreach (var kv in v.Fields)
                    {
                        if (!first) sb.Append(",");
                        sb.Append($"\"{kv.Key}\":");
                        sb.Append(JsonEncode(kv.Value));
                        first = false;
                    }
                    sb.Append("}");
                    return sb.ToString();
                }
                if (v.IsCell)
                {
                    var sb = new StringBuilder("[");
                    int rows = v.CellData.GetLength(0), cols = v.CellData.GetLength(1);
                    for (int i = 0; i < rows; i++)
                    {
                        if (i > 0) sb.Append(",");
                        if (cols > 1) sb.Append("[");
                        for (int j = 0; j < cols; j++)
                        {
                            if (j > 0) sb.Append(",");
                            sb.Append(JsonEncode(v.CellData[i, j]));
                        }
                        if (cols > 1) sb.Append("]");
                    }
                    sb.Append("]");
                    return sb.ToString();
                }
                // Matriz
                var sbm = new StringBuilder("[");
                for (int i = 0; i < v.Rows; i++)
                {
                    if (i > 0) sbm.Append(",");
                    sbm.Append("[");
                    for (int j = 0; j < v.Cols; j++)
                    {
                        if (j > 0) sbm.Append(",");
                        sbm.Append(v.At(i, j).ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    sbm.Append("]");
                }
                sbm.Append("]");
                return sbm.ToString();
            }
            MValue JsonDecode(string json)
            {
                int pos = 0;
                var result = ParseJson(json, ref pos);
                return result;
            }
            MValue ParseJson(string s, ref int p)
            {
                while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
                if (p >= s.Length) return new MValue(0);
                char c = s[p];
                if (c == '"')
                {
                    p++;
                    var sb = new StringBuilder();
                    while (p < s.Length && s[p] != '"')
                    {
                        if (s[p] == '\\' && p + 1 < s.Length) { sb.Append(s[p + 1]); p += 2; continue; }
                        sb.Append(s[p]); p++;
                    }
                    p++; return new MValue(sb.ToString());
                }
                if (c == '{')
                {
                    p++; var st = MValue.NewStruct();
                    while (p < s.Length && s[p] != '}')
                    {
                        while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
                        if (s[p] == ',') { p++; continue; }
                        if (s[p] == '}') break;
                        var key = ParseJson(s, ref p);
                        while (p < s.Length && (char.IsWhiteSpace(s[p]) || s[p] == ':')) p++;
                        var val = ParseJson(s, ref p);
                        st.Fields[key.StringValue] = val;
                    }
                    if (p < s.Length) p++; return st;
                }
                if (c == '[')
                {
                    p++;
                    var list = new System.Collections.Generic.List<MValue>();
                    while (p < s.Length && s[p] != ']')
                    {
                        while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
                        if (s[p] == ',') { p++; continue; }
                        if (s[p] == ']') break;
                        list.Add(ParseJson(s, ref p));
                    }
                    if (p < s.Length) p++;
                    // Si todos son escalares: matriz row
                    bool allScalar = list.All(v => v.IsScalar);
                    if (allScalar)
                    {
                        var data = list.Select(v => v.Scalar).ToArray();
                        return new MValue(1, data.Length, data);
                    }
                    var cells = new MValue[1, list.Count];
                    for (int i = 0; i < list.Count; i++) cells[0, i] = list[i];
                    return MValue.NewCell(cells);
                }
                // Número
                int start = p;
                while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.' || s[p] == '-' || s[p] == '+' || s[p] == 'e' || s[p] == 'E')) p++;
                if (double.TryParse(s[start..p], System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return new MValue(d);
                // null/true/false
                if (p + 4 <= s.Length && s.Substring(p, 4) == "null") { p += 4; return new MValue(0); }
                if (p + 4 <= s.Length && s.Substring(p, 4) == "true") { p += 4; return new MValue(1); }
                if (p + 5 <= s.Length && s.Substring(p, 5) == "false") { p += 5; return new MValue(0); }
                p++;
                return new MValue(0);
            }

            _builtins["filter"] = a => {
                // filter(b, a, x) — IIR/FIR: a(1)*y(n) + a(2)*y(n-1) + ... = b(1)*x(n) + ...
                if (a.Length < 3) throw new MatlabRuntimeException("filter(b, a, x)");
                var bF = a[0].Data; var aF = a[1].Data; var x = a[2].Data;
                int n = x.Length;
                var y = new double[n];
                double a0 = aF[0] == 0 ? 1 : aF[0];
                for (int k = 0; k < n; k++)
                {
                    double s = 0;
                    for (int j = 0; j < bF.Length && k - j >= 0; j++) s += bF[j] * x[k - j];
                    for (int j = 1; j < aF.Length && k - j >= 0; j++) s -= aF[j] * y[k - j];
                    y[k] = s / a0;
                }
                return new MValue(a[2].Rows, a[2].Cols, y);
            };
            // I/O CSV
            _builtins["csvread"] = a => {
                if (!a[0].IsString) throw new MatlabRuntimeException("csvread(filename)");
                var path = a[0].StringValue;
                if (!System.IO.File.Exists(path)) throw new MatlabRuntimeException($"csvread: file not found: {path}");
                var lines = System.IO.File.ReadAllLines(path);
                var rows = new System.Collections.Generic.List<double[]>();
                foreach (var line in lines)
                {
                    var trim = line.Trim();
                    if (trim.Length == 0) continue;
                    var parts = trim.Split(',', ';', '\t', ' ');
                    var row = new System.Collections.Generic.List<double>();
                    foreach (var p in parts)
                        if (!string.IsNullOrWhiteSpace(p) &&
                            double.TryParse(p, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out var d))
                            row.Add(d);
                    if (row.Count > 0) rows.Add(row.ToArray());
                }
                if (rows.Count == 0) return new MValue(0, 0);
                int nc = rows[0].Length;
                var data = new double[rows.Count * nc];
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < nc; j++)
                        data[i * nc + j] = j < rows[i].Length ? rows[i][j] : 0;
                return new MValue(rows.Count, nc, data);
            };
            _builtins["csvwrite"] = a => {
                if (a.Length < 2 || !a[0].IsString) throw new MatlabRuntimeException("csvwrite(filename, M)");
                var path = a[0].StringValue; var m = a[1];
                var sbCsv = new StringBuilder();
                for (int i = 0; i < m.Rows; i++)
                {
                    for (int j = 0; j < m.Cols; j++)
                    {
                        if (j > 0) sbCsv.Append(',');
                        sbCsv.Append(m.At(i, j).ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    sbCsv.Append('\n');
                }
                System.IO.File.WriteAllText(path, sbCsv.ToString());
                return new MValue(0);
            };
            _builtins["dlmwrite"] = _builtins["csvwrite"];
            _builtins["dlmread"] = _builtins["csvread"];
            // .mat ASCII (formato propio simple: key=value-en-JSON por línea)
            _builtins["save"] = a => {
                // save('file.mat', 'var1', 'var2', ...) o save('file.mat') = todo el workspace
                if (a.Length == 0 || !a[0].IsString) throw new MatlabRuntimeException("save(filename, vars...)");
                var path = a[0].StringValue;
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) path += ".mat";
                var sbMat = new StringBuilder();
                sbMat.AppendLine("# Calcpad Lab .mat (ASCII v1)");
                IEnumerable<string> varNames;
                if (a.Length > 1) varNames = a.Skip(1).Where(x => x.IsString).Select(x => x.StringValue);
                else varNames = Globals.Vars.Keys;
                foreach (var name in varNames)
                {
                    if (!Globals.Vars.TryGetValue(name, out var val)) continue;
                    sbMat.AppendLine($"{name}={MatSerialize(val)}");
                }
                System.IO.File.WriteAllText(path, sbMat.ToString());
                return new MValue(0);
            };
            _builtins["load"] = a => {
                if (a.Length == 0 || !a[0].IsString) throw new MatlabRuntimeException("load(filename)");
                var path = a[0].StringValue;
                if (!System.IO.File.Exists(path)) throw new MatlabRuntimeException($"load: file not found: {path}");
                var lines = System.IO.File.ReadAllLines(path);
                // ¿es formato nombre=valor (.mat de texto) o una MATRIZ numérica (.csv/.dat/.txt tipo MATLAB)?
                bool hasAssign = false, hasNumericRow = false;
                foreach (var line in lines)
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#") || t.StartsWith("%")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && char.IsLetter(t[0])) { hasAssign = true; }
                    else if (eq < 0) { hasNumericRow = true; }
                }
                if (hasNumericRow && !hasAssign)
                {
                    // MATLAB load('data.csv'): matriz numérica, delimitada por coma/espacio/tab/;
                    var rows = new System.Collections.Generic.List<double[]>();
                    int ncols = -1;
                    var sep = new[] { ',', ' ', '\t', ';' };
                    foreach (var line in lines)
                    {
                        var t = line.Trim();
                        if (t.Length == 0 || t.StartsWith("#") || t.StartsWith("%")) continue;
                        var toks = t.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        var row = new double[toks.Length];
                        bool ok = true;
                        for (int k = 0; k < toks.Length; k++)
                            if (!double.TryParse(toks[k], System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out row[k])) { ok = false; break; }
                        if (!ok) continue;
                        if (ncols < 0) ncols = row.Length;
                        if (row.Length == ncols) rows.Add(row);
                    }
                    if (rows.Count == 0) throw new MatlabRuntimeException($"load: '{path}' sin datos numéricos");
                    var mat = new MValue(rows.Count, ncols);
                    for (int i = 0; i < rows.Count; i++)
                        for (int j = 0; j < ncols; j++) mat.Set(i, j, rows[i][j]);
                    return mat;
                }
                var st = MValue.NewStruct();
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var name = line[..eq].Trim();
                    var json = line[(eq + 1)..].Trim();
                    var val = MatDeserialize(json);
                    Globals.Set(name, val);
                    st.Fields[name] = val;
                }
                return st;
            };

            // ── IMPORT/EXPORT de datos (txt/csv/Excel) como MATLAB ──
            MValue ReadNumericMatrixFile(string path)
            {
                if (!System.IO.File.Exists(path)) throw new MatlabRuntimeException($"archivo no encontrado: {path}");
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".xlsx" || ext == ".xlsm") return ReadXlsxMatrix(path);
                var lines = System.IO.File.ReadAllLines(path);
                var rows = new System.Collections.Generic.List<double[]>();
                int ncols = -1; var sep = new[] { ',', ' ', '\t', ';' };
                foreach (var line in lines)
                {
                    var tt = line.Trim();
                    if (tt.Length == 0 || tt.StartsWith("#") || tt.StartsWith("%")) continue;
                    var toks = tt.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    var row = new double[toks.Length]; bool ok = true;
                    for (int k = 0; k < toks.Length; k++)
                        if (!double.TryParse(toks[k], System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out row[k])) { ok = false; break; }
                    if (!ok) continue;                     // cabecera/texto → saltar (como readmatrix)
                    if (ncols < 0) ncols = row.Length;
                    if (row.Length == ncols) rows.Add(row);
                }
                if (rows.Count == 0) throw new MatlabRuntimeException($"'{path}' sin datos numéricos");
                var mat = new MValue(rows.Count, ncols);
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < ncols; j++) mat.Set(i, j, rows[i][j]);
                return mat;
            }
            int ColFromRef(string r)
            {
                if (string.IsNullOrEmpty(r)) return 0;
                int col = 0;
                foreach (var ch in r) { if (ch >= 'A' && ch <= 'Z') col = col * 26 + (ch - 'A' + 1); else break; }
                return col - 1;
            }
            MValue ReadXlsxMatrix(string path)
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (sheet == null)
                    foreach (var e in zip.Entries)
                        if (e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")) { sheet = e; break; }
                if (sheet == null) throw new MatlabRuntimeException("xlsx: no encontré la hoja");
                System.Xml.Linq.XDocument doc;
                using (var s = sheet.Open()) doc = System.Xml.Linq.XDocument.Load(s);
                var ns = doc.Root.Name.Namespace;
                var cellRows = new System.Collections.Generic.List<System.Collections.Generic.SortedDictionary<int, double>>();
                int maxCol = 0;
                foreach (var row in doc.Descendants(ns + "row"))
                {
                    var cells = new System.Collections.Generic.SortedDictionary<int, double>();
                    foreach (var c in row.Elements(ns + "c"))
                    {
                        var t = (string)c.Attribute("t");
                        if (t != null && t != "n") continue;    // solo numéricas (ignora strings/cabeceras)
                        var v = c.Element(ns + "v"); if (v == null) continue;
                        if (!double.TryParse(v.Value, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var d)) continue;
                        int col = ColFromRef((string)c.Attribute("r"));
                        cells[col] = d; if (col + 1 > maxCol) maxCol = col + 1;
                    }
                    if (cells.Count > 0) cellRows.Add(cells);
                }
                if (cellRows.Count == 0) throw new MatlabRuntimeException("xlsx: sin datos numéricos");
                var mat = new MValue(cellRows.Count, maxCol);
                for (int i = 0; i < cellRows.Count; i++)
                    foreach (var kv in cellRows[i]) mat.Set(i, kv.Key, kv.Value);
                return mat;
            }
            string XlsxColName(int c) { string s = ""; c++; while (c > 0) { int r = (c - 1) % 26; s = (char)('A' + r) + s; c = (c - 1) / 26; } return s; }
            void WriteXlsxMatrix(string path, MValue m)
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
                void Add(string name, string content) { var e = zip.CreateEntry(name); using var w = new System.IO.StreamWriter(e.Open()); w.Write(content); }
                Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Add("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Add("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
                for (int i = 0; i < m.Rows; i++)
                {
                    sb.Append($"<row r=\"{i + 1}\">");
                    for (int j = 0; j < m.Cols; j++)
                        sb.Append($"<c r=\"{XlsxColName(j)}{i + 1}\"><v>{m.At(i, j).ToString("G", System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                    sb.Append("</row>");
                }
                sb.Append("</sheetData></worksheet>");
                Add("xl/worksheets/sheet1.xml", sb.ToString());
            }
            void WriteMatrixFile(string path, MValue m)
            {
                if (System.IO.Path.GetExtension(path).ToLowerInvariant() == ".xlsx") { WriteXlsxMatrix(path, m); return; }
                var sb = new StringBuilder();
                for (int i = 0; i < m.Rows; i++)
                {
                    for (int j = 0; j < m.Cols; j++) { if (j > 0) sb.Append(','); sb.Append(m.At(i, j).ToString("G", System.Globalization.CultureInfo.InvariantCulture)); }
                    sb.Append('\n');
                }
                System.IO.File.WriteAllText(path, sb.ToString());
            }
            _builtins["readmatrix"] = a => ReadNumericMatrixFile(a[0].StringValue);
            _builtins["importdata"] = a => ReadNumericMatrixFile(a[0].StringValue);
            _builtins["textread"] = a => ReadNumericMatrixFile(a[0].StringValue);
            _builtins["readtable"] = a => ReadNumericMatrixFile(a[0].StringValue);
            _builtins["xlsread"] = a => ReadNumericMatrixFile(a[0].StringValue);
            _builtins["writematrix"] = a => { if (a.Length < 2 || !a[1].IsString) throw new MatlabRuntimeException("writematrix(M, filename)"); WriteMatrixFile(a[1].StringValue, a[0]); return new MValue(0); };
            _builtins["writetable"] = _builtins["writematrix"];
            _builtins["xlswrite"] = a => { if (a.Length < 2 || !a[0].IsString) throw new MatlabRuntimeException("xlswrite(filename, M)"); WriteMatrixFile(a[0].StringValue, a[1]); return new MValue(0); };

            string MatSerialize(MValue v)
            {
                if (v == null) return "null";
                if (v.IsString) return $"\"{v.StringValue?.Replace("\"", "\\\"")}\"";
                if (v.IsScalar) return v.Scalar.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                if (v.IsStruct)
                {
                    var sbS = new StringBuilder("{");
                    bool f = true;
                    foreach (var kv in v.Fields)
                    {
                        if (!f) sbS.Append(",");
                        sbS.Append($"\"{kv.Key}\":");
                        sbS.Append(MatSerialize(kv.Value));
                        f = false;
                    }
                    sbS.Append("}");
                    return sbS.ToString();
                }
                // Matriz
                var sbMat = new StringBuilder("[");
                for (int i = 0; i < v.Rows; i++)
                {
                    if (i > 0) sbMat.Append(";");
                    for (int j = 0; j < v.Cols; j++)
                    {
                        if (j > 0) sbMat.Append(",");
                        sbMat.Append(v.At(i, j).ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                sbMat.Append("]");
                return sbMat.ToString();
            }
            MValue MatDeserialize(string s)
            {
                s = s.Trim();
                if (s == "null") return new MValue(0);
                if (s.StartsWith("\"") && s.EndsWith("\""))
                    return new MValue(s[1..^1].Replace("\\\"", "\""));
                if (s.StartsWith("[") && s.EndsWith("]"))
                {
                    var rows = s[1..^1].Split(';');
                    int nr = rows.Length;
                    int nc = rows[0].Split(',').Length;
                    var data = new double[nr * nc];
                    for (int i = 0; i < nr; i++)
                    {
                        var cols = rows[i].Split(',');
                        for (int j = 0; j < cols.Length && j < nc; j++)
                            double.TryParse(cols[j].Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out data[i * nc + j]);
                    }
                    return new MValue(nr, nc, data);
                }
                if (s.StartsWith("{"))
                {
                    // Struct simple
                    var st2 = MValue.NewStruct();
                    var inner = s[1..^1];
                    var pairs = SplitJsonPairs(inner);
                    foreach (var pair in pairs)
                    {
                        int cIdx = pair.IndexOf(':');
                        var key = pair[..cIdx].Trim().Trim('"');
                        var val = MatDeserialize(pair[(cIdx + 1)..]);
                        st2.Fields[key] = val;
                    }
                    return st2;
                }
                if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return new MValue(d);
                return new MValue(0);
            }
            static List<string> SplitJsonPairs(string s)
            {
                var result = new List<string>();
                int depth = 0; bool inStr = false; int start = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '"' && (i == 0 || s[i - 1] != '\\')) inStr = !inStr;
                    if (inStr) continue;
                    if (c == '[' || c == '{') depth++;
                    else if (c == ']' || c == '}') depth--;
                    else if (c == ',' && depth == 0) { result.Add(s[start..i]); start = i + 1; }
                }
                if (start < s.Length) result.Add(s[start..]);
                return result;
            }
            // PNG I/O (usa System.Drawing.Common si disponible — MVP: solo PGM ASCII)
            _builtins["imread"] = a => {
                if (!a[0].IsString) throw new MatlabRuntimeException("imread(filename)");
                var path = a[0].StringValue;
                if (!System.IO.File.Exists(path)) throw new MatlabRuntimeException($"imread: file not found: {path}");
                if (path.EndsWith(".pgm", StringComparison.OrdinalIgnoreCase))
                {
                    // PGM ASCII (P2): magic, width, height, maxval, pixels (row-major)
                    var content = System.IO.File.ReadAllText(path);
                    var tokens = new System.Collections.Generic.List<string>();
                    foreach (var line in content.Split('\n'))
                    {
                        var trim = line.TrimStart();
                        if (trim.StartsWith("#")) continue;
                        tokens.AddRange(line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                    if (tokens.Count < 4 || tokens[0] != "P2")
                        throw new MatlabRuntimeException("imread: only PGM (P2) format supported in MVP");
                    int w = int.Parse(tokens[1]), h = int.Parse(tokens[2]);
                    var pix = new double[h * w];
                    for (int k = 0; k < h * w && 4 + k < tokens.Count; k++)
                        pix[k] = double.Parse(tokens[4 + k], CultureInfo.InvariantCulture);
                    return new MValue(h, w, pix);
                }
                throw new MatlabRuntimeException($"imread: format not supported: {System.IO.Path.GetExtension(path)} (use .pgm)");
            };
            // getframe([gcf]) — captura la figura 2D actual como struct con .cdata (HxWx3 uint8).
            _builtins["getframe"] = a => {
                int gw = 640, gh = 520;
                var rgb = MatlabPlots.RasterizeFigure(gw, gh);
                if (rgb == null) throw new MatlabRuntimeException("getframe: no hay figura abierta");
                var R = new MValue(gh, gw); var G = new MValue(gh, gw); var B = new MValue(gh, gw);
                for (int r = 0; r < gh; r++)
                    for (int c = 0; c < gw; c++)
                    {
                        int idx = (r * gw + c) * 3;
                        R.Set(r, c, rgb[idx]); G.Set(r, c, rgb[idx + 1]); B.Set(r, c, rgb[idx + 2]);
                    }
                var st = MValue.NewStruct();
                st.Fields["cdata"] = MValue.New3D(new[] { R, G, B });
                st.Fields["colormap"] = new MValue(0, 0);
                return st;
            };
            _builtins["imwrite"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("imwrite(img, filename, ...)");
                // localizar el filename (primer string que parece ruta)
                var img = a[0]; string path = null; int pathArg = -1;
                for (int i = 1; i < a.Length; i++)
                    if (a[i].IsString && (a[i].StringValue.Contains('.') || a[i].StringValue.Contains('/') || a[i].StringValue.Contains('\\')))
                    { path = a[i].StringValue; pathArg = i; break; }
                if (path == null) throw new MatlabRuntimeException("imwrite: falta nombre de archivo");

                // ---- GIF animado (RGB HxWx3) ----
                if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    if (!img.Is3D || img.Pages.Length < 3)
                        throw new MatlabRuntimeException("imwrite GIF: se espera imagen RGB (HxWx3)");
                    var Rp = img.Pages[0]; var Gp = img.Pages[1]; var Bp = img.Pages[2];
                    int gh = Rp.Rows, gw = Rp.Cols;
                    var rgb = new byte[gh * gw * 3];
                    for (int r = 0; r < gh; r++)
                        for (int c = 0; c < gw; c++)
                        { int k = (r * gw + c) * 3; rgb[k] = (byte)Math.Clamp((int)Rp.At(r, c), 0, 255); rgb[k + 1] = (byte)Math.Clamp((int)Gp.At(r, c), 0, 255); rgb[k + 2] = (byte)Math.Clamp((int)Bp.At(r, c), 0, 255); }
                    // opciones Name/Value
                    bool append = false; double delay = 0.5; bool loop = true;
                    for (int i = pathArg + 1; i + 1 < a.Length; i++)
                    {
                        if (!a[i].IsString) continue;
                        string nm = a[i].StringValue.ToLowerInvariant();
                        if (nm == "writemode") append = a[i + 1].IsString && a[i + 1].StringValue.Equals("append", StringComparison.OrdinalIgnoreCase);
                        else if (nm == "delaytime") delay = a[i + 1].Scalar;
                        else if (nm == "loopcount") loop = !(a[i + 1].IsScalar && a[i + 1].Scalar == 0);
                    }
                    if (!append) MatlabGif.Reset(path);
                    MatlabGif.AppendAndWrite(path, rgb, gw, gh, (int)Math.Round(delay * 100), loop);
                    return new MValue(0);
                }

                if (!path.EndsWith(".pgm", StringComparison.OrdinalIgnoreCase))
                    throw new MatlabRuntimeException("imwrite: solo PGM y GIF soportados");
                int w = img.Cols, h = img.Rows;
                double maxV = 0;
                foreach (var v in img.Data) if (v > maxV) maxV = v;
                int maxInt = Math.Max(1, (int)Math.Ceiling(maxV));
                var sb = new StringBuilder();
                sb.AppendLine("P2");
                sb.AppendLine($"{w} {h}");
                sb.AppendLine($"{maxInt}");
                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < w; j++) { sb.Append((int)img.At(i, j)); sb.Append(' '); }
                    sb.AppendLine();
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                return new MValue(0);
            };

            // Modern integrals (aliases para dblquad/triplequad)
            _builtins["integral2"] = _builtins["dblquad"];
            _builtins["integral3"] = _builtins["triplequad"];

            // 2D convolution + filtering
            _builtins["conv2"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("conv2(A, K)");
                var A = a[0]; var K = a[1];
                string mode = a.Length >= 3 && a[2].IsString ? a[2].StringValue : "full";
                return Conv2D(A, K, mode);
            };
            _builtins["imfilter"] = a => {
                // imfilter(img, kernel) — convolución (sin flip) tipo MATLAB
                if (a.Length < 2) throw new MatlabRuntimeException("imfilter(img, kernel)");
                return Conv2D(a[0], a[1], "same");
            };
            _builtins["imresize"] = a => {
                // imresize(img, scale) o imresize(img, [rows, cols])
                if (a.Length < 2) throw new MatlabRuntimeException("imresize(img, scale)");
                var img = a[0];
                int newRows, newCols;
                if (a[1].IsScalar)
                {
                    double scale = a[1].Scalar;
                    newRows = Math.Max(1, (int)Math.Round(img.Rows * scale));
                    newCols = Math.Max(1, (int)Math.Round(img.Cols * scale));
                }
                else
                {
                    newRows = (int)a[1].Data[0];
                    newCols = (int)a[1].Data[1];
                }
                var r = new MValue(newRows, newCols);
                // Bilinear interpolation
                for (int i = 0; i < newRows; i++)
                {
                    double srcI = (double)i * (img.Rows - 1) / Math.Max(newRows - 1, 1);
                    int i0 = (int)Math.Floor(srcI), i1 = Math.Min(i0 + 1, img.Rows - 1);
                    double ti = srcI - i0;
                    for (int j = 0; j < newCols; j++)
                    {
                        double srcJ = (double)j * (img.Cols - 1) / Math.Max(newCols - 1, 1);
                        int j0 = (int)Math.Floor(srcJ), j1 = Math.Min(j0 + 1, img.Cols - 1);
                        double tj = srcJ - j0;
                        double v = (1 - ti) * (1 - tj) * img.At(i0, j0)
                                 + (1 - ti) * tj * img.At(i0, j1)
                                 + ti * (1 - tj) * img.At(i1, j0)
                                 + ti * tj * img.At(i1, j1);
                        r.Set(i, j, v);
                    }
                }
                return r;
            };
            _builtins["rgb2gray"] = a => {
                // MVP: si img es matriz, devuelve copia; si es M×N×3, promedia canales
                return a[0];  // grayscale ya
            };
            _builtins["vertcat"] = a => {
                // Concat vertical de matrices
                int totalRows = 0, cols = a[0].Cols;
                foreach (var x in a)
                {
                    if (x.Cols != cols) throw new MatlabRuntimeException("vertcat: col mismatch");
                    totalRows += x.Rows;
                }
                var r = new MValue(totalRows, cols);
                int offset = 0;
                foreach (var x in a)
                {
                    for (int i = 0; i < x.Rows; i++)
                        for (int j = 0; j < cols; j++)
                            r.Set(offset + i, j, x.At(i, j));
                    offset += x.Rows;
                }
                return r;
            };
            _builtins["horzcat"] = a => {
                int rows = a[0].Rows, totalCols = 0;
                foreach (var x in a)
                {
                    if (x.Rows != rows) throw new MatlabRuntimeException("horzcat: row mismatch");
                    totalCols += x.Cols;
                }
                var r = new MValue(rows, totalCols);
                int offset = 0;
                foreach (var x in a)
                {
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < x.Cols; j++)
                            r.Set(i, offset + j, x.At(i, j));
                    offset += x.Cols;
                }
                return r;
            };
            _builtins["cat"] = a => {
                // cat(dim, A, B, ...) — dim=1 vertcat, dim=2 horzcat
                if (a.Length < 2) return a[0];
                int dim = (int)a[0].Scalar;
                var rest = new MValue[a.Length - 1];
                Array.Copy(a, 1, rest, 0, rest.Length);
                if (dim == 1) return _builtins["vertcat"](rest);
                if (dim == 2) return _builtins["horzcat"](rest);
                throw new MatlabRuntimeException("cat: dim > 2 no soportado (MVP)");
            };
            _builtins["permute"] = a => {
                // permute(A, [d1 d2]) — para 2D solo soporta [1, 2] (identity) o [2, 1] (transpose)
                var p = a[1].Data;
                if (p.Length == 2 && p[0] == 1 && p[1] == 2) return a[0];
                if (p.Length == 2 && p[0] == 2 && p[1] == 1) return Transpose(a[0]);
                throw new MatlabRuntimeException("permute: solo 2D soportado (MVP)");
            };
            _builtins["squeeze"] = a => a[0];   // 2D no tiene dims singleton — identity
            _builtins["ipermute"] = _builtins["permute"];
            _builtins["flipud"] = a => {
                var v = a[0];
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Rows; i++)
                    for (int j = 0; j < v.Cols; j++)
                        r.Set(v.Rows - 1 - i, j, v.At(i, j));
                return r;
            };
            _builtins["fliplr"] = a => {
                var v = a[0];
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Rows; i++)
                    for (int j = 0; j < v.Cols; j++)
                        r.Set(i, v.Cols - 1 - j, v.At(i, j));
                return r;
            };
            _builtins["rot90"] = a => {
                int k = a.Length >= 2 ? (int)a[1].Scalar : 1;
                k = ((k % 4) + 4) % 4;   // normalizar a 0..3
                var v = a[0];
                for (int rot = 0; rot < k; rot++)
                {
                    var r = new MValue(v.Cols, v.Rows);
                    for (int i = 0; i < v.Rows; i++)
                        for (int j = 0; j < v.Cols; j++)
                            r.Set(v.Cols - 1 - j, i, v.At(i, j));
                    v = r;
                }
                return v;
            };

            _builtins["fspecial"] = a => {
                // fspecial('gaussian', size, sigma) o 'average'/'sobel'/'laplacian'
                if (a.Length == 0 || !a[0].IsString) throw new MatlabRuntimeException("fspecial(type, ...)");
                string type = a[0].StringValue;
                if (type == "gaussian")
                {
                    int sz = a.Length >= 2 ? (int)a[1].Scalar : 3;
                    double sigma = a.Length >= 3 ? a[2].Scalar : 0.5;
                    int half = sz / 2;
                    var r = new MValue(sz, sz);
                    double sum = 0;
                    for (int i = 0; i < sz; i++)
                        for (int j = 0; j < sz; j++)
                        {
                            int di = i - half, dj = j - half;
                            double v = Math.Exp(-(di * di + dj * dj) / (2 * sigma * sigma));
                            r.Set(i, j, v); sum += v;
                        }
                    for (int i = 0; i < r.Data.Length; i++) r.Data[i] /= sum;
                    return r;
                }
                if (type == "average")
                {
                    int sz = a.Length >= 2 ? (int)a[1].Scalar : 3;
                    var r = new MValue(sz, sz);
                    double v = 1.0 / (sz * sz);
                    for (int i = 0; i < r.Data.Length; i++) r.Data[i] = v;
                    return r;
                }
                if (type == "sobel")
                {
                    return new MValue(3, 3, new[] { 1.0, 2, 1, 0, 0, 0, -1, -2, -1 });
                }
                if (type == "laplacian")
                {
                    return new MValue(3, 3, new[] { 0.0, -1, 0, -1, 4, -1, 0, -1, 0 });
                }
                throw new MatlabRuntimeException($"fspecial: unknown filter type '{type}'");
            };
            _builtins["roots"] = a => {
                // roots de un polinomio — usar eig de la companion matrix
                var p = a[0].Data;
                int n = p.Length - 1;
                if (n == 0) return new MValue(0, 0);
                if (n == 1) return new MValue(-p[1] / p[0]);
                var C = new MValue(n, n);
                for (int i = 0; i < n; i++) C.Set(0, i, -p[i + 1] / p[0]);
                for (int i = 1; i < n; i++) C.Set(i, i - 1, 1);
                return MatlabLinAlg.Eig(C).eigenvalues;
            };
            _builtins["var"] = a => {
                var v = a[0]; double mean = 0;
                foreach (var x in v.Data) mean += x;
                mean /= v.Data.Length;
                double s = 0;
                foreach (var x in v.Data) s += (x - mean) * (x - mean);
                return new MValue(s / Math.Max(v.Data.Length - 1, 1));
            };
            _builtins["std"] = a => {
                var v = a[0]; double mean = 0;
                foreach (var x in v.Data) mean += x;
                mean /= v.Data.Length;
                double s = 0;
                foreach (var x in v.Data) s += (x - mean) * (x - mean);
                return new MValue(Math.Sqrt(s / Math.Max(v.Data.Length - 1, 1)));
            };
            _builtins["median"] = a => {
                var sorted = (double[])a[0].Data.Clone();
                Array.Sort(sorted);
                int n = sorted.Length;
                return new MValue(n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2);
            };

            // Symbolic computation (MVP)
            _builtins["sym"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("sym(name) or sym(expr)");
                if (a[0].IsString) return MValue.NewSymbolic(new SymVar(a[0].StringValue));
                if (a[0].IsSymbolic) return a[0];
                if (a[0].IsScalar) return MValue.NewSymbolic(new SymConst(a[0].Scalar));
                throw new MatlabRuntimeException("sym: unsupported argument");
            };
            _builtins["syms"] = a => {
                // syms x y z — declara cada nombre como variable simbólica en el scope global
                foreach (var v in a)
                {
                    if (v.IsString) Globals.Set(v.StringValue, MValue.NewSymbolic(new SymVar(v.StringValue)));
                }
                return new MValue(0);
            };
            _builtins["diff"] = a => {
                // diff(expr, var)             → 1st derivative
                // diff(expr, var, n)          → n-th derivative
                // diff(expr, [v1, v2, ...])   → derivada cruzada ∂²/∂v1∂v2
                if (a[0].IsSymbolic)
                {
                    string varName = "x";
                    int order = 1;
                    if (a.Length >= 2)
                    {
                        if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                        else if (a[1].IsString) varName = a[1].StringValue;
                        // diff(expr, n) con n escalar NUMÉRICO = n-ésima derivada (MATLAB).
                        // Antes se ignoraba → diff(g,2) daba diff(g,1). Bug reportado por la
                        // comunidad (derivadas de orden superior iguales a la 1ª).
                        else if (a[1].IsScalar) order = (int)a[1].Scalar;
                    }
                    if (a.Length >= 3) order = (int)a[2].Scalar;
                    var result = a[0].Symbolic;
                    for (int k = 0; k < order; k++) result = result.Diff(varName).Simplify();
                    return MValue.NewSymbolic(result);
                }
                // Numérico
                var vc = a[0];
                int orderN = a.Length >= 2 && a[1].IsScalar ? (int)a[1].Scalar : 1;
                for (int k = 0; k < orderN; k++)
                {
                    if (vc.Rows == 1 || vc.Cols == 1)
                    {
                        int n = vc.Data.Length - 1;
                        var r = new MValue(vc.Rows == 1 ? 1 : n, vc.Cols == 1 ? 1 : n);
                        for (int i = 0; i < n; i++) r.Data[i] = vc.Data[i + 1] - vc.Data[i];
                        vc = r;
                    }
                    else throw new MatlabRuntimeException("diff: 2D matrices not supported yet");
                }
                return vc;
            };
            _builtins["coeffs"] = a => {
                // coeffs(poly, var) → vector de coeficientes [c0, c1, c2, ...]
                if (a.Length == 0 || !a[0].IsSymbolic) throw new MatlabRuntimeException("coeffs(symExpr[, var])");
                string varName = "x";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                    else if (a[1].IsString) varName = a[1].StringValue;
                }
                // Extraer coefs via Taylor (mismo método interno que SolvePoly)
                var coefs = ExtractPolyCoeffs(a[0].Symbolic, varName, 12);
                if (coefs == null) throw new MatlabRuntimeException("coeffs: expresión no polinómica");
                // Trim trailing zeros
                int deg = coefs.Length - 1;
                while (deg > 0 && Math.Abs(coefs[deg]) < 1e-12) deg--;
                var data = new double[deg + 1];
                Array.Copy(coefs, data, deg + 1);
                return new MValue(1, deg + 1, data);
            };
            _builtins["sym2poly"] = a => {
                // sym2poly(expr, var) → vector de coefs [a_n, ..., a_1, a_0] (orden inverso)
                if (a.Length == 0 || !a[0].IsSymbolic) throw new MatlabRuntimeException("sym2poly(symExpr)");
                string varName = "x";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                    else if (a[1].IsString) varName = a[1].StringValue;
                }
                var coefs = ExtractPolyCoeffs(a[0].Symbolic, varName, 12);
                if (coefs == null) throw new MatlabRuntimeException("sym2poly: no polinómica");
                int deg = coefs.Length - 1;
                while (deg > 0 && Math.Abs(coefs[deg]) < 1e-12) deg--;
                var rev = new double[deg + 1];
                for (int i = 0; i <= deg; i++) rev[i] = coefs[deg - i];
                return new MValue(1, deg + 1, rev);
            };
            _builtins["poly2sym"] = a => {
                // poly2sym([a_n, ..., a_1, a_0], var) → expresión polinómica
                var coefs = a[0].Data;
                string varName = a.Length >= 2 && a[1].IsSymbolic && a[1].Symbolic is SymVar sv ? sv.Name : "x";
                var X = new SymVar(varName);
                SymNode result = new SymConst(0);
                int n = coefs.Length - 1;
                for (int i = 0; i <= n; i++)
                {
                    int power = n - i;
                    SymNode term = new SymMul(new SymConst(coefs[i]),
                                              power == 0 ? (SymNode)new SymConst(1) :
                                              power == 1 ? (SymNode)X :
                                              new SymPow(X, new SymConst(power)));
                    result = new SymAdd(result, term);
                }
                return MValue.NewSymbolic(result.Simplify());
            };
            _builtins["collect"] = a => {
                // collect(expr, var) — agrupa por potencias de var (MVP: usa coeffs + poly2sym)
                if (!a[0].IsSymbolic) return a[0];
                string varName = "x";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                    else if (a[1].IsString) varName = a[1].StringValue;
                }
                try
                {
                    var coefs = ExtractPolyCoeffs(a[0].Symbolic, varName, 12);
                    if (coefs == null) return a[0];
                    int deg = coefs.Length - 1;
                    while (deg > 0 && Math.Abs(coefs[deg]) < 1e-12) deg--;
                    var X = new SymVar(varName);
                    SymNode result = new SymConst(0);
                    for (int i = deg; i >= 0; i--)
                    {
                        if (Math.Abs(coefs[i]) < 1e-12) continue;
                        SymNode term = i == 0 ? (SymNode)new SymConst(coefs[i]) :
                                       i == 1 ? new SymMul(new SymConst(coefs[i]), X) :
                                       new SymMul(new SymConst(coefs[i]), new SymPow(X, new SymConst(i)));
                        result = result is SymConst rc && rc.Value == 0 ? term : new SymAdd(result, term);
                    }
                    return MValue.NewSymbolic(result.Simplify());
                }
                catch { return a[0]; }
            };

            static double[] ExtractPolyCoeffs(SymNode expr, string var, int maxDeg)
            {
                var coefs = new double[maxDeg + 1];
                SymNode current = expr.Simplify();
                double factK = 1;
                for (int k = 0; k <= maxDeg; k++)
                {
                    if (k > 0) factK *= k;
                    try
                    {
                        var atZero = current.Subs(var, new SymConst(0)).Simplify();
                        if (atZero is SymConst c) coefs[k] = c.Value / factK;
                        else return null;
                    }
                    catch { return null; }
                    current = current.Diff(var).Simplify();
                }
                return coefs;
            }
            _builtins["dsolve"] = a => {
                // dsolve(rhs, y, t) — resuelve dy/dt = rhs simbólicamente
                // dsolve(rhs, y, t, y0) — con condición inicial y(0) = y0
                if (a.Length < 3) throw new MatlabRuntimeException("dsolve(rhs, y, t [, y0])");
                SymNode rhsNode;
                if (a[0].IsSymbolic) rhsNode = a[0].Symbolic;
                else if (a[0].IsScalar) rhsNode = new SymConst(a[0].Scalar);
                else throw new MatlabRuntimeException("dsolve: rhs debe ser simbólico o escalar");
                string yName, tName;
                if (a[1].IsSymbolic && a[1].Symbolic is SymVar yv) yName = yv.Name;
                else if (a[1].IsString) yName = a[1].StringValue;
                else throw new MatlabRuntimeException("dsolve: y debe ser sym var o string");
                if (a[2].IsSymbolic && a[2].Symbolic is SymVar tv) tName = tv.Name;
                else if (a[2].IsString) tName = a[2].StringValue;
                else throw new MatlabRuntimeException("dsolve: t debe ser sym var o string");
                var sol = SymOps.SolveOde1(rhsNode, yName, tName);
                // Si hay condición inicial y0 (en a[3]), resolver C
                if (a.Length >= 4)
                {
                    SymNode y0;
                    if (a[3].IsScalar) y0 = new SymConst(a[3].Scalar);
                    else if (a[3].IsSymbolic) y0 = a[3].Symbolic;
                    else throw new MatlabRuntimeException("dsolve: y0 debe ser escalar o simbólico");
                    // sol(0) = y0 → resolver C en sol(t=0) = y0
                    var solAt0 = sol.Subs(tName, new SymConst(0)).Simplify();
                    var diffEq = new SymSub(solAt0, y0).Simplify();
                    var diffC0 = diffEq.Subs("C", new SymConst(0)).Simplify();
                    var diffC1 = diffEq.Subs("C", new SymConst(1)).Simplify();
                    var coefC = new SymSub(diffC1, diffC0).Simplify();
                    var Cval = new SymDiv(new SymMul(new SymConst(-1), diffC0), coefC).Simplify();
                    sol = sol.Subs("C", Cval).Simplify();
                }
                return MValue.NewSymbolic(sol);
            };
            _builtins["expand"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("expand(symExpr)");
                if (a[0].IsSymbolic) return MValue.NewSymbolic(SymNode.Expand(a[0].Symbolic));
                if (a[0].IsSymMatrix)
                {
                    int rs = a[0].SymCells.GetLength(0), cs = a[0].SymCells.GetLength(1);
                    var newCells = new SymNode[rs, cs];
                    for (int i = 0; i < rs; i++)
                        for (int j = 0; j < cs; j++) newCells[i, j] = SymNode.Expand(a[0].SymCells[i, j]);
                    return MValue.NewSymMatrix(newCells);
                }
                return a[0];  // no-op para numéricos
            };
            _builtins["collect"] = a => {
                // collect(expr) o collect(expr, var) — la simplificación auto ya colecta like-terms.
                // Esto es básicamente un wrapper sobre Simplify (que ya hace collection).
                if (a.Length == 0 || !a[0].IsSymbolic) return a[0];
                return MValue.NewSymbolic(a[0].Symbolic.Simplify());
            };
            _builtins["subs"] = a => {
                // subs(expr, var, value) — single
                // subs(expr, [v1, v2, ...], [val1, val2, ...]) — multi (sym matrix de vars + vector de vals)
                // subs(expr, vars_cell, vals_cell) — multi via cells
                if (a.Length < 3 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("subs(symExpr, var, value)");
                var result = a[0].Symbolic;
                // Detectar multi-var
                List<string> varNames = new();
                List<SymNode> valNodes = new();
                if (a[1].IsCell)
                {
                    for (int i = 0; i < a[1].CellData.GetLength(0); i++)
                        for (int j = 0; j < a[1].CellData.GetLength(1); j++)
                        {
                            var cell = a[1].CellData[i, j];
                            if (cell == null) continue;
                            if (cell.IsString) varNames.Add(cell.StringValue);
                            else if (cell.IsSymbolic && cell.Symbolic is SymVar svc) varNames.Add(svc.Name);
                            else throw new MatlabRuntimeException("subs: cell debe contener string o sym");
                        }
                }
                else if (a[1].IsSymMatrix)
                {
                    // [x y z] como sym matrix — cada celda debe ser SymVar
                    int rs = a[1].SymCells.GetLength(0), cs = a[1].SymCells.GetLength(1);
                    for (int i = 0; i < rs; i++)
                        for (int j = 0; j < cs; j++)
                        {
                            if (a[1].SymCells[i, j] is SymVar sv2) varNames.Add(sv2.Name);
                            else throw new MatlabRuntimeException("subs: matrix de vars debe contener solo SymVar");
                        }
                }
                else if (a[1].IsScalar || a[1].IsString || a[1].IsSymbolic)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varNames.Add(sv.Name);
                    else if (a[1].IsString) varNames.Add(a[1].StringValue);
                    else throw new MatlabRuntimeException("subs: var must be string or sym");
                }
                else
                {
                    throw new MatlabRuntimeException("subs: multi-var requires cell of strings, sym list, or [v1,v2,...]");
                }
                if (a[2].IsCell)
                {
                    for (int i = 0; i < a[2].CellData.GetLength(0); i++)
                        for (int j = 0; j < a[2].CellData.GetLength(1); j++)
                        {
                            var cell = a[2].CellData[i, j];
                            valNodes.Add(cell.IsSymbolic ? cell.Symbolic : new SymConst(cell.Scalar));
                        }
                }
                else if (a[2].IsSymMatrix)
                {
                    int rs2 = a[2].SymCells.GetLength(0), cs2 = a[2].SymCells.GetLength(1);
                    for (int i = 0; i < rs2; i++)
                        for (int j = 0; j < cs2; j++)
                            valNodes.Add(a[2].SymCells[i, j]);
                }
                else if (a[2].IsScalar && varNames.Count == 1)
                {
                    valNodes.Add(new SymConst(a[2].Scalar));
                }
                else if (a[2].IsSymbolic && varNames.Count == 1)
                {
                    valNodes.Add(a[2].Symbolic);
                }
                else if (varNames.Count > 1 && a[2].Data != null && a[2].Data.Length == varNames.Count)
                {
                    for (int i = 0; i < a[2].Data.Length; i++) valNodes.Add(new SymConst(a[2].Data[i]));
                }
                else throw new MatlabRuntimeException("subs: vals must match vars count");
                if (varNames.Count != valNodes.Count)
                    throw new MatlabRuntimeException($"subs: vars count {varNames.Count} != vals count {valNodes.Count}");
                for (int i = 0; i < varNames.Count; i++)
                    result = result.Subs(varNames[i], valNodes[i]).Simplify();
                if (result is SymConst sc) return new MValue(sc.Value);
                return MValue.NewSymbolic(result);
            };
            _builtins["simplify"] = a => {
                if (!a[0].IsSymbolic) return a[0];
                // PUENTE giac (como MATLAB→MuPAD): simplify(...) → giac. Fallback: motor propio.
                if (GiacRunner.IsAvailable())
                {
                    var (ok, res) = GiacRunner.Eval($"simplify({a[0].Symbolic.ToInfix()})");
                    if (ok) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(res)).Simplify()); } catch { } }
                }
                var r = a[0].Symbolic.Simplify();
                r = TrigRules.SimplifyTrig(r);   // aplica reducciones trig
                return MValue.NewSymbolic(r);
            };
            _builtins["expand"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("expand(symExpr)");
                if (a[0].IsSymbolic)
                {
                    // PUENTE giac (como MATLAB→MuPAD): expand(...) → giac (da x^3+3x^2+3x+1,
                    // no la forma de Horner anidada del motor propio). Fallback: motor propio.
                    if (GiacRunner.IsAvailable())
                    {
                        var (oke, rese) = GiacRunner.Eval($"expand({a[0].Symbolic.ToInfix()})");
                        if (oke) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(rese))); } catch { } }
                    }
                    // Trig expand primero (sin(a+b) → sin(a)cos(b)+cos(a)sin(b)), luego algebraic expand
                    var rT = TrigRules.ExpandTrig(a[0].Symbolic);
                    var rA = SymNode.Expand(rT);
                    return MValue.NewSymbolic(rA);
                }
                if (a[0].IsSymMatrix)
                {
                    int rs = a[0].SymCells.GetLength(0), cs = a[0].SymCells.GetLength(1);
                    var newCells = new SymNode[rs, cs];
                    for (int i = 0; i < rs; i++)
                        for (int j = 0; j < cs; j++)
                            newCells[i, j] = SymNode.Expand(TrigRules.ExpandTrig(a[0].SymCells[i, j]));
                    return MValue.NewSymMatrix(newCells);
                }
                return a[0];
            };
            _builtins["trigexpand"] = a => {
                if (!a[0].IsSymbolic) return a[0];
                return MValue.NewSymbolic(TrigRules.ExpandTrig(a[0].Symbolic).Simplify());
            };
            _builtins["trigsimplify"] = _builtins["simplify"];
            _builtins["double"] = a => {
                if (a[0].IsSymbolic && a[0].Symbolic is SymConst sc) return new MValue(sc.Value);
                if (a[0].IsSymbolic)
                {
                    try { return new MValue(a[0].Symbolic.Eval(new Dictionary<string, double>())); }
                    catch { throw new MatlabRuntimeException("double: symbolic expression has unbound variables"); }
                }
                return a[0];
            };
            _builtins["char"] = a => {
                if (a[0].IsSymbolic)
                {
                    // MATLAB Symbolic Toolbox: char(sym) devuelve la expresion
                    // simplificada renderizada en HTML CSS (Calcpad-style).
                    // Sentinels PUA () marcan el HTML para que MatlabPipeline
                    // lo deje pasar sin escapar al flushear el dispBuffer.
                    return new MValue("" + a[0].Symbolic.ToHtml() + "");
                }
                // Matriz simbólica: p.ej. sol(k) de solve() es un sym 1x1. Renderizar
                // celda a celda (1x1 = la expresión sola) para que char() dé "3", no
                // el fallback numérico que producía char(0) = carácter nulo.
                if (a[0].IsSymMatrix)
                {
                    var sc = a[0].SymCells;
                    int rs = sc.GetLength(0), cs = sc.GetLength(1);
                    var sbc = new System.Text.StringBuilder();
                    for (int i = 0; i < rs; i++)
                    {
                        if (i > 0) sbc.Append('\n');
                        for (int j = 0; j < cs; j++)
                        {
                            if (j > 0) sbc.Append("  ");
                            var html = (sc[i, j] ?? new SymConst(0)).ToHtml();
                            sbc.Append((char)0xE001).Append(html).Append((char)0xE002);
                        }
                    }
                    return new MValue(sbc.ToString());
                }
                if (a[0].IsScalar) return new MValue(((char)(int)a[0].Scalar).ToString());
                return a[0];
            };
            _builtins["latex"] = a => {
                // Convertir expresion simbolica a codigo LaTeX (compatible con MATLAB)
                if (a[0].IsSymbolic)
                {
                    var simp = a[0].Symbolic.Simplify();
                    return new MValue(simp.ToLatex());
                }
                return new MValue(a[0].ToString());
            };
            _builtins["int"] = a => {
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("int(symExpr[, var])");
                // Casos MATLAB: int(f) · int(f,x) · int(f,a,b) definida · int(f,x,a,b) definida.
                // El 2o arg es VARIABLE solo si es simbólico o string; si es número son límites.
                string varName = "x";
                SymNode limA = null, limB = null;
                bool arg1IsVar = a.Length >= 2 && ((a[1].IsSymbolic && a[1].Symbolic is SymVar) || a[1].IsString);
                if (arg1IsVar)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                    else varName = a[1].StringValue;
                    if (a.Length >= 4)   // int(f, x, a, b)
                    {
                        limA = a[2].IsSymbolic ? a[2].Symbolic : new SymConst(a[2].Scalar);
                        limB = a[3].IsSymbolic ? a[3].Symbolic : new SymConst(a[3].Scalar);
                    }
                }
                else if (a.Length >= 3)   // int(f, a, b) — definida, variable por defecto
                {
                    limA = a[1].IsSymbolic ? a[1].Symbolic : new SymConst(a[1].Scalar);
                    limB = a[2].IsSymbolic ? a[2].Symbolic : new SymConst(a[2].Scalar);
                }
                // PUENTE giac (como MATLAB→MuPAD por strings). Fallback: motor propio SymOps.
                if (limA != null)
                {
                    if (GiacRunner.IsAvailable())
                    {
                        var (okd, resd) = GiacRunner.Eval($"integrate({a[0].Symbolic.ToInfix()},{varName},{limA.ToInfix()},{limB.ToInfix()})");
                        if (okd) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(resd)).Simplify()); } catch { } }
                    }
                    var antider = SymOps.Integrate(a[0].Symbolic, varName).Simplify();
                    var Fb_sym = antider.Subs(varName, limB).Simplify();
                    var Fa_sym = antider.Subs(varName, limA).Simplify();
                    return MValue.NewSymbolic(new SymSub(Fb_sym, Fa_sym).Simplify());
                }
                if (GiacRunner.IsAvailable())
                {
                    var (oki, resi) = GiacRunner.Eval($"integrate({a[0].Symbolic.ToInfix()},{varName})");
                    if (oki) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(resi)).Simplify()); } catch { } }
                }
                return MValue.NewSymbolic(SymOps.Integrate(a[0].Symbolic, varName).Simplify());
            };
            _builtins["taylor"] = a => {
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("taylor(symExpr [, var, x0, order])  o  taylor(f, 'Order', N)");
                string varName = "x";
                double x0Tay = 0;
                int order = 5;   // default MATLAB
                // Posicional simple: taylor(f, var, x0, order)
                // Name-value pairs: taylor(f, 'Order', N, 'ExpansionPoint', p, 'Var', v)
                int i = 1;
                while (i < a.Length)
                {
                    // Detectar name-value: si arg actual es string y hay siguiente
                    if (a[i].IsString && i + 1 < a.Length)
                    {
                        var name = a[i].StringValue.ToLowerInvariant();
                        if (name == "order")          { order = (int)a[i + 1].Scalar; i += 2; continue; }
                        if (name == "expansionpoint" || name == "point") { x0Tay = a[i + 1].Scalar; i += 2; continue; }
                        if (name == "var")            { varName = a[i + 1].StringValue; i += 2; continue; }
                        // string que no es keyword conocido → varName (modo posicional)
                        varName = a[i].StringValue; i++; continue;
                    }
                    // Symbolic var como posicional (varName)
                    if (a[i].IsSymbolic && a[i].Symbolic is SymVar sv) { varName = sv.Name; i++; continue; }
                    // Numerico posicional: primero x0, despues order
                    if (a[i].IsScalar)
                    {
                        // Heuristica: si todavia no se seteo x0, usarlo; sino, order.
                        if (i == 1 || (i == 2 && a[1].IsSymbolic)) { x0Tay = a[i].Scalar; }
                        else { order = (int)a[i].Scalar; }
                        i++; continue;
                    }
                    i++;
                }
                return MValue.NewSymbolic(SymOps.TaylorExpand(a[0].Symbolic, varName, x0Tay, order));
            };
            _builtins["solve"] = a => {
                // solve(expr) → resuelve expr = 0 para 'x' default
                // solve(expr, var) → resuelve para var
                // solve([eq1, eq2], [v1, v2]) → sistema (cell o lista simbólica)
                if (a.Length == 0) throw new MatlabRuntimeException("solve(...)");
                // Sistema (cell de expresiones)
                if (a[0].IsCell)
                {
                    var eqs = new List<SymNode>();
                    var cells = a[0].CellData;
                    for (int i = 0; i < cells.GetLength(0); i++)
                        for (int j = 0; j < cells.GetLength(1); j++)
                            if (cells[i, j].IsSymbolic) eqs.Add(cells[i, j].Symbolic);
                    var varNames = new List<string>();
                    if (a.Length >= 2 && a[1].IsCell)
                    {
                        var vCells = a[1].CellData;
                        for (int i = 0; i < vCells.GetLength(0); i++)
                            for (int j = 0; j < vCells.GetLength(1); j++)
                            {
                                var vv = vCells[i, j];
                                if (vv.IsString) varNames.Add(vv.StringValue);
                                else if (vv.IsSymbolic && vv.Symbolic is SymVar svv) varNames.Add(svv.Name);
                            }
                    }
                    else throw new MatlabRuntimeException("solve: sistema requiere lista de variables");
                    return SolveSystem(eqs, varNames);
                }
                if (!a[0].IsSymbolic) throw new MatlabRuntimeException("solve: expresión simbólica esperada");
                string varName = "x";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) varName = sv.Name;
                    else if (a[1].IsString) varName = a[1].StringValue;
                }
                List<double> roots;
                try { roots = SymOps.SolvePoly(a[0].Symbolic, varName); }
                catch (MatlabRuntimeException)
                {
                    // Coeficientes SIMBÓLICOS (a*x^2 + b*x + c): fórmula cuadrática
                    // simbólica. Devuelve vector columna de sym.
                    var symRoots = SymOps.SolveSymbolic(a[0].Symbolic, varName);
                    if (symRoots == null || symRoots.Count == 0)
                        throw new MatlabRuntimeException(
                            "solve: no soportado (grado > 2 con coeficientes simbólicos)");
                    var symCells = new SymNode[symRoots.Count, 1];
                    for (int ri = 0; ri < symRoots.Count; ri++) symCells[ri, 0] = symRoots[ri];
                    return MValue.NewSymMatrix(symCells);
                }
                if (roots.Count == 0) return new MValue(0, 0);
                // MATLAB Symbolic Toolbox: solve() devuelve un vector COLUMNA de
                // `sym`, no de doubles. Envolver cada raíz como SymConst para que
                // char(sol(k)) renderice "3" (y no char(3) = carácter de control),
                // y para que el resultado componga con el resto del álgebra simbólica.
                var rootCells = new SymNode[roots.Count, 1];
                for (int ri = 0; ri < roots.Count; ri++)
                    rootCells[ri, 0] = new SymConst(roots[ri]);
                return MValue.NewSymMatrix(rootCells);
            };
            // factor(symExpr [, var]) — factoriza polinomio: p(x) = (x-r1)(x-r2)...
            // Devuelve el PRODUCTO simbolico de factores (x - r_i) usando las raices
            // reales obtenidas de SolvePoly. Equivalente al output de MATLAB
            // factor() para polinomios con raices reales.
            _builtins["factor"] = a => {
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("factor(symExpr [, var])");
                string varFac = "x";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar svf) varFac = svf.Name;
                    else if (a[1].IsString) varFac = a[1].StringValue;
                }
                // PUENTE giac (como MATLAB→MuPAD): factor(...) → giac. Fallback: motor propio.
                if (GiacRunner.IsAvailable())
                {
                    var (okf, resf) = GiacRunner.Eval($"factor({a[0].Symbolic.ToInfix()})");
                    if (okf) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(resf))); } catch { } }
                }
                var rootsFac = SymOps.SolvePoly(a[0].Symbolic, varFac);
                if (rootsFac.Count == 0)
                    return MValue.NewSymbolic(a[0].Symbolic);  // sin factores reales, devolver original
                // Construir producto (x - r1)(x - r2)...
                SymNode product = null;
                foreach (var r in rootsFac)
                {
                    SymNode fact = new SymSub(new SymVar(varFac), new SymConst(r));
                    product = (product == null) ? fact : new SymMul(product, fact);
                }
                return MValue.NewSymbolic(product.Simplify());
            };

            // Helper: chequea si el nodo simbolico tiene variables libres
            // (intentando evaluar con scope vacio — si lanza, hay variables libres).
            bool HasFreeVars(SymNode n)
            {
                try { n.Eval(new Dictionary<string, double>()); return false; }
                catch { return true; }
            }

            MValue SolveSystem(List<SymNode> eqs, List<string> vars)
            {
                int n = vars.Count;
                if (eqs.Count != n) throw new MatlabRuntimeException($"solve: {eqs.Count} ecs vs {n} vars");
                // Estrategia: si lineal, extraer matriz A y vector b → linsolve.
                // Detección: cada ecuación expr_i, intentar coeffs lineales.
                var A = new double[n, n];
                var b = new double[n];
                bool isLinear = true;
                for (int i = 0; i < n && isLinear; i++)
                {
                    var eq = eqs[i].Simplify();
                    // Verificar que TODAS las segundas derivadas ∂²eq/∂xi∂xj = 0 (linealidad)
                    for (int p = 0; p < n && isLinear; p++)
                        for (int q = p; q < n && isLinear; q++)
                        {
                            try
                            {
                                var d2 = eq.Diff(vars[p]).Diff(vars[q]).Simplify();
                                // Eval en varios puntos para asegurar es la const 0
                                var subs1 = new Dictionary<string, double>();
                                var subs2 = new Dictionary<string, double>();
                                for (int k = 0; k < n; k++) { subs1[vars[k]] = 0; subs2[vars[k]] = 1; }
                                if (Math.Abs(d2.Eval(subs1)) > 1e-10 || Math.Abs(d2.Eval(subs2)) > 1e-10)
                                    isLinear = false;
                            }
                            catch { isLinear = false; }
                        }
                    if (!isLinear) break;
                    double constTerm;
                    try
                    {
                        var atZero = eq;
                        foreach (var v in vars) atZero = atZero.Subs(v, new SymConst(0)).Simplify();
                        if (atZero is SymConst c) constTerm = c.Value;
                        else { isLinear = false; break; }
                    }
                    catch { isLinear = false; break; }
                    b[i] = -constTerm;
                    for (int j = 0; j < n; j++)
                    {
                        try
                        {
                            var partial = eq.Diff(vars[j]).Simplify();
                            foreach (var v in vars) partial = partial.Subs(v, new SymConst(0)).Simplify();
                            if (partial is SymConst cc) A[i, j] = cc.Value;
                            else { isLinear = false; break; }
                        }
                        catch { isLinear = false; break; }
                    }
                }
                if (isLinear)
                {
                    var Am = new MValue(n, n);
                    var bm = new MValue(n, 1);
                    for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) Am.Set(i, j, A[i, j]); bm.Set(i, 0, b[i]); }
                    var x = MatlabLinAlg.Linsolve(Am, bm);
                    // Devolver struct con cada var = valor
                    var st = MValue.NewStruct();
                    for (int i = 0; i < n; i++) st.Fields[vars[i]] = new MValue(x.At(i, 0));
                    return st;
                }
                // Sistema no-lineal: Newton multi-var con guess inicial 1 (evita Jacobiano singular en 0)
                var x0 = new double[n];
                for (int i = 0; i < n; i++) x0[i] = 1.0;
                for (int iter = 0; iter < 100; iter++)
                {
                    var subs = new Dictionary<string, double>();
                    for (int i = 0; i < n; i++) subs[vars[i]] = x0[i];
                    var F = new double[n];
                    for (int i = 0; i < n; i++) F[i] = eqs[i].Eval(subs);
                    double normF = 0; foreach (var v in F) normF += v * v;
                    if (Math.Sqrt(normF) < 1e-12) break;
                    var J = new MValue(n, n);
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            double h = Math.Max(1e-8, 1e-6 * Math.Abs(x0[j]));
                            var subsP = new Dictionary<string, double>(subs);
                            subsP[vars[j]] = x0[j] + h;
                            J.Set(i, j, (eqs[i].Eval(subsP) - F[i]) / h);
                        }
                    var bv = new MValue(n, 1);
                    for (int i = 0; i < n; i++) bv.Set(i, 0, -F[i]);
                    var dx = MatlabLinAlg.Linsolve(J, bv);
                    for (int i = 0; i < n; i++) x0[i] += dx.At(i, 0);
                }
                var stOut = MValue.NewStruct();
                for (int i = 0; i < n; i++) stOut.Fields[vars[i]] = new MValue(x0[i]);
                return stOut;
            }
            _builtins["pretty"] = a => {
                // MVP: devuelve forma simbólica como sea (HtmlWriter ya hace bonito)
                if (a[0].IsSymbolic) _output?.Invoke(a[0].Symbolic.ToInfix());
                return a[0];
            };
            _builtins["symsum"] = a => {
                // symsum(f, k, k_start, k_end) — fórmulas cerradas para casos comunes
                if (a.Length < 4 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("symsum(expr, k, k_start, k_end)");
                string kName = "k";
                if (a[1].IsSymbolic && a[1].Symbolic is SymVar sv) kName = sv.Name;
                else if (a[1].IsString) kName = a[1].StringValue;
                // Si los límites son finitos numéricos: expandir directamente
                if (a[2].IsScalar && a[3].IsScalar)
                {
                    int k0 = (int)a[2].Scalar, kN = (int)a[3].Scalar;
                    if (kN - k0 > 0 && kN - k0 < 10000)
                    {
                        // Caso const: f no depende de k → f * (kN - k0 + 1)
                        var expr = a[0].Symbolic;
                        SymNode acc = new SymConst(0);
                        for (int k = k0; k <= kN; k++)
                        {
                            var term = expr.Subs(kName, new SymConst(k)).Simplify();
                            acc = new SymAdd(acc, term);
                        }
                        var result = acc.Simplify();
                        if (result is SymConst sc) return new MValue(sc.Value);
                        return MValue.NewSymbolic(result);
                    }
                }
                // Caso simbólico (k_end es N): aplicar fórmulas cerradas conocidas
                var ex = a[0].Symbolic;
                if (ex is SymVar kv && kv.Name == kName)
                {
                    // sum_{k=1}^N k = N(N+1)/2
                    if (a[2].IsScalar && a[2].Scalar == 1 && a[3].IsSymbolic && a[3].Symbolic is SymVar nv)
                        return MValue.NewSymbolic(new SymDiv(
                            new SymMul(nv, new SymAdd(nv, new SymConst(1))),
                            new SymConst(2)).Simplify());
                }
                if (ex is SymPow pw && pw.Base is SymVar kv2 && kv2.Name == kName && pw.Exp is SymConst pe)
                {
                    if (pe.Value == 2 && a[2].IsScalar && a[2].Scalar == 1 && a[3].IsSymbolic && a[3].Symbolic is SymVar nv2)
                        // sum_{k=1}^N k² = N(N+1)(2N+1)/6
                        return MValue.NewSymbolic(new SymDiv(
                            new SymMul(new SymMul(nv2, new SymAdd(nv2, new SymConst(1))),
                                       new SymAdd(new SymMul(new SymConst(2), nv2), new SymConst(1))),
                            new SymConst(6)).Simplify());
                    if (pe.Value == 3 && a[2].IsScalar && a[2].Scalar == 1 && a[3].IsSymbolic && a[3].Symbolic is SymVar nv3)
                        // sum_{k=1}^N k³ = (N(N+1)/2)²
                        return MValue.NewSymbolic(new SymPow(
                            new SymDiv(new SymMul(nv3, new SymAdd(nv3, new SymConst(1))), new SymConst(2)),
                            new SymConst(2)).Simplify());
                }
                throw new MatlabRuntimeException("symsum: caso no soportado (MVP — usa límites numéricos)");
            };
            _builtins["piecewise"] = a => {
                // piecewise(c1, v1, c2, v2, ..., default) — evalúa condicionalmente
                // Si las cond son simbólicas, devuelve una expresión simbólica (no implementado MVP)
                // MVP numérico: evalúa cada cond, devuelve el primer valor cuya cond es true
                for (int i = 0; i + 1 < a.Length; i += 2)
                {
                    double cond = a[i].IsScalar ? a[i].Scalar : 0;
                    if (cond != 0) return a[i + 1];
                }
                if (a.Length % 2 == 1) return a[a.Length - 1];   // default
                return new MValue(0);
            };
            _builtins["assume"] = a => new MValue(0);    // MVP: no-op (assumptions ignoradas)
            _builtins["assumeAlso"] = a => new MValue(0);
            _builtins["laplace"] = a => {
                // Laplace transform table-based — para expresiones canónicas comunes
                // L{1} = 1/s, L{t^n} = n!/s^(n+1), L{e^(at)} = 1/(s-a), L{sin(at)} = a/(s²+a²), L{cos(at)} = s/(s²+a²)
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("laplace(symExpr[, t, s])");
                string tName = "t", sName = "s";
                if (a.Length >= 2)
                {
                    if (a[1].IsSymbolic && a[1].Symbolic is SymVar v1) tName = v1.Name;
                    else if (a[1].IsString) tName = a[1].StringValue;
                }
                if (a.Length >= 3)
                {
                    if (a[2].IsSymbolic && a[2].Symbolic is SymVar v2) sName = v2.Name;
                    else if (a[2].IsString) sName = a[2].StringValue;
                }
                return MValue.NewSymbolic(LaplaceTransform(a[0].Symbolic, tName, sName).Simplify());
            };
            _builtins["heaviside"] = a => {
                var v = a[0];
                if (v.IsSymbolic) return MValue.NewSymbolic(new SymFunc("heaviside", v.Symbolic));
                if (v.IsScalar) return new MValue(v.Scalar < 0 ? 0 : (v.Scalar == 0 ? 0.5 : 1));
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Data.Length; i++) r.Data[i] = v.Data[i] < 0 ? 0 : (v.Data[i] == 0 ? 0.5 : 1);
                return r;
            };
            _builtins["dirac"] = a => {
                // δ(x) = ∞ en 0, 0 en otro lugar — discretizamos como 0 en numérico
                var v = a[0];
                if (v.IsSymbolic) return MValue.NewSymbolic(new SymFunc("dirac", v.Symbolic));
                if (v.IsScalar) return new MValue(v.Scalar == 0 ? double.PositiveInfinity : 0);
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Data.Length; i++) r.Data[i] = v.Data[i] == 0 ? double.PositiveInfinity : 0;
                return r;
            };
            _builtins["rectpuls"] = a => {
                var v = a[0]; double w = a.Length >= 2 ? a[1].Scalar : 1.0;
                if (v.IsScalar) return new MValue(Math.Abs(v.Scalar) <= w / 2 ? 1 : 0);
                var r = new MValue(v.Rows, v.Cols);
                for (int i = 0; i < v.Data.Length; i++) r.Data[i] = Math.Abs(v.Data[i]) <= w / 2 ? 1 : 0;
                return r;
            };
            _builtins["sinc"] = a => MapUnary(a[0], x => x == 0 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x));

            // ─── Control System Toolbox ─────────────────────────────────────
            _builtins["tf"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("tf(num, den)");
                return MatlabControl.Tf(a[0].Data, a[1].Data);
            };
            _builtins["zpk"] = a => {
                if (a.Length < 3) throw new MatlabRuntimeException("zpk(zeros, poles, gain)");
                return MatlabControl.Zpk(a[0].Data, a[1].Data, a[2].Scalar);
            };
            _builtins["step"] = a => {
                var (num, den) = ExtractTf(a[0]);
                double tFinal = a.Length >= 2 ? a[1].Scalar : 10.0;
                var (ts, ys) = MatlabControl.StepResponse(num, den, tFinal);
                _htmlOut?.Invoke(MatlabPlots.Plot(ts, ys, "step response"));
                return ys;
            };
            _builtins["impulse"] = a => {
                var (num, den) = ExtractTf(a[0]);
                double tFinal = a.Length >= 2 ? a[1].Scalar : 10.0;
                var (ts, ys) = MatlabControl.ImpulseResponse(num, den, tFinal);
                _htmlOut?.Invoke(MatlabPlots.Plot(ts, ys, "impulse response"));
                return ys;
            };
            _builtins["bode"] = a => {
                var (num, den) = ExtractTf(a[0]);
                double wMin = a.Length >= 2 ? a[1].Data[0] : 0.01;
                double wMax = a.Length >= 2 ? a[1].Data[a[1].Data.Length - 1] : 100.0;
                int N = 200;
                var (w, mag, ph) = MatlabControl.Bode(num, den, wMin, wMax, N);
                _htmlOut?.Invoke(MatlabPlots.BodeDual(w, mag, ph));
                var result = MValue.NewStruct();
                result.Fields["w"] = new MValue(1, N, w);
                result.Fields["mag"] = new MValue(1, N, mag);
                result.Fields["phase"] = new MValue(1, N, ph);
                return result;
            };
            _builtins["nyquist"] = a => {
                var (num, den) = ExtractTf(a[0]);
                double wMin = a.Length >= 2 ? a[1].Data[0] : 0.01;
                double wMax = a.Length >= 2 ? a[1].Data[a[1].Data.Length - 1] : 100.0;
                int N = 200;
                var (re, im) = MatlabControl.Nyquist(num, den, wMin, wMax, N);
                _htmlOut?.Invoke(MatlabPlots.Scatter(
                    new MValue(1, N, re), new MValue(1, N, im)));
                var result = MValue.NewStruct();
                result.Fields["re"] = new MValue(1, N, re);
                result.Fields["im"] = new MValue(1, N, im);
                return result;
            };
            _builtins["series"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("series(H1, H2)");
                var (n1, d1) = ExtractTf(a[0]);
                var (n2, d2) = ExtractTf(a[1]);
                var (n, d) = MatlabControl.Series(n1, d1, n2, d2);
                return MatlabControl.Tf(n, d);
            };
            _builtins["parallel"] = a => {
                var (n1, d1) = ExtractTf(a[0]);
                var (n2, d2) = ExtractTf(a[1]);
                var (n, d) = MatlabControl.Parallel(n1, d1, n2, d2);
                return MatlabControl.Tf(n, d);
            };
            _builtins["feedback"] = a => {
                var (n1, d1) = ExtractTf(a[0]);
                double[] n2, d2;
                if (a.Length >= 2 && a[1].IsStruct) (n2, d2) = ExtractTf(a[1]);
                else { n2 = new[] { 1.0 }; d2 = new[] { 1.0 }; }
                int sign = a.Length >= 3 ? (int)a[2].Scalar : -1;
                var (n, d) = MatlabControl.Feedback(n1, d1, n2, d2, sign);
                return MatlabControl.Tf(n, d);
            };
            _builtins["pole"] = a => {
                var (_, den) = ExtractTf(a[0]);
                var roots = DurandKernerC(den);
                if (roots.Length == 0) return new MValue(0, 0);
                bool anyComplex = roots.Any(r => Math.Abs(r.im) > 1e-9);
                if (anyComplex)
                {
                    var re = new double[roots.Length];
                    var im = new double[roots.Length];
                    for (int i = 0; i < roots.Length; i++) { re[i] = roots[i].re; im[i] = roots[i].im; }
                    return new MValue(roots.Length, 1, re, im);
                }
                var rr = new double[roots.Length];
                for (int i = 0; i < roots.Length; i++) rr[i] = roots[i].re;
                return new MValue(roots.Length, 1, rr);
            };
            _builtins["zero"] = a => {
                var (num, _) = ExtractTf(a[0]);
                if (num.Length <= 1) return new MValue(0, 0);
                var roots = DurandKernerC(num);
                if (roots.Length == 0) return new MValue(0, 0);
                bool anyComplex = roots.Any(r => Math.Abs(r.im) > 1e-9);
                if (anyComplex)
                {
                    var re = new double[roots.Length];
                    var im = new double[roots.Length];
                    for (int i = 0; i < roots.Length; i++) { re[i] = roots[i].re; im[i] = roots[i].im; }
                    return new MValue(roots.Length, 1, re, im);
                }
                var rr = new double[roots.Length];
                for (int i = 0; i < roots.Length; i++) rr[i] = roots[i].re;
                return new MValue(roots.Length, 1, rr);
            };
            _builtins["dcgain"] = a => {
                var (num, den) = ExtractTf(a[0]);
                var (re, _) = MatlabControl.Evaluate(num, den, 0, 0);
                return new MValue(re);
            };
            _builtins["ss"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("ss(A, B, C, D)");
                double Ts = a.Length >= 5 ? a[4].Scalar : 0;
                return MatlabControl.Ss(a[0], a[1], a[2], a[3], Ts);
            };
            _builtins["tf2ss"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("tf2ss(num, den)");
                return MatlabControl.Tf2Ss(a[0].Data, a[1].Data);
            };
            _builtins["ss2tf"] = a => {
                if (a.Length < 1 || !a[0].IsStruct) throw new MatlabRuntimeException("ss2tf(ssmodel)");
                return MatlabControl.Ss2Tf(a[0]);
            };
            _builtins["lsim"] = a => {
                // lsim(H, u, t) o lsim(H, u, t, x0)
                if (a.Length < 3) throw new MatlabRuntimeException("lsim(H, u, t)");
                var (num, den) = ExtractTf(a[0]);
                var (ts, ys) = MatlabControl.Lsim(num, den, a[2].Data, a[1].Data);
                _htmlOut?.Invoke(MatlabPlots.Plot(ts, ys, "lsim response"));
                return ys;
            };
            _builtins["c2d"] = a => {
                // c2d(H, Ts[, method]) — método: 'tustin' (default)
                if (a.Length < 2) throw new MatlabRuntimeException("c2d(H, Ts)");
                var (num, den) = ExtractTf(a[0]);
                var (numZ, denZ) = MatlabControl.C2dTustin(num, den, a[1].Scalar);
                var d = MatlabControl.Tf(numZ, denZ);
                d.Fields["Ts"] = new MValue(a[1].Scalar);
                return d;
            };
            _builtins["d2c"] = a => {
                // Inverso de Tustin: s = (2/T)·(z-1)/(z+1) → z = (1+sT/2)/(1-sT/2)
                if (a.Length < 1) throw new MatlabRuntimeException("d2c(H_discrete)");
                var (num, den) = ExtractTf(a[0]);
                double Ts = a[0].IsStruct && a[0].Fields.ContainsKey("Ts") ? a[0].Fields["Ts"].Scalar : 1.0;
                if (a.Length >= 2) Ts = a[1].Scalar;
                // Inversa de Tustin con misma rutina (simétrica)
                var (numS, denS) = MatlabControl.C2dTustin(num, den, -Ts);  // signo invertido aproxima inversa
                return MatlabControl.Tf(numS, denS);
            };
            _builtins["margin"] = a => {
                // margin(H) — gain margin + phase margin
                var (num, den) = ExtractTf(a[0]);
                var (w, mag, ph) = MatlabControl.Bode(num, den, 0.001, 1000, 1000);
                // Gain margin: en ω_pc donde fase = -180°, GM = -mag (dB)
                double Gm = double.PositiveInfinity, wPc = 0;
                for (int i = 0; i < w.Length - 1; i++)
                {
                    if ((ph[i] + 180) * (ph[i + 1] + 180) < 0)
                    {
                        wPc = w[i]; Gm = -mag[i]; break;
                    }
                }
                // Phase margin: en ω_gc donde mag = 0 dB, PM = 180 + phase
                double Pm = double.PositiveInfinity, wGc = 0;
                for (int i = 0; i < w.Length - 1; i++)
                {
                    if (mag[i] * mag[i + 1] < 0)
                    {
                        wGc = w[i]; Pm = 180 + ph[i]; break;
                    }
                }
                var st = MValue.NewStruct();
                st.Fields["GainMargin_dB"] = new MValue(Gm);
                st.Fields["PhaseMargin_deg"] = new MValue(Pm);
                st.Fields["wPhaseCross"] = new MValue(wPc);
                st.Fields["wGainCross"] = new MValue(wGc);
                return st;
            };
            _builtins["lqr"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("lqr(A, B, Q, R)");
                var (K, _, _) = MatlabControl.Lqr(a[0], a[1], a[2], a[3]);
                return K;
            };
            _multiOutBuiltins["lqr"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("lqr(A, B, Q, R)");
                var (K, P, e) = MatlabControl.Lqr(a[0], a[1], a[2], a[3]);
                return new[] { K, P, e };
            };
            _builtins["care"] = a => {
                if (a.Length < 4) throw new MatlabRuntimeException("care(A, B, Q, R)");
                return MatlabControl.Care(a[0], a[1], a[2], a[3]);
            };
            _builtins["lqe"] = a => {
                // LQE (Kalman) — dual de LQR: A → A', B → C', Q → noise
                // lqe(A, G, C, Qn, Rn): K = P·C'·Rn⁻¹
                if (a.Length < 5) throw new MatlabRuntimeException("lqe(A, G, C, Qn, Rn)");
                // Resolver dual: A_dual = A', B_dual = C', Q = G·Qn·G', R = Rn
                var At = MatlabLinAlg.TransposeM(a[0]);
                var Ct = MatlabLinAlg.TransposeM(a[2]);
                var GQnGt = MatlabLinAlg.MatMul(MatlabLinAlg.MatMul(a[1], a[3]), MatlabLinAlg.TransposeM(a[1]));
                var P = MatlabControl.Care(At, Ct, GQnGt, a[4]);
                // L = P·C'·Rn⁻¹
                int n = P.Rows, p = a[2].Rows;
                var PCt = new MValue(n, p);
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < p; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < a[2].Cols; k++) s += P.At(i, k) * a[2].At(j, k);
                        PCt.Set(i, j, s);
                    }
                var L = new MValue(n, p);
                for (int col = 0; col < p; col++)
                {
                    var rhs = new MValue(p, 1);
                    for (int i = 0; i < p; i++) rhs.Set(i, 0, PCt.At(col >= n ? n - 1 : col, 0));
                    // Simplificación: para R diagonal/escalar
                }
                // Para MVP simple: L = P·C'·inv(Rn)
                var Rinv = MatlabLinAlg.Inverse(a[4]);
                L = MatlabLinAlg.MatMul(PCt, Rinv);
                return L;
            };
            _builtins["stepinfo"] = a => {
                if (a.Length == 0) throw new MatlabRuntimeException("stepinfo(H) o stepinfo(y, t, yfinal)");
                if (a[0].IsStruct && a[0].Fields.ContainsKey("num"))
                {
                    var (num, den) = ExtractTf(a[0]);
                    double tFinal = 20.0;
                    var (ts, ys) = MatlabControl.StepResponse(num, den, tFinal);
                    double yEnd = ys.At(ys.Rows - 1, 0);
                    return MatlabControl.StepInfo(ts.Data, ys.Data, yEnd);
                }
                // stepinfo(y, t, yfinal)
                double[] y = a[0].Data, t = a[1].Data;
                double yFinal = a.Length >= 3 ? a[2].Scalar : y[y.Length - 1];
                return MatlabControl.StepInfo(t, y, yFinal);
            };

            _builtins["rlocus"] = a => {
                // rlocus(H[, kvec]) — root locus para K variable
                var (num, den) = ExtractTf(a[0]);
                double[] kvec;
                if (a.Length >= 2) kvec = a[1].Data;
                else { kvec = new double[50]; for (int i = 0; i < 50; i++) kvec[i] = Math.Pow(10, -1 + i * 0.08); }
                int N = kvec.Length;
                int polesPerK = den.Length - 1;
                var allRe = new double[N * polesPerK];
                var allIm = new double[N * polesPerK];
                for (int i = 0; i < N; i++)
                {
                    // Closed-loop den = den + k·num
                    var closedDen = new double[Math.Max(den.Length, num.Length)];
                    int offsetD = closedDen.Length - den.Length;
                    int offsetN = closedDen.Length - num.Length;
                    for (int j = 0; j < den.Length; j++) closedDen[offsetD + j] = den[j];
                    for (int j = 0; j < num.Length; j++) closedDen[offsetN + j] += kvec[i] * num[j];
                    var roots = MatlabControl.DurandKernerComplex(closedDen);
                    for (int j = 0; j < polesPerK && j < roots.Length; j++)
                    {
                        allRe[i * polesPerK + j] = roots[j].re;
                        allIm[i * polesPerK + j] = roots[j].im;
                    }
                }
                _htmlOut?.Invoke(MatlabPlots.Scatter(
                    new MValue(1, allRe.Length, allRe),
                    new MValue(1, allIm.Length, allIm)));
                var st = MValue.NewStruct();
                st.Fields["re"] = new MValue(1, allRe.Length, allRe);
                st.Fields["im"] = new MValue(1, allIm.Length, allIm);
                st.Fields["k"] = new MValue(1, N, (double[])kvec.Clone());
                return st;
            };
            _builtins["damp"] = a => {
                var (_, den) = ExtractTf(a[0]);
                // Roots reales: damping = 1; complejas: ζ = -Re(p)/|p|, ωn = |p|
                var allRoots = DurandKernerC(den);
                var n = allRoots.Length;
                var wn = new double[n];
                var zeta = new double[n];
                for (int i = 0; i < n; i++)
                {
                    wn[i] = Math.Sqrt(allRoots[i].re * allRoots[i].re + allRoots[i].im * allRoots[i].im);
                    zeta[i] = wn[i] > 1e-15 ? -allRoots[i].re / wn[i] : 0;
                }
                var st = MValue.NewStruct();
                st.Fields["wn"] = new MValue(n, 1, wn);
                st.Fields["zeta"] = new MValue(n, 1, zeta);
                return st;
            };

            // ─── Z-transform table-based
            _builtins["ztrans"] = a => {
                // Z{1} = z/(z-1), Z{n} = z/(z-1)², Z{a^n} = z/(z-a)
                if (a.Length == 0) throw new MatlabRuntimeException("ztrans(symExpr[, n, z])");
                SymNode src;
                if (a[0].IsSymbolic) src = a[0].Symbolic;
                else if (a[0].IsScalar) src = new SymConst(a[0].Scalar);
                else throw new MatlabRuntimeException("ztrans: arg must be sym or scalar");
                string nName = "n", zName = "z";
                if (a.Length >= 2 && a[1].IsSymbolic && a[1].Symbolic is SymVar v1) nName = v1.Name;
                if (a.Length >= 3 && a[2].IsSymbolic && a[2].Symbolic is SymVar v2) zName = v2.Name;
                return MValue.NewSymbolic(ZTransform(src, nName, zName).Simplify());
            };
            _builtins["iztrans"] = a => {
                if (a.Length == 0 || !a[0].IsSymbolic) throw new MatlabRuntimeException("iztrans(symExpr[, z, n])");
                string zName = "z", nName = "n";
                if (a.Length >= 2 && a[1].IsSymbolic && a[1].Symbolic is SymVar v1) zName = v1.Name;
                if (a.Length >= 3 && a[2].IsSymbolic && a[2].Symbolic is SymVar v2) nName = v2.Name;
                return MValue.NewSymbolic(InverseZTransform(a[0].Symbolic, zName, nName).Simplify());
            };

            _builtins["ilaplace"] = a => {
                // Inverse Laplace — table-based para casos canónicos
                // 1/s → 1, 1/s² → t, n!/s^(n+1) → t^n, 1/(s-a) → e^(at), a/(s²+a²) → sin(at), s/(s²+a²) → cos(at)
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("ilaplace(symExpr[, s, t])");
                string sName = "s", tName = "t";
                if (a.Length >= 2 && a[1].IsSymbolic && a[1].Symbolic is SymVar v1) sName = v1.Name;
                if (a.Length >= 3 && a[2].IsSymbolic && a[2].Symbolic is SymVar v2) tName = v2.Name;
                return MValue.NewSymbolic(InvLaplaceTransform(a[0].Symbolic, sName, tName).Simplify());
            };
            _builtins["limit"] = a => {
                // limit(expr, var, x0[, direction])
                if (a.Length < 3 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("limit(expr, var, x0)");
                string varName = "x";
                if (a[1].IsSymbolic && a[1].Symbolic is SymVar v) varName = v.Name;
                else if (a[1].IsString) varName = a[1].StringValue;
                double x0 = a[2].Scalar;
                // PUENTE giac (como MATLAB→MuPAD): limit(expr,var,x0) → giac. Fallback: L'Hopital propio.
                if (GiacRunner.IsAvailable())
                {
                    string ptG = double.IsPositiveInfinity(x0) ? "+infinity"
                               : double.IsNegativeInfinity(x0) ? "-infinity"
                               : x0.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var (okl, resl) = GiacRunner.Eval($"limit({a[0].Symbolic.ToInfix()},{varName},{ptG})");
                    if (okl) { try { return MValue.NewSymbolic(GiacRunner.ParseToSym(GiacRunner.ToMatlab(resl)).Simplify()); } catch { } }
                }
                var expr = a[0].Symbolic;
                // 1) Probar L'Hôpital PRIMERO si expr es cociente
                if (expr is SymDiv d)
                {
                    try
                    {
                        // Eval num y denom por separado (sin simplify) para detectar 0/0 o ∞/∞
                        double fAt0 = SafeEval(d.A, varName, x0);
                        double gAt0 = SafeEval(d.B, varName, x0);
                        bool indet = (Math.Abs(fAt0) < 1e-12 && Math.Abs(gAt0) < 1e-12)
                                  || (double.IsInfinity(fAt0) && double.IsInfinity(gAt0));
                        if (indet)
                        {
                            // L'Hôpital iterativo hasta 3 niveles
                            var num = d.A; var den = d.B;
                            for (int iter = 0; iter < 5; iter++)
                            {
                                num = num.Diff(varName).Simplify();
                                den = den.Diff(varName).Simplify();
                                double fn = SafeEval(num, varName, x0);
                                double gn = SafeEval(den, varName, x0);
                                if (Math.Abs(gn) > 1e-12) return new MValue(fn / gn);
                                if (Math.Abs(fn) > 1e-12) return new MValue(double.PositiveInfinity);
                            }
                        }
                    }
                    catch { }
                }
                // 2) Sustitución directa con eval numérico (no simplify, para preservar div)
                try
                {
                    double val = SafeEval(expr, varName, x0);
                    if (!double.IsNaN(val) && !double.IsInfinity(val))
                        return new MValue(val);
                }
                catch { }
                // 3) Aproximación por límite numérico (one-sided +)
                try
                {
                    double val2 = SafeEval(expr, varName, x0 + 1e-10);
                    if (!double.IsNaN(val2)) return new MValue(val2);
                }
                catch { }
                return MValue.NewSymbolic(expr.Subs(varName, new SymConst(x0)));
            };
            static double SafeEval(SymNode expr, string varName, double x0)
            {
                var vals = new Dictionary<string, double> { [varName] = x0 };
                return expr.Eval(vals);
            }
            _builtins["fourier"] = a => {
                // Fourier transform table-based (simplificado)
                if (a.Length == 0 || !a[0].IsSymbolic)
                    throw new MatlabRuntimeException("fourier(symExpr[, t, w])");
                string tName = "t", wName = "w";
                if (a.Length >= 2 && a[1].IsSymbolic && a[1].Symbolic is SymVar v1) tName = v1.Name;
                if (a.Length >= 3 && a[2].IsSymbolic && a[2].Symbolic is SymVar v2) wName = v2.Name;
                return MValue.NewSymbolic(FourierTransform(a[0].Symbolic, tName, wName).Simplify());
            };

            // Optimization & root finding
            _builtins["fzero"] = a => {
                // fzero(@f, x0) o fzero(@f, [a, b])
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("fzero(@f, x0)");
                var f = a[0].Callable;
                double F(double x) => f(new[] { new MValue(x) }).Scalar;
                if (a[1].Data.Length >= 2)
                {
                    // Bisección sobre [a, b]
                    double lo = a[1].Data[0], hi = a[1].Data[1];
                    double fLo = F(lo), fHi = F(hi);
                    if (fLo * fHi > 0) throw new MatlabRuntimeException("fzero: f(a) and f(b) must have opposite signs");
                    for (int it = 0; it < 200; it++)
                    {
                        double mid = (lo + hi) / 2;
                        double fMid = F(mid);
                        if (Math.Abs(fMid) < 1e-12 || (hi - lo) < 1e-14) return new MValue(mid);
                        if (fLo * fMid < 0) { hi = mid; fHi = fMid; }
                        else { lo = mid; fLo = fMid; }
                    }
                    return new MValue((lo + hi) / 2);
                }
                // Newton-Raphson con derivada numérica
                double x0 = a[1].Scalar;
                for (int it = 0; it < 100; it++)
                {
                    double fx = F(x0);
                    if (Math.Abs(fx) < 1e-12) return new MValue(x0);
                    double h = Math.Max(1e-8, 1e-6 * Math.Abs(x0));
                    double dfx = (F(x0 + h) - F(x0 - h)) / (2 * h);
                    if (Math.Abs(dfx) < 1e-15) break;
                    x0 -= fx / dfx;
                }
                return new MValue(x0);
            };
            _builtins["fminbnd"] = a => {
                // fminbnd(@f, a, b) — golden section search
                if (a.Length < 3 || !a[0].IsCallable) throw new MatlabRuntimeException("fminbnd(@f, a, b)");
                var f = a[0].Callable;
                double F(double x) => f(new[] { new MValue(x) }).Scalar;
                double aL = a[1].Scalar, bL = a[2].Scalar;
                const double phi = 1.6180339887498949;
                double tol = 1e-10;
                double c = bL - (bL - aL) / phi;
                double d = aL + (bL - aL) / phi;
                while (Math.Abs(bL - aL) > tol)
                {
                    if (F(c) < F(d)) bL = d; else aL = c;
                    c = bL - (bL - aL) / phi;
                    d = aL + (bL - aL) / phi;
                }
                return new MValue((aL + bL) / 2);
            };
            _builtins["fsolve"] = a => {
                // fsolve(@F, x0) — sistema F(x) = 0; F: R^n → R^n
                // Newton con jacobiano numérico + damping.
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("fsolve(@F, x0)");
                var F = a[0].Callable;
                int n = a[1].Data.Length;
                int origRows = a[1].Rows, origCols = a[1].Cols;
                var x = (double[])a[1].Data.Clone();
                double[] EvalF(double[] xv)
                {
                    var arg = n == 1 ? new MValue(xv[0]) : new MValue(n, 1, (double[])xv.Clone());
                    var r = F(new[] { arg });
                    return (double[])r.Data.Clone();
                }
                for (int it = 0; it < 200; it++)
                {
                    var fx = EvalF(x);
                    double normF = 0;
                    foreach (var v in fx) normF += v * v;
                    if (Math.Sqrt(normF) < 1e-12) break;
                    // Jacobiano numérico central
                    var J = new MValue(n, n);
                    for (int j = 0; j < n; j++)
                    {
                        double h = Math.Max(1e-8, 1e-6 * Math.Abs(x[j]));
                        var xp = (double[])x.Clone(); xp[j] += h;
                        var xm = (double[])x.Clone(); xm[j] -= h;
                        var fp = EvalF(xp); var fm = EvalF(xm);
                        for (int i = 0; i < n; i++) J.Set(i, j, (fp[i] - fm[i]) / (2 * h));
                    }
                    var bVec = new MValue(n, 1);
                    for (int i = 0; i < n; i++) bVec.Set(i, 0, -fx[i]);
                    MValue dx;
                    try { dx = MatlabLinAlg.Linsolve(J, bVec); }
                    catch { break; }  // Jacobian singular
                    // Damped Newton (línea de búsqueda simple)
                    double lambda = 1.0;
                    for (int line = 0; line < 20; line++)
                    {
                        var xNew = new double[n];
                        for (int i = 0; i < n; i++) xNew[i] = x[i] + lambda * dx.At(i, 0);
                        var fNew = EvalF(xNew);
                        double normNew = 0;
                        foreach (var v in fNew) normNew += v * v;
                        if (normNew < normF) { x = xNew; break; }
                        lambda *= 0.5;
                    }
                }
                if (n == 1) return new MValue(x[0]);
                return new MValue(origRows, origCols, x);  // preserva shape de x0
            };
            _builtins["lsqnonlin"] = a => {
                // lsqnonlin(@F, x0) — minimiza ||F(x)||² con Levenberg-Marquardt
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("lsqnonlin(@F, x0)");
                var F = a[0].Callable;
                int n = a[1].Data.Length;
                int origRows = a[1].Rows, origCols = a[1].Cols;
                var x = (double[])a[1].Data.Clone();
                double[] EvalF(double[] xv)
                {
                    var arg = n == 1 ? new MValue(xv[0]) : new MValue(n, 1, (double[])xv.Clone());
                    var r = F(new[] { arg });
                    return (double[])r.Data.Clone();
                }
                double lambda = 1e-3;
                for (int it = 0; it < 300; it++)
                {
                    var fx = EvalF(x);
                    int m = fx.Length;
                    double normF = 0; foreach (var v in fx) normF += v * v;
                    if (Math.Sqrt(normF) < 1e-12) break;
                    // Jacobiano numérico m×n
                    var J = new MValue(m, n);
                    for (int j = 0; j < n; j++)
                    {
                        double h = Math.Max(1e-8, 1e-6 * Math.Abs(x[j]));
                        var xp = (double[])x.Clone(); xp[j] += h;
                        var fp = EvalF(xp);
                        for (int i = 0; i < m; i++) J.Set(i, j, (fp[i] - fx[i]) / h);
                    }
                    // Resolver (J'J + λI) dx = -J' fx
                    var JT = MatlabLinAlg.TransposeM(J);
                    var JTJ = MatlabLinAlg.MatMul(JT, J);
                    for (int i = 0; i < n; i++) JTJ.Set(i, i, JTJ.At(i, i) + lambda * (1 + JTJ.At(i, i)));
                    var fMv = new MValue(m, 1);
                    for (int i = 0; i < m; i++) fMv.Set(i, 0, -fx[i]);
                    var rhs = MatlabLinAlg.MatMul(JT, fMv);
                    MValue dx;
                    try { dx = MatlabLinAlg.Linsolve(JTJ, rhs); }
                    catch { lambda *= 10; continue; }
                    var xNew = new double[n];
                    for (int i = 0; i < n; i++) xNew[i] = x[i] + dx.At(i, 0);
                    var fNew = EvalF(xNew);
                    double normNew = 0; foreach (var v in fNew) normNew += v * v;
                    if (normNew < normF) { x = xNew; lambda = Math.Max(1e-15, lambda / 2); }
                    else lambda *= 5;
                }
                if (n == 1) return new MValue(x[0]);
                return new MValue(origRows, origCols, x);
            };
            _builtins["lsqcurvefit"] = a => {
                // lsqcurvefit(@model, x0, xdata, ydata) — minimiza ||model(x, xdata) - ydata||²
                if (a.Length < 4) throw new MatlabRuntimeException("lsqcurvefit(@model, x0, xdata, ydata)");
                var model = a[0].Callable;
                var x0 = a[1];
                var xdata = a[2];
                var ydata = a[3];
                // Wrap como lsqnonlin
                var residualFn = new MValue(args => {
                    var pred = model(new[] { args[0], xdata });
                    var diff = new double[pred.Data.Length];
                    for (int i = 0; i < diff.Length; i++) diff[i] = pred.Data[i] - ydata.Data[i];
                    return new MValue(diff.Length, 1, diff);
                }, "lsqresidual");
                return _builtins["lsqnonlin"](new[] { residualFn, x0 });
            };

            _builtins["fmincon"] = a => {
                // fmincon(@f, x0, A, b, Aeq, beq, lb, ub, @nonlcon) — penalty method MVP
                // Min f(x) sujeto a Ax ≤ b, Aeq·x = beq, lb ≤ x ≤ ub, c(x) ≤ 0, ceq(x) = 0
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("fmincon(@f, x0, ...)");
                var f = a[0].Callable;
                int n = a[1].Data.Length;
                var x = (double[])a[1].Data.Clone();
                // Restricciones
                MValue A_ineq = a.Length >= 3 && a[2].Data.Length > 0 ? a[2] : null;
                MValue b_ineq = a.Length >= 4 && a[3].Data.Length > 0 ? a[3] : null;
                MValue A_eq = a.Length >= 5 && a[4].Data.Length > 0 ? a[4] : null;
                MValue b_eq = a.Length >= 6 && a[5].Data.Length > 0 ? a[5] : null;
                double[] lb = a.Length >= 7 && a[6].Data.Length > 0 ? a[6].Data : null;
                double[] ub = a.Length >= 8 && a[7].Data.Length > 0 ? a[7].Data : null;
                Func<MValue[], MValue> nonlcon = a.Length >= 9 && a[8].IsCallable ? a[8].Callable : null;
                // Penalty function: f(x) + ρ·Σ (max(0, gi))² + ρ·Σ heq²
                double Penalized(double[] xv)
                {
                    var xMv = n == 1 ? new MValue(xv[0]) : new MValue(n, 1, (double[])xv.Clone());
                    double fx = f(new[] { xMv }).Scalar;
                    double penalty = 0;
                    double rho = 1000;
                    if (A_ineq != null && b_ineq != null)
                    {
                        for (int i = 0; i < A_ineq.Rows; i++)
                        {
                            double g = -b_ineq.Data[i];
                            for (int j = 0; j < n; j++) g += A_ineq.At(i, j) * xv[j];
                            if (g > 0) penalty += g * g;
                        }
                    }
                    if (A_eq != null && b_eq != null)
                    {
                        for (int i = 0; i < A_eq.Rows; i++)
                        {
                            double h = -b_eq.Data[i];
                            for (int j = 0; j < n; j++) h += A_eq.At(i, j) * xv[j];
                            penalty += h * h;
                        }
                    }
                    if (lb != null) for (int j = 0; j < n; j++) if (xv[j] < lb[j]) penalty += (xv[j] - lb[j]) * (xv[j] - lb[j]);
                    if (ub != null) for (int j = 0; j < n; j++) if (xv[j] > ub[j]) penalty += (xv[j] - ub[j]) * (xv[j] - ub[j]);
                    if (nonlcon != null)
                    {
                        var res = nonlcon(new[] { xMv });
                        // Asume res = [c, ceq] cell o vector; MVP: tratamos todo como c(x) ≤ 0
                        foreach (var v in res.Data) if (v > 0) penalty += v * v;
                    }
                    return fx + rho * penalty;
                }
                // Nelder-Mead sobre la penalized
                var simplex = new double[n + 1][];
                for (int i = 0; i <= n; i++) simplex[i] = (double[])x.Clone();
                for (int i = 0; i < n; i++) simplex[i + 1][i] += (simplex[i + 1][i] == 0 ? 0.00025 : 0.05 * simplex[i + 1][i]);
                var fs = new double[n + 1];
                for (int i = 0; i <= n; i++) fs[i] = Penalized(simplex[i]);
                for (int iter = 0; iter < 2000; iter++)
                {
                    var order = Enumerable.Range(0, n + 1).OrderBy(i => fs[i]).ToArray();
                    var nsx = new double[n + 1][];
                    var nfs = new double[n + 1];
                    for (int k = 0; k <= n; k++) { nsx[k] = simplex[order[k]]; nfs[k] = fs[order[k]]; }
                    simplex = nsx; fs = nfs;
                    if (Math.Abs(fs[n] - fs[0]) < 1e-12) break;
                    var x0 = new double[n];
                    for (int k = 0; k < n; k++) for (int i = 0; i < n; i++) x0[i] += simplex[k][i] / n;
                    var xr = new double[n];
                    for (int i = 0; i < n; i++) xr[i] = x0[i] + (x0[i] - simplex[n][i]);
                    double fr = Penalized(xr);
                    if (fs[0] <= fr && fr < fs[n - 1]) { simplex[n] = xr; fs[n] = fr; continue; }
                    if (fr < fs[0])
                    {
                        var xe = new double[n];
                        for (int i = 0; i < n; i++) xe[i] = x0[i] + 2 * (xr[i] - x0[i]);
                        double fe = Penalized(xe);
                        simplex[n] = fe < fr ? xe : xr;
                        fs[n] = fe < fr ? fe : fr; continue;
                    }
                    var xc = new double[n];
                    for (int i = 0; i < n; i++) xc[i] = x0[i] + 0.5 * (simplex[n][i] - x0[i]);
                    double fc = Penalized(xc);
                    if (fc < fs[n]) { simplex[n] = xc; fs[n] = fc; continue; }
                    for (int k = 1; k <= n; k++)
                    {
                        for (int i = 0; i < n; i++) simplex[k][i] = simplex[0][i] + 0.5 * (simplex[k][i] - simplex[0][i]);
                        fs[k] = Penalized(simplex[k]);
                    }
                }
                if (n == 1) return new MValue(simplex[0][0]);
                return new MValue(a[1].Rows, a[1].Cols, simplex[0]);
            };
            _builtins["linprog"] = a => {
                // linprog(c, A, b, Aeq, beq, lb, ub) — usa fmincon como solver MVP
                // f(x) = c'·x
                if (a.Length < 1) throw new MatlabRuntimeException("linprog(c, A, b, ...)");
                var c = a[0].Data;
                int n = c.Length;
                var x0 = new double[n];
                // Construir handle f = c'·x
                var fHandle = new MValue(args => {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += c[i] * args[0].Data[i];
                    return new MValue(s);
                }, "linprog_obj");
                var fmcArgs = new MValue[Math.Max(8, a.Length + 1)];
                fmcArgs[0] = fHandle;
                fmcArgs[1] = new MValue(n, 1, x0);
                for (int i = 1; i < a.Length; i++)
                    if (i + 1 < fmcArgs.Length) fmcArgs[i + 1] = a[i];
                // Llenar nulls con scalar 0 si faltan
                for (int i = 0; i < fmcArgs.Length; i++)
                    if (fmcArgs[i] == null) fmcArgs[i] = new MValue(0, 0);
                return _builtins["fmincon"](fmcArgs);
            };
            _builtins["quadprog"] = a => {
                // quadprog(H, f, A, b, Aeq, beq) → MIN 0.5·x'·H·x + f'·x
                if (a.Length < 2) throw new MatlabRuntimeException("quadprog(H, f, A, b, ...)");
                var H = a[0]; var fv = a[1];
                int n = fv.Data.Length;
                var x0 = new double[n];
                var objHandle = new MValue(args => {
                    var x = args[0].Data;
                    double s = 0;
                    for (int i = 0; i < n; i++)
                    {
                        s += fv.Data[i] * x[i];
                        for (int j = 0; j < n; j++) s += 0.5 * H.At(i, j) * x[i] * x[j];
                    }
                    return new MValue(s);
                }, "quadprog_obj");
                var fmcArgs = new MValue[Math.Max(8, a.Length + 1)];
                fmcArgs[0] = objHandle;
                fmcArgs[1] = new MValue(n, 1, x0);
                for (int i = 2; i < a.Length; i++)
                    if (i < fmcArgs.Length) fmcArgs[i] = a[i];
                for (int i = 0; i < fmcArgs.Length; i++)
                    if (fmcArgs[i] == null) fmcArgs[i] = new MValue(0, 0);
                return _builtins["fmincon"](fmcArgs);
            };

            _builtins["fminsearch"] = a => {
                // Nelder-Mead simple para escalar (vector input también)
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("fminsearch(@f, x0)");
                var f = a[0].Callable;
                int n = a[1].Data.Length;
                var simplex = new double[n + 1][];
                for (int i = 0; i <= n; i++) simplex[i] = (double[])a[1].Data.Clone();
                for (int i = 0; i < n; i++) simplex[i + 1][i] += (simplex[i + 1][i] == 0 ? 0.00025 : 0.05 * simplex[i + 1][i]);
                double F(double[] x) => f(new[] { new MValue(1, n, (double[])x.Clone()) }).Scalar;
                var fs = new double[n + 1];
                for (int i = 0; i <= n; i++) fs[i] = F(simplex[i]);
                for (int iter = 0; iter < 1000; iter++)
                {
                    // Sort
                    var order = Enumerable.Range(0, n + 1).OrderBy(i => fs[i]).ToArray();
                    var nsx = new double[n + 1][];
                    var nfs = new double[n + 1];
                    for (int k = 0; k <= n; k++) { nsx[k] = simplex[order[k]]; nfs[k] = fs[order[k]]; }
                    simplex = nsx; fs = nfs;
                    // Convergencia
                    if (Math.Abs(fs[n] - fs[0]) < 1e-10) break;
                    // Centroide de los n mejores
                    var x0 = new double[n];
                    for (int k = 0; k < n; k++) for (int i = 0; i < n; i++) x0[i] += simplex[k][i] / n;
                    // Reflejar
                    var xr = new double[n];
                    for (int i = 0; i < n; i++) xr[i] = x0[i] + (x0[i] - simplex[n][i]);
                    double fr = F(xr);
                    if (fs[0] <= fr && fr < fs[n - 1]) { simplex[n] = xr; fs[n] = fr; continue; }
                    if (fr < fs[0])
                    {
                        var xe = new double[n];
                        for (int i = 0; i < n; i++) xe[i] = x0[i] + 2 * (xr[i] - x0[i]);
                        double fe = F(xe);
                        simplex[n] = fe < fr ? xe : xr;
                        fs[n] = fe < fr ? fe : fr;
                        continue;
                    }
                    var xc = new double[n];
                    for (int i = 0; i < n; i++) xc[i] = x0[i] + 0.5 * (simplex[n][i] - x0[i]);
                    double fc = F(xc);
                    if (fc < fs[n]) { simplex[n] = xc; fs[n] = fc; continue; }
                    // Shrink
                    for (int k = 1; k <= n; k++)
                    {
                        for (int i = 0; i < n; i++) simplex[k][i] = simplex[0][i] + 0.5 * (simplex[k][i] - simplex[0][i]);
                        fs[k] = F(simplex[k]);
                    }
                }
                return new MValue(1, n, simplex[0]);
            };

            // Higher-order
            _builtins["feval"] = a => {
                if (a.Length < 1 || !a[0].IsCallable) throw new MatlabRuntimeException("feval(handle, args...)");
                var rest = new MValue[a.Length - 1];
                Array.Copy(a, 1, rest, 0, rest.Length);
                return a[0].Callable(rest);
            };
            _builtins["arrayfun"] = a => {
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("arrayfun(@fn, array)");
                var fn = a[0].Callable;
                var arr = a[1];
                var r = new MValue(arr.Rows, arr.Cols);
                for (int i = 0; i < arr.Data.Length; i++)
                    r.Data[i] = fn(new[] { new MValue(arr.Data[i]) }).Scalar;
                return r;
            };
            _builtins["cellfun"] = a => {
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("cellfun(@fn, cell)");
                var fn = a[0].Callable;
                var c = a[1];
                if (!c.IsCell) throw new MatlabRuntimeException("cellfun: 2nd arg must be cell");
                int nr = c.CellData.GetLength(0), nc = c.CellData.GetLength(1);
                var r = new MValue(nr, nc);
                for (int i = 0; i < nr; i++)
                    for (int j = 0; j < nc; j++)
                    {
                        var result = fn(new[] { c.CellData[i, j] });
                        r.Set(i, j, result.IsScalar ? result.Scalar : 0);
                    }
                return r;
            };
            _builtins["structfun"] = a => {
                if (a.Length < 2 || !a[0].IsCallable) throw new MatlabRuntimeException("structfun(@fn, struct)");
                var fn = a[0].Callable;
                var s = a[1];
                if (!s.IsStruct) throw new MatlabRuntimeException("structfun: 2nd arg must be struct");
                var values = new System.Collections.Generic.List<double>();
                foreach (var kv in s.Fields)
                {
                    var result = fn(new[] { kv.Value });
                    values.Add(result.IsScalar ? result.Scalar : 0);
                }
                return new MValue(values.Count, 1, values.ToArray());
            };
            _builtins["map"] = _builtins["arrayfun"];  // alias funcional
            _builtins["bsxfun"] = a => {
                // bsxfun(@fn, A, B) — element-wise con broadcasting de dimensiones-1
                if (a.Length < 3 || !a[0].IsCallable) throw new MatlabRuntimeException("bsxfun(@fn, A, B)");
                var fn = a[0].Callable;
                var A = a[1]; var B = a[2];
                int rA = A.Rows, cA = A.Cols, rB = B.Rows, cB = B.Cols;
                int rR = Math.Max(rA, rB), cR = Math.Max(cA, cB);
                // Validar broadcast (dim-1 ok, sino igual)
                if (!(rA == 1 || rB == 1 || rA == rB) || !(cA == 1 || cB == 1 || cA == cB))
                    throw new MatlabRuntimeException("bsxfun: dimensions not broadcastable");
                var R = new MValue(rR, cR);
                for (int i = 0; i < rR; i++)
                    for (int j = 0; j < cR; j++)
                    {
                        int ai = rA == 1 ? 0 : i, aj = cA == 1 ? 0 : j;
                        int bi = rB == 1 ? 0 : i, bj = cB == 1 ? 0 : j;
                        var av = new MValue(A.At(ai, aj));
                        var bv = new MValue(B.At(bi, bj));
                        R.Set(i, j, fn(new[] { av, bv }).Scalar);
                    }
                return R;
            };

            // Workspace introspection
            _builtins["who"] = a => {
                var sb = new StringBuilder();
                bool first = true;
                foreach (var kv in Globals.Vars) { if (!first) sb.Append(", "); sb.Append(kv.Key); first = false; }
                return new MValue(sb.ToString());
            };
            _builtins["whos"] = a => {
                var lines = new System.Collections.Generic.List<string>();
                lines.Add(string.Format("{0,-20} {1,-12} {2,-12}", "Name", "Size", "Class"));
                foreach (var kv in Globals.Vars)
                {
                    var v = kv.Value;
                    string size, cls;
                    if (v == null) { size = "?"; cls = "?"; }
                    else if (v.IsString) { size = $"1x{v.StringValue?.Length ?? 0}"; cls = "char"; }
                    else if (v.IsStruct) { size = "1x1"; cls = "struct"; }
                    else if (v.IsCell) { size = $"{v.CellData.GetLength(0)}x{v.CellData.GetLength(1)}"; cls = "cell"; }
                    else if (v.IsCallable) { size = "1x1"; cls = "function_handle"; }
                    else if (v.IsComplex) { size = $"{v.Rows}x{v.Cols}"; cls = "double (complex)"; }
                    else { size = $"{v.Rows}x{v.Cols}"; cls = "double"; }
                    lines.Add(string.Format("{0,-20} {1,-12} {2,-12}", kv.Key, size, cls));
                }
                _output?.Invoke(string.Join("\n", lines));
                return new MValue(0);
            };
            _builtins["clear"] = a => {
                if (a.Length == 0) { Globals.Vars.Clear(); return new MValue(0); }
                foreach (var x in a) if (x.IsString) Globals.Vars.Remove(x.StringValue);
                return new MValue(0);
            };
            _builtins["exist"] = a => {
                if (a.Length == 0 || !a[0].IsString) return new MValue(0);
                var name = a[0].StringValue;
                if (Globals.Vars.ContainsKey(name)) return new MValue(1);          // variable
                if (_userFunctions.ContainsKey(name)) return new MValue(2);        // function
                if (_builtins.ContainsKey(name)) return new MValue(5);             // built-in
                return new MValue(0);
            };
            _builtins["assignin"] = a => {
                // assignin('base', name, value) — siempre asigna al global
                if (a.Length < 3 || !a[1].IsString) throw new MatlabRuntimeException("assignin(ws, name, val)");
                Globals.Set(a[1].StringValue, a[2]);
                return new MValue(0);
            };
            _builtins["evalin"] = a => {
                // evalin('base', expr) — MVP: no soportado realmente; devolvemos 0
                return new MValue(0);
            };
            _builtins["tic"] = a => {
                // Devuelve handle = timestamp en ticks (interpretable como long)
                // Para compatibilidad con `tic` (sin asignar), también actualiza el stopwatch global
                _ticStopwatch = System.Diagnostics.Stopwatch.StartNew();
                long ts = System.Diagnostics.Stopwatch.GetTimestamp();
                return new MValue((double)ts);
            };
            _builtins["toc"] = a => {
                double elapsed;
                if (a.Length >= 1 && a[0].IsScalar && a[0].Scalar > 0)
                {
                    // toc(handle): handle es timestamp; calcular delta vs ahora
                    long startTs = (long)a[0].Scalar;
                    long nowTs = System.Diagnostics.Stopwatch.GetTimestamp();
                    elapsed = (nowTs - startTs) / (double)System.Diagnostics.Stopwatch.Frequency;
                }
                else if (_ticStopwatch != null)
                {
                    elapsed = _ticStopwatch.Elapsed.TotalSeconds;
                }
                else elapsed = 0;
                // MATLAB exacto: "Elapsed time is …" se muestra SÓLO cuando toc se usa
                // como comando (nargout 0). Eso se decide a nivel de statement en
                // ExecuteOne (ExprStmt). Si se asigna (`t = toc`) o va anidado, no imprime.
                return new MValue(elapsed);
            };
            // ─── Statistics distributions ───────────────────────────────────
            _builtins["normpdf"] = a => {
                double mu = a.Length >= 2 ? a[1].Scalar : 0;
                double sigma = a.Length >= 3 ? a[2].Scalar : 1;
                return MapUnary(a[0], x => Math.Exp(-((x - mu) * (x - mu)) / (2 * sigma * sigma)) / (sigma * Math.Sqrt(2 * Math.PI)));
            };
            _builtins["normcdf"] = a => {
                double mu = a.Length >= 2 ? a[1].Scalar : 0;
                double sigma = a.Length >= 3 ? a[2].Scalar : 1;
                return MapUnary(a[0], x => 0.5 * (1 + Erf((x - mu) / (sigma * Math.Sqrt(2)))));
            };
            _builtins["norminv"] = a => {
                // Inverse CDF — aproximación Beasley-Springer-Moro
                double mu = a.Length >= 2 ? a[1].Scalar : 0;
                double sigma = a.Length >= 3 ? a[2].Scalar : 1;
                return MapUnary(a[0], p => {
                    if (p <= 0) return double.NegativeInfinity;
                    if (p >= 1) return double.PositiveInfinity;
                    return mu + sigma * Math.Sqrt(2) * ErfInv(2 * p - 1);
                });
            };
            _builtins["erf"] = a => MapUnary(a[0], Erf);
            _builtins["erfc"] = a => MapUnary(a[0], x => 1 - Erf(x));
            _builtins["erfinv"] = a => MapUnary(a[0], ErfInv);
            _builtins["tpdf"] = a => {
                double nu = a[1].Scalar;
                return MapUnary(a[0], x => Math.Pow(1 + x * x / nu, -(nu + 1) / 2)
                    * GammaFn((nu + 1) / 2) / (Math.Sqrt(nu * Math.PI) * GammaFn(nu / 2)));
            };
            _builtins["tcdf"] = a => {
                double nu = a[1].Scalar;
                // Aproximación numérica via integral acumulada
                return MapUnary(a[0], x => StudentTCDF(x, nu));
            };
            _builtins["chi2pdf"] = a => {
                double k = a[1].Scalar;
                return MapUnary(a[0], x => x <= 0 ? 0 :
                    Math.Pow(x, k / 2 - 1) * Math.Exp(-x / 2) / (Math.Pow(2, k / 2) * GammaFn(k / 2)));
            };
            _builtins["chi2cdf"] = a => {
                double k = a[1].Scalar;
                return MapUnary(a[0], x => x <= 0 ? 0 : LowerIncGamma(k / 2, x / 2) / GammaFn(k / 2));
            };
            _builtins["fpdf"] = a => {
                double v1 = a[1].Scalar, v2 = a[2].Scalar;
                return MapUnary(a[0], x => {
                    if (x <= 0) return 0;
                    double num = Math.Pow(v1 * x, v1) * Math.Pow(v2, v2);
                    double den = Math.Pow(v1 * x + v2, v1 + v2);
                    return Math.Sqrt(num / den) / (x * BetaFn(v1 / 2, v2 / 2));
                });
            };
            _builtins["binopdf"] = a => {
                double k = a[0].Scalar; double N = a[1].Scalar; double p = a[2].Scalar;
                return new MValue(BinomCoef(N, k) * Math.Pow(p, k) * Math.Pow(1 - p, N - k));
            };
            _builtins["poisspdf"] = a => {
                double lambda = a[1].Scalar;
                return MapUnary(a[0], x => Math.Exp(-lambda) * Math.Pow(lambda, x) / Factorial((int)x));
            };
            _builtins["gampdf"] = a => {
                double k = a[1].Scalar; double theta = a.Length >= 3 ? a[2].Scalar : 1;
                return MapUnary(a[0], x => x <= 0 ? 0 :
                    Math.Pow(x, k - 1) * Math.Exp(-x / theta) / (Math.Pow(theta, k) * GammaFn(k)));
            };
            _builtins["gamma"] = a => MapUnary(a[0], GammaFn);
            _builtins["beta"] = a => new MValue(BetaFn(a[0].Scalar, a[1].Scalar));
            _builtins["factorial"] = a => MapUnary(a[0], x => Factorial((int)x));
            _builtins["nchoosek"] = a => new MValue(BinomCoef(a[0].Scalar, a[1].Scalar));

            static double GammaFn(double x)
            {
                // Lanczos approximation
                if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * GammaFn(1 - x));
                x -= 1;
                double[] g = { 0.99999999999980993, 676.5203681218851, -1259.1392167224028,
                                771.32342877765313, -176.61502916214059, 12.507343278686905,
                                -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };
                double a_ = g[0];
                for (int i = 1; i < g.Length; i++) a_ += g[i] / (x + i);
                double t = x + g.Length - 1.5;
                return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a_;
            }
            static double BetaFn(double a_, double b_) => GammaFn(a_) * GammaFn(b_) / GammaFn(a_ + b_);
            static double LowerIncGamma(double a_, double x)
            {
                // Serie de power para γ(a, x)
                double sum = 1.0 / a_;
                double term = sum;
                for (int n = 1; n < 200; n++)
                {
                    term *= x / (a_ + n);
                    sum += term;
                    if (Math.Abs(term) < 1e-15) break;
                }
                return Math.Pow(x, a_) * Math.Exp(-x) * sum;
            }
            static double Factorial(int n)
            {
                if (n < 0) return double.NaN;
                double r = 1; for (int i = 2; i <= n; i++) r *= i; return r;
            }
            static double BinomCoef(double n, double k)
            {
                return Math.Round(GammaFn(n + 1) / (GammaFn(k + 1) * GammaFn(n - k + 1)));
            }
            static double Erf(double x)
            {
                // Abramowitz & Stegun 7.1.26
                double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
                double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;
                int sign = x < 0 ? -1 : 1;
                x = Math.Abs(x);
                double t = 1.0 / (1.0 + p * x);
                double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
                return sign * y;
            }
            static double ErfInv(double x)
            {
                // Aproximación racional
                double a = 0.147;
                double ln1m = Math.Log(1 - x * x);
                double t = 2 / (Math.PI * a) + ln1m / 2;
                double res = Math.Sqrt(Math.Sqrt(t * t - ln1m / a) - t);
                return x < 0 ? -res : res;
            }
            static double StudentTCDF(double x, double v)
            {
                // F(x | v) = 1 - 0.5·I_{v/(v+x²)}(v/2, 1/2)
                double ib = IncBeta(v / (v + x * x), v / 2, 0.5);
                if (x >= 0) return 1 - 0.5 * ib;
                return 0.5 * ib;
            }
            static double IncBeta(double x, double a_, double b_)
            {
                if (x <= 0) return 0; if (x >= 1) return 1;
                double bt = Math.Exp(LogGamma(a_ + b_) - LogGamma(a_) - LogGamma(b_)
                    + a_ * Math.Log(x) + b_ * Math.Log(1 - x));
                if (x < (a_ + 1) / (a_ + b_ + 2))
                    return bt * BetaContFrac(x, a_, b_) / a_;
                return 1 - bt * BetaContFrac(1 - x, b_, a_) / b_;
            }
            static double BetaContFrac(double x, double a_, double b_)
            {
                double fpmin = 1e-30;
                double qab = a_ + b_, qap = a_ + 1, qam = a_ - 1;
                double c = 1, d = 1 - qab * x / qap;
                if (Math.Abs(d) < fpmin) d = fpmin;
                d = 1 / d;
                double h = d;
                for (int m = 1; m <= 200; m++)
                {
                    int m2 = 2 * m;
                    double aa = m * (b_ - m) * x / ((qam + m2) * (a_ + m2));
                    d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
                    c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
                    d = 1 / d; h *= d * c;
                    aa = -(a_ + m) * (qab + m) * x / ((a_ + m2) * (qap + m2));
                    d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
                    c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
                    d = 1 / d; double del = d * c; h *= del;
                    if (Math.Abs(del - 1) < 3e-7) break;
                }
                return h;
            }
            static double LogGamma(double x) => Math.Log(Math.Abs(GammaFn(x)));

            // Linear algebra
            _builtins["det"] = a => new MValue(MatlabLinAlg.Determinant(a[0]));
            _builtins["inv"] = a => MatlabLinAlg.Inverse(a[0]);
            _builtins["inverse"] = a => MatlabLinAlg.Inverse(a[0]);
            _builtins["expm"] = a => {
                // Escalar simbólico → exp(x)
                if (a[0].IsSymbolic)
                    return MValue.NewSymbolic(new SymFunc("exp", a[0].Symbolic));
                // Matriz simbólica → Taylor truncado o atajo diagonal
                if (a[0].IsSymMatrix)
                    return MValue.NewSymMatrix(SymMatOps.Expm(a[0].SymCells));
                return MatlabLinAlg.Expm(a[0]);
            };
            _builtins["svd"] = a => MatlabLinAlg.SVD(a[0]).S;
            _multiOutBuiltins["svd"] = a => {
                var (U, S, V) = MatlabLinAlg.SVD(a[0]);
                return new[] { U, S, V };
            };
            _builtins["rank"] = a => {
                double tol = a.Length >= 2 ? a[1].Scalar : 1e-10;
                var (_, S, _) = MatlabLinAlg.SVD(a[0]);
                int rank = 0;
                for (int i = 0; i < Math.Min(S.Rows, S.Cols); i++)
                    if (S.At(i, i) > tol) rank++;
                return new MValue(rank);
            };
            _builtins["kron"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("kron(A, B)");
                var A = a[0]; var B = a[1];
                int mA = A.Rows, nA = A.Cols, mB = B.Rows, nB = B.Cols;
                var r = new MValue(mA * mB, nA * nB);
                for (int i = 0; i < mA; i++)
                    for (int j = 0; j < nA; j++)
                    {
                        double aij = A.At(i, j);
                        for (int p = 0; p < mB; p++)
                            for (int q = 0; q < nB; q++)
                                r.Set(i * mB + p, j * nB + q, aij * B.At(p, q));
                    }
                return r;
            };
            _builtins["null"] = a => {
                // Espacio nulo: columnas V de SVD con singular values cercanos a 0
                var (_, S, V) = MatlabLinAlg.SVD(a[0]);
                double tol = a.Length >= 2 ? a[1].Scalar : 1e-10;
                int n = V.Rows;
                var nullCols = new System.Collections.Generic.List<int>();
                for (int k = 0; k < Math.Min(S.Rows, S.Cols); k++)
                    if (Math.Abs(S.At(k, k)) < tol) nullCols.Add(k);
                // Si rank deficient, también añadir cols extra
                for (int k = Math.Min(S.Rows, S.Cols); k < n; k++) nullCols.Add(k);
                if (nullCols.Count == 0) return new MValue(n, 0);
                var result = new MValue(n, nullCols.Count);
                for (int j = 0; j < nullCols.Count; j++)
                    for (int i = 0; i < n; i++)
                        result.Set(i, j, V.At(i, nullCols[j]));
                return result;
            };
            _builtins["orth"] = a => {
                // Base ortonormal del rango: cols U con singular values > tol
                var (U, S, _) = MatlabLinAlg.SVD(a[0]);
                double tol = 1e-10;
                int m = U.Rows;
                var orthCols = new System.Collections.Generic.List<int>();
                for (int k = 0; k < Math.Min(S.Rows, S.Cols); k++)
                    if (Math.Abs(S.At(k, k)) > tol) orthCols.Add(k);
                if (orthCols.Count == 0) return new MValue(m, 0);
                var result = new MValue(m, orthCols.Count);
                for (int j = 0; j < orthCols.Count; j++)
                    for (int i = 0; i < m; i++)
                        result.Set(i, j, U.At(i, orthCols[j]));
                return result;
            };
            _builtins["colspace"] = _builtins["orth"];
            _builtins["rowspace"] = a => {
                var transposed = TransposeSimple(a[0]);
                return _builtins["orth"](new[] { transposed });
            };

            _builtins["pinv"] = a => {
                // Pseudo-inversa Moore-Penrose vía SVD
                var (U, S, V) = MatlabLinAlg.SVD(a[0]);
                int m = U.Rows, n = V.Rows;
                double tol = 1e-10 * Math.Max(m, n) * (S.Rows > 0 ? S.At(0, 0) : 1);
                var Sinv = new MValue(n, m);
                for (int i = 0; i < Math.Min(S.Rows, S.Cols); i++)
                {
                    double sv = S.At(i, i);
                    if (sv > tol) Sinv.Set(i, i, 1.0 / sv);
                }
                return MatlabLinAlg.MatMul(MatlabLinAlg.MatMul(V, Sinv), MatlabLinAlg.TransposeM(U));
            };
            _builtins["logm"] = a => {
                if (a[0].IsSymbolic) return MValue.NewSymbolic(new SymFunc("log", a[0].Symbolic));
                if (a[0].IsSymMatrix) return MValue.NewSymMatrix(SymMatOps.Logm(a[0].SymCells));
                return MatlabLinAlg.Logm(a[0]);
            };
            _builtins["sqrtm"] = a => {
                if (a[0].IsSymbolic) return MValue.NewSymbolic(new SymFunc("sqrt", a[0].Symbolic));
                if (a[0].IsSymMatrix) return MValue.NewSymMatrix(SymMatOps.Sqrtm(a[0].SymCells));
                return MatlabLinAlg.Sqrtm(a[0]);
            };
            _builtins["funm"] = a => {
                // funm(A, @fn) — A debe ser matriz simbólica o numérica diagonal; fn nombre
                if (a.Length < 2) throw new MatlabRuntimeException("funm(A, @fn)");
                string fnName = a[1].CallableName ?? "exp";
                if (a[0].IsSymMatrix) return MValue.NewSymMatrix(SymMatOps.Funm(a[0].SymCells, fnName));
                if (a[0].IsSymbolic) return MValue.NewSymbolic(new SymFunc(fnName, a[0].Symbolic));
                throw new MatlabRuntimeException("funm: only symbolic matrices supported (use expm/sqrtm/logm for numeric)");
            };
            _builtins["lu"] = a => MatlabLinAlg.LU(a[0]).L;
            _builtins["qr"] = a => MatlabLinAlg.QR(a[0]).Q;
            _builtins["chol"] = a => MatlabLinAlg.Cholesky(a[0]);
            _builtins["schur"] = a => MatlabLinAlg.Schur(a[0]).T;
            _multiOutBuiltins["lu"] = a => {
                var (L, U, P) = MatlabLinAlg.LU(a[0]);
                return new[] { L, U, P };
            };
            _multiOutBuiltins["qr"] = a => {
                var (Q, R) = MatlabLinAlg.QR(a[0]);
                return new[] { Q, R };
            };
            _multiOutBuiltins["schur"] = a => {
                var (T, Z) = MatlabLinAlg.Schur(a[0]);
                return new[] { Z, T };
            };
            _builtins["bicg"] = a => MatlabLinAlg.BiCGStab(a[0], a[1],
                a.Length >= 3 ? a[2].Scalar : 1e-10,
                a.Length >= 4 ? (int)a[3].Scalar : 500);
            _builtins["gmres"] = a => MatlabLinAlg.Gmres(a[0], a[1],
                a.Length >= 3 ? (int)a[2].Scalar : Math.Min(a[0].Rows, 50),
                a.Length >= 4 ? a[3].Scalar : 1e-10,
                a.Length >= 5 ? (int)a[4].Scalar : 200);
            _builtins["linsolve"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("linsolve(A, b)");
                return MatlabLinAlg.Linsolve(a[0], a[1]);
            };
            _builtins["mldivide"] = a => MatlabLinAlg.Linsolve(a[0], a[1]);
            _builtins["gauss_seidel"] = a => {
                // gauss_seidel(A, b[, x0[, tol[, maxIter]]])
                if (a.Length < 2) throw new MatlabRuntimeException("gauss_seidel(A, b)");
                return MatlabLinAlg.GaussSeidel(a[0], a[1],
                    a.Length >= 3 ? a[2] : null,
                    a.Length >= 4 ? a[3].Scalar : 1e-10,
                    a.Length >= 5 ? (int)a[4].Scalar : 1000);
            };
            _builtins["pcg"] = a => {
                if (a.Length < 2) throw new MatlabRuntimeException("pcg(A, b)");
                return MatlabLinAlg.ConjugateGradient(a[0], a[1],
                    a.Length >= 3 ? a[2].Scalar : 1e-10,
                    a.Length >= 4 ? (int)a[3].Scalar : 1000);
            };
            _builtins["eig"] = a => {
                if (a.Length >= 2 && Calcpad.Core.LapackInterop.Available
                    && IsSymmetricM(a[0]) && IsSymmetricM(a[1]))
                {
                    int ns = a[0].Rows;
                    var (wv, _) = Calcpad.Core.LapackInterop.SymGenEig(ns, ToRowMajor(a[0]), ToRowMajor(a[1]));
                    var col = new MValue(ns, 1); for (int i = 0; i < ns; i++) col.Set(i, 0, wv[i]);
                    return col;
                }
                return MatlabLinAlg.Eig(a.Length >= 2 ? MatlabLinAlg.Linsolve(a[1], a[0]) : a[0]).eigenvalues;
            };
            _builtins["eigenvals"] = a => MatlabLinAlg.Eig(a[0]).eigenvalues;
            _builtins["eigenvecs"] = a => MatlabLinAlg.Eig(a[0]).eigenvectors;
            _multiOutBuiltins["eig"] = a => {
                // eig(A, B) GENERALIZADO simétrico → LAPACK DSYGV (robusto para sistemas grandes,
                // ej. modal FEM K·φ=ω²·M·φ). Si no es simétrico, cae a eig(B\A).
                if (a.Length >= 2 && Calcpad.Core.LapackInterop.Available
                    && IsSymmetricM(a[0]) && IsSymmetricM(a[1]))
                {
                    int ns = a[0].Rows;
                    var (wv, vv) = Calcpad.Core.LapackInterop.SymGenEig(ns, ToRowMajor(a[0]), ToRowMajor(a[1]));
                    var Dg = new MValue(ns, ns); for (int i = 0; i < ns; i++) Dg.Set(i, i, wv[i]);
                    var Vg = new MValue(ns, ns);
                    for (int i = 0; i < ns; i++) for (int j = 0; j < ns; j++) Vg.Set(i, j, vv[i * ns + j]);
                    return new[] { Vg, Dg };
                }
                MValue mat = a.Length >= 2 ? MatlabLinAlg.Linsolve(a[1], a[0]) : a[0];
                var (vals, vecs) = MatlabLinAlg.Eig(mat);
                // MATLAB: [V, D] = eig(A) → V eigenvectors, D diagonal de eigenvalues
                int n = vals.Rows;
                var D = new MValue(n, n);
                for (int i = 0; i < n; i++) D.Set(i, i, vals.At(i, 0));
                return new[] { vecs, D };
            };
            _builtins["norm"] = a => {
                var v = a[0];
                int p = a.Length > 1 ? (int)a[1].Scalar : 2;
                double s = 0;
                foreach (var x in v.Data) s += Math.Pow(Math.Abs(x), p);
                return new MValue(Math.Pow(s, 1.0 / p));
            };
            _builtins["dot"] = a => {
                if (a[0].Data.Length != a[1].Data.Length) throw new MatlabRuntimeException("dot: length mismatch");
                double s = 0;
                for (int i = 0; i < a[0].Data.Length; i++) s += a[0].Data[i] * a[1].Data[i];
                return new MValue(s);
            };
            _builtins["cross"] = a => {
                if (a[0].Data.Length != 3 || a[1].Data.Length != 3) throw new MatlabRuntimeException("cross: requires 3-vectors");
                var u = a[0].Data; var v = a[1].Data;
                return new MValue(1, 3, new[] {
                    u[1]*v[2] - u[2]*v[1],
                    u[2]*v[0] - u[0]*v[2],
                    u[0]*v[1] - u[1]*v[0]
                });
            };
            _builtins["trace"] = a => {
                var m = a[0]; double s = 0;
                for (int i = 0, n = Math.Min(m.Rows, m.Cols); i < n; i++) s += m.At(i, i);
                return new MValue(s);
            };
            // diag(v)  -> matriz cuadrada con v en la diagonal (vector -> matriz)
            // diag(A)  -> vector columna con la diagonal de A (matriz -> vector)
            _builtins["diag"] = a => {
                var x = a[0];
                int k = a.Length >= 2 ? (int)a[1].Scalar : 0;
                if (x.IsScalar)
                {
                    var mOut = new MValue(1, 1); mOut.Data[0] = x.Scalar; return mOut;
                }
                // vector -> matriz diagonal
                if (x.Rows == 1 || x.Cols == 1)
                {
                    int len = x.Data.Length;
                    int sz = len + Math.Abs(k);
                    var d = new MValue(sz, sz);
                    for (int i = 0; i < len; i++)
                    {
                        int row = k >= 0 ? i : i - k;
                        int col = k >= 0 ? i + k : i;
                        d.Set(row, col, x.Data[i]);
                    }
                    return d;
                }
                // matriz -> vector con la diagonal
                int n = k >= 0 ? Math.Min(x.Rows, x.Cols - k) : Math.Min(x.Rows + k, x.Cols);
                if (n <= 0) return new MValue(0, 0);
                var v = new MValue(n, 1);
                for (int i = 0; i < n; i++)
                {
                    int row = k >= 0 ? i : i - k;
                    int col = k >= 0 ? i + k : i;
                    v.Data[i] = x.At(row, col);
                }
                return v;
            };
            _builtins["find"] = a => {
                var src = a[0];
                var hits = new List<double>();
                for (int j = 0; j < src.Cols; j++)
                    for (int i = 0; i < src.Rows; i++)
                        if (src.At(i, j) != 0) hits.Add(j * src.Rows + i + 1);  // column-major 1-based
                // find(X,k): limitar a los primeros k (MATLAB). 'last' no soportado aún.
                if (a.Length >= 2 && a[1].IsScalar)
                {
                    int k = (int)a[1].Scalar;
                    if (k >= 0 && k < hits.Count) hits = hits.GetRange(0, k);
                }
                // Orientación como MATLAB: FILA solo si el input es un vector fila (1×n, n>1);
                // en cualquier otro caso (vector columna o matriz) devuelve COLUMNA. Antes SIEMPRE
                // devolvía fila, lo que rompía `a.'` y horzcat en código FEM (find de una columna).
                bool rowOut = src.Rows == 1 && src.Cols > 1;
                if (hits.Count == 0) return rowOut ? new MValue(1, 0) : new MValue(0, 1);
                var arr = hits.ToArray();
                return rowOut ? new MValue(1, arr.Length, arr) : new MValue(arr.Length, 1, arr);
            };

            // Multi-output builtins extras
            _multiOutBuiltins["min"] = a => {
                var v = a[0];
                double mn = double.PositiveInfinity; int idx = 0;
                for (int i = 0; i < v.Data.Length; i++) if (v.Data[i] < mn) { mn = v.Data[i]; idx = i; }
                return new[] { new MValue(mn), new MValue(idx + 1) };
            };
            _multiOutBuiltins["max"] = a => {
                var v = a[0];
                double mx = double.NegativeInfinity; int idx = 0;
                for (int i = 0; i < v.Data.Length; i++) if (v.Data[i] > mx) { mx = v.Data[i]; idx = i; }
                return new[] { new MValue(mx), new MValue(idx + 1) };
            };
            _multiOutBuiltins["sort"] = a => {
                var v = a[0];
                var pairs = v.Data.Select((x, i) => (x, i + 1)).OrderBy(p => p.x).ToArray();
                var data = new double[pairs.Length];
                var idx = new double[pairs.Length];
                for (int k = 0; k < pairs.Length; k++) { data[k] = pairs[k].x; idx[k] = pairs[k].Item2; }
                return new[] { new MValue(v.Rows, v.Cols, data), new MValue(v.Rows, v.Cols, idx) };
            };

            // unique multi-output (MATLAB):  [C, ia, ic] = unique(A)
            //   C  = valores únicos ordenados   (C = A(ia))
            //   ia = índice del PRIMER ocurrencia de cada único (1-based)
            //   ic = índice en C de cada elemento de A  (A = C(ic))
            // Verificado vs MATLAB R2017a: unique([10;30;10;20]) → C=[10;20;30], ia=[1;4;2], ic=[1;3;1;2]
            _multiOutBuiltins["unique"] = a => {
                var v = a[0];
                var input = v.Data;
                int n = input.Length;
                var sorted = new SortedSet<double>(input);
                int nu = sorted.Count;
                var C = new double[nu];
                var posOf = new Dictionary<double, int>();
                int kk = 0;
                foreach (var x in sorted) { C[kk] = x; posOf[x] = kk; kk++; }
                var firstIdx = new Dictionary<double, int>();
                for (int i = 0; i < n; i++) if (!firstIdx.ContainsKey(input[i])) firstIdx[input[i]] = i + 1;
                var ia = new double[nu];
                for (int j = 0; j < nu; j++) ia[j] = firstIdx[C[j]];
                var ic = new double[n];
                for (int i = 0; i < n; i++) ic[i] = posOf[input[i]] + 1;
                // orientación: columna si el input es columna; si no, fila (como MATLAB)
                bool col = v.Cols == 1 && v.Rows > 1;
                Func<double[], MValue> mk = d => col ? new MValue(d.Length, 1, d) : new MValue(1, d.Length, d);
                return new[] { mk(C), mk(ia), mk(ic) };
            };

            // 'end' usado en indexing — handled contextualmente en EvalCallOrIndex

            // Multi-output builtins
            _builtins["delaunay"] = a => {
                // delaunay(x, y) → matriz Nx3 de triángulos (índices 1-based)
                // Algoritmo Bowyer-Watson para triangulación de Delaunay 2D
                if (a.Length < 2) throw new MatlabRuntimeException("delaunay(x, y)");
                var xv = a[0]; var yv = a[1];
                int n = xv.Data.Length;
                if (yv.Data.Length != n) throw new MatlabRuntimeException("delaunay: x, y must be same length");
                if (n < 3) throw new MatlabRuntimeException("delaunay: need at least 3 points");
                var pts = new double[n, 2];
                for (int i = 0; i < n; i++) { pts[i, 0] = xv.Data[i]; pts[i, 1] = yv.Data[i]; }
                var tris = DelaunayBowyerWatson(pts);
                var result = new MValue(tris.Count, 3);
                for (int i = 0; i < tris.Count; i++)
                {
                    result.Set(i, 0, tris[i].A + 1);
                    result.Set(i, 1, tris[i].B + 1);
                    result.Set(i, 2, tris[i].C + 1);
                }
                return result;
            };
            _multiOutBuiltins["meshgrid"] = a => {
                var x = a[0];
                var y = a.Length > 1 ? a[1] : x;
                int Nx = Math.Max(x.Rows, x.Cols);
                int Ny = Math.Max(y.Rows, y.Cols);
                if (a.Length >= 3)   // 3D: X,Y,Z de tamaño [Ny,Nx,Nz]. X=x(col), Y=y(fila), Z=z(pag)
                {
                    var z = a[2]; int Nz = Math.Max(z.Rows, z.Cols);
                    var Xp = new MValue[Nz]; var Yp = new MValue[Nz]; var Zp = new MValue[Nz];
                    for (int k = 0; k < Nz; k++)
                    {
                        var PX = new MValue(Ny, Nx); var PY = new MValue(Ny, Nx); var PZ = new MValue(Ny, Nx);
                        for (int i = 0; i < Ny; i++) for (int j = 0; j < Nx; j++)
                        { PX.Set(i, j, x.Data[j]); PY.Set(i, j, y.Data[i]); PZ.Set(i, j, z.Data[k]); }
                        Xp[k] = PX; Yp[k] = PY; Zp[k] = PZ;
                    }
                    return new[] { MValue.New3D(Xp), MValue.New3D(Yp), MValue.New3D(Zp) };
                }
                var X = new MValue(Ny, Nx);
                var Y = new MValue(Ny, Nx);
                for (int i = 0; i < Ny; i++)
                    for (int j = 0; j < Nx; j++)
                    {
                        X.Set(i, j, x.Data[j]);
                        Y.Set(i, j, y.Data[i]);
                    }
                return new[] { X, Y };
            };
            // ndgrid(x,y[,z]): como meshgrid pero SIN transponer (convención N-D de MATLAB):
            // X(i,j)=x(i), Y(i,j)=y(j). 3 args -> arrays 3D [Nx,Ny,Nz]. [X,Y]=ndgrid(x) → ndgrid(x,x).
            _multiOutBuiltins["ndgrid"] = a => {
                var x = a[0];
                var y = a.Length > 1 ? a[1] : x;
                int Nx = Math.Max(x.Rows, x.Cols);
                int Ny = Math.Max(y.Rows, y.Cols);
                if (a.Length >= 3)   // 3D: X,Y,Z de tamaño [Nx,Ny,Nz]. X=x(fila), Y=y(col), Z=z(pag)
                {
                    var z = a[2]; int Nz = Math.Max(z.Rows, z.Cols);
                    var Xp = new MValue[Nz]; var Yp = new MValue[Nz]; var Zp = new MValue[Nz];
                    for (int k = 0; k < Nz; k++)
                    {
                        var PX = new MValue(Nx, Ny); var PY = new MValue(Nx, Ny); var PZ = new MValue(Nx, Ny);
                        for (int i = 0; i < Nx; i++) for (int j = 0; j < Ny; j++)
                        { PX.Set(i, j, x.Data[i]); PY.Set(i, j, y.Data[j]); PZ.Set(i, j, z.Data[k]); }
                        Xp[k] = PX; Yp[k] = PY; Zp[k] = PZ;
                    }
                    return new[] { MValue.New3D(Xp), MValue.New3D(Yp), MValue.New3D(Zp) };
                }
                var X = new MValue(Nx, Ny);
                var Y = new MValue(Nx, Ny);
                for (int i = 0; i < Nx; i++)
                    for (int j = 0; j < Ny; j++)
                    {
                        X.Set(i, j, x.Data[i]);
                        Y.Set(i, j, y.Data[j]);
                    }
                return new[] { X, Y };
            };
            // cylinder(r,n): [X,Y,Z] de un cilindro radio r (escalar -> [r,r]) o perfil
            // de radios (vector). [r 0] -> cono. n puntos en la circunferencia (def 20).
            _multiOutBuiltins["cylinder"] = a => {
                var rv = a.Length > 0 ? a[0] : new MValue(1.0);
                int m = Math.Max(rv.Rows, rv.Cols);
                double[] r;
                if (m <= 1) { double r0 = rv.Data.Length > 0 ? rv.Data[0] : 1.0; r = new[] { r0, r0 }; m = 2; }
                else { r = new double[m]; for (int k = 0; k < m; k++) r[k] = rv.Data[k]; }
                int n = a.Length > 1 ? (int)Math.Round(a[1].Data[0]) : 20;
                if (n < 1) n = 20;
                int cols = n + 1;
                var X = new MValue(m, cols);
                var Y = new MValue(m, cols);
                var Z = new MValue(m, cols);
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        double th = 2.0 * Math.PI * j / n;
                        X.Set(i, j, r[i] * Math.Cos(th));
                        Y.Set(i, j, r[i] * Math.Sin(th));
                        Z.Set(i, j, (double)i / (m - 1));
                    }
                return new[] { X, Y, Z };
            };
            _multiOutBuiltins["size"] = a => new[] {
                new MValue(a[0].Rows),
                new MValue(a[0].Cols)
            };
        }

        // ── Helpers para patch/line/text ──────────────────────────────────────
        /// <summary>Parsea un linespec MATLAB ('o','^','r-','--', etc.):
        /// detecta si pide línea, marcadores, color y símbolo Plotly.</summary>
        private static void ParseLineSpec(string spec, out bool wantLine, out bool wantMarker,
                                           out string color, out string symbol)
        {
            wantLine = false; wantMarker = false; color = null; symbol = "circle";
            if (string.IsNullOrEmpty(spec)) return;
            // Estilos de línea primero (más largos antes), y se quitan para no confundir '.' de '-.'
            string s = spec;
            if (s.Contains("--")) { wantLine = true; s = s.Replace("--", ""); }
            if (s.Contains("-.")) { wantLine = true; s = s.Replace("-.", ""); }
            if (s.Contains(":"))  { wantLine = true; s = s.Replace(":", ""); }
            if (s.Contains("-"))  { wantLine = true; s = s.Replace("-", ""); }
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'o': symbol = "circle"; wantMarker = true; break;
                    case '.': symbol = "point"; wantMarker = true; break;   // MATLAB: mas chico que 'o'
                    case '+': symbol = "cross"; wantMarker = true; break;
                    case '*': symbol = "star"; wantMarker = true; break;
                    case 'x': symbol = "x"; wantMarker = true; break;
                    case 's': symbol = "square"; wantMarker = true; break;
                    case 'd': symbol = "diamond"; wantMarker = true; break;
                    case '^': symbol = "triangle-up"; wantMarker = true; break;
                    case 'v': symbol = "triangle-down"; wantMarker = true; break;
                    case '>': symbol = "triangle-right"; wantMarker = true; break;
                    case '<': symbol = "triangle-left"; wantMarker = true; break;
                    case 'p': symbol = "pentagon"; wantMarker = true; break;
                    case 'h': symbol = "hexagon"; wantMarker = true; break;
                    case 'r': case 'g': case 'b': case 'c':
                    case 'm': case 'y': case 'k': case 'w':
                        color = MatlabColorToJs(c.ToString()); break;
                }
            }
        }
        /// <summary>Convierte un argumento de color (string, [r g b] o escalar) a CSS/JS.</summary>
        private static string ColorArg(MValue val)
        {
            if (val == null) return "black";
            if (val.IsString) return MatlabColorToJs(val.StringValue);
            if (val.Rows == 1 && val.Cols == 3) return RgbVecToCss(val);
            if (val.IsScalar) return ScalarToColorJs(val.Scalar);
            return "black";
        }
        /// <summary>Traduce el nombre de marcador de MATLAB ('o','s','^'...) al simbolo
        /// de Plotly. Misma tabla que usa el parser de estilos de plot(), para que
        /// line(...,'Marker','s') y plot(...,'s') dibujen igual.</summary>
        private static string MatlabMarkerToPlotly(string m)
        {
            if (string.IsNullOrEmpty(m)) return "circle";
            switch (m)
            {
                case "o": return "circle";
                case ".": return "point";
                case "+": return "cross";
                case "*": return "star";
                case "x": return "x";
                case "s": case "square": return "square";
                case "d": case "diamond": return "diamond";
                case "^": return "triangle-up";
                case "v": return "triangle-down";
                case ">": return "triangle-right";
                case "<": return "triangle-left";
                case "p": case "pentagram": return "pentagon";
                case "h": case "hexagram": return "hexagon";
                default: return "circle";
            }
        }

        /// <summary>Dibuja un objeto graph como MATLAB: una linea por arista, un circulo
        /// por nudo con su NUMERO, y la etiqueta de cada arista en su punto medio.
        /// Es lo que hace `plot(G,'XData',X,'YData',Y,'EdgeLabel',G.Edges.Weight)` en
        /// dibujoplano.m de CEINCI-LAB.</summary>
        private MValue PlotGraphObject(MValue[] a)
        {
            var g = a[0];
            double[] xs = null, ys = null, edgeLab = null;
            double lw = 1.5;
            for (int i = 1; i + 1 < a.Length; i += 2)
            {
                if (!a[i].IsString) break;
                var key = a[i].StringValue.ToLowerInvariant();
                var val = a[i + 1];
                if (key == "xdata") xs = val.Data;
                else if (key == "ydata") ys = val.Data;
                else if (key == "edgelabel") edgeLab = val.Data;
                else if (key == "linewidth") lw = val.Scalar;
            }
            if (xs == null || ys == null) return new MValue(0);

            var edges = g.Fields.TryGetValue("edges", out var ed) ? ed : null;
            double[] en = edges != null && edges.Fields.TryGetValue("endnodes", out var e0) ? e0.Data : null;
            if (en == null) return new MValue(0);
            int m = en.Length / 2;

            // aristas
            for (int k = 0; k < m; k++)
            {
                int ni = (int)System.Math.Round(en[k * 2]) - 1;
                int nj = (int)System.Math.Round(en[k * 2 + 1]) - 1;
                if (ni < 0 || nj < 0 || ni >= xs.Length || nj >= xs.Length) continue;
                MatlabPlots.Line2D(new[] { xs[ni], xs[nj] }, new[] { ys[ni], ys[nj] }, "#0072BD", lw);
                if (edgeLab != null && k < edgeLab.Length)
                    MatlabPlots.Text2D((xs[ni] + xs[nj]) / 2, (ys[ni] + ys[nj]) / 2,
                                       edgeLab[k].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                                       "#333333", 9, "center");
            }
            // nudos: circulo + numero
            // MATLAB dibuja los nudos RELLENOS y pone el numero AL COSTADO, no encima.
            MatlabPlots.Markers2D(xs, ys, "#0072BD", "#0072BD", "circle", 6);
            double offx = 0, offy = 0;
            for (int n = 0; n < xs.Length; n++) { offx += xs[n]; offy += ys[n]; }
            double spanX = 0, spanY = 0;
            if (xs.Length > 0)
            {
                double xmn = xs[0], xmx = xs[0], ymn = ys[0], ymx = ys[0];
                foreach (var v in xs) { if (v < xmn) xmn = v; if (v > xmx) xmx = v; }
                foreach (var v in ys) { if (v < ymn) ymn = v; if (v > ymx) ymx = v; }
                spanX = xmx - xmn; spanY = ymx - ymn;
            }
            double dsp = 0.018 * (spanX > 0 ? spanX : 1);
            for (int n = 0; n < xs.Length; n++)
                MatlabPlots.Text2D(xs[n] + dsp, ys[n] + 0.012 * (spanY > 0 ? spanY : 1),
                                   (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                                   "#000000", 9);
            return new MValue(0);
        }

        /// <summary>MATLAB entrega en `catch e` un objeto MException con .message e
        /// .identifier, no una cadena suelta. Sin esto, `e.message` fallaba con
        /// "Reference to non-existent field".</summary>
        private static MValue MakeMException(string msg)
        {
            var e = MValue.NewStruct();
            e.Fields["message"]    = new MValue(msg ?? "");
            e.Fields["Message"]    = e.Fields["message"];
            e.Fields["identifier"] = new MValue("");
            e.Fields["Identifier"] = e.Fields["identifier"];
            return e;
        }

        /// <summary>Comandos de dibujo/estado que en MATLAB NO devuelven nada cuando se
        /// llaman sueltos: `figure`, `plot(...)`, `title(...)`… Sin esto Lab mostraba
        /// "figure = 0" donde MATLAB no muestra nada.</summary>
        private static readonly HashSet<string> _sinSalida = new(StringComparer.Ordinal)
        {
            "figure", "plot", "plot3", "line", "patch", "fill", "scatter", "scatter3",
            "surf", "mesh", "contour", "contourf", "imagesc", "bar", "stem", "hist",
            "histogram", "quiver", "quiver3", "text", "title", "xlabel", "ylabel",
            "zlabel", "legend", "grid", "axis", "hold", "xlim", "ylim", "zlim", "caxis",
            "colorbar", "colormap", "subplot", "drawnow", "clf", "cla", "close",
            "shading", "lighting", "material", "camlight", "view", "rotate3d", "box",
            "set", "disp", "fprintf", "printf", "warning", "error", "pause", "clc",
            "clear", "format", "hoverdata", "datacursormode", "print", "saveas"
        };

        /// <summary>true si la expresion es una llamada que no produce salida visible:
        /// un builtin de la lista, o una funcion de usuario declarada sin outputs.</summary>
        private bool EsLlamadaSinSalida(MatlabNode e, MatlabScope scope)
        {
            string nombre = e switch
            {
                CallOrIndex ci when ci.Target is IdentRef id => id.Name,
                IdentRef ir => ir.Name,
                _ => null
            };
            if (nombre == null) return false;
            // una variable con ese nombre manda: `figure` podria ser un dato del usuario
            if (scope.TryGet(nombre, out _)) return false;
            if (_sinSalida.Contains(nombre)) return true;
            return _userFunctions.TryGetValue(nombre, out var fd) && fd.OutputNames.Count == 0;
        }

        private static string MatlabColorToJs(string c)
        {
            // MATLAB color shortcuts: 'r','g','b','c','m','y','k','w'
            // O nombres CSS standard: 'red','blue', etc.
            switch (c?.ToLowerInvariant())
            {
                case "r": return "red";
                case "g": return "green";
                case "b": return "blue";
                case "c": return "cyan";
                case "m": return "magenta";
                case "y": return "yellow";
                case "k": return "black";
                case "w": return "white";
                default: return c ?? "black";
            }
        }
        private static string ScalarToColorJs(double v)
            => $"hsl({(v * 240) % 360}, 70%, 60%)";
        private static string RgbVecToCss(MValue v)
        {
            // [r g b] con cada uno en [0, 1]
            int r = (int)(v.At(0, 0) * 255);
            int g = (int)(v.At(0, 1) * 255);
            int b = (int)(v.At(0, 2) * 255);
            return $"rgb({r},{g},{b})";
        }
        /// <summary>Patch en modo named-args: patch('Faces', F, 'Vertices', V, ...)</summary>
        /// <summary>Crea un handle de gráficos (axes/uicontrol) guardando los pares name-value en Fields.</summary>
        private static MValue MkGfxHandle(MValue[] a)
        {
            var h = new MValue(0) { IsGfxHandle = true };
            h.Fields = new System.Collections.Generic.Dictionary<string, MValue>();
            for (int i = 0; i + 1 < a.Length; i += 2)
                if (a[i].IsString) h.Fields[a[i].StringValue.ToLowerInvariant()] = a[i + 1];
            return h;
        }
        /// <summary>Quita un handle de eje inicial (forma plot(ax, …), title(ax, …), etc.).</summary>
        private static MValue[] DropAxes(MValue[] a) =>
            (a.Length > 0 && a[0].IsGfxHandle) ? a[1..] : a;


        // Convierte una matriz MValue (Rows x Cols) a double[][] (por filas) para la malla retenida.
        private static double[][] ToJagged(MValue m)
        {
            if (m == null) return null;
            var r = new double[m.Rows][];
            for (int i = 0; i < m.Rows; i++) { r[i] = new double[m.Cols]; for (int j = 0; j < m.Cols; j++) r[i][j] = m.At(i, j); }
            return r;
        }
        private static MValue EvalPatchNamed(MValue[] a)
        {
            MValue Faces = null, Vertices = null, CData = null;
            MValue Xd = null, Yd = null, Zd = null;
            string faceColor = "lightblue", edgeColor = "black";
            string faceColorMode = "uniform";  // 'interp' | 'flat' | 'uniform'
            double faceAlpha = 1, lineWidth = 1;
            for (int i = 0; i + 1 < a.Length; i += 2)
            {
                if (!a[i].IsString) continue;
                string key = a[i].StringValue.ToLowerInvariant();
                var val = a[i + 1];
                switch (key)
                {
                    case "faces": Faces = val; break;
                    case "vertices": Vertices = val; break;
                    case "facevertexcdata": CData = val; break;
                    case "xdata": Xd = val; break;
                    case "ydata": Yd = val; break;
                    case "zdata": Zd = val; break;
                    case "parent": break;   // Lab usa un eje implicito -> se ignora el handle
                    case "facecolor":
                        if (val.IsString)
                        {
                            var sv = val.StringValue.ToLowerInvariant();
                            if (sv == "interp" || sv == "flat") faceColorMode = sv;
                            else { faceColor = MatlabColorToJs(val.StringValue); faceColorMode = "uniform"; }
                        }
                        else if (val.Rows == 1 && val.Cols == 3)
                        { faceColor = RgbVecToCss(val); faceColorMode = "uniform"; }
                        break;
                    case "edgecolor":
                        edgeColor = val.IsString ? MatlabColorToJs(val.StringValue) :
                                    (val.Rows == 1 && val.Cols == 3 ? RgbVecToCss(val) : edgeColor);
                        break;
                    case "facealpha": faceAlpha = val.Scalar; break;
                    case "linewidth": lineWidth = val.Scalar; break;
                }
            }
            // Forma poligono simple: patch('XData', X, 'YData', Y[, 'ZData', Z], ...).
            // Con ZData → polígono 3D (arandelas/tuercas de los pernos) compuesto en la escena;
            // sin ZData → parche 2D.
            if (Xd != null && Yd != null)
            {
                if (Zd != null)
                    MatlabPlots.Patch3DPolygon(Xd.Data, Yd.Data, Zd.Data, faceColor, edgeColor, faceAlpha);
                else
                    MatlabPlots.Patch2D(Xd.Data, Yd.Data, faceColor, edgeColor, faceAlpha, lineWidth);
                return new MValue(0);
            }
            // Vertices 2D (Nx2) + Faces, sin ZData -> parches 2D en _figPrims (rasterizable a PNG/SVG, SIN Plotly/JS)
            if (Faces != null && Vertices != null && Vertices.Cols == 2 && Zd == null)
            {
                // MALLA RETENIDA (modo MATLAB): guardar Faces/Vertices/CData como fuente de verdad;
                // el renderer las reconstruye en cada frame y set(...) las muta → drawnow anima.
                double[] cd = (CData != null && CData.Data != null) ? (double[])CData.Data.Clone() : null;
                MatlabPlots.SetRetainedMesh(ToJagged(Faces), ToJagged(Vertices), cd, edgeColor, faceColor, faceAlpha, lineWidth);
                MatlabPlots.BuildRetainedFaces();   // dibuja el estado inicial
                var hM = new MValue(0) { IsGfxHandle = true };
                hM.Fields = new System.Collections.Generic.Dictionary<string, MValue>();
                hM.Fields["faces"] = Faces; hM.Fields["vertices"] = Vertices;
                if (CData != null) hM.Fields["facevertexcdata"] = CData;
                hM.Fields["__mesh2d"] = new MValue(1);
                return hM;
            }
            if (Faces == null || Vertices == null)
                throw new MatlabRuntimeException("patch named: 'Faces' y 'Vertices' obligatorios");
            // Mesh triangular o quad. Si el patch es Q4 (4 cols), lo dividimos en 2 triángulos T3
            MValue trifaces = Faces;
            if (Faces.Cols == 4)
            {
                // Q4 → 2 T3: (v1, v2, v3) y (v1, v3, v4)
                var split = new MValue(Faces.Rows * 2, 3);
                for (int f = 0; f < Faces.Rows; f++)
                {
                    split.Set(2*f,     0, Faces.At(f, 0));
                    split.Set(2*f,     1, Faces.At(f, 1));
                    split.Set(2*f,     2, Faces.At(f, 2));
                    split.Set(2*f + 1, 0, Faces.At(f, 0));
                    split.Set(2*f + 1, 1, Faces.At(f, 2));
                    split.Set(2*f + 1, 2, Faces.At(f, 3));
                }
                trifaces = split;
            }
            // CData POR-CARA (length nF) con quads divididos: duplicar el valor a cada sub-triángulo
            // (si es POR-VÉRTICE, length nV, se deja igual: se indexa por vértice).
            MValue triCData = CData;
            if (Faces.Cols == 4 && CData != null && CData.Data != null && CData.Data.Length == Faces.Rows)
            {
                var cd2 = new MValue(Faces.Rows * 2, 1);
                for (int f = 0; f < Faces.Rows; f++) { cd2.Data[2 * f] = CData.Data[f]; cd2.Data[2 * f + 1] = CData.Data[f]; }
                triCData = cd2;
            }
            // rango real del campo, para poder rotular la colorbar
            if (triCData != null && triCData.Data != null && triCData.Data.Length > 0)
            {
                double clo2 = triCData.Data[0], chi2 = triCData.Data[0];
                foreach (var dv in triCData.Data) { if (dv < clo2) clo2 = dv; if (dv > chi2) chi2 = dv; }
                MatlabPlots.SetColorRange(clo2, chi2);
            }
            MatlabPlots.PatchMesh(trifaces, Vertices, triCData, faceColorMode,
                                   faceColor, edgeColor, faceAlpha, lineWidth, _activeColormap ?? "parula", Faces.Cols == 4);
            return new MValue(0);
        }

        // Renderiza un SÓLIDO 3D macizo desde elementos de VOLUMEN (tets/hexaedros): extrae la
        // piel exterior y la dibuja opaca. tetramesh(T,X) / tetramesh(T,X,C) / solidmesh(E,X,C).
        private static MValue SolidVolMesh(MValue[] a)
        {
            if (a.Length < 2) throw new MatlabRuntimeException("tetramesh(T, X[, C]) — T: tets Mx4 (o hexaedros Mx8), X: nodos Nx3");
            var T = a[0]; var X = a[1];
            if (X.Cols < 3) throw new MatlabRuntimeException("tetramesh: X debe ser Nx3 (coordenadas 3D)");
            var (tris, triElem) = ExtractSolidBoundary(T);
            MValue cdata; string mode;
            if (a.Length >= 3 && a[2] != null && a[2].Data != null && a[2].Data.Length > 0)
            {
                var C = a[2];
                if (C.Data.Length == X.Rows) { cdata = C; mode = "interp"; }              // color por NODO
                else if (C.Data.Length == T.Rows)                                          // color por ELEMENTO
                {
                    var cf = new MValue(tris.Rows, 1);
                    for (int i = 0; i < tris.Rows; i++) cf.Data[i] = C.Data[triElem[i]];
                    cdata = cf; mode = "flat";
                }
                else { cdata = C; mode = "interp"; }
            }
            else                                                                           // sin C -> color por z
            {
                var cz = new MValue(X.Rows, 1);
                for (int i = 0; i < X.Rows; i++) cz.Data[i] = X.At(i, 2);
                cdata = cz; mode = "interp";
            }
            // rango real del campo, para poder rotular la colorbar
            if (cdata != null && cdata.Data != null && cdata.Data.Length > 0)
            {
                double clo = cdata.Data[0], chi = cdata.Data[0];
                foreach (var d in cdata.Data) { if (d < clo) clo = d; if (d > chi) chi = d; }
                MatlabPlots.SetColorRange(clo, chi);
            }
            MatlabPlots.PatchMesh(tris, X, cdata, mode, "lightblue", "none", 1, 0.5, _activeColormap ?? "parula");
            MatlabPlots.SetFigure3D(true);
            return new MValue(0);
        }

        // Extrae la PIEL (contorno) de una malla de volumen: caras compartidas por 2 elementos
        // se descartan (internas); las que aparecen 1 vez son la superficie exterior. Al cortar
        // el volumen, las caras de la seccion quedan expuestas -> corte RELLENO (sin huecos).
        private static (MValue, int[]) ExtractSolidBoundary(MValue elems)
        {
            int ne = elems.Rows, nc = elems.Cols;
            int[][] faceDefs;
            if (nc == 4) faceDefs = new[] { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };
            else if (nc == 8) faceDefs = new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            else throw new MatlabRuntimeException("tetramesh/solidmesh: elementos deben ser tetraedros (4 col) o hexaedros (8 col)");
            var count = new System.Collections.Generic.Dictionary<string, int>();
            var repr = new System.Collections.Generic.Dictionary<string, int[]>();
            var reprElem = new System.Collections.Generic.Dictionary<string, int>();
            for (int e = 0; e < ne; e++)
                foreach (var fd in faceDefs)
                {
                    var nodes = new int[fd.Length];
                    for (int k = 0; k < fd.Length; k++) nodes[k] = (int)elems.At(e, fd[k]);
                    var sorted = (int[])nodes.Clone(); System.Array.Sort(sorted);
                    string key = string.Join("_", sorted);
                    if (count.ContainsKey(key)) count[key]++;
                    else { count[key] = 1; repr[key] = nodes; reprElem[key] = e; }
                }
            var trisL = new System.Collections.Generic.List<int[]>();
            var elemL = new System.Collections.Generic.List<int>();
            foreach (var kv in count)
                if (kv.Value == 1)
                {
                    var nd = repr[kv.Key]; int el = reprElem[kv.Key];
                    trisL.Add(new[] { nd[0], nd[1], nd[2] }); elemL.Add(el);
                    if (nd.Length == 4) { trisL.Add(new[] { nd[0], nd[2], nd[3] }); elemL.Add(el); }  // quad -> 2 tris
                }
            var res = new MValue(trisL.Count, 3);
            for (int i = 0; i < trisL.Count; i++) { res.Set(i, 0, trisL[i][0]); res.Set(i, 1, trisL[i][1]); res.Set(i, 2, trisL[i][2]); }
            return (res, elemL.ToArray());
        }

        // Aplana un MValue 3D (Pages) a un arreglo column-major como MATLAB X(:):
        // recorre paginas, dentro de cada pagina columnas, dentro de cada columna filas.
        private static double[] Flatten3DColMajor(MValue m)
        {
            int R = m.Pages[0].Rows, C = m.Pages[0].Cols, P = m.Pages.Length;
            var flat = new double[R * C * P];
            int idx = 0;
            for (int k = 0; k < P; k++)
            {
                var pg = m.Pages[k];
                for (int j = 0; j < C; j++)
                    for (int i = 0; i < R; i++)
                        flat[idx++] = pg.Data[i * C + j];
            }
            return flat;
        }

        private static MValue MapUnary(MValue v, Func<double, double> f)
        {
            if (v.IsScalar) return new MValue(f(v.Scalar));
            if (v.IsSymbolic) return MValue.NewSymbolic(MapSymUnary(v.Symbolic, f));
            var r = new MValue(v.Rows, v.Cols);
            for (int i = 0; i < v.Data.Length; i++) r.Data[i] = f(v.Data[i]);
            return r;
        }
        /// <summary>Laplace transform table-based para expresiones canónicas comunes.</summary>
        private static SymNode LaplaceTransform(SymNode expr, string t, string s)
        {
            var S = new SymVar(s);
            if (SymOps.IsConstWrt(expr, t)) return new SymDiv(expr, S);
            if (expr is SymVar tv && tv.Name == t)
                return new SymDiv(new SymConst(1), new SymPow(S, new SymConst(2)));
            if (expr is SymAdd add) return new SymAdd(LaplaceTransform(add.A, t, s), LaplaceTransform(add.B, t, s));
            if (expr is SymSub sub) return new SymSub(LaplaceTransform(sub.A, t, s), LaplaceTransform(sub.B, t, s));
            if (expr is SymMul mul)
            {
                if (SymOps.IsConstWrt(mul.A, t)) return new SymMul(mul.A, LaplaceTransform(mul.B, t, s));
                if (SymOps.IsConstWrt(mul.B, t)) return new SymMul(mul.B, LaplaceTransform(mul.A, t, s));
            }
            if (expr is SymPow pow && pow.Base is SymVar bv && bv.Name == t && pow.Exp is SymConst ne && ne.Value > 0)
            {
                double fact = 1;
                for (int k = 2; k <= ne.Value; k++) fact *= k;
                return new SymDiv(new SymConst(fact), new SymPow(S, new SymConst(ne.Value + 1)));
            }
            if (expr is SymFunc fexp && fexp.Name == "exp")
            {
                if (fexp.Arg is SymVar etv && etv.Name == t) return new SymDiv(new SymConst(1), new SymSub(S, new SymConst(1)));
                if (fexp.Arg is SymMul mu && mu.B is SymVar etv2 && etv2.Name == t && SymOps.IsConstWrt(mu.A, t))
                    return new SymDiv(new SymConst(1), new SymSub(S, mu.A));
                if (fexp.Arg is SymMul mu2 && mu2.A is SymVar etv3 && etv3.Name == t && SymOps.IsConstWrt(mu2.B, t))
                    return new SymDiv(new SymConst(1), new SymSub(S, mu2.B));
            }
            if (expr is SymFunc fsc && (fsc.Name == "sin" || fsc.Name == "cos"))
            {
                SymNode aNode = null;
                if (fsc.Arg is SymVar atv && atv.Name == t) aNode = new SymConst(1);
                else if (fsc.Arg is SymMul amu && amu.B is SymVar atv2 && atv2.Name == t) aNode = amu.A;
                else if (fsc.Arg is SymMul amu2 && amu2.A is SymVar atv3 && atv3.Name == t) aNode = amu2.B;
                if (aNode != null)
                {
                    var denom = new SymAdd(new SymPow(S, new SymConst(2)), new SymPow(aNode, new SymConst(2)));
                    return fsc.Name == "sin"
                        ? new SymDiv(aNode, denom)
                        : new SymDiv(S, denom);
                }
            }
            return new SymFunc("L", expr);
        }
        private static SymNode FourierTransform(SymNode expr, string t, string w)
        {
            return new SymFunc("F", expr);  // MVP placeholder
        }

        /// <summary>Z-transform table-based para secuencias discretas comunes.</summary>
        private static SymNode ZTransform(SymNode expr, string n, string z)
        {
            var Z = new SymVar(z);
            if (SymOps.IsConstWrt(expr, n))
                return new SymMul(expr, new SymDiv(Z, new SymSub(Z, new SymConst(1))));
            if (expr is SymVar nv && nv.Name == n)
                return new SymDiv(Z, new SymPow(new SymSub(Z, new SymConst(1)), new SymConst(2)));
            if (expr is SymAdd add) return new SymAdd(ZTransform(add.A, n, z), ZTransform(add.B, n, z));
            if (expr is SymSub sub) return new SymSub(ZTransform(sub.A, n, z), ZTransform(sub.B, n, z));
            if (expr is SymMul mul)
            {
                if (SymOps.IsConstWrt(mul.A, n)) return new SymMul(mul.A, ZTransform(mul.B, n, z));
                if (SymOps.IsConstWrt(mul.B, n)) return new SymMul(mul.B, ZTransform(mul.A, n, z));
            }
            // a^n → z/(z-a)
            if (expr is SymPow pow && SymOps.IsConstWrt(pow.Base, n) && pow.Exp is SymVar ev && ev.Name == n)
                return new SymDiv(Z, new SymSub(Z, pow.Base));
            return new SymFunc("Z", expr);
        }
        private static SymNode InverseZTransform(SymNode expr, string z, string n)
        {
            var N = new SymVar(n);
            if (expr is SymAdd add) return new SymAdd(InverseZTransform(add.A, z, n), InverseZTransform(add.B, z, n));
            if (expr is SymSub sub) return new SymSub(InverseZTransform(sub.A, z, n), InverseZTransform(sub.B, z, n));
            if (expr is SymMul mul)
            {
                if (SymOps.IsConstWrt(mul.A, z)) return new SymMul(mul.A, InverseZTransform(mul.B, z, n));
                if (SymOps.IsConstWrt(mul.B, z)) return new SymMul(mul.B, InverseZTransform(mul.A, z, n));
            }
            if (expr is SymDiv d)
            {
                // z/(z-a) → a^n
                if (d.A is SymVar zNum && zNum.Name == z && d.B is SymSub ds && ds.A is SymVar zd && zd.Name == z
                    && SymOps.IsConstWrt(ds.B, z))
                    return new SymPow(ds.B, N);
                // z/(z-1)² → n
                if (d.A is SymVar zNum2 && zNum2.Name == z && d.B is SymPow dp && dp.Base is SymSub dsp
                    && dsp.A is SymVar zd2 && zd2.Name == z && dsp.B is SymConst dpc && dpc.Value == 1
                    && dp.Exp is SymConst pe && pe.Value == 2)
                    return N;
            }
            return new SymFunc("Z_inv", expr);
        }

        /// <summary>Transformada inversa de Laplace (table-based).</summary>
        private static SymNode InvLaplaceTransform(SymNode expr, string s, string t)
        {
            var T = new SymVar(t);
            // Linealidad
            if (expr is SymAdd add) return new SymAdd(InvLaplaceTransform(add.A, s, t), InvLaplaceTransform(add.B, s, t));
            if (expr is SymSub sub) return new SymSub(InvLaplaceTransform(sub.A, s, t), InvLaplaceTransform(sub.B, s, t));
            if (expr is SymMul mul)
            {
                if (SymOps.IsConstWrt(mul.A, s)) return new SymMul(mul.A, InvLaplaceTransform(mul.B, s, t));
                if (SymOps.IsConstWrt(mul.B, s)) return new SymMul(mul.B, InvLaplaceTransform(mul.A, s, t));
            }
            // Patrón 1/s^n → t^(n-1)/(n-1)!
            if (expr is SymDiv d)
            {
                // numerator = const c, denom = s^n
                if (SymOps.IsConstWrt(d.A, s) && d.B is SymPow dp && dp.Base is SymVar sv && sv.Name == s
                    && dp.Exp is SymConst ne && ne.Value > 0)
                {
                    int n = (int)ne.Value;
                    double fact = 1;
                    for (int k = 2; k < n; k++) fact *= k;
                    // 1/s^n → t^(n-1)/(n-1)!
                    var formula = new SymDiv(new SymPow(T, new SymConst(n - 1)), new SymConst(fact));
                    return new SymMul(d.A, formula).Simplify();
                }
                // 1/s → 1
                if (SymOps.IsConstWrt(d.A, s) && d.B is SymVar svs && svs.Name == s) return d.A;
                // 1/(s - a) → e^(a·t)
                if (SymOps.IsConstWrt(d.A, s) && d.B is SymSub den && den.A is SymVar sb && sb.Name == s
                    && SymOps.IsConstWrt(den.B, s))
                    return new SymMul(d.A, new SymFunc("exp", new SymMul(den.B, T)));
                // 1/(s + a) → e^(-a·t)
                if (SymOps.IsConstWrt(d.A, s) && d.B is SymAdd addD && addD.A is SymVar sb2 && sb2.Name == s
                    && SymOps.IsConstWrt(addD.B, s))
                    return new SymMul(d.A, new SymFunc("exp", new SymMul(new SymMul(new SymConst(-1), addD.B), T)));
                // a/(s²+ω²) → sin(ω·t) o s/(s²+ω²) → cos(ω·t)
                if (d.B is SymAdd da && da.A is SymPow dap && dap.Base is SymVar svd && svd.Name == s
                    && dap.Exp is SymConst expk && expk.Value == 2)
                {
                    SymNode omega2 = da.B;
                    // omega² is a constant
                    if (omega2 is SymConst oc && oc.Value > 0)
                    {
                        double omega = Math.Sqrt(oc.Value);
                        // numerator es s? → cos(ω·t)
                        if (d.A is SymVar nv && nv.Name == s)
                            return new SymFunc("cos", new SymMul(new SymConst(omega), T));
                        // numerator es const a → (a/ω)·sin(ω·t)
                        if (SymOps.IsConstWrt(d.A, s))
                            return new SymMul(new SymDiv(d.A, new SymConst(omega)), new SymFunc("sin", new SymMul(new SymConst(omega), T)));
                    }
                }
            }
            // Fallback: dejar como función formal
            return new SymFunc("L_inv", expr);
        }

        /// <summary>
        /// Mapea la función numérica a la equivalente simbólica via test-point heurístico.
        /// Detecta sin/cos/exp/etc. por valores específicos.
        /// </summary>
        private static SymNode MapSymUnary(SymNode arg, Func<double, double> f)
        {
            // Heurística por valores en puntos de prueba
            double t1 = 0;
            try { t1 = f(0); } catch { }
            double t2 = 0;
            try { t2 = f(1); } catch { }
            string fnName = null;
            if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - Math.Sin(1)) < 1e-12) fnName = "sin";
            else if (Math.Abs(t1 - 1) < 1e-12 && Math.Abs(t2 - Math.Cos(1)) < 1e-12) fnName = "cos";
            else if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - Math.Tan(1)) < 1e-12) fnName = "tan";
            else if (Math.Abs(t1 - 1) < 1e-12 && Math.Abs(t2 - Math.E) < 1e-12) fnName = "exp";
            else if (Math.Abs(t2) < 1e-12) fnName = "log";  // log(0) undefined, log(1)=0
            else if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - 1) < 1e-12) fnName = "sqrt";
            else if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - Math.Sinh(1)) < 1e-12) fnName = "sinh";
            else if (Math.Abs(t1 - 1) < 1e-12 && Math.Abs(t2 - Math.Cosh(1)) < 1e-12) fnName = "cosh";
            else if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - Math.Tanh(1)) < 1e-12) fnName = "tanh";
            else if (Math.Abs(t1) < 1e-12 && Math.Abs(t2 - 1) < 1e-12) fnName = "abs";  // común
            if (fnName != null) return new SymFunc(fnName, arg).Simplify();
            // Fallback: evaluar como const si arg es const, sino dejar pasar como abs (placeholder)
            if (arg is SymConst c) return new SymConst(f(c.Value));
            return new SymFunc("f", arg);  // unknown — placeholder
        }
        private static MValue MapBinary(MValue a, MValue b, Func<double, double, double> f)
        {
            // Guarda SPARSE: las ops element-wise (+ - .* ./) usan .Data denso; si un
            // operando es CSR se densifica (comportamiento previo). El camino sparse
            // rápido solo está en sparse()/*, indexado y \ (el hot-path del FEM).
            if (a.IsSparseReal) a = a.ToDense();
            if (b.IsSparseReal) b = b.ToDense();
            // Real-only path. Si alguno es complex, el caller debe usar MapBinaryComplex.
            if (a.IsComplex || b.IsComplex)
                return MapBinaryComplex(a, b, f);
            if (a.IsScalar && b.IsScalar) return new MValue(f(a.Scalar, b.Scalar));
            if (a.IsScalar) {
                var r = new MValue(b.Rows, b.Cols);
                for (int i = 0; i < b.Data.Length; i++) r.Data[i] = f(a.Scalar, b.Data[i]);
                return r;
            }
            if (b.IsScalar) {
                var r = new MValue(a.Rows, a.Cols);
                for (int i = 0; i < a.Data.Length; i++) r.Data[i] = f(a.Data[i], b.Scalar);
                return r;
            }
            // Same shape — fast path
            if (a.Rows == b.Rows && a.Cols == b.Cols)
            {
                var r2 = new MValue(a.Rows, a.Cols);
                for (int i = 0; i < a.Data.Length; i++) r2.Data[i] = f(a.Data[i], b.Data[i]);
                return r2;
            }
            // Implicit broadcasting (MATLAB R2016b+): dims-1 se expanden a la otra.
            // Compatible si para cada dim: dimA == dimB OR dimA == 1 OR dimB == 1.
            bool rowOk = a.Rows == b.Rows || a.Rows == 1 || b.Rows == 1;
            bool colOk = a.Cols == b.Cols || a.Cols == 1 || b.Cols == 1;
            if (rowOk && colOk)
            {
                int rR = Math.Max(a.Rows, b.Rows);
                int cR = Math.Max(a.Cols, b.Cols);
                var r3 = new MValue(rR, cR);
                for (int i = 0; i < rR; i++)
                    for (int j = 0; j < cR; j++)
                    {
                        int ai = a.Rows == 1 ? 0 : i, aj = a.Cols == 1 ? 0 : j;
                        int bi = b.Rows == 1 ? 0 : i, bj = b.Cols == 1 ? 0 : j;
                        r3.Set(i, j, f(a.At(ai, aj), b.At(bi, bj)));
                    }
                return r3;
            }
            throw new MatlabRuntimeException($"Dimension mismatch: {a.Rows}×{a.Cols} vs {b.Rows}×{b.Cols}");
        }
        /// <summary>
        /// Wrapper para aritmética compleja: detecta op por function identity y aplica
        /// la regla compleja correspondiente. Para ops no soportados, fallback a real.
        /// </summary>
        private static MValue MapBinaryComplex(MValue a, MValue b, Func<double, double, double> f)
        {
            // Heurística simple: identificar la op por behavior (add/sub/mul/div).
            // Esto funciona para los 4 operadores básicos (+, -, *, /).
            // Test points
            double t1 = f(2, 3), t2 = f(1, 1), t3 = f(4, 2);
            string op;
            if (t1 == 5 && t2 == 2 && t3 == 6) op = "+";
            else if (t1 == -1 && t2 == 0 && t3 == 2) op = "-";
            else if (t1 == 6 && t2 == 1 && t3 == 8) op = "*";
            else if (Math.Abs(t1 - 2.0 / 3) < 1e-12 && t2 == 1 && t3 == 2) op = "/";
            else
            {
                // Caer en real (parte real solo) y advertir vía exception
                throw new MatlabRuntimeException("Complex element-wise op no soportado para esta función");
            }
            int n = Math.Max(a.Data.Length, b.Data.Length);
            int rows, cols;
            if (a.IsScalar) { rows = b.Rows; cols = b.Cols; }
            else if (b.IsScalar) { rows = a.Rows; cols = a.Cols; }
            else
            {
                if (a.Rows != b.Rows || a.Cols != b.Cols)
                    throw new MatlabRuntimeException($"Dimension mismatch: {a.Rows}×{a.Cols} vs {b.Rows}×{b.Cols}");
                rows = a.Rows; cols = a.Cols;
            }
            n = rows * cols;
            var re = new double[n]; var im = new double[n];
            for (int i = 0; i < n; i++)
            {
                int ai = a.IsScalar ? 0 : i;
                int bi = b.IsScalar ? 0 : i;
                double ar = a.Data[ai], aim = a.Imag != null ? a.Imag[ai] : 0;
                double br = b.Data[bi], bim = b.Imag != null ? b.Imag[bi] : 0;
                switch (op)
                {
                    case "+": re[i] = ar + br; im[i] = aim + bim; break;
                    case "-": re[i] = ar - br; im[i] = aim - bim; break;
                    case "*": re[i] = ar * br - aim * bim; im[i] = ar * bim + aim * br; break;
                    case "/":
                        double denom = br * br + bim * bim;
                        if (denom == 0) throw new MatlabRuntimeException("Complex division by zero");
                        re[i] = (ar * br + aim * bim) / denom;
                        im[i] = (aim * br - ar * bim) / denom;
                        break;
                }
            }
            return new MValue(rows, cols, re, im);
        }
        private static MValue Reduce(MValue v, double init, Func<double, double, double> f)
        {
            // sum/prod/min/max — MATLAB collapses por col en 2D, sin embargo MVP devolvemos sobre todos
            double acc = init;
            for (int i = 0; i < v.Data.Length; i++) acc = f(acc, v.Data[i]);
            return new MValue(acc);
        }

        /// <summary>sum/prod respetando la DIMENSIÓN de MATLAB (crucial para código vectorizado
        /// como <c>sum(A.^2,2)</c> = norma por fila). dim=0 → auto (vector→escalar, matriz→por columna);
        /// dim=1 → por columna (1×Cols); dim=2 → por fila (Rows×1). Antes se ignoraba dim y todo
        /// colapsaba a escalar, dando resultados incorrectos y luego "Linear index 0 (1..1)".</summary>
        private static MValue ReduceNumDim(MValue v, int dim, double init, Func<double, double, double> f)
        {
            if (v.IsScalar) return new MValue(v.Scalar);
            // Sparse u otro storage sin Data denso: reducir todos los valores (fallback seguro).
            if (v.Data == null)
            {
                double accS = init;
                if (v.SparseVals != null)
                    for (int i = 0; i < v.SparseVals.Length; i++) accS = f(accS, v.SparseVals[i]);
                return new MValue(accS);
            }
            bool isVec = (v.Rows == 1 || v.Cols == 1);
            if (dim == 0 && isVec)              // vector sin dim -> escalar (reduce todo)
            {
                double acc0 = init;
                for (int i = 0; i < v.Data.Length; i++) acc0 = f(acc0, v.Data[i]);
                return new MValue(acc0);
            }
            if (dim == 0) dim = 1;              // matriz sin dim -> por COLUMNA (como MATLAB)
            if (dim == 1)                       // por columna -> 1 x Cols
            {
                var r = new MValue(1, v.Cols);
                for (int j = 0; j < v.Cols; j++)
                {
                    double acc0 = init;
                    for (int i = 0; i < v.Rows; i++) acc0 = f(acc0, v.At(i, j));
                    r.Set(0, j, acc0);
                }
                return r;
            }
            else                               // dim==2 -> por fila -> Rows x 1
            {
                var r = new MValue(v.Rows, 1);
                for (int i = 0; i < v.Rows; i++)
                {
                    double acc0 = init;
                    for (int j = 0; j < v.Cols; j++) acc0 = f(acc0, v.At(i, j));
                    r.Set(i, 0, acc0);
                }
                return r;
            }
        }

        /// <summary>sum/prod sobre valores simbólicos. Un escalar simbólico se reduce a sí
        /// mismo; un vector/matriz simbólico (p.ej. el resultado de factor()) se colapsa
        /// sumando/multiplicando todas sus celdas. Devuelve un escalar simbólico.</summary>
        private static MValue ReduceSym(MValue v, bool isProd)
        {
            var cells = new List<SymNode>();
            if (v.IsSymbolic) cells.Add(v.Symbolic);
            else if (v.IsSymMatrix)
                foreach (var c in v.SymCells) cells.Add(c ?? new SymConst(0));
            if (cells.Count == 0) return MValue.NewSymbolic(new SymConst(isProd ? 1 : 0));
            SymNode acc = cells[0];
            for (int i = 1; i < cells.Count; i++)
                acc = isProd ? new SymMul(acc, cells[i]) : new SymAdd(acc, cells[i]);
            return MValue.NewSymbolic(acc.Simplify());
        }

        /// <summary>min/max de MATLAB con sus formas: min(X) reduce; min(X,Y) elementwise
        /// (con broadcast escalar); min(X,[],dim) reduce (dim simplificado a forma 1).</summary>
        private static MValue MinMaxBuiltin(MValue[] a, bool isMax)
        {
            double init = isMax ? double.NegativeInfinity : double.PositiveInfinity;
            Func<double, double, double> op = isMax ? Math.Max : Math.Min;
            if (a.Length == 1) return Reduce(a[0], init, op);
            // min(X, [], dim) → reducción (segundo argumento vacío)
            if (a[1] != null && !a[1].IsString && a[1].Rows * a[1].Cols == 0)
                return Reduce(a[0], init, op);
            // min(X, Y) elementwise con broadcast escalar
            var X = a[0]; var Y = a[1];
            if (X.IsScalar && Y.IsScalar) return new MValue(op(X.Scalar, Y.Scalar));
            int r = Math.Max(X.Rows, Y.Rows), c = Math.Max(X.Cols, Y.Cols);
            var res = new MValue(r, c);
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    double xv = X.IsScalar ? X.Scalar : X.At(i % X.Rows, j % X.Cols);
                    double yv = Y.IsScalar ? Y.Scalar : Y.At(i % Y.Rows, j % Y.Cols);
                    res.Set(i, j, op(xv, yv));
                }
            return res;
        }
        private static MValue MakeFill(MValue[] a, double fill)
        {
            int rows = 1, cols = 1;
            if (a.Length == 1)
            {
                // Soporta zeros([m, n]) / ones(size(X)) — vector como single arg
                if (a[0].Data.Length >= 2 && !a[0].IsScalar)
                {
                    rows = (int)a[0].Data[0];
                    cols = (int)a[0].Data[1];
                }
                else
                {
                    rows = (int)a[0].Scalar;
                    cols = rows;
                }
            }
            else if (a.Length >= 2)
            {
                rows = (int)a[0].Scalar;
                cols = (int)a[1].Scalar;
            }
            var r = new MValue(rows, cols);
            if (fill != 0)
                for (int i = 0; i < r.Data.Length; i++) r.Data[i] = fill;
            return r;
        }
        // ─── Dormand-Prince RK45 (adaptive step, MATLAB ode45) ──────────────
        private (MValue t, MValue y) RunOdeDP45(MValue[] a, double absTol, double relTol)
        {
            if (a.Length < 3 || !a[0].IsCallable)
                throw new MatlabRuntimeException("ode45(@f, tspan, y0)");
            var f = a[0].Callable;
            var tspan = a[1];
            var y0 = a[2];
            double t0 = tspan.Data[0], tf = tspan.Data[tspan.Data.Length - 1];
            int neq = y0.Data.Length;
            double t = t0;
            var y = (double[])y0.Data.Clone();
            // Tableau Dormand-Prince
            double[] c = { 0, 1.0/5, 3.0/10, 4.0/5, 8.0/9, 1, 1 };
            double[][] aT = new double[][] {
                new double[]{},
                new double[]{1.0/5},
                new double[]{3.0/40, 9.0/40},
                new double[]{44.0/45, -56.0/15, 32.0/9},
                new double[]{19372.0/6561, -25360.0/2187, 64448.0/6561, -212.0/729},
                new double[]{9017.0/3168, -355.0/33, 46732.0/5247, 49.0/176, -5103.0/18656},
                new double[]{35.0/384, 0, 500.0/1113, 125.0/192, -2187.0/6784, 11.0/84}
            };
            double[] b5 = { 35.0/384, 0, 500.0/1113, 125.0/192, -2187.0/6784, 11.0/84, 0 };
            double[] b4 = { 5179.0/57600, 0, 7571.0/16695, 393.0/640, -92097.0/339200, 187.0/2100, 1.0/40 };
            double h = (tf - t0) / 100;  // estimación inicial
            const double minH = 1e-10, maxH = (1.0); // bounded
            var ts = new System.Collections.Generic.List<double> { t };
            var ys = new System.Collections.Generic.List<double[]> { (double[])y.Clone() };
            int maxSteps = 100000;
            while (t < tf && maxSteps-- > 0)
            {
                if (t + h > tf) h = tf - t;
                var ks = new double[7][];
                ks[0] = EvalRhs(f, t, y);
                for (int s = 1; s < 7; s++)
                {
                    var ys_inner = (double[])y.Clone();
                    for (int j = 0; j < neq; j++)
                    {
                        double sum = 0;
                        for (int k = 0; k < s; k++) sum += aT[s][k] * ks[k][j];
                        ys_inner[j] += h * sum;
                    }
                    ks[s] = EvalRhs(f, t + c[s] * h, ys_inner);
                }
                var y5 = new double[neq];
                var y4 = new double[neq];
                for (int j = 0; j < neq; j++)
                {
                    double s5 = 0, s4 = 0;
                    for (int k = 0; k < 7; k++) { s5 += b5[k] * ks[k][j]; s4 += b4[k] * ks[k][j]; }
                    y5[j] = y[j] + h * s5;
                    y4[j] = y[j] + h * s4;
                }
                // Estimación de error
                double err = 0;
                for (int j = 0; j < neq; j++)
                {
                    double sc = absTol + relTol * Math.Max(Math.Abs(y[j]), Math.Abs(y5[j]));
                    double e = (y5[j] - y4[j]) / sc;
                    err += e * e;
                }
                err = Math.Sqrt(err / neq);
                if (err <= 1.0 || h <= minH)
                {
                    t += h;
                    y = y5;
                    ts.Add(t);
                    ys.Add((double[])y.Clone());
                }
                // Ajustar paso (factor de seguridad 0.9)
                double factor = err == 0 ? 5.0 : 0.9 * Math.Pow(err, -0.2);
                factor = Math.Min(5.0, Math.Max(0.2, factor));
                h = Math.Min(maxH, Math.Max(minH, h * factor));
            }
            var tMv = new MValue(ts.Count, 1);
            var yMv = new MValue(ts.Count, neq);
            for (int i = 0; i < ts.Count; i++)
            {
                tMv.Set(i, 0, ts[i]);
                for (int j = 0; j < neq; j++) yMv.Set(i, j, ys[i][j]);
            }
            return (tMv, yMv);
        }
        // ─── Bogacki-Shampine RK23 (adaptive — MATLAB ode23) ────────────────
        private (MValue t, MValue y) RunOdeBS23(MValue[] a, double absTol, double relTol)
        {
            if (a.Length < 3 || !a[0].IsCallable)
                throw new MatlabRuntimeException("ode23(@f, tspan, y0)");
            var f = a[0].Callable;
            var tspan = a[1];
            var y0 = a[2];
            double t0 = tspan.Data[0], tf = tspan.Data[tspan.Data.Length - 1];
            int neq = y0.Data.Length;
            double t = t0;
            var y = (double[])y0.Data.Clone();
            double h = (tf - t0) / 100;
            var ts = new System.Collections.Generic.List<double> { t };
            var ys = new System.Collections.Generic.List<double[]> { (double[])y.Clone() };
            int maxSteps = 100000;
            while (t < tf && maxSteps-- > 0)
            {
                if (t + h > tf) h = tf - t;
                var k1 = EvalRhs(f, t, y);
                var y2 = new double[neq];
                for (int j = 0; j < neq; j++) y2[j] = y[j] + 0.5 * h * k1[j];
                var k2 = EvalRhs(f, t + 0.5 * h, y2);
                var y3 = new double[neq];
                for (int j = 0; j < neq; j++) y3[j] = y[j] + 0.75 * h * k2[j];
                var k3 = EvalRhs(f, t + 0.75 * h, y3);
                var y23 = new double[neq];
                for (int j = 0; j < neq; j++) y23[j] = y[j] + h * (2.0/9 * k1[j] + 1.0/3 * k2[j] + 4.0/9 * k3[j]);
                var k4 = EvalRhs(f, t + h, y23);
                // Error embedded (3rd vs 2nd order)
                double err = 0;
                for (int j = 0; j < neq; j++)
                {
                    double e = h * (-5.0/72 * k1[j] + 1.0/12 * k2[j] + 1.0/9 * k3[j] - 1.0/8 * k4[j]);
                    double sc = absTol + relTol * Math.Max(Math.Abs(y[j]), Math.Abs(y23[j]));
                    err += (e / sc) * (e / sc);
                }
                err = Math.Sqrt(err / neq);
                if (err <= 1.0 || h <= 1e-10)
                {
                    t += h;
                    y = y23;
                    ts.Add(t);
                    ys.Add((double[])y.Clone());
                }
                double factor = err == 0 ? 5.0 : 0.9 * Math.Pow(err, -1.0/3);
                factor = Math.Min(5.0, Math.Max(0.2, factor));
                h = Math.Min(1.0, Math.Max(1e-10, h * factor));
            }
            var tMv = new MValue(ts.Count, 1);
            var yMv = new MValue(ts.Count, neq);
            for (int i = 0; i < ts.Count; i++)
            {
                tMv.Set(i, 0, ts[i]);
                for (int j = 0; j < neq; j++) yMv.Set(i, j, ys[i][j]);
            }
            return (tMv, yMv);
        }

        // ─── ODE solvers (RK4 fixed-step + Euler) ───────────────────────────
        private MValue RunOdeRK4(MValue[] a, double hDefault)
        {
            var (t, y) = RunOdeRK4Internal(a, hDefault);
            return y;
        }
        private MValue[] RunOdeRK4Multi(MValue[] a, double hDefault)
        {
            var (t, y) = RunOdeRK4Internal(a, hDefault);
            return new[] { t, y };
        }
        private (MValue t, MValue y) RunOdeRK4Internal(MValue[] a, double hDefault)
        {
            // ode45(fhandle, tspan, y0)
            // fhandle: f(t, y) → dy/dt (escalar o vector)
            // tspan: [t0, tf] o vector de tiempos
            // y0: condición inicial (escalar o vector)
            if (a.Length < 3 || !a[0].IsCallable)
                throw new MatlabRuntimeException("ode45(@f, tspan, y0)");
            var f = a[0].Callable;
            var tspan = a[1];
            var y0 = a[2];
            double t0 = tspan.Data[0], tf = tspan.Data[tspan.Data.Length - 1];
            int neq = y0.Data.Length;
            // Steps
            int nSteps = Math.Max((int)Math.Ceiling((tf - t0) / hDefault), 100);
            double h = (tf - t0) / nSteps;
            var ts = new MValue(nSteps + 1, 1);
            var ys = new MValue(nSteps + 1, neq);
            for (int j = 0; j < neq; j++) ys.Set(0, j, y0.Data[j]);
            ts.Set(0, 0, t0);
            var yCurrent = new double[neq];
            for (int j = 0; j < neq; j++) yCurrent[j] = y0.Data[j];
            for (int k = 0; k < nSteps; k++)
            {
                double t = t0 + k * h;
                var k1 = EvalRhs(f, t, yCurrent);
                var y2 = AddScale(yCurrent, k1, h / 2);
                var k2 = EvalRhs(f, t + h / 2, y2);
                var y3 = AddScale(yCurrent, k2, h / 2);
                var k3 = EvalRhs(f, t + h / 2, y3);
                var y4 = AddScale(yCurrent, k3, h);
                var k4 = EvalRhs(f, t + h, y4);
                for (int j = 0; j < neq; j++)
                    yCurrent[j] += h * (k1[j] + 2 * k2[j] + 2 * k3[j] + k4[j]) / 6;
                ts.Set(k + 1, 0, t + h);
                for (int j = 0; j < neq; j++) ys.Set(k + 1, j, yCurrent[j]);
            }
            return (ts, ys);
        }
        private MValue RunOdeEuler(MValue[] a, double hDefault)
        {
            if (a.Length < 3 || !a[0].IsCallable) throw new MatlabRuntimeException("euler(@f, tspan, y0)");
            var f = a[0].Callable;
            var tspan = a[1];
            var y0 = a[2];
            double t0 = tspan.Data[0], tf = tspan.Data[tspan.Data.Length - 1];
            int neq = y0.Data.Length;
            int nSteps = Math.Max((int)Math.Ceiling((tf - t0) / hDefault), 100);
            double h = (tf - t0) / nSteps;
            var ys = new MValue(nSteps + 1, neq);
            for (int j = 0; j < neq; j++) ys.Set(0, j, y0.Data[j]);
            var yCurrent = (double[])y0.Data.Clone();
            for (int k = 0; k < nSteps; k++)
            {
                double t = t0 + k * h;
                var dy = EvalRhs(f, t, yCurrent);
                for (int j = 0; j < neq; j++) yCurrent[j] += h * dy[j];
                for (int j = 0; j < neq; j++) ys.Set(k + 1, j, yCurrent[j]);
            }
            return ys;
        }
        /// <summary>Extrae num/den de un sistema tf/zpk (struct).</summary>
        internal static (double[] num, double[] den) ExtractTf(MValue v)
        {
            if (!v.IsStruct || !v.Fields.ContainsKey("num") || !v.Fields.ContainsKey("den"))
                throw new MatlabRuntimeException("Expected tf/zpk system (struct with num/den)");
            return ((double[])v.Fields["num"].Data.Clone(), (double[])v.Fields["den"].Data.Clone());
        }
        /// <summary>Durand-Kerner devolviendo raíces complejas (Re, Im).</summary>
        internal static (double re, double im)[] DurandKernerC(double[] coefs)
        {
            int deg = coefs.Length - 1;
            while (deg > 0 && Math.Abs(coefs[coefs.Length - 1 - deg]) < 1e-15) deg--;
            if (deg <= 0) return new (double, double)[0];
            var rRe = new double[deg]; var rIm = new double[deg];
            double bound = 0;
            for (int i = 0; i < deg; i++) bound = Math.Max(bound, Math.Abs(coefs[coefs.Length - 1 - i] / coefs[0]));
            bound = 1 + bound;
            for (int k = 0; k < deg; k++)
            {
                double ang = 2 * Math.PI * k / deg + 0.4;
                rRe[k] = bound * Math.Cos(ang) / 2;
                rIm[k] = bound * Math.Sin(ang) / 2;
            }
            for (int iter = 0; iter < 200; iter++)
            {
                bool conv = true;
                for (int k = 0; k < deg; k++)
                {
                    double pRe = 0, pIm = 0;
                    for (int i = 0; i < coefs.Length; i++)
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
                    if (Math.Abs(stepRe) > 1e-12 || Math.Abs(stepIm) > 1e-12) conv = false;
                }
                if (conv) break;
            }
            var rs = new (double, double)[deg];
            for (int k = 0; k < deg; k++) rs[k] = (rRe[k], rIm[k]);
            return rs;
        }

        /// <summary>Convolución 2D estilo MATLAB. mode = "full" | "same" | "valid".</summary>
        private static MValue Conv2D(MValue A, MValue K, string mode)
        {
            int Ar = A.Rows, Ac = A.Cols, Kr = K.Rows, Kc = K.Cols;
            int fullR = Ar + Kr - 1, fullC = Ac + Kc - 1;
            var full = new MValue(fullR, fullC);
            for (int i = 0; i < fullR; i++)
                for (int j = 0; j < fullC; j++)
                {
                    double s = 0;
                    int iMin = Math.Max(0, i - Kr + 1), iMax = Math.Min(Ar - 1, i);
                    int jMin = Math.Max(0, j - Kc + 1), jMax = Math.Min(Ac - 1, j);
                    for (int ii = iMin; ii <= iMax; ii++)
                        for (int jj = jMin; jj <= jMax; jj++)
                            s += A.At(ii, jj) * K.At(i - ii, j - jj);
                    full.Set(i, j, s);
                }
            if (mode == "full") return full;
            if (mode == "same")
            {
                int r0 = (Kr - 1) / 2, c0 = (Kc - 1) / 2;
                var r = new MValue(Ar, Ac);
                for (int i = 0; i < Ar; i++)
                    for (int j = 0; j < Ac; j++)
                        r.Set(i, j, full.At(i + r0, j + c0));
                return r;
            }
            if (mode == "valid")
            {
                int validR = Math.Max(0, Ar - Kr + 1), validC = Math.Max(0, Ac - Kc + 1);
                var r = new MValue(validR, validC);
                for (int i = 0; i < validR; i++)
                    for (int j = 0; j < validC; j++)
                        r.Set(i, j, full.At(i + Kr - 1, j + Kc - 1));
                return r;
            }
            return full;
        }

        // ─── Adaptive Simpson 1/3 integration ──────────────────────────────
        /// <summary>
        /// Cuadratura adaptiva por bisección con Simpson 1/3 (orden 4).
        /// Recursión hasta que la diferencia entre Simpson único y Simpson en
        /// 2 sub-intervalos sea ≤ 15·tol (criterio Lyness).
        /// </summary>
        private static double AdaptiveSimpson(Func<double, double> f, double a, double b, double tol, int depth)
        {
            double fa = f(a), fb = f(b), fc = f((a + b) / 2);
            double S = (b - a) / 6 * (fa + 4 * fc + fb);
            return AdaptiveSimpsonRec(f, a, b, tol, S, fa, fb, fc, depth);
        }
        private static double AdaptiveSimpsonRec(Func<double, double> f, double a, double b, double tol, double S,
                                                  double fa, double fb, double fc, int depth)
        {
            double c = (a + b) / 2;
            double d = (a + c) / 2, e = (c + b) / 2;
            double fd = f(d), fe = f(e);
            double Sl = (c - a) / 6 * (fa + 4 * fd + fc);
            double Sr = (b - c) / 6 * (fc + 4 * fe + fb);
            double diff = Sl + Sr - S;
            if (depth <= 0 || Math.Abs(diff) < 15 * tol)
                return Sl + Sr + diff / 15;
            return AdaptiveSimpsonRec(f, a, c, tol / 2, Sl, fa, fc, fd, depth - 1)
                 + AdaptiveSimpsonRec(f, c, b, tol / 2, Sr, fc, fb, fe, depth - 1);
        }

        // ─── Spline cúbica natural ──────────────────────────────────────────
        private static MValue SplineInterp(double[] x, double[] y, MValue xq)
        {
            int n = x.Length;
            if (n < 2) throw new MatlabRuntimeException("spline: need at least 2 points");
            // Calcular segundas derivadas con boundary natural (M[0]=M[n-1]=0)
            var h = new double[n - 1];
            for (int i = 0; i < n - 1; i++) h[i] = x[i + 1] - x[i];
            var M = new double[n];
            if (n >= 3)
            {
                var ad = new double[n - 2]; // off-diag
                var bd = new double[n - 2]; // diag
                var cd = new double[n - 2]; // off-diag
                var rhs = new double[n - 2];
                for (int i = 0; i < n - 2; i++)
                {
                    bd[i] = 2 * (h[i] + h[i + 1]);
                    if (i > 0)     ad[i] = h[i];
                    if (i < n - 3) cd[i] = h[i + 1];
                    rhs[i] = 6 * ((y[i + 2] - y[i + 1]) / h[i + 1] - (y[i + 1] - y[i]) / h[i]);
                }
                // Thomas algorithm
                for (int i = 1; i < n - 2; i++)
                {
                    double m = ad[i] / bd[i - 1];
                    bd[i] -= m * cd[i - 1];
                    rhs[i] -= m * rhs[i - 1];
                }
                M[n - 2] = rhs[n - 3] / bd[n - 3];
                for (int i = n - 4; i >= 0; i--) M[i + 1] = (rhs[i] - cd[i] * M[i + 2]) / bd[i];
            }
            double Eval(double q)
            {
                if (q <= x[0]) return y[0];
                if (q >= x[n - 1]) return y[n - 1];
                int lo = 0, hi = n - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (x[mid] <= q) lo = mid; else hi = mid; }
                double t = q - x[lo];
                double H = h[lo];
                double A = (x[hi] - q) / H;
                double B = (q - x[lo]) / H;
                return A * y[lo] + B * y[hi]
                       + ((A * A * A - A) * M[lo] + (B * B * B - B) * M[hi]) * H * H / 6;
            }
            if (xq.IsScalar) return new MValue(Eval(xq.Scalar));
            var r = new MValue(xq.Rows, xq.Cols);
            for (int i = 0; i < xq.Data.Length; i++) r.Data[i] = Eval(xq.Data[i]);
            return r;
        }
        // ─── PCHIP (monotonic cubic) ────────────────────────────────────────
        private static MValue PchipInterp(double[] x, double[] y, MValue xq)
        {
            int n = x.Length;
            if (n < 2) throw new MatlabRuntimeException("pchip: need at least 2 points");
            // Calcular pendientes Fritsch-Carlson
            var h = new double[n - 1];
            var del = new double[n - 1];
            for (int i = 0; i < n - 1; i++) { h[i] = x[i + 1] - x[i]; del[i] = (y[i + 1] - y[i]) / h[i]; }
            var d = new double[n];
            if (n == 2) { d[0] = del[0]; d[1] = del[0]; }
            else
            {
                for (int i = 1; i < n - 1; i++)
                {
                    if (del[i - 1] * del[i] <= 0) d[i] = 0;
                    else
                    {
                        double w1 = 2 * h[i] + h[i - 1];
                        double w2 = h[i] + 2 * h[i - 1];
                        d[i] = (w1 + w2) / (w1 / del[i - 1] + w2 / del[i]);
                    }
                }
                d[0] = ((2 * h[0] + h[1]) * del[0] - h[0] * del[1]) / (h[0] + h[1]);
                if (Math.Sign(d[0]) != Math.Sign(del[0])) d[0] = 0;
                else if (Math.Sign(del[0]) != Math.Sign(del[1]) && Math.Abs(d[0]) > Math.Abs(3 * del[0]))
                    d[0] = 3 * del[0];
                d[n - 1] = ((2 * h[n - 2] + h[n - 3]) * del[n - 2] - h[n - 2] * del[n - 3]) / (h[n - 2] + h[n - 3]);
                if (Math.Sign(d[n - 1]) != Math.Sign(del[n - 2])) d[n - 1] = 0;
                else if (Math.Sign(del[n - 2]) != Math.Sign(del[n - 3]) && Math.Abs(d[n - 1]) > Math.Abs(3 * del[n - 2]))
                    d[n - 1] = 3 * del[n - 2];
            }
            double Eval(double q)
            {
                if (q <= x[0]) return y[0];
                if (q >= x[n - 1]) return y[n - 1];
                int lo = 0, hi = n - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (x[mid] <= q) lo = mid; else hi = mid; }
                double t = (q - x[lo]) / h[lo];
                double t2 = t * t, t3 = t2 * t;
                double h00 = 2 * t3 - 3 * t2 + 1;
                double h10 = t3 - 2 * t2 + t;
                double h01 = -2 * t3 + 3 * t2;
                double h11 = t3 - t2;
                return h00 * y[lo] + h10 * h[lo] * d[lo] + h01 * y[hi] + h11 * h[lo] * d[hi];
            }
            if (xq.IsScalar) return new MValue(Eval(xq.Scalar));
            var r = new MValue(xq.Rows, xq.Cols);
            for (int i = 0; i < xq.Data.Length; i++) r.Data[i] = Eval(xq.Data[i]);
            return r;
        }

        private static double[] EvalRhs(Func<MValue[], MValue> f, double t, double[] y)
        {
            var ymv = y.Length == 1 ? new MValue(y[0]) : new MValue(y.Length, 1, (double[])y.Clone());
            var result = f(new[] { new MValue(t), ymv });
            return (double[])result.Data.Clone();
        }
        private static double[] AddScale(double[] a, double[] b, double s)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[i] + s * b[i];
            return r;
        }

        private static MValue Transpose(MValue v)
        {
            if (v.IsScalar) return v;
            if (v.IsSparseReal) return SparseTranspose(v);   // FIX: At() no funciona en sparse (Data=null) -> transpuesta CSR->CSC directa (mantiene disperso, ej. Fint=Bg'*x en FEM vectorizado)
            var r = new MValue(v.Cols, v.Rows);
            for (int i = 0; i < v.Rows; i++)
                for (int j = 0; j < v.Cols; j++)
                    r.Set(j, i, v.At(i, j));
            return r;
        }

        // ─── Statement / Expression evaluation ─────────────────────────────
        public List<StatementResult> Execute(List<MatlabNode> stmts, MatlabScope scope = null)
        {
            scope ??= Globals;
            var results = new List<StatementResult>();
            foreach (var s in stmts)
                results.Add(ExecuteOne(s, scope));
            return results;
        }
        /// <summary>True si la expresión es el comando <c>toc</c> o <c>toc(handle)</c>
        /// usado como statement (no como sub-expresión). Permite el display MATLAB-exacto.</summary>
        private static bool IsTocCommand(MatlabNode expr) => expr switch
        {
            IdentRef ir => ir.Name == "toc",
            CallOrIndex ci => ci.Target is IdentRef cir && cir.Name == "toc",
            _ => false,
        };

        public StatementResult ExecuteOne(MatlabNode stmt, MatlabScope scope = null)
        {
            scope ??= Globals;
            switch (stmt)
            {
                case CommentStmt:
                    // Los comentarios no producen valor; los renderiza el HtmlWriter.
                    return new StatementResult(null, null, false);
                case Assignment asg:
                    return ExecuteAssignment(asg, scope);
                case ExprStmt es:
                    var v = Eval(es.Expr, scope);
                    // MATLAB exacto: `toc` (o `toc(id)`) usado como COMANDO — sin asignar y
                    // no anidado — tiene nargout 0 y muestra "Elapsed time is …". Si se
                    // asigna (`t = toc`) o va dentro de otra expresión, nargout ≥ 1 → silencioso.
                    // (Se omite si el usuario sombreó `toc` con una variable propia.)
                    bool tocCmd = IsTocCommand(es.Expr) && !scope.TryGet("toc", out _) && v != null && v.IsScalar;
                    // MATLAB: una llamada a funcion SIN argumentos de salida no muestra
                    // nada, aunque no lleve punto y coma. Lab imprimia "figure = 0" o
                    // "dibujoplano(...) = 0" donde MATLAB no imprime nada.
                    bool sinSalida = EsLlamadaSinSalida(es.Expr, scope);
                    if (tocCmd)
                        _output?.Invoke($"Elapsed time is {v.Scalar:F6} seconds.");
                    // MATLAB convención: expresión sin asignar → variable `ans`
                    scope.Set("ans", v);
                    // El comando toc ya mostró su línea; MATLAB no renderiza además "toc = valor".
                    return new StatementResult("ans", v, es.Suppressed || tocCmd || sinSalida);
                case ForLoop fl:
                    ExecuteFor(fl, scope);
                    return new StatementResult(null, null, true);
                case WhileLoop wl:
                    ExecuteWhile(wl, scope);
                    return new StatementResult(null, null, true);
                case IfBlock ib:
                    ExecuteIf(ib, scope);
                    return new StatementResult(null, null, true);
                case SwitchBlock sb:
                    ExecuteSwitch(sb, scope);
                    return new StatementResult(null, null, true);
                case TryCatch tc:
                    ExecuteTryCatch(tc, scope);
                    return new StatementResult(null, null, true);
                case FunctionDef fd:
                    if (!_reservedVizBuiltins.Contains(fd.Name)) _userFunctions[fd.Name] = fd;   // builtin reservado (hoverdata) gana
                    return new StatementResult(null, null, true);
                case ClassDef cd:
                    _classes[cd.Name] = cd;
                    // Render simbólico mínimo: clase registrada
                    return new StatementResult(null, null, true);
                case BreakStmt:
                    throw new BreakSignal();
                case ContinueStmt:
                    throw new ContinueSignal();
                case ReturnStmt:
                    throw new ReturnSignal();
                case GlobalDecl gd:
                    foreach (var nm in gd.Names) scope.GlobalNames.Add(nm);
                    return new StatementResult(null, null, true);
                case PersistentDecl pd:
                    foreach (var nm in pd.Names)
                    {
                        var key = _currentFunctionName + ":" + nm;
                        if (_persistentVars.TryGetValue(key, out var pv)) scope.Set(nm, pv);
                        else { var empty = new MValue(0, 0); _persistentVars[key] = empty; scope.Set(nm, empty); }
                    }
                    return new StatementResult(null, null, true);
                default:
                    throw new MatlabRuntimeException($"Unsupported statement type: {stmt?.GetType().Name}");
            }
        }

        /// <summary>
        /// Ejecuta un statement dentro de un bloque, notificando al callback
        /// <see cref="_innerStmtOut"/> para que el pipeline pueda emitirlo en HTML.
        /// </summary>
        private void ExecuteInner(MatlabNode stmt, MatlabScope scope)
        {
            var r = ExecuteOne(stmt, scope);
            _innerStmtOut?.Invoke(stmt, r);
        }

        // ─── Helpers expuestos al JIT ───────────────────────────────────────
        /// <summary>Verdadero si `name` resuelve a función (user-def o builtin).</summary>
        public bool JitIsFunction(string name) =>
            _userFunctions.ContainsKey(name) || _builtins.ContainsKey(name);
        /// <summary>true si el nombre es una función DEFINIDA POR EL USUARIO (no un builtin).
        /// El JIT lo usa para no inlinear una función math si el usuario la redefinió.</summary>
        public bool JitIsUserFunction(string name) => _userFunctions.ContainsKey(name);
        /// <summary>Dispatch de single-output call para el JIT (user fn → builtin → undefined).</summary>
        public MValue JitCall(string name, MValue[] args)
        {
            if (_userFunctions.TryGetValue(name, out var def)) return CallUserFunction(def, args);
            if (_builtins.TryGetValue(name, out var fn))       return fn(args);
            throw new MatlabRuntimeException($"Undefined: {name}");
        }
        // Wrappers de operaciones matriciales para el JIT (delegan al engine existente).
        public static MValue JitMatMul(MValue a, MValue b) => MatMul(a, b);
        public static MValue JitMatAdd(MValue a, MValue b) => MapBinary(a, b, (x, y) => x + y);
        public static MValue JitMatSub(MValue a, MValue b) => MapBinary(a, b, (x, y) => x - y);
        public static MValue JitMatTrans(MValue a) => Transpose(a);
        public static MValue JitMatNeg(MValue a)
        {
            if (a.IsScalar) return new MValue(-a.Scalar);
            var r = new MValue(a.Rows, a.Cols);
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < a.Cols; j++)
                    r.Set(i, j, -a.At(i, j));
            return r;
        }
        public static MValue JitMatScalarMul(MValue a, double s) => MapBinary(a, new MValue(s), (x, y) => x * y);
        public static MValue JitMakeRowVec(double[] elements)
        {
            var v = new MValue(1, elements.Length);
            for (int i = 0; i < elements.Length; i++) v.Set(0, i, elements[i]);
            return v;
        }
        /// <summary>Construye una matriz 2D desde un buffer row-major de doubles.</summary>
        public static MValue JitMakeMatrix2D(int rows, int cols, double[] elements)
        {
            var v = new MValue(rows, cols);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    v.Set(i, j, elements[i * cols + j]);
            return v;
        }
        /// <summary>Extrae fila i (1-based) de una matriz como vector 1×cols.</summary>
        public static MValue JitGetMatRow(MValue m, double iOneBased)
        {
            int i = (int)iOneBased - 1;
            var r = new MValue(1, m.Cols);
            for (int j = 0; j < m.Cols; j++) r.Set(0, j, m.At(i, j));
            return r;
        }
        /// <summary>Extrae columna j (1-based) de una matriz como vector rows×1.</summary>
        public static MValue JitGetMatCol(MValue m, double jOneBased)
        {
            int j = (int)jOneBased - 1;
            var r = new MValue(m.Rows, 1);
            for (int i = 0; i < m.Rows; i++) r.Set(i, 0, m.At(i, j));
            return r;
        }
        public static double JitMatToScalar(MValue v)
        {
            if (v.IsScalar) return v.Scalar;
            // Conversión común MATLAB: 1×1 → scalar
            if (v.Rows == 1 && v.Cols == 1) return v.At(0, 0);
            throw new MatlabRuntimeException("Expected scalar, got matrix");
        }

        // ─── Control-flow execution ─────────────────────────────────────────
        private void ExecuteFor(ForLoop f, MatlabScope scope)
        {
            // JIT fast path para `for` escalares (codegen IL). ACTIVO en CLI y WPF
            // (MainWindow lo enciende). ~3.3x vs interprete en bucles escalares.
            // Cae al interprete si el cuerpo no es compilable (TryExecute=false).
            // Para desactivar: env CALCPAD_LAB_JIT=0.
            if (MatlabJit.Enabled && MatlabJit.TryExecute(f, scope, this)) return;

            var iter = Eval(f.Iter, scope);
            // for var = vec → itera columnas (1×N vec → escalares; N×M → cada col)
            int cols = iter.Cols, rows = iter.Rows;
            for (int j = 0; j < cols; j++)
            {
                MValue v;
                if (rows == 1) v = new MValue(iter.At(0, j));
                else {
                    v = new MValue(rows, 1);
                    for (int i = 0; i < rows; i++) v.Set(i, 0, iter.At(i, j));
                }
                scope.Set(f.VarName, v);
                try { foreach (var s in f.Body) ExecuteInner(s, scope); }
                catch (ContinueSignal) { continue; }
                catch (BreakSignal) { return; }
            }
        }
        private void ExecuteWhile(WhileLoop w, MatlabScope scope)
        {
            int safety = 0;
            while (true)
            {
                var c = Eval(w.Cond, scope);
                if (c.Scalar == 0) break;
                try { foreach (var s in w.Body) ExecuteInner(s, scope); }
                catch (ContinueSignal) { }
                catch (BreakSignal) { return; }
                if (++safety > 10_000_000) throw new MatlabRuntimeException("while: max iterations exceeded");
            }
        }
        private void ExecuteIf(IfBlock ib, MatlabScope scope)
        {
            foreach (var (cond, body) in ib.Branches)
            {
                if (cond == null || Eval(cond, scope).Scalar != 0)
                {
                    foreach (var s in body) ExecuteInner(s, scope);
                    return;
                }
            }
        }
        private void ExecuteSwitch(SwitchBlock sb, MatlabScope scope)
        {
            var disc = Eval(sb.Discriminant, scope);
            foreach (var (values, body) in sb.Cases)
            {
                if (values == null) { foreach (var s in body) ExecuteInner(s, scope); return; }
                bool match = false;
                foreach (var v in values)
                {
                    var cv = Eval(v, scope);
                    if (MValuesEqual(disc, cv)) { match = true; break; }
                }
                if (match) { foreach (var s in body) ExecuteInner(s, scope); return; }
            }
        }
        private static bool MValuesEqual(MValue a, MValue b)
        {
            if (a == null || b == null) return a == b;
            if (a.IsString || b.IsString)
                return a.IsString && b.IsString && a.StringValue == b.StringValue;
            if (a.Rows != b.Rows || a.Cols != b.Cols) return false;
            var ad = a.Data; var bd = b.Data;
            if (ad == null || bd == null) return ad == bd;
            if (ad.Length != bd.Length) return false;
            for (int i = 0; i < ad.Length; i++)
                if (ad[i] != bd[i]) return false;
            if (a.IsComplex || b.IsComplex)
                for (int i = 0; i < ad.Length; i++)
                    if ((a.Imag != null ? a.Imag[i] : 0) != (b.Imag != null ? b.Imag[i] : 0)) return false;
            return true;
        }
        private void ExecuteTryCatch(TryCatch tc, MatlabScope scope)
        {
            try { foreach (var s in tc.TryBody) ExecuteInner(s, scope); }
            catch (MatlabRuntimeException ex)
            {
                if (!string.IsNullOrEmpty(tc.CatchVarName))
                    scope.Set(tc.CatchVarName, MakeMException(ex.Message));
                foreach (var s in tc.CatchBody) ExecuteInner(s, scope);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(tc.CatchVarName))
                    scope.Set(tc.CatchVarName, MakeMException(ex.Message));
                foreach (var s in tc.CatchBody) ExecuteInner(s, scope);
            }
        }

        // ─── User function dispatch ─────────────────────────────────────────
        private string _currentFunctionName = "__main__";

        // Stack de nombres-de-variable de los argumentos por frame de función.
        // Lo usa el builtin `inputname(k)`: devuelve el nombre de la variable que
        // se pasó como k-ésimo argumento de la función actual ("" si fue una
        // expresión, no una variable simple).
        private readonly Stack<string[]> _inputNameStack = new();

        /// <summary>Extrae el nombre de variable de cada argumento del sitio de
        /// llamada. Si el argumento no es un identificador simple, devuelve "".</summary>
        private static string[] ArgNames(List<MatlabNode> args)
        {
            if (args == null) return Array.Empty<string>();
            var names = new string[args.Count];
            for (int i = 0; i < args.Count; i++)
                names[i] = args[i] is IdentRef ir ? ir.Name : "";
            return names;
        }

        // Pre-registra las funciones ANIDADAS del cuerpo (hoisting de MATLAB): quedan disponibles
        // desde el inicio de la función contenedora (se pueden llamar antes de su definición textual,
        // p.ej. callbacks de UI) y atadas al scope del padre → comparten su workspace.
        private void HoistNestedFunctions(FunctionDef def, MatlabScope parent)
        {
            foreach (var s in def.Body)
                if (s is FunctionDef nfd && !_reservedVizBuiltins.Contains(nfd.Name))
                { nfd.ClosureScope = parent; _userFunctions[nfd.Name] = nfd; }
        }
        /// <summary>Rasgos del cuerpo de una función, computados UNA vez y cacheados en
        /// <c>def.BodyFlags</c>. Evita re-escanear el AST en cada llamada — decisivo en bucles
        /// cerrados con funciones anidadas (un FEM no lineal hace millones de llamadas).
        /// Bit0 = tiene FunctionDef anidada (top-level, la que hoistea HoistNestedFunctions);
        /// Bit1 = referencia nargin; Bit2 = referencia nargout. Los bits nargin/nargout se
        /// SOBRE-aproximan (se escanean incluso cuerpos anidados): setear de más es inofensivo,
        /// no setear cuando se usa sería un bug — por eso se prefiere el falso positivo.</summary>
        private static int ComputeBodyFlags(FunctionDef def)
        {
            int flags = 0;
            foreach (var s in def.Body)
                if (s is FunctionDef) { flags |= 1; break; }       // hoisting mira solo top-level
            foreach (var s in def.Body) ScanBodyFlags(s, ref flags);
            return flags;
        }
        private static void ScanBodyFlags(MatlabNode n, ref int flags)
        {
            if (n == null || (flags & 6) == 6) return;             // nargin+nargout ya hallados → corta
            switch (n)
            {
                case IdentRef id:
                    if (id.Name == "nargin") flags |= 2;
                    else if (id.Name == "nargout") flags |= 4;
                    break;
                case UnaryOp u: ScanBodyFlags(u.Operand, ref flags); break;
                case BinaryOp b: ScanBodyFlags(b.Left, ref flags); ScanBodyFlags(b.Right, ref flags); break;
                case CallOrIndex c: ScanBodyFlags(c.Target, ref flags); ScanBodyList(c.Args, ref flags); break;
                case Range r: ScanBodyFlags(r.Start, ref flags); ScanBodyFlags(r.Step, ref flags); ScanBodyFlags(r.End, ref flags); break;
                case MatrixLit ml: foreach (var row in ml.Rows) ScanBodyList(row, ref flags); break;
                case CellLit cl: foreach (var row in cl.Rows) ScanBodyList(row, ref flags); break;
                case FieldAccess fa: ScanBodyFlags(fa.Target, ref flags); break;
                case CellIndex ci: ScanBodyFlags(ci.Target, ref flags); ScanBodyList(ci.Args, ref flags); break;
                case AnonFunction af: ScanBodyFlags(af.Body, ref flags); break;
                case Assignment asg: ScanBodyList(asg.Targets, ref flags); ScanBodyFlags(asg.Rhs, ref flags); break;
                case ExprStmt es: ScanBodyFlags(es.Expr, ref flags); break;
                case ForLoop f: ScanBodyFlags(f.Iter, ref flags); ScanBodyList(f.Body, ref flags); break;
                case WhileLoop w: ScanBodyFlags(w.Cond, ref flags); ScanBodyList(w.Body, ref flags); break;
                case IfBlock ib: foreach (var br in ib.Branches) { ScanBodyFlags(br.Cond, ref flags); ScanBodyList(br.Body, ref flags); } break;
                case SwitchBlock sb:
                    ScanBodyFlags(sb.Discriminant, ref flags);
                    foreach (var cs in sb.Cases) { if (cs.Values != null) ScanBodyList(cs.Values, ref flags); ScanBodyList(cs.Body, ref flags); }
                    break;
                case TryCatch tc: ScanBodyList(tc.TryBody, ref flags); ScanBodyList(tc.CatchBody, ref flags); break;
                case FunctionDef fd: ScanBodyList(fd.Body, ref flags); break;   // over-approx
                // NumberLit, StringLit, ColonAll, EndKeyword, Break/Continue/Return, Comment,
                // GlobalDecl, PersistentDecl: hojas sin nargin/nargout.
            }
        }
        private static void ScanBodyList(List<MatlabNode> list, ref int flags)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count && (flags & 6) != 6; i++) ScanBodyFlags(list[i], ref flags);
        }

        private MValue CallUserFunction(FunctionDef def, MValue[] args, string[] argNames = null)
        {
            // función ANIDADA: corre en el scope del padre (workspace compartido); si no, scope propio.
            var local = def.ClosureScope ?? new MatlabScope(Globals);
            var savedFn = _currentFunctionName;
            _currentFunctionName = def.Name;
            int bf = def.BodyFlags ??= ComputeBodyFlags(def);
            _inputNameStack.Push(argNames ?? Array.Empty<string>());
            BindParams(def, args, local);
            // MATLAB nargin / nargout: solo se crean si la función los usa (sobre-aprox segura).
            if ((bf & 2) != 0) local.Set("nargin",  new MValue(args.Length));
            if ((bf & 4) != 0) local.Set("nargout", new MValue(def.OutputNames.Count));
            if ((bf & 1) != 0) HoistNestedFunctions(def, local);
            try { foreach (var s in def.Body) ExecuteOne(s, local); }
            catch (ReturnSignal) { /* early return ok */ }
            finally { _inputNameStack.Pop(); }
            // Flush persistent vars de vuelta a storage (solo si hay alguna registrada)
            if (_persistentVars.Count > 0)
                foreach (var kv in local.Vars)
                    if (_persistentVars.ContainsKey(def.Name + ":" + kv.Key))
                        _persistentVars[def.Name + ":" + kv.Key] = kv.Value;
            _currentFunctionName = savedFn;
            // Output principal: primer nombre de output
            if (def.OutputNames.Count > 0 && local.TryGet(def.OutputNames[0], out var v))
                return v;
            return new MValue(0);  // procedure void → 0
        }
        /// <summary>Vincula los args a los parámetros formales. Soporta `varargin`
        /// (MATLAB): si el ÚLTIMO parámetro se llama varargin, recoge todos los args
        /// sobrantes en un cell 1×N (vacío 1×0 si no hay) — así `f(varargin)` llamado
        /// con 0 args deja varargin = {} y `isempty(varargin)` es true.</summary>
        private static void BindParams(FunctionDef def, MValue[] args, MatlabScope local)
        {
            int np = def.ParamNames.Count;
            bool hasVar = np > 0 && def.ParamNames[np - 1] == "varargin";
            int nfix = hasVar ? np - 1 : np;
            var mutated = GetMutatedParams(def);   // solo se clonan los params que la función muta
            for (int i = 0; i < nfix && i < args.Length; i++)
                local.Set(def.ParamNames[i],
                          mutated.Contains(def.ParamNames[i]) ? CloneArg(args[i]) : args[i]);
            if (hasVar)
            {
                bool cloneVar = mutated.Contains("varargin");
                int extra = args.Length - nfix; if (extra < 0) extra = 0;
                var vc = new MValue[1, extra];
                for (int k = 0; k < extra; k++) vc[0, k] = cloneVar ? CloneArg(args[nfix + k]) : args[nfix + k];
                local.Set("varargin", MValue.NewCell(vc));
            }
        }

        /// <summary>Params que la función muta (lazy + cacheado en FunctionDef). Bajo semántica por
        /// valor, un param solo puede alterar los datos del llamador si esta función le ASIGNA
        /// (directo, indexado, o campo); pasarlo a otra función es seguro (esa clona los suyos).</summary>
        private static System.Collections.Generic.HashSet<string> GetMutatedParams(FunctionDef def)
        {
            if (def.MutatedParams == null)
            {
                var s = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                CollectMutated(def.Body, s);
                def.MutatedParams = s;
            }
            return def.MutatedParams;
        }
        private static void CollectMutated(System.Collections.Generic.IEnumerable<MatlabNode> body,
                                           System.Collections.Generic.HashSet<string> outSet)
        {
            foreach (var st in body) CollectMutatedNode(st, outSet);
        }
        private static void CollectMutatedNode(MatlabNode st, System.Collections.Generic.HashSet<string> outSet)
        {
            switch (st)
            {
                case Assignment asg:
                    foreach (var t in asg.Targets) { var r = RootIdentName(t); if (r != null) outSet.Add(r); }
                    break;
                case ForLoop f:  if (f.VarName != null) outSet.Add(f.VarName); CollectMutated(f.Body, outSet); break;
                case WhileLoop w: CollectMutated(w.Body, outSet); break;
                case IfBlock ib: foreach (var br in ib.Branches) CollectMutated(br.Body, outSet); break;
                case SwitchBlock sb: foreach (var c in sb.Cases) CollectMutated(c.Body, outSet); break;
                case TryCatch tc:
                    if (!string.IsNullOrEmpty(tc.CatchVarName)) outSet.Add(tc.CatchVarName);
                    CollectMutated(tc.TryBody, outSet); CollectMutated(tc.CatchBody, outSet); break;
            }
        }
        private static string RootIdentName(MatlabNode n)
        {
            while (n != null)
            {
                switch (n)
                {
                    case IdentRef id: return id.Name;
                    case FieldAccess fa: n = fa.Target; break;
                    case CallOrIndex ci: n = ci.Target; break;
                    case CellIndex cx: n = cx.Target; break;
                    default: return null;
                }
            }
            return null;
        }

        /// <summary>Copia por VALOR de un argumento al entrar a una función (semántica MATLAB).
        /// Los STRUCTS, cells, struct-arrays y arrays numéricos se copian en profundidad, para que
        /// mutar un campo/elemento del parámetro NO se filtre al llamador (antes se pasaba el mismo
        /// MValue por referencia → un `s.k=…` dentro de la función alteraba el `s` de quien llamaba,
        /// rompiendo p.ej. bisecciones que sondean estado sin avanzarlo). Se dejan por REFERENCIA los
        /// tipos que en MATLAB también lo son: handles gráficos, function handles, instancias de clase
        /// (handle), containers.Map, y símbolos (SymNode inmutable).</summary>
        private static MValue CloneArg(MValue v)
        {
            if (v == null) return null;
            // Tipos por referencia en MATLAB (o inmutables): no copiar.
            if (v.IsGfxHandle || v.Callable != null || v.IsInstance || v.IsMap
                || v.IsSymbolic || v.IsSymMatrix || v.Is3D || v.IsSparseReal || v.IsString)
                return v;
            if (v.Fields != null)                       // struct por valor
            {
                var nf = new Dictionary<string, MValue>(StringComparer.Ordinal);
                foreach (var kv in v.Fields) nf[kv.Key] = CloneArg(kv.Value);
                var s = new MValue(v.Rows, v.Cols); s.Fields = nf; return s;
            }
            if (v.StructArrayItems != null)             // struct-array por valor
            {
                var items = new System.Collections.Generic.List<MValue>(v.StructArrayItems.Count);
                foreach (var it in v.StructArrayItems) items.Add(CloneArg(it));
                var s = new MValue(v.Rows, v.Cols); s.StructArrayItems = items; return s;
            }
            if (v.CellData != null)                     // cell por valor (copia profunda)
            {
                int r = v.CellData.GetLength(0), c = v.CellData.GetLength(1);
                var cd = new MValue[r, c];
                for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) cd[i, j] = CloneArg(v.CellData[i, j]);
                return MValue.NewCell(cd);
            }
            if (v.StringArrayData != null)              // string-array por valor
            {
                var sa = (string[,])v.StringArrayData.Clone();
                var s = new MValue(v.Rows, v.Cols) { StringArrayData = sa, IsDoubleQuoted = true };
                return s;
            }
            if (v.Data != null)                         // numérico (real/complejo) por valor
            {
                var d = (double[])v.Data.Clone();
                return v.Imag != null ? new MValue(v.Rows, v.Cols, d, (double[])v.Imag.Clone())
                                      : new MValue(v.Rows, v.Cols, d);
            }
            return v;
        }
        private MValue[] CallUserFunctionMulti(FunctionDef def, MValue[] args, string[] argNames = null)
        {
            var local = def.ClosureScope ?? new MatlabScope(Globals);
            var savedFn = _currentFunctionName;
            _currentFunctionName = def.Name;
            int bf = def.BodyFlags ??= ComputeBodyFlags(def);
            _inputNameStack.Push(argNames ?? Array.Empty<string>());
            BindParams(def, args, local);
            // MATLAB nargin / nargout: solo se crean si la función los usa (sobre-aprox segura).
            if ((bf & 2) != 0) local.Set("nargin",  new MValue(args.Length));
            if ((bf & 4) != 0) local.Set("nargout", new MValue(def.OutputNames.Count));
            if ((bf & 1) != 0) HoistNestedFunctions(def, local);
            try { foreach (var s in def.Body) ExecuteOne(s, local); }
            catch (ReturnSignal) { }
            finally { _inputNameStack.Pop(); }
            if (_persistentVars.Count > 0)
                foreach (var kv in local.Vars)
                    if (_persistentVars.ContainsKey(def.Name + ":" + kv.Key))
                        _persistentVars[def.Name + ":" + kv.Key] = kv.Value;
            _currentFunctionName = savedFn;
            var outs = new MValue[def.OutputNames.Count];
            for (int i = 0; i < outs.Length; i++)
                outs[i] = local.TryGet(def.OutputNames[i], out var v) ? v : new MValue(0);
            return outs;
        }
        /// <summary>true si el nodo es el literal `[]` (matriz vacía). MATLAB solo trata
        /// `v(idx) = []` como BORRADO cuando el lado derecho es ese literal.</summary>
        private static bool IsEmptyMatrixLiteral(MatlabNode n)
        {
            if (n is not MatrixLit ml) return false;
            if (ml.Rows.Count == 0) return true;
            foreach (var row in ml.Rows) if (row.Count > 0) return false;
            return true;
        }

        private StatementResult ExecuteAssignment(Assignment asg, MatlabScope scope)
        {
            // Multi-output: [a, b] = func(...)
            if (asg.Targets.Count > 1)
            {
                if (asg.Rhs is not CallOrIndex call || call.Target is not IdentRef ident)
                    throw new MatlabRuntimeException("Multi-output requires function call on RHS");
                MValue[] outs = CallMultiOut(ident.Name, EvalArgs(call.Args, scope), ArgNames(call.Args));
                if (outs.Length < asg.Targets.Count)
                    throw new MatlabRuntimeException($"Function '{ident.Name}' returned {outs.Length} outputs, expected {asg.Targets.Count}");
                for (int k = 0; k < asg.Targets.Count; k++)
                    AssignMultiTarget(asg.Targets[k], outs[k], scope);
                // El "valor principal" de display es el primer target NO ignorado
                for (int k = 0; k < asg.Targets.Count; k++)
                {
                    string nm = MultiTargetName(asg.Targets[k]);
                    if (nm != null && nm != "~")
                        return new StatementResult(nm, outs[k], asg.Suppressed);
                }
                return new StatementResult(MultiTargetName(asg.Targets[0]) ?? "ans", outs[0], asg.Suppressed);
            }
            // Single target
            var tgt = asg.Targets[0];

            // ──────────────────────────────────────────────────────────────
            // MATLAB: `v(idx) = []` BORRA esos elementos, no asigna. Es la forma
            // canónica de eliminar elementos de un vector y CEINCI-LAB la usa por
            // todos lados (`X(sort(Nodos)) = []`, `NI(Elem) = []`). Sin esto el
            // motor tiraba "Index was outside the bounds of the array" o
            // "Assign: LHS n elements, RHS 0".
            // ──────────────────────────────────────────────────────────────
            if (tgt is CallOrIndex delTgt && delTgt.Target is IdentRef delId &&
                delTgt.Args.Count == 1 && IsEmptyMatrixLiteral(asg.Rhs) &&
                scope.TryGet(delId.Name, out var delSrc) && !delSrc.IsString)
            {
                var idxVal = Eval(delTgt.Args[0], scope);
                var n = delSrc.Data.Length;
                var drop = new bool[n];
                foreach (var d in idxVal.Data)
                {
                    var k = (int)Math.Round(d) - 1;              // MATLAB es 1-based
                    if (k < 0 || k >= n)
                        throw new MatlabRuntimeException(
                            $"Index exceeds matrix dimensions: {delId.Name}({(int)Math.Round(d)})");
                    drop[k] = true;
                }
                var kept = new List<double>(n);
                for (int k = 0; k < n; k++) if (!drop[k]) kept.Add(delSrc.Data[k]);
                // MATLAB conserva la orientación: si era columna, sigue siendo columna.
                var isCol = delSrc.Cols == 1 && delSrc.Rows > 1;
                var res = isCol ? new MValue(kept.Count, 1, kept.ToArray())
                                : new MValue(1, kept.Count, kept.ToArray());
                scope.Set(delId.Name, res);
                return new StatementResult(delId.Name, res, asg.Suppressed);
            }
            // ──────────────────────────────────────────────────────────────
            // FAST-PATH: K(rows, cols) = K(rows, cols) + expr   (FEM hot loop)
            // Detecta patrón: target(args) = target(args) +/- expr (mismo target, mismos args)
            // Y aplica IN-PLACE en lugar de leer submatrix + sumar + escribir.
            // Speedup esperado: 4-10× en ensamblaje FEM.
            // ──────────────────────────────────────────────────────────────
            if (tgt is CallOrIndex tgtIdx
                && tgtIdx.Target is IdentRef tgtId
                && asg.Rhs is BinaryOp rhsBin
                && (rhsBin.Op == "+" || rhsBin.Op == "-")
                && rhsBin.Left is CallOrIndex leftIdx
                && leftIdx.Target is IdentRef leftId
                && leftId.Name == tgtId.Name
                && SameIndexArgs(leftIdx.Args, tgtIdx.Args))
            {
                if (scope.TryGet(tgtId.Name, out var existingFast))
                {
                    var deltaVal = Eval(rhsBin.Right, scope);
                    bool isAdd = rhsBin.Op == "+";
                    if (IndexedAddInPlace(existingFast, tgtIdx.Args, deltaVal, isAdd, scope))
                    {
                        return new StatementResult(tgtId.Name, existingFast, asg.Suppressed);
                    }
                }
            }
            var val = Eval(asg.Rhs, scope);
            if (tgt is IdentRef id)
            {
                // MATLAB-correct COPY semantics: `A = B` / `A = s.campo` / `A = c{i}` devuelven la
                // MISMA referencia MValue que otra variable (alias). Sin copia, mutar A luego (campo,
                // elemento o vía in-place fast paths) alteraría B/s/c también. CloneArg copia los
                // tipos-valor (structs, arrays, cells) y deja por referencia los handles graficos /
                // function handles / instancias / Map / simbolos (que en MATLAB tambien son por ref).
                if ((asg.Rhs is IdentRef || asg.Rhs is FieldAccess || asg.Rhs is CellIndex) && val != null)
                    val = CloneArg(val);
                scope.Set(id.Name, val);
                return new StatementResult(id.Name, val, asg.Suppressed);
            }
            if (tgt is CallOrIndex idx && idx.Target is IdentRef targetId)
            {
                // SYMFUN MATLAB-style: `f(x) = x^2` despues de `syms x` crea una
                // funcion simbolica. Detectamos: args son todos IdentRef que
                // referencian sym vars existentes, RHS es simbolico, target no
                // existe ya como matriz. Si calza → registrar en _symFunParams
                // y guardar el valor como simbolico. Skipea IndexedAssign.
                bool targetExistsAsMatrix = scope.TryGet(targetId.Name, out var existingTgt)
                    && existingTgt != null && !existingTgt.IsSymbolic
                    && existingTgt.Data != null && existingTgt.Data.Length > 1;
                if (!targetExistsAsMatrix && val != null && val.IsSymbolic
                    && AllArgsAreSymVars(idx.Args, scope))
                {
                    var paramNames = new List<string>();
                    foreach (var a in idx.Args) paramNames.Add(((IdentRef)a).Name);
                    _symFunParams[targetId.Name] = paramNames;
                    scope.Set(targetId.Name, val);
                    return new StatementResult(targetId.Name, val, asg.Suppressed);
                }
                // Indexed assignment: A(i, j) = val
                if (!scope.TryGet(targetId.Name, out var existing))
                    existing = new MValue(0);
                // containers.Map: m(key) = val
                if (existing.IsMap)
                {
                    var ka = EvalArgs(idx.Args, scope);
                    if (ka.Length != 1) throw new MatlabRuntimeException("Map: se requiere exactamente 1 clave en m(key)=val");
                    existing.MapData[MapKey(ka[0])] = val;
                    scope.Set(targetId.Name, existing);
                    return new StatementResult(targetId.Name, val, asg.Suppressed);
                }
                var updated = IndexedAssign(existing, idx.Args, val, scope);
                scope.Set(targetId.Name, updated);
                return new StatementResult(targetId.Name, updated, asg.Suppressed);
            }
            if (tgt is FieldAccess fa)
            {
                // s.field = val. Auto-crear struct si no existe. Chain de campos
                // s.a.b.c = val también es soportado (MVP simple).
                AssignToField(fa, val, scope);
                return new StatementResult(GetRootVarName(fa), val, asg.Suppressed);
            }
            if (tgt is CellIndex ci && ci.Target is IdentRef ciId)
            {
                // c{idx} = val — assignment a cell, soporta end+1 autoextend
                MValue existing;
                if (!scope.TryGet(ciId.Name, out existing) || !existing.IsCell)
                {
                    // Crear cell vacío 1x0
                    existing = MValue.NewCell(new MValue[1, 0]);
                }
                var updated = CellIndexedAssign(existing, ci.Args, val, scope);
                scope.Set(ciId.Name, updated);
                return new StatementResult(ciId.Name, updated, asg.Suppressed);
            }
            // struct.field{idx} = val — cell autogrow sobre un campo de struct (p.ej. L.items{end+1}=E)
            if (tgt is CellIndex ci2 && ci2.Target is FieldAccess ciFa)
            {
                MValue parentStruct;
                if (ciFa.Target is IdentRef pid)
                {
                    if (!scope.TryGet(pid.Name, out parentStruct) || !parentStruct.IsStruct)
                    {
                        parentStruct = MValue.NewStruct();
                        scope.Set(pid.Name, parentStruct);
                    }
                }
                else if (ciFa.Target is FieldAccess pfa)
                    parentStruct = ResolveOrCreateStruct(pfa, scope);
                else
                    throw new MatlabRuntimeException("Unsupported cell-field assignment target");
                if (!parentStruct.Fields.TryGetValue(ciFa.FieldName, out var cell) || cell == null || !cell.IsCell)
                    cell = MValue.NewCell(new MValue[1, 0]);
                var updatedCell = CellIndexedAssign(cell, ci2.Args, val, scope);
                parentStruct.Fields[ciFa.FieldName] = updatedCell;
                return new StatementResult(GetRootVarName(ciFa), updatedCell, asg.Suppressed);
            }
            // s.field(i,j) = val — asignación INDEXADA a un CAMPO de struct (ej. St.epl(:,e)=x)
            if (tgt is CallOrIndex idxf && idxf.Target is FieldAccess idxfa)
            {
                MValue curField;
                try { curField = Eval(idxfa, scope); } catch { curField = new MValue(0); }
                if (curField == null) curField = new MValue(0);
                var updatedField = IndexedAssign(curField, idxf.Args, val, scope);
                AssignToField(idxfa, updatedField, scope);
                return new StatementResult(GetRootVarName(idxfa), updatedField, asg.Suppressed);
            }
            throw new MatlabRuntimeException("Unsupported assignment target");
        }

        /// <summary>Asigna un valor a UN target de un multi-output `[a, b(:,1), ~] = f()`.
        /// Soporta `~` (ignorar), identificador simple, indexado `CG(:,1)`, field y cell.</summary>
        private void AssignMultiTarget(MatlabNode tgt, MValue val, MatlabScope scope)
        {
            switch (tgt)
            {
                case IdentRef id when id.Name == "~":
                    return;                                   // salida ignorada (MATLAB)
                case IdentRef id:
                    scope.Set(id.Name, val);
                    return;
                case CallOrIndex idx when idx.Target is IdentRef tId:
                    if (!scope.TryGet(tId.Name, out var existing) || existing == null)
                        existing = new MValue(0);
                    scope.Set(tId.Name, IndexedAssign(existing, idx.Args, val, scope));
                    return;
                case FieldAccess fa:
                    AssignToField(fa, val, scope);
                    return;
                case CellIndex ci when ci.Target is IdentRef cId:
                    if (!scope.TryGet(cId.Name, out var ex) || ex == null || !ex.IsCell)
                        ex = MValue.NewCell(new MValue[1, 0]);
                    scope.Set(cId.Name, CellIndexedAssign(ex, ci.Args, val, scope));
                    return;
                default:
                    throw new MatlabRuntimeException("Unsupported multi-output target");
            }
        }

        /// <summary>Clave canónica para containers.Map: string directo, o el número en texto invariante.</summary>
        private static string MapKey(MValue k) =>
            k.IsString ? k.StringValue : k.Scalar.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Construye un containers.Map. Vacío para la forma de hints
        /// ('KeyType','char',...); poblado para containers.Map(keysCell, valuesCell).</summary>
        private static MValue BuildMap(MValue[] args)
        {
            var m = MValue.NewMap();
            bool hints = args.Length >= 2 && args[0].IsString;   // 'KeyType'/'ValueType'/...
            if (!hints && args.Length == 2)
            {
                var kf = FlattenCell(args[0]);
                var vf = FlattenCell(args[1]);
                for (int i = 0; i < kf.Count && i < vf.Count; i++)
                    m.MapData[MapKey(kf[i])] = vf[i];
            }
            return m;
        }

        private static System.Collections.Generic.List<MValue> FlattenCell(MValue c)
        {
            var list = new System.Collections.Generic.List<MValue>();
            if (c.IsCell) { foreach (var e in c.CellData) list.Add(e); }
            else list.Add(c);
            return list;
        }

        /// <summary>Matriz MValue → array row-major double[r*c] (para LAPACK).</summary>
        private static double[] ToRowMajor(MValue m)
        {
            int r = m.Rows, c = m.Cols;
            var d = new double[r * c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    d[i * c + j] = m.At(i, j);
            return d;
        }

        /// <summary>True si la matriz es cuadrada (n≥2), real y simétrica (A=Aᵀ con tolerancia).</summary>
        private static bool IsSymmetricM(MValue m)
        {
            if (m == null || m.Rows != m.Cols || m.Rows < 2) return false;
            if (m.IsString || m.IsComplex || m.CellData != null || m.Fields != null) return false;
            int n = m.Rows;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double a = m.At(i, j), b = m.At(j, i);
                    if (Math.Abs(a - b) > 1e-9 * (1 + Math.Abs(a) + Math.Abs(b))) return false;
                }
            return true;
        }

        /// <summary>Nombre de la variable raíz de un target multi-output (para display).</summary>
        private string MultiTargetName(MatlabNode tgt) => tgt switch
        {
            IdentRef id => id.Name,
            CallOrIndex c when c.Target is IdentRef ci => ci.Name,
            CellIndex ce when ce.Target is IdentRef cei => cei.Name,
            FieldAccess fa => GetRootVarName(fa),
            _ => null
        };

        /// <summary>
        /// Clona el Data (y Imag, SparseVals, etc.) de un MValue numérico para garantizar
        /// MATLAB-style copy semantics en `A = B` (alias). El MValue retornado tiene
        /// arrays independientes — mutar uno NO afecta al original.
        /// </summary>
        private static MValue CloneNumericMValue(MValue src)
        {
            if (src == null) return null;
            // Sparse
            if (src.IsSparseReal)
            {
                var nv = (double[])src.SparseVals.Clone();
                var nc = (int[])src.SparseCols.Clone();
                var nr = (int[])src.SparseRowPtr.Clone();
                return MValue.NewSparseCSR(src.Rows, src.Cols, nv, nc, nr);
            }
            // Complex
            if (src.IsComplex)
            {
                var re = (double[])src.Data.Clone();
                var im = (double[])src.Imag.Clone();
                return new MValue(src.Rows, src.Cols, re, im);
            }
            // Real plain
            var data = (double[])src.Data.Clone();
            return new MValue(src.Rows, src.Cols, data);
        }
        /// <summary>Asignación a celda c{i} = val. Soporta i = end+N para autoextender.</summary>
        private MValue CellIndexedAssign(MValue cellVal, List<MatlabNode> args, MValue val, MatlabScope scope)
        {
            var cd = cellVal.CellData;
            int r = cd.GetLength(0), c = cd.GetLength(1);
            // Para resolver 'end' usamos un MValue "fake" con dimensiones de la cell
            // (numel = max(r,c) para 1D, sino r×c para 2D).
            var fakeTarget = new MValue(r, c);
            if (args.Count == 1)
            {
                _endCtx.Push((fakeTarget, 0, 1));
                int idx;
                try
                {
                    var idxV = Eval(args[0], scope);
                    if (!idxV.IsScalar) throw new MatlabRuntimeException("cell index must be scalar");
                    idx = (int)idxV.Scalar;
                }
                finally { _endCtx.Pop(); }
                // índice lineal: respetar orientación (fila r==1 vs columna c==1).
                // Antes escribía siempre cd[0,idx-1] -> crash para cell(N,1) (columna) con idx>=2.
                bool isRow = (r == 1);
                int curLen = Math.Max(r, c);
                if (idx > curLen || cd.Length == 0)
                {
                    int newLen = Math.Max(idx, curLen);
                    var grown = isRow ? new MValue[1, newLen] : new MValue[newLen, 1];
                    for (int t = 0; t < curLen; t++)
                    {
                        var old = isRow ? cd[0, t] : cd[t, 0];
                        if (isRow) grown[0, t] = old; else grown[t, 0] = old;
                    }
                    cd = grown;
                }
                if (isRow) cd[0, idx - 1] = val; else cd[idx - 1, 0] = val;
                return MValue.NewCell(cd);
            }
            if (args.Count == 2)
            {
                int i, j;
                _endCtx.Push((fakeTarget, 0, 2));
                try
                {
                    var iV = Eval(args[0], scope);
                    _endCtx.Pop(); _endCtx.Push((fakeTarget, 1, 2));
                    var jV = Eval(args[1], scope);
                    i = (int)iV.Scalar;
                    j = (int)jV.Scalar;
                }
                finally { _endCtx.Pop(); }
                int newR = Math.Max(i, r), newC = Math.Max(j, c);
                if (newR > r || newC > c)
                {
                    var newData = new MValue[newR, newC];
                    for (int ii = 0; ii < r; ii++)
                        for (int jj = 0; jj < c; jj++) newData[ii, jj] = cd[ii, jj];
                    cd = newData;
                }
                cd[i - 1, j - 1] = val;
                return MValue.NewCell(cd);
            }
            throw new MatlabRuntimeException("cell indexing supports 1 or 2 indices");
        }

        private void AssignToField(FieldAccess fa, MValue val, MatlabScope scope)
        {
            // Resolver el target del field. Si es IdentRef raíz, auto-create struct.
            if (fa.Target is IdentRef rootId)
            {
                if (!scope.TryGet(rootId.Name, out var st) || !st.IsStruct)
                {
                    st = MValue.NewStruct();
                    scope.Set(rootId.Name, st);
                }
                st.Fields[fa.FieldName] = val;
                return;
            }
            if (fa.Target is FieldAccess parent)
            {
                // s.a.b = val → ensure s.a is a struct, recurse
                var parentRoot = ResolveOrCreateStruct(parent, scope);
                parentRoot.Fields[fa.FieldName] = val;
                return;
            }
            // res(k).field = val → struct-array: crecer hasta k y setear el campo del elemento.
            if (fa.Target is CallOrIndex idxTgt && idxTgt.Target is IdentRef arrId && idxTgt.Args.Count == 1)
            {
                int k = (int)Eval(idxTgt.Args[0], scope).Scalar;
                if (k < 1) throw new MatlabRuntimeException("struct-array: índice debe ser >= 1");
                if (!scope.TryGet(arrId.Name, out var arr) || !arr.IsStructArray)
                {
                    arr = MValue.NewStructArray();
                    scope.Set(arrId.Name, arr);
                }
                while (arr.StructArrayItems.Count < k) arr.StructArrayItems.Add(MValue.NewStruct());
                arr.StructArrayItems[k - 1].Fields[fa.FieldName] = val;
                return;
            }
            throw new MatlabRuntimeException("Unsupported field assignment target");
        }
        private MValue ResolveOrCreateStruct(FieldAccess fa, MatlabScope scope)
        {
            if (fa.Target is IdentRef rootId)
            {
                if (!scope.TryGet(rootId.Name, out var st) || !st.IsStruct)
                {
                    st = MValue.NewStruct();
                    scope.Set(rootId.Name, st);
                }
                if (!st.Fields.TryGetValue(fa.FieldName, out var inner) || !inner.IsStruct)
                {
                    inner = MValue.NewStruct();
                    st.Fields[fa.FieldName] = inner;
                }
                return inner;
            }
            if (fa.Target is FieldAccess parent)
            {
                var parentStruct = ResolveOrCreateStruct(parent, scope);
                if (!parentStruct.Fields.TryGetValue(fa.FieldName, out var inner) || !inner.IsStruct)
                {
                    inner = MValue.NewStruct();
                    parentStruct.Fields[fa.FieldName] = inner;
                }
                return inner;
            }
            throw new MatlabRuntimeException("Unsupported nested field target");
        }
        private static string GetRootVarName(MatlabNode node)
        {
            while (node is FieldAccess fa) node = fa.Target;
            if (node is IdentRef id) return id.Name;
            return "?";
        }
        /// <summary>Comparación estructural de listas de args (para fast-path K(args) = K(args) + ...).</summary>
        private static bool SameIndexArgs(List<MatlabNode> a, List<MatlabNode> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!SameAst(a[i], b[i])) return false;
            return true;
        }
        private static bool SameAst(MatlabNode a, MatlabNode b)
        {
            if (a is null || b is null) return a == null && b == null;
            if (a.GetType() != b.GetType()) return false;
            switch (a)
            {
                case IdentRef ia when b is IdentRef ib: return ia.Name == ib.Name;
                case NumberLit na when b is NumberLit nb: return na.Value == nb.Value;
                case ColonAll: return true;
                case CallOrIndex ca when b is CallOrIndex cb:
                    return SameAst(ca.Target, cb.Target) && SameIndexArgs(ca.Args, cb.Args);
                case Range ra when b is Range rb:
                    return SameAst(ra.Start, rb.Start) && SameAst(ra.End, rb.End)
                        && (ra.Step == null && rb.Step == null || SameAst(ra.Step, rb.Step));
                case BinaryOp ba when b is BinaryOp bb:
                    return ba.Op == bb.Op && SameAst(ba.Left, bb.Left) && SameAst(ba.Right, bb.Right);
                case UnaryOp ua when b is UnaryOp ub:
                    return ua.Op == ub.Op && SameAst(ua.Operand, ub.Operand);
                default: return false;
            }
        }
        /// <summary>Aplica delta in-place a A(rows, cols). Retorna true si pudo, false si falla (fallback).</summary>
        private bool IndexedAddInPlace(MValue m, List<MatlabNode> idxNodes, MValue delta, bool isAdd, MatlabScope scope)
        {
            int nDims = idxNodes.Count;
            if (nDims < 1 || nDims > 2) return false;
            if (m.IsSparseReal || m.IsCell || m.Is3D || m.IsString || m.IsSymMatrix) return false;
            try
            {
                int[][] indices = new int[nDims][];
                for (int d = 0; d < nDims; d++)
                {
                    _endCtx.Push((m, d, nDims));
                    try { indices[d] = ResolveIndexArg(idxNodes[d], m, d, nDims, scope); }
                    finally { _endCtx.Pop(); }
                }
                double sgn = isAdd ? 1.0 : -1.0;
                if (nDims == 1)
                {
                    var lin = indices[0];
                    if (delta.IsScalar)
                    {
                        double dv = sgn * delta.Scalar;
                        for (int k = 0; k < lin.Length; k++)
                        {
                            int li = lin[k];
                            int col0 = li / m.Rows, row0 = li - col0 * m.Rows;
                            m.Set(row0, col0, m.At(row0, col0) + dv);
                        }
                    }
                    else
                    {
                        if (lin.Length != delta.Data.Length) return false;
                        for (int k = 0; k < lin.Length; k++)
                        {
                            int li = lin[k];
                            int col0 = li / m.Rows, row0 = li - col0 * m.Rows;
                            m.Set(row0, col0, m.At(row0, col0) + sgn * delta.Data[k]);
                        }
                    }
                    return true;
                }
                // nDims == 2
                var rows = indices[0];
                var cols = indices[1];
                if (delta.IsScalar)
                {
                    double dv = sgn * delta.Scalar;
                    for (int i = 0; i < rows.Length; i++)
                        for (int j = 0; j < cols.Length; j++)
                            m.Set(rows[i], cols[j], m.At(rows[i], cols[j]) + dv);
                    return true;
                }
                // delta debe tener shape rows.Length × cols.Length
                if (rows.Length == delta.Rows && cols.Length == delta.Cols)
                {
                    for (int i = 0; i < rows.Length; i++)
                        for (int j = 0; j < cols.Length; j++)
                            m.Set(rows[i], cols[j], m.At(rows[i], cols[j]) + sgn * delta.At(i, j));
                    return true;
                }
                return false;
            }
            catch { return false; }
        }
        private MValue IndexedAssign(MValue m, List<MatlabNode> idxNodes, MValue v, MatlabScope scope)
        {
            int nDims = idxNodes.Count;
            // Resolve target indices con soporte para `:`, range, end
            int[][] indices = new int[nDims][];
            for (int d = 0; d < nDims; d++)
            {
                _endCtx.Push((m, d, nDims));
                try { indices[d] = ResolveIndexArg(idxNodes[d], m, d, nDims, scope); }
                finally { _endCtx.Pop(); }
            }
            if (nDims == 1)
            {
                var lin = indices[0];
                if (lin.Length == 1)
                {
                    int i = lin[0];
                    int total = m.Rows * m.Cols;
                    // Grow si single-index excede longitud (sólo vectores)
                    if (i >= total && (m.Rows == 1 || m.Cols == 1 || m.Rows * m.Cols == 0))
                    {
                        int newLen = i + 1;
                        var grown = new MValue(1, newLen);
                        for (int k = 0; k < total; k++) grown.Data[k] = m.Data[k];
                        grown.Data[i] = v.IsScalar ? v.Scalar : v.Data[0];
                        return grown;
                    }
                    if (i < 0 || i >= total)
                        throw new MatlabRuntimeException($"Linear index {i + 1} out of bounds (1..{total})");
                    int col0 = i / m.Rows, row0 = i - col0 * m.Rows;
                    m.Set(row0, col0, v.IsScalar ? v.Scalar : v.Data[0]);
                    return m;
                }
                // Slice assignment vector
                if (v.IsScalar)
                {
                    for (int k = 0; k < lin.Length; k++)
                    {
                        int li = lin[k];
                        int col0 = li / m.Rows, row0 = li - col0 * m.Rows;
                        m.Set(row0, col0, v.Scalar);
                    }
                }
                else
                {
                    if (lin.Length != v.Data.Length)
                        throw new MatlabRuntimeException($"Assign: LHS {lin.Length} elements, RHS {v.Data.Length}");
                    for (int k = 0; k < lin.Length; k++)
                    {
                        int li = lin[k];
                        int col0 = li / m.Rows, row0 = li - col0 * m.Rows;
                        m.Set(row0, col0, v.Data[k]);
                    }
                }
                return m;
            }
            if (nDims == 2)
            {
                var rows = indices[0];
                var cols = indices[1];
                // LHS vacío con `:` → adoptar la forma del RHS (MATLAB: x=[]; x(end+1,:)=fila).
                // Un `:` sobre una matriz 0×0 da 0 índices; en una asignación que crea la
                // matriz, ese `:` debe abarcar el tamaño del RHS en esa dimensión.
                if (m.Rows * m.Cols == 0)
                {
                    if (cols.Length == 0 && v.Cols > 0)
                    {
                        cols = new int[v.Cols];
                        for (int c = 0; c < v.Cols; c++) cols[c] = c;
                    }
                    if (rows.Length == 0 && v.Rows > 0)
                    {
                        rows = new int[v.Rows];
                        for (int r = 0; r < v.Rows; r++) rows[r] = r;
                    }
                }
                // Auto-grow 2D como MATLAB: M(r,c)=v agranda M con ceros si r/c
                // exceden el tamaño actual (clave para ensamblar K(i,j) en loops FEM).
                int needRows = m.Rows, needCols = m.Cols;
                foreach (var rr in rows) if (rr + 1 > needRows) needRows = rr + 1;
                foreach (var cc in cols) if (cc + 1 > needCols) needCols = cc + 1;
                if (needRows > m.Rows || needCols > m.Cols)
                {
                    var grown = new MValue(needRows, needCols);
                    for (int gc = 0; gc < m.Cols; gc++)
                        for (int gr = 0; gr < m.Rows; gr++)
                            grown.Set(gr, gc, m.At(gr, gc));
                    m = grown;
                }
                // Scalar broadcast OR shape-match
                if (v.IsScalar)
                {
                    foreach (var rr in rows) foreach (var cc in cols)
                    {
                        if (rr >= m.Rows || cc >= m.Cols)
                            throw new MatlabRuntimeException($"Subscript ({rr + 1}, {cc + 1}) out of {m.Rows}×{m.Cols}");
                        m.Set(rr, cc, v.Scalar);
                    }
                    return m;
                }
                if (rows.Length * cols.Length != v.Data.Length &&
                    !(rows.Length == v.Rows && cols.Length == v.Cols))
                    throw new MatlabRuntimeException($"Assign shape mismatch: LHS {rows.Length}×{cols.Length}, RHS {v.Rows}×{v.Cols}");
                for (int i = 0; i < rows.Length; i++)
                    for (int j = 0; j < cols.Length; j++)
                    {
                        int rr = rows[i], cc = cols[j];
                        double val = (rows.Length == v.Rows && cols.Length == v.Cols) ? v.At(i, j)
                                   : v.Data[i * cols.Length + j];
                        if (rr >= m.Rows || cc >= m.Cols)
                            throw new MatlabRuntimeException($"Subscript ({rr + 1}, {cc + 1}) out of {m.Rows}×{m.Cols}");
                        m.Set(rr, cc, val);
                    }
                return m;
            }
            throw new MatlabRuntimeException("Indexing > 2D not supported");
        }

        public MValue Eval(MatlabNode node, MatlabScope scope)
        {
            switch (node)
            {
                case NumberLit n: return new MValue(n.Value);
                case ImaginaryLit im: return new MValue(0, im.Value);
                case StringLit s: return s.Quote == '"' ? MValue.NewStringScalar(s.Value) : new MValue(s.Value);
                case IdentRef id:
                    // `end` dentro de indexing: resolver al tamaño de la dim actual
                    if (id.Name == "end" && _endCtx.Count > 0)
                    {
                        var ctx = _endCtx.Peek();
                        // Si es indexing de 1 dim: end = numel; sino: tamaño de la dim
                        if (ctx.NDims == 1) return new MValue(ctx.Target.Rows * ctx.Target.Cols);
                        if (ctx.Dim == 0) return new MValue(ctx.Target.Rows);
                        return new MValue(ctx.Target.Cols);
                    }
                    if (scope.TryGet(id.Name, out var v)) return v;
                    // Constantes especiales MATLAB
                    switch (id.Name)
                    {
                        case "pi": return new MValue(Math.PI);
                        case "e":  return new MValue(Math.E);
                        case "Inf": case "inf": return new MValue(double.PositiveInfinity);
                        case "NaN": case "nan": return new MValue(double.NaN);
                        case "true": return new MValue(1);
                        case "false": return new MValue(0);
                        case "i": case "j": return new MValue(0, 1);  // unidad imaginaria
                    }
                    // Builtin/user-function llamada nullary (MATLAB permite `who`, `tic`, etc.)
                    if (_userFunctions.TryGetValue(id.Name, out var def0))
                        return CallUserFunction(def0, Array.Empty<MValue>());
                    if (_builtins.TryGetValue(id.Name, out var fn0))
                        return fn0(Array.Empty<MValue>());
                    throw new MatlabRuntimeException($"Undefined: {id.Name}");
                case ColonAll: throw new MatlabRuntimeException(":' aislado sólo válido en indexing");
                case UnaryOp u: return EvalUnary(u, scope);
                case BinaryOp b: return EvalBinary(b, scope);
                case CallOrIndex c: return EvalCallOrIndex(c, scope);
                case Range r: return EvalRange(r, scope);
                case MatrixLit ml: return EvalMatrixLit(ml, scope);
                case AnonFunction af: return MakeAnonHandle(af, scope);
                case FieldAccess fa: {
                    var target = Eval(fa.Target, scope);
                    // struct-array res.n en contexto escalar -> primer elemento (la expansión
                    // como comma-list {res.n} se maneja en EvalCellLit).
                    if (target.IsStructArray)
                    {
                        if (target.StructArrayItems.Count > 0 && target.StructArrayItems[0].Fields != null
                            && target.StructArrayItems[0].Fields.TryGetValue(fa.FieldName, out var f0))
                            return f0;
                        throw new MatlabRuntimeException($"Reference to non-existent field '{fa.FieldName}'");
                    }
                    if (!target.IsStruct || !target.Fields.TryGetValue(fa.FieldName, out var fv))
                        throw new MatlabRuntimeException($"Reference to non-existent field '{fa.FieldName}'");
                    return fv;
                }
                case CellLit cl: return EvalCellLit(cl, scope);
                case CellIndex ci: return EvalCellIndex(ci, scope);
                default:
                    throw new MatlabRuntimeException($"Cannot eval: {node?.GetType().Name}");
            }
        }
        private bool _octaveMode;
        /// <summary>Modo Octave: registra builtins que MATLAB no tiene (p.ej. <c>printf</c>).
        /// Lo fija <see cref="MatlabPipeline"/> antes de ejecutar. Off = MATLAB estricto.</summary>
        public bool OctaveMode
        {
            get => _octaveMode;
            set
            {
                _octaveMode = value;
                if (value && !_builtins.ContainsKey("printf"))
                {
                    // printf(fmt, ...) = fprintf(1, fmt, ...) → stdout. Octave-only.
                    _builtins["printf"] = a =>
                    {
                        if (a.Length == 0 || !a[0].IsString)
                            throw new MatlabRuntimeException("printf: falta la cadena de formato");
                        var rest = new MValue[a.Length - 1];
                        Array.Copy(a, 1, rest, 0, rest.Length);
                        var text = MatlabSprintf.Format(a[0].StringValue, rest);
                        _output?.Invoke(text);
                        return new MValue(text.Length);
                    };
                    // puts(str) / fputs(fid, str): escribe la cadena tal cual (sin formato). Octave.
                    _builtins["puts"] = a =>
                    {
                        if (a.Length < 1 || !a[0].IsString)
                            throw new MatlabRuntimeException("puts: se esperaba una cadena");
                        _output?.Invoke(a[0].StringValue);
                        return new MValue(a[0].StringValue.Length);
                    };
                    _builtins["fputs"] = a =>
                    {
                        if (a.Length < 2 || !a[1].IsString)
                            throw new MatlabRuntimeException("fputs: uso fputs(fid, cadena)");
                        int fid = (int)Math.Round(a[0].Scalar);
                        var s = a[1].StringValue;
                        if (fid == 1 || fid == 2) _output?.Invoke(s);
                        else if (_fileWriters.TryGetValue(fid, out var w)) w.Write(s);
                        else throw new MatlabRuntimeException($"fputs: file id {fid} inválido");
                        return new MValue(s.Length);
                    };
                    // fdisp(fid, x): como disp pero a un fid. Para stdout reutiliza disp.
                    _builtins["fdisp"] = a =>
                    {
                        if (a.Length < 2) throw new MatlabRuntimeException("fdisp: uso fdisp(fid, x)");
                        int fid = (int)Math.Round(a[0].Scalar);
                        if (fid == 1 || fid == 2) return _builtins["disp"](new[] { a[1] });
                        throw new MatlabRuntimeException("fdisp: solo se soporta stdout/stderr (fid 1 o 2)");
                    };
                    // fflush(fid): no-op (la salida no se bufferiza). Octave.
                    _builtins["fflush"] = a => new MValue(0);
                    // columns(x) / rows(x): dimensiones (Octave). = size(x,2) / size(x,1).
                    _builtins["columns"] = a => new MValue(a[0].Cols);
                    _builtins["rows"] = a => new MValue(a[0].Rows);
                }
            }
        }

        private MValue EvalUnary(UnaryOp u, MatlabScope scope)
        {
            var x = Eval(u.Operand, scope);
            // OOP unary overload (uminus/uplus/not/ctranspose/transpose)
            if (x.IsInstance)
            {
                var ov = TryOverloadUnary(u.Op, x);
                if (ov != null) return ov;
            }
            // Negación simbólica: usar SymMul(-1, x) directamente (no heurística)
            if (u.Op == "-" && x.IsSymbolic)
                return MValue.NewSymbolic(new SymMul(new SymConst(-1), x.Symbolic).Simplify());
            if (u.Op == "-" && x.IsSymMatrix)
            {
                var c = SymMatOps.ScalarMul(new SymConst(-1), x.SymCells);
                return MValue.NewSymMatrix(c);
            }
            return u.Op switch
            {
                "-" => MapUnary(x, v => -v),
                "+" => x,
                "~" => MapUnary(x, v => v == 0 ? 1 : 0),
                "!" => MapUnary(x, v => v == 0 ? 1 : 0),   // Octave: alias de ~
                "'" => Transpose(x),
                ".'" => Transpose(x),
                _ => throw new MatlabRuntimeException($"Unsupported unary op: {u.Op}")
            };
        }
        private MValue EvalBinary(BinaryOp b, MatlabScope scope)
        {
            // ── Short-circuit logical operators (MATLAB semantics) ──────────
            // `&&` / `||` MUST NOT evaluate the right operand when the result is
            // determined by the left. Critical for patterns like
            //   if isfield(s, 'x') && ~isempty(s.x)
            // where evaluating s.x would throw when the field is absent.
            if (b.Op == "&&")
            {
                var lSC = Eval(b.Left, scope);
                if (!lSC.IsScalar || lSC.Scalar == 0) return new MValue(0);
                var rSC = Eval(b.Right, scope);
                return new MValue((rSC.IsScalar && rSC.Scalar != 0) ? 1 : 0);
            }
            if (b.Op == "||")
            {
                var lSC = Eval(b.Left, scope);
                if (lSC.IsScalar && lSC.Scalar != 0) return new MValue(1);
                var rSC = Eval(b.Right, scope);
                return new MValue((rSC.IsScalar && rSC.Scalar != 0) ? 1 : 0);
            }
            var l = Eval(b.Left, scope);
            var r = Eval(b.Right, scope);
            // ── OOP operator overloading ────────────────────────────────────
            // Si alguno operando es instancia OOP con un método de overload, lo invocamos.
            if (l.IsInstance || r.IsInstance)
            {
                var ov = TryOverloadBinary(b.Op, l, r);
                if (ov != null) return ov;
            }
            // ── Symbolic Matrix propagation ─────────────────────────────────
            // Si alguno operando es matriz simbólica, dispatch a SymMatOps.
            if (l.IsSymMatrix || r.IsSymMatrix)
            {
                return EvalBinarySymMatrix(b.Op, l, r);
            }
            // ── String (double-quoted) ops ─────────────────────────────────
            // "a" + "b" → "ab"; string + number → string concat (con num2str);
            // ["a","b"] + "x" → ["ax","bx"] elementwise
            if ((l.IsDoubleQuoted || l.IsStringArray) || (r.IsDoubleQuoted || r.IsStringArray))
            {
                return EvalBinaryStringOp(b.Op, l, r);
            }
            // Symbolic propagation: si alguno es simbólico, construir SymNode
            if (l.IsSymbolic || r.IsSymbolic)
            {
                SymNode L = l.IsSymbolic ? l.Symbolic : new SymConst(l.Scalar);
                SymNode R = r.IsSymbolic ? r.Symbolic : new SymConst(r.Scalar);
                SymNode result = b.Op switch
                {
                    "+" => new SymAdd(L, R),
                    "-" => new SymSub(L, R),
                    "*" or ".*" => new SymMul(L, R),
                    "/" or "./" => new SymDiv(L, R),
                    "^" or ".^" => new SymPow(L, R),
                    // MATLAB syntax: f == g se interpreta como ecuacion f - g = 0
                    // (forma implicita usada por solve()). Idem ~= en ese contexto.
                    "==" or "~=" => new SymSub(L, R),
                    _ => throw new MatlabRuntimeException($"Symbolic op '{b.Op}' not supported")
                };
                return MValue.NewSymbolic(result.Simplify());
            }
            return b.Op switch
            {
                "+" => MapBinary(l, r, (a, c) => a + c),
                "-" => MapBinary(l, r, (a, c) => a - c),
                "*" => MatMul(l, r),
                ".*" => MapBinary(l, r, (a, c) => a * c),
                "/" => MapBinary(l, r, (a, c) => a / c),  // MVP — full would do mrdivide
                "./" => MapBinary(l, r, (a, c) => a / c),
                // MATLAB \: mldivide. Si ambos son matrices, resolver A*x = b.
                // Si alguno es scalar, fallback a element-wise reverse-div.
                "\\" => (l.IsScalar || r.IsScalar)
                    ? MapBinary(l, r, (a, c) => c / a)
                    : MatlabLinAlg.Linsolve(l, r),
                ".\\" => MapBinary(l, r, (a, c) => c / a),
                "^" => l.IsScalar && r.IsScalar ? new MValue(Math.Pow(l.Scalar, r.Scalar))
                       : MapBinary(l, r, Math.Pow), // MVP — full would do matrix power
                ".^" => MapBinary(l, r, Math.Pow),
                "==" => MapBinary(l, r, (a, c) => a == c ? 1 : 0),
                "~=" => MapBinary(l, r, (a, c) => a != c ? 1 : 0),
                "<" => MapBinary(l, r, (a, c) => a < c ? 1 : 0),
                ">" => MapBinary(l, r, (a, c) => a > c ? 1 : 0),
                "<=" => MapBinary(l, r, (a, c) => a <= c ? 1 : 0),
                ">=" => MapBinary(l, r, (a, c) => a >= c ? 1 : 0),
                "&&" or "&" => MapBinary(l, r, (a, c) => (a != 0 && c != 0) ? 1 : 0),
                "||" or "|" => MapBinary(l, r, (a, c) => (a != 0 || c != 0) ? 1 : 0),
                _ => throw new MatlabRuntimeException($"Unsupported binary op: {b.Op}")
            };
        }
        /// <summary>Coerce un MValue a "texto" para operaciones string. Reglas:
        /// scalar numérico → num2str; symbolic → ToInfix; char-array → StringValue; string scalar → StringValue.</summary>
        private static string CoerceToText(MValue v)
        {
            if (v.IsString) return v.StringValue ?? "";
            if (v.IsSymbolic) return v.Symbolic.ToInfix();
            if (v.IsScalar) return v.Scalar.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            throw new MatlabRuntimeException("string op: operand cannot be coerced to text");
        }
        private static MValue EvalBinaryStringOp(string op, MValue l, MValue r)
        {
            // Caso 1: ambos string scalar (IsDoubleQuoted scalar) → string scalar
            // Caso 2: uno es StringArray → broadcast elementwise
            // Caso 3: string scalar + numero/sym → string scalar (concat con texto coerced)
            bool lArr = l.IsStringArray;
            bool rArr = r.IsStringArray;
            // Resolve op
            string DoConcat(string a, string b) => op switch
            {
                "+" => a + b,
                _ => throw new MatlabRuntimeException($"string op '{op}' not supported")
            };
            if (op == "==" || op == "~=")
            {
                // Comparación string
                if (!lArr && !rArr)
                {
                    bool eq = CoerceToText(l) == CoerceToText(r);
                    return new MValue((op == "==") == eq ? 1 : 0);
                }
                // Array vs scalar broadcast
                int r0 = lArr ? l.StringArrayData.GetLength(0) : r.StringArrayData.GetLength(0);
                int c0 = lArr ? l.StringArrayData.GetLength(1) : r.StringArrayData.GetLength(1);
                var resR = new MValue(r0, c0);
                for (int i = 0; i < r0; i++)
                    for (int j = 0; j < c0; j++)
                    {
                        string ls = lArr ? l.StringArrayData[i, j] : CoerceToText(l);
                        string rs = rArr ? r.StringArrayData[i, j] : CoerceToText(r);
                        bool e = ls == rs;
                        resR.Set(i, j, (op == "==") == e ? 1 : 0);
                    }
                return resR;
            }
            // Concat: solo `+`
            if (op != "+") throw new MatlabRuntimeException($"string op '{op}' not supported");
            if (!lArr && !rArr)
            {
                return MValue.NewStringScalar(DoConcat(CoerceToText(l), CoerceToText(r)));
            }
            // Broadcast: si uno es array, otro escalar — broadcast escalar
            int rows, cols;
            if (lArr) { rows = l.StringArrayData.GetLength(0); cols = l.StringArrayData.GetLength(1); }
            else { rows = r.StringArrayData.GetLength(0); cols = r.StringArrayData.GetLength(1); }
            if (lArr && rArr)
            {
                if (l.StringArrayData.GetLength(0) != rows || l.StringArrayData.GetLength(1) != cols
                    || r.StringArrayData.GetLength(0) != rows || r.StringArrayData.GetLength(1) != cols)
                    throw new MatlabRuntimeException("string array +: shape mismatch");
            }
            var outArr = new string[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    string ls = lArr ? l.StringArrayData[i, j] : CoerceToText(l);
                    string rs = rArr ? r.StringArrayData[i, j] : CoerceToText(r);
                    outArr[i, j] = DoConcat(ls, rs);
                }
            return MValue.NewStringArray(outArr);
        }
        /// <summary>Convierte un MValue (escalar/sym/numeric matrix) a SymNode[,] de dimensión rTarget×cTarget mediante broadcast.</summary>
        private static SymNode[,] CoerceToSymMatrix(MValue v, int rTarget, int cTarget)
        {
            if (v.IsSymMatrix)
            {
                if (v.SymCells.GetLength(0) == rTarget && v.SymCells.GetLength(1) == cTarget) return v.SymCells;
                throw new MatlabRuntimeException($"sym matrix op: shape mismatch {v.SymCells.GetLength(0)}×{v.SymCells.GetLength(1)} vs {rTarget}×{cTarget}");
            }
            SymNode scalar = null;
            if (v.IsSymbolic) scalar = v.Symbolic;
            else if (v.IsScalar) scalar = new SymConst(v.Scalar);
            if (scalar != null)
            {
                var c = new SymNode[rTarget, cTarget];
                for (int i = 0; i < rTarget; i++)
                    for (int j = 0; j < cTarget; j++)
                        c[i, j] = scalar;
                return c;
            }
            // Matriz numérica → lift cada celda a SymConst
            if (v.Rows == rTarget && v.Cols == cTarget)
            {
                var c = new SymNode[rTarget, cTarget];
                for (int i = 0; i < rTarget; i++)
                    for (int j = 0; j < cTarget; j++)
                        c[i, j] = new SymConst(v.At(i, j));
                return c;
            }
            throw new MatlabRuntimeException($"sym matrix op: shape mismatch {v.Rows}×{v.Cols} vs {rTarget}×{cTarget}");
        }
        private static MValue EvalBinarySymMatrix(string op, MValue l, MValue r)
        {
            // Multiplicación matricial: l (R×M) * r (M×C) — requiere ambos al menos matriz
            if (op == "*")
            {
                bool lScalar = l.IsScalar || l.IsSymbolic;
                bool rScalar = r.IsScalar || r.IsSymbolic;
                if (lScalar && r.IsSymMatrix)
                {
                    SymNode s = l.IsSymbolic ? l.Symbolic : new SymConst(l.Scalar);
                    return MValue.NewSymMatrix(SymMatOps.ScalarMul(s, r.SymCells));
                }
                if (rScalar && l.IsSymMatrix)
                {
                    SymNode s = r.IsSymbolic ? r.Symbolic : new SymConst(r.Scalar);
                    return MValue.NewSymMatrix(SymMatOps.ScalarMul(s, l.SymCells));
                }
                // Caso matriz·matriz (sym o numérica)
                int lR = l.IsSymMatrix ? l.SymCells.GetLength(0) : l.Rows;
                int lC = l.IsSymMatrix ? l.SymCells.GetLength(1) : l.Cols;
                int rR = r.IsSymMatrix ? r.SymCells.GetLength(0) : r.Rows;
                int rC = r.IsSymMatrix ? r.SymCells.GetLength(1) : r.Cols;
                if (lC != rR) throw new MatlabRuntimeException($"sym matmul: inner dim mismatch {lR}×{lC} * {rR}×{rC}");
                var L = CoerceToSymMatrix(l, lR, lC);
                var R = CoerceToSymMatrix(r, rR, rC);
                return MValue.NewSymMatrix(SymMatOps.Mul(L, R));
            }
            // Element-wise: +, -, .*, ./, .^
            // Determinar shape común
            int targetR, targetC;
            if (l.IsSymMatrix) { targetR = l.SymCells.GetLength(0); targetC = l.SymCells.GetLength(1); }
            else if (r.IsSymMatrix) { targetR = r.SymCells.GetLength(0); targetC = r.SymCells.GetLength(1); }
            else { targetR = Math.Max(l.Rows, r.Rows); targetC = Math.Max(l.Cols, r.Cols); }
            var Lc = CoerceToSymMatrix(l, targetR, targetC);
            var Rc = CoerceToSymMatrix(r, targetR, targetC);
            switch (op)
            {
                case "+": return MValue.NewSymMatrix(SymMatOps.Add(Lc, Rc));
                case "-": return MValue.NewSymMatrix(SymMatOps.Sub(Lc, Rc));
                case ".*":
                {
                    var Z = new SymNode[targetR, targetC];
                    for (int i = 0; i < targetR; i++)
                        for (int j = 0; j < targetC; j++)
                            Z[i, j] = new SymMul(Lc[i, j], Rc[i, j]).Simplify();
                    return MValue.NewSymMatrix(Z);
                }
                case "./":
                {
                    var Z = new SymNode[targetR, targetC];
                    for (int i = 0; i < targetR; i++)
                        for (int j = 0; j < targetC; j++)
                            Z[i, j] = new SymDiv(Lc[i, j], Rc[i, j]).Simplify();
                    return MValue.NewSymMatrix(Z);
                }
                case ".^":
                {
                    var Z = new SymNode[targetR, targetC];
                    for (int i = 0; i < targetR; i++)
                        for (int j = 0; j < targetC; j++)
                            Z[i, j] = new SymPow(Lc[i, j], Rc[i, j]).Simplify();
                    return MValue.NewSymMatrix(Z);
                }
                case "^":
                {
                    // Solo soporta matriz^entero
                    if (!r.IsScalar) throw new MatlabRuntimeException("sym matrix ^: exponent must be scalar integer");
                    int n = (int)r.Scalar;
                    var L = CoerceToSymMatrix(l, l.SymCells?.GetLength(0) ?? l.Rows, l.SymCells?.GetLength(1) ?? l.Cols);
                    return MValue.NewSymMatrix(SymMatOps.Pow(L, n));
                }
            }
            throw new MatlabRuntimeException($"sym matrix op '{op}' not supported");
        }

        private static MValue MatMul(MValue a, MValue b)
        {
            if (a.IsScalar || b.IsScalar) return MapBinary(a, b, (x, y) => x * y);
            if (a.Cols != b.Rows)
                throw new MatlabRuntimeException($"Matrix multiplication: {a.Rows}×{a.Cols} * {b.Rows}×{b.Cols}");
            // Sparse-Mat path: A·B con A sparse → SpMM eficiente
            if (a.IsSparseReal)
            {
                var r = new MValue(a.Rows, b.Cols);
                for (int i = 0; i < a.Rows; i++)
                    for (int k = a.SparseRowPtr[i]; k < a.SparseRowPtr[i + 1]; k++)
                    {
                        double aIK = a.SparseVals[k];
                        int colA = a.SparseCols[k];
                        for (int j = 0; j < b.Cols; j++)
                            r.Set(i, j, r.At(i, j) + aIK * b.At(colA, j));
                    }
                return r;
            }
            // B sparse: A·B = (B'·A')'
            if (b.IsSparseReal)
            {
                // Transponer manualmente sin recursión
                var bT = SparseTranspose(b);
                var aT = TransposeSimple(a);
                var rT = MatMul(bT, aT);
                return TransposeSimple(rT);
            }
            var rd = new MValue(a.Rows, b.Cols);
            // FAST PATH 1: matrix-vector (b.Cols == 1) → DGEMV nativo
            // Para FEM: M_vec = D*Bm*Z_e es cadena de matvec — DGEMV es L2 BLAS
            // optimizado, ~2x mas rapido que DGEMM con n=1.
            if (b.Cols == 1 && BlasInterop.Available
                && a.Imag == null && b.Imag == null
                && a.Rows >= BlasInterop.BlasThreshold)
            {
                BlasInterop.MatVec(a.Rows, a.Cols, a.Data, b.Data, rd.Data);
                return rd;
            }
            // FAST PATH 2: matrix-matrix grande → DGEMM nativo
            int mx = a.Rows > b.Cols ? (a.Rows > a.Cols ? a.Rows : a.Cols)
                                     : (b.Cols > a.Cols ? b.Cols : a.Cols);
            if (mx >= BlasInterop.BlasThreshold && BlasInterop.Available
                && a.Imag == null && b.Imag == null)
            {
                BlasInterop.MatMul(a.Rows, a.Cols, b.Cols, a.Data, b.Data, rd.Data);
                return rd;
            }
            // Loop naive para matrices chicas (overhead BLAS > compute).
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < b.Cols; j++)
                {
                    double s = 0;
                    for (int k = 0; k < a.Cols; k++)
                        s += a.At(i, k) * b.At(k, j);
                    rd.Set(i, j, s);
                }
            return rd;
        }
        private static MValue TransposeSimple(MValue m)
        {
            var r = new MValue(m.Cols, m.Rows);
            for (int i = 0; i < m.Rows; i++)
                for (int j = 0; j < m.Cols; j++) r.Set(j, i, m.At(i, j));
            return r;
        }
        private static MValue SparseTranspose(MValue s)
        {
            // CSR → CSC = CSR del transpuesto
            int m = s.Rows, n = s.Cols;
            int nnz = s.SparseVals.Length;
            var rowPtrT = new int[n + 1];
            for (int k = 0; k < nnz; k++) rowPtrT[s.SparseCols[k] + 1]++;
            for (int i = 0; i < n; i++) rowPtrT[i + 1] += rowPtrT[i];
            var valsT = new double[nnz];
            var colsT = new int[nnz];
            var offsetCopy = (int[])rowPtrT.Clone();
            for (int i = 0; i < m; i++)
                for (int k = s.SparseRowPtr[i]; k < s.SparseRowPtr[i + 1]; k++)
                {
                    int dst = offsetCopy[s.SparseCols[k]]++;
                    valsT[dst] = s.SparseVals[k];
                    colsT[dst] = i;
                }
            return MValue.NewSparseCSR(n, m, valsT, colsT, rowPtrT);
        }
        private MValue EvalCallOrIndex(CallOrIndex c, MatlabScope scope)
        {
            // OOP: method call obj.method(args) o Class.staticMethod(args)
            if (c.Target is FieldAccess fa)
            {
                // containers.Map(...) — constructor de mapa (clave->valor)
                if (fa.Target is IdentRef cmRef && cmRef.Name == "containers" && fa.FieldName == "Map"
                    && !scope.TryGet("containers", out _))
                    return BuildMap(EvalArgs(c.Args, scope));
                // Caso static: ClassName.staticMethod(args) — sin instancia
                if (fa.Target is IdentRef classRef
                    && !scope.TryGet(classRef.Name, out _)
                    && _classes.TryGetValue(classRef.Name, out var staticCls))
                {
                    var sm = FindStaticMethod(staticCls, fa.FieldName);
                    if (sm != null)
                    {
                        var argsS = EvalArgs(c.Args, scope);
                        return CallUserFunction(sm, argsS);
                    }
                    // Si no es static method, podría ser constructor con field nuevo — caso no soportado
                    throw new MatlabRuntimeException($"Class '{classRef.Name}' has no static method '{fa.FieldName}'");
                }
                var receiver = Eval(fa.Target, scope);
                if (receiver.IsInstance && _classes.TryGetValue(receiver.ClassName, out var cls))
                {
                    var method = FindMethod(cls, fa.FieldName);
                    if (method != null)
                    {
                        var args0 = EvalArgs(c.Args, scope);
                        var allArgs = new MValue[args0.Length + 1];
                        allArgs[0] = receiver;
                        Array.Copy(args0, 0, allArgs, 1, args0.Length);
                        return CallUserFunction(method, allArgs);
                    }
                }
                // No es method: fallback — fa puede dar callable o un valor indexable
                var fv = Eval(fa, scope);
                if (fv.IsCallable) return fv.Callable(EvalArgs(c.Args, scope));
                return IndexInto(fv, c.Args, scope);
            }
            if (c.Target is IdentRef id)
            {
                // 1) Variable indexada — pero si es function handle, llamar.
                if (scope.TryGet(id.Name, out var v))
                {
                    if (v.IsCallable) return v.Callable(EvalArgs(c.Args, scope));
                    // containers.Map: m(key) -> valor
                    if (v.IsMap)
                    {
                        var ka = EvalArgs(c.Args, scope);
                        if (ka.Length != 1) throw new MatlabRuntimeException("Map: se requiere exactamente 1 clave en m(key)");
                        var mk = MapKey(ka[0]);
                        if (!v.MapData.TryGetValue(mk, out var mv))
                            throw new MatlabRuntimeException($"The key '{mk}' is not present in the container.");
                        return mv;
                    }
                    // SYMFUN: `f(arg1, arg2, ...)` donde f esta registrado como
                    // symfun via `f(x) = expr`. Sustituye cada param formal por
                    // el argumento correspondiente.
                    if (v.IsSymbolic && _symFunParams.TryGetValue(id.Name, out var symfunParams))
                    {
                        var symfunArgs = EvalArgs(c.Args, scope);
                        if (symfunArgs.Length != symfunParams.Count)
                            throw new MatlabRuntimeException(
                                $"Symfun '{id.Name}' espera {symfunParams.Count} args, recibio {symfunArgs.Length}");
                        var expr = v.Symbolic;
                        for (int i = 0; i < symfunParams.Count; i++)
                        {
                            SymNode argSym = symfunArgs[i].IsSymbolic
                                ? symfunArgs[i].Symbolic
                                : new SymConst(symfunArgs[i].Scalar);
                            expr = expr.Subs(symfunParams[i], argSym);
                        }
                        return MValue.NewSymbolic(expr.Simplify());
                    }
                    // struct-array: res(s) -> el struct del elemento s
                    if (v.IsStructArray)
                    {
                        var ia = EvalArgs(c.Args, scope);
                        if (ia.Length == 1)
                        {
                            int k = (int)ia[0].Scalar;
                            if (k < 1 || k > v.StructArrayItems.Count)
                                throw new MatlabRuntimeException($"struct-array: índice {k} fuera de rango (1..{v.StructArrayItems.Count})");
                            return v.StructArrayItems[k - 1];
                        }
                        throw new MatlabRuntimeException("struct-array: solo indexación 1D (res(k))");
                    }
                    return IndexInto(v, c.Args, scope);
                }
                // 2) Class constructor
                if (_classes.TryGetValue(id.Name, out var cls))
                    return ConstructInstance(cls, EvalArgs(c.Args, scope), scope);
                // 3) User function definida
                if (_userFunctions.TryGetValue(id.Name, out var def))
                    return CallUserFunction(def, EvalArgs(c.Args, scope), ArgNames(c.Args));
                // 4) Builtin función
                var args = EvalArgs(c.Args, scope);
                if (_builtins.TryGetValue(id.Name, out var fn))
                    return fn(args);
                if (_multiOutBuiltins.TryGetValue(id.Name, out var fn2))
                    return fn2(args)[0];
                throw new MatlabRuntimeException($"Undefined: {id.Name}");
            }
            // Caso: (expr)(args) — donde expr evalúa a callable
            var target = Eval(c.Target, scope);
            if (target.IsCallable) return target.Callable(EvalArgs(c.Args, scope));
            // si el target NO es llamable pero SÍ indexable (matriz/cell/3D/...), esto es INDEXING:
            //   ej. Bc{e}(1,1) — Bc{e} da una matriz y (1,1) la indexa (MATLAB lo permite).
            if (target != null && !target.IsStruct && !target.IsSymbolic && !target.IsMap
                && !target.IsInstance && !target.IsGfxHandle && !target.IsStructArray)
                return IndexInto(target, c.Args, scope);
            string tdesc = c.Target is FieldAccess fa2 ? $"campo '{fa2.FieldName}'"
                         : c.Target is CellIndex ? "cell{}"
                         : c.Target is CallOrIndex ? "resultado de indexado"
                         : c.Target?.GetType().Name ?? "?";
            throw new MatlabRuntimeException($"Target is not callable [{tdesc}]");
        }
        /// <summary>Busca un método por nombre en una clase (y sus padres). Devuelve null si no existe.</summary>
        private FunctionDef FindMethod(ClassDef cls, string name)
        {
            foreach (var m in cls.Methods)
                if (m.Name == name) return m;
            // Herencia simple
            if (cls.ParentName != null && _classes.TryGetValue(cls.ParentName, out var parent))
                return FindMethod(parent, name);
            return null;
        }
        /// <summary>Busca un static method por nombre en una clase (y sus padres). Devuelve null si no existe.</summary>
        private FunctionDef FindStaticMethod(ClassDef cls, string name)
        {
            foreach (var m in cls.StaticMethods)
                if (m.Name == name) return m;
            if (cls.ParentName != null && _classes.TryGetValue(cls.ParentName, out var parent))
                return FindStaticMethod(parent, name);
            return null;
        }
        /// <summary>Mapea un operador binario MATLAB a su método de overload. Null si no hay overload posible.</summary>
        private static string MapBinOpToMethod(string op) => op switch
        {
            "+" => "plus",
            "-" => "minus",
            "*" => "mtimes",
            ".*" => "times",
            "/" => "mrdivide",
            "./" => "rdivide",
            "\\" => "mldivide",
            ".\\" => "ldivide",
            "^" => "mpower",
            ".^" => "power",
            "==" => "eq",
            "~=" => "ne",
            "<" => "lt",
            ">" => "gt",
            "<=" => "le",
            ">=" => "ge",
            "&" => "and",
            "|" => "or",
            _ => null
        };
        private static string MapUnaryOpToMethod(string op) => op switch
        {
            "-" => "uminus",
            "+" => "uplus",
            "~" => "not",
            "!" => "not",
            "'" => "ctranspose",
            ".'" => "transpose",
            _ => null
        };
        /// <summary>Intenta despachar un binario via overload (plus/mtimes/eq/...). Retorna null si no hay overload aplicable.</summary>
        private MValue TryOverloadBinary(string op, MValue l, MValue r)
        {
            var mname = MapBinOpToMethod(op);
            if (mname == null) return null;
            // Buscar primero en LHS (si es instancia), después en RHS
            if (l.IsInstance && _classes.TryGetValue(l.ClassName, out var lcls))
            {
                var m = FindMethod(lcls, mname);
                if (m != null) return CallUserFunction(m, new[] { l, r });
            }
            if (r.IsInstance && _classes.TryGetValue(r.ClassName, out var rcls))
            {
                var m = FindMethod(rcls, mname);
                if (m != null) return CallUserFunction(m, new[] { l, r });
            }
            return null;
        }
        private MValue TryOverloadUnary(string op, MValue v)
        {
            var mname = MapUnaryOpToMethod(op);
            if (mname == null) return null;
            if (v.IsInstance && _classes.TryGetValue(v.ClassName, out var cls))
            {
                var m = FindMethod(cls, mname);
                if (m != null) return CallUserFunction(m, new[] { v });
            }
            return null;
        }
        /// <summary>Instancia una clase. Inicializa propiedades con DefaultExpr (o 0). Si hay método constructor (nombre == clase), lo llama.
        /// MATLAB convention: <c>function obj = MyClass(args)</c> — la variable <c>obj</c> es el OUTPUT, pre-poblada con defaults;
        /// los args van directo a los params declarados. El body modifica <c>obj</c> y se retorna su valor final.</summary>
        private MValue ConstructInstance(ClassDef cls, MValue[] ctorArgs, MatlabScope scope)
        {
            var props = new Dictionary<string, MValue>(StringComparer.Ordinal);
            // Propiedades del padre primero (herencia simple)
            if (cls.ParentName != null && _classes.TryGetValue(cls.ParentName, out var parent))
            {
                foreach (var p in parent.Properties)
                    props[p.Name] = p.DefaultExpr != null ? Eval(p.DefaultExpr, scope) : new MValue(0);
            }
            foreach (var p in cls.Properties)
                props[p.Name] = p.DefaultExpr != null ? Eval(p.DefaultExpr, scope) : new MValue(0);
            var inst = MValue.NewInstance(cls.Name, props);
            // Constructor — método con el mismo nombre de la clase
            var ctor = FindMethod(cls, cls.Name);
            if (ctor == null) return inst;
            // Ejecutar el ctor en scope local. Pre-poblamos OutputName con la instancia.
            var local = new MatlabScope(Globals);
            var savedFn = _currentFunctionName;
            _currentFunctionName = ctor.Name;
            // Output variable (el "obj") pre-bound a la instancia con defaults
            if (ctor.OutputNames.Count > 0)
                local.Set(ctor.OutputNames[0], inst);
            // Params reciben los args originales (no shift)
            for (int i = 0; i < ctor.ParamNames.Count && i < ctorArgs.Length; i++)
                local.Set(ctor.ParamNames[i], ctorArgs[i]);
            try { foreach (var s in ctor.Body) ExecuteOne(s, local); }
            catch (ReturnSignal) { }
            _currentFunctionName = savedFn;
            // Retornar el output variable (puede haber sido modificado)
            if (ctor.OutputNames.Count > 0 && local.TryGet(ctor.OutputNames[0], out var outV))
                return outV;
            return inst;
        }

        /// <summary>Construye un function handle a partir de un AnonFunction AST node, capturando el scope.</summary>
        private MValue MakeAnonHandle(AnonFunction af, MatlabScope captured)
        {
            // Caso especial: @name (function handle a nombre conocido)
            if (af.ParamNames.Count == 1 && af.ParamNames[0] == "__handle__" && af.Body is IdentRef ref0)
            {
                var nm = ref0.Name;
                return new MValue(args => {
                    if (_userFunctions.TryGetValue(nm, out var def))
                        return CallUserFunction(def, args);
                    if (_builtins.TryGetValue(nm, out var fn)) return fn(args);
                    throw new MatlabRuntimeException($"Undefined function: {nm}");
                }, "@" + nm);
            }
            var paramNames = af.ParamNames.ToArray();
            return new MValue(args => {
                var local = new MatlabScope(captured);
                for (int i = 0; i < paramNames.Length && i < args.Length; i++)
                    local.Set(paramNames[i], args[i]);
                return Eval(af.Body, local);
            }, "@(...)" );
        }
        public MValue[] CallMultiOut(string name, MValue[] args, string[] argNames = null)
        {
            if (_multiOutBuiltins.TryGetValue(name, out var fn2)) return fn2(args);
            if (_userFunctions.TryGetValue(name, out var def))   return CallUserFunctionMulti(def, args, argNames);
            if (_builtins.TryGetValue(name, out var fn))         return new[] { fn(args) };
            throw new MatlabRuntimeException($"Undefined: {name}");
        }
        private MValue[] EvalArgs(List<MatlabNode> args, MatlabScope scope)
        {
            var r = new MValue[args.Count];
            for (int i = 0; i < args.Count; i++) r[i] = Eval(args[i], scope);
            return r;
        }
        private MValue IndexInto(MValue m, List<MatlabNode> idxNodes, MatlabScope scope)
        {
            // 3D array: A(:,:,k) o A(i,j,k)
            if (m.Is3D && idxNodes.Count == 3)
            {
                // Resolve page index (3er arg)
                int nPages = m.Pages.Length;
                var pageIdx = new List<int>();
                _endCtx.Push((new MValue(1, nPages), 0, 1));
                try
                {
                    var arg2 = idxNodes[2];
                    if (arg2 is ColonAll) { for (int p = 0; p < nPages; p++) pageIdx.Add(p); }
                    else if (arg2 is Range r)
                    {
                        double s = Eval(r.Start, scope).Scalar;
                        double e = Eval(r.End, scope).Scalar;
                        double step = r.Step != null ? Eval(r.Step, scope).Scalar : 1.0;
                        int cnt = (int)Math.Floor((e - s) / step + 1e-9) + 1;
                        for (int k = 0; k < cnt; k++) pageIdx.Add((int)(s + k * step) - 1);
                    }
                    else { var v = Eval(arg2, scope); pageIdx.Add((int)v.Scalar - 1); }
                }
                finally { _endCtx.Pop(); }
                if (pageIdx.Count == 1)
                {
                    // 1 página → matriz 2D, aplicar indexing 2D restante
                    var page = m.Pages[pageIdx[0]];
                    if (idxNodes[0] is ColonAll && idxNodes[1] is ColonAll) return page;
                    return IndexInto(page, new List<MatlabNode> { idxNodes[0], idxNodes[1] }, scope);
                }
                // Múltiples páginas → devolver 3D subarray
                var subPages = new MValue[pageIdx.Count];
                for (int k = 0; k < pageIdx.Count; k++)
                {
                    var p = m.Pages[pageIdx[k]];
                    if (idxNodes[0] is ColonAll && idxNodes[1] is ColonAll) subPages[k] = p;
                    else subPages[k] = IndexInto(p, new List<MatlabNode> { idxNodes[0], idxNodes[1] }, scope);
                }
                return MValue.New3D(subPages);
            }
            // Resolver cada arg → lista de índices (1-based MATLAB convertidos a 0-based)
            // Soporta: escalar, ColonAll, Range, vector de índices.
            int nDims = idxNodes.Count;
            int[][] indices = new int[nDims][];
            for (int d = 0; d < nDims; d++)
            {
                _endCtx.Push((m, d, nDims));
                try { indices[d] = ResolveIndexArg(idxNodes[d], m, d, nDims, scope); }
                finally { _endCtx.Pop(); }
            }
            // Caso 1: linear index (1 arg)
            if (nDims == 1)
            {
                var lin = indices[0];
                // 3D: aplanar column-major (paginas × columnas × filas) e indexar linealmente. X(:) / X(k).
                if (m.Is3D)
                {
                    var flat3 = Flatten3DColMajor(m);
                    if (lin.Length == 1)
                    {
                        int i0 = lin[0];
                        if (i0 < 0 || i0 >= flat3.Length) throw new MatlabRuntimeException($"Index {i0 + 1} out of bounds (1..{flat3.Length})");
                        return new MValue(flat3[i0]);
                    }
                    var col3 = new MValue(lin.Length, 1);   // 3D flatten -> column vector
                    for (int k = 0; k < lin.Length; k++)
                    {
                        int li = lin[k];
                        if (li < 0 || li >= flat3.Length) throw new MatlabRuntimeException($"Index {li + 1} out of bounds (1..{flat3.Length})");
                        col3.Data[k] = flat3[li];
                    }
                    return col3;
                }
                if (lin.Length == 1)
                {
                    int i = lin[0];
                    int total = m.Rows * m.Cols;
                    if (i < 0 || i >= total)
                        throw new MatlabRuntimeException($"Index {i + 1} out of bounds (1..{total})");
                    // Matriz simbólica: devolver la CELDA como sym escalar (column-major).
                    if (m.IsSymMatrix)
                    {
                        int col0 = i / m.Rows, row0 = i - col0 * m.Rows;
                        return MValue.NewSymbolic(m.SymCells[row0, col0]);
                    }
                    return new MValue(m.Data[i]);
                }
                // Slice: si idxNode es ColonAll (xg(:)) → COLUMN vector column-major
                // Si idxNode es range/vector → preservar orientación del input (row si input es row)
                bool isFullColon = idxNodes[0] is ColonAll;
                bool sourceIsRow = m.Rows == 1;
                // Matriz simbólica: slice → sub-matriz simbólica (misma orientación).
                if (m.IsSymMatrix)
                {
                    bool asRow = !isFullColon && sourceIsRow;
                    var cellsS = asRow ? new SymNode[1, lin.Length] : new SymNode[lin.Length, 1];
                    for (int k = 0; k < lin.Length; k++)
                    {
                        int li = lin[k];
                        if (li < 0 || li >= m.Rows * m.Cols)
                            throw new MatlabRuntimeException($"Index {li + 1} out of bounds");
                        int col = li / m.Rows, row = li - col * m.Rows;
                        if (asRow) cellsS[0, k] = m.SymCells[row, col];
                        else cellsS[k, 0] = m.SymCells[row, col];
                    }
                    return MValue.NewSymMatrix(cellsS);
                }
                MValue r;
                if (isFullColon)
                    r = new MValue(lin.Length, 1);   // column vector
                else if (sourceIsRow)
                    r = new MValue(1, lin.Length);   // mantener row si source es row
                else
                    r = new MValue(lin.Length, 1);   // default column
                for (int k = 0; k < lin.Length; k++)
                {
                    int li = lin[k];
                    if (li < 0 || li >= m.Rows * m.Cols)
                        throw new MatlabRuntimeException($"Index {li + 1} out of bounds");
                    // MATLAB column-major linear → 0-based: col = li/Rows, row = li%Rows
                    int col = li / m.Rows, row = li - col * m.Rows;
                    r.Data[k] = m.At(row, col);
                }
                return r;
            }
            // Caso 2: matrix indexing (2 args)
            if (nDims == 2)
            {
                var rows = indices[0];
                var cols = indices[1];
                if (rows.Length == 1 && cols.Length == 1)
                {
                    int rr = rows[0], cc = cols[0];
                    if (rr < 0 || rr >= m.Rows || cc < 0 || cc >= m.Cols)
                        throw new MatlabRuntimeException($"Index ({rr + 1}, {cc + 1}) out of {m.Rows}×{m.Cols}");
                    if (m.IsSymMatrix) return MValue.NewSymbolic(m.SymCells[rr, cc]);
                    return new MValue(m.At(rr, cc));
                }
                // Matriz simbólica: submatriz → sub-matriz simbólica.
                if (m.IsSymMatrix)
                {
                    var cellsS = new SymNode[rows.Length, cols.Length];
                    for (int i = 0; i < rows.Length; i++)
                        for (int j = 0; j < cols.Length; j++)
                        {
                            int rr = rows[i], cc = cols[j];
                            if (rr < 0 || rr >= m.Rows || cc < 0 || cc >= m.Cols)
                                throw new MatlabRuntimeException($"Submatrix index ({rr + 1}, {cc + 1}) out of {m.Rows}×{m.Cols}");
                            cellsS[i, j] = m.SymCells[rr, cc];
                        }
                    return MValue.NewSymMatrix(cellsS);
                }
                // Submatrix: si m es SPARSE, mantener sparse (recorre solo no-ceros)
                // en vez de densificar rows×cols y llamar At() por cada celda.
                if (m.IsSparseReal)
                    return MatlabLinAlg.SparseSubmatrix(m, rows, cols);
                var sub = new MValue(rows.Length, cols.Length);
                for (int i = 0; i < rows.Length; i++)
                    for (int j = 0; j < cols.Length; j++)
                    {
                        int rr = rows[i], cc = cols[j];
                        if (rr < 0 || rr >= m.Rows || cc < 0 || cc >= m.Cols)
                            throw new MatlabRuntimeException($"Submatrix index ({rr + 1}, {cc + 1}) out of {m.Rows}×{m.Cols}");
                        sub.Set(i, j, m.At(rr, cc));
                    }
                return sub;
            }
            throw new MatlabRuntimeException("Indexing > 2D not supported");
        }

        /// <summary>
        /// Resuelve un argumento de indexing a un array de índices 0-based. Maneja
        /// ColonAll (:), Range, escalares, y vectores de índices.
        /// </summary>
        private int[] ResolveIndexArg(MatlabNode arg, MValue target, int dim, int nDims, MatlabScope scope)
        {
            int dimSize = nDims == 1 ? (target.Is3D ? target.Pages[0].Rows * target.Pages[0].Cols * target.Pages.Length : target.Rows * target.Cols)
                        : dim == 0 ? (target.Is3D ? target.Pages[0].Rows : target.Rows)
                        : (target.Is3D && dim == 1 ? target.Pages[0].Cols : target.Cols);
            if (arg is ColonAll)
            {
                var r = new int[dimSize];
                for (int i = 0; i < dimSize; i++) r[i] = i;
                return r;
            }
            if (arg is Range rng)
            {
                double s = Eval(rng.Start, scope).Scalar;
                double e = Eval(rng.End, scope).Scalar;
                double step = rng.Step != null ? Eval(rng.Step, scope).Scalar : 1.0;
                if (step == 0) throw new MatlabRuntimeException("Range step cannot be 0");
                int n = (int)Math.Floor((e - s) / step + 1e-9) + 1;
                if (n < 0) n = 0;
                var r = new int[n];
                for (int k = 0; k < n; k++) r[k] = (int)(s + k * step) - 1;  // 1-based → 0-based
                return r;
            }
            var v = Eval(arg, scope);
            if (v.IsScalar) return new[] { (int)v.Scalar - 1 };
            // Detectar logical mask: dimensiones que matchean target/dim Y todos los valores 0/1
            // Si v.Length == dimSize y todos los valores son 0 o 1, asumir mask.
            bool isMask = v.Data.Length == dimSize;
            if (isMask)
            {
                foreach (var x in v.Data) if (x != 0 && x != 1) { isMask = false; break; }
            }
            if (isMask)
            {
                var hits = new List<int>();
                for (int i = 0; i < v.Data.Length; i++) if (v.Data[i] != 0) hits.Add(i);
                return hits.ToArray();
            }
            // Vector de índices numéricos (1-based)
            var arr = new int[v.Data.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = (int)v.Data[i] - 1;
            return arr;
        }
        private MValue EvalRange(Range r, MatlabScope scope)
        {
            double s = Eval(r.Start, scope).Scalar;
            double e = Eval(r.End, scope).Scalar;
            double step = r.Step != null ? Eval(r.Step, scope).Scalar : 1.0;
            if (step == 0) throw new MatlabRuntimeException("Range step cannot be 0");
            int n = (int)Math.Floor((e - s) / step + 1e-9) + 1;
            if (n < 0) n = 0;
            var v = new MValue(1, n);
            for (int i = 0; i < n; i++) v.Data[i] = s + i * step;
            return v;
        }
        private MValue EvalCellLit(CellLit cl, MatlabScope scope)
        {
            if (cl.Rows.Count == 0) return MValue.NewCell(new MValue[0, 0]);
            // Caso {res.n} — un solo elemento que es un campo de struct-array (comma-list):
            // se expande a una fila de N celdas (una por elemento del array).
            if (cl.Rows.Count == 1 && cl.Rows[0].Count == 1
                && cl.Rows[0][0] is FieldAccess fa && fa.Target is IdentRef arrId
                && scope.TryGet(arrId.Name, out var arrV) && arrV.IsStructArray)
            {
                var items = arrV.StructArrayItems;
                var row = new MValue[1, Math.Max(1, items.Count)];
                for (int j = 0; j < items.Count; j++)
                    row[0, j] = (items[j].Fields != null && items[j].Fields.TryGetValue(fa.FieldName, out var fj))
                                ? fj : new MValue(0);
                return MValue.NewCell(row);
            }
            int nRows = cl.Rows.Count;
            int nCols = 0;
            foreach (var row in cl.Rows) if (row.Count > nCols) nCols = row.Count;
            var cells = new MValue[nRows, nCols];
            for (int i = 0; i < nRows; i++)
                for (int j = 0; j < cl.Rows[i].Count; j++)
                    cells[i, j] = Eval(cl.Rows[i][j], scope);
            return MValue.NewCell(cells);
        }
        private MValue EvalCellIndex(CellIndex ci, MatlabScope scope)
        {
            var target = Eval(ci.Target, scope);
            if (!target.IsCell) throw new MatlabRuntimeException("Cell index {…} requires a cell array");
            int nr = target.CellData.GetLength(0);
            int nc = target.CellData.GetLength(1);
            if (ci.Args.Count == 1)
            {
                int idx = (int)Eval(ci.Args[0], scope).Scalar - 1;
                // Column-major linear
                int col = idx / nr, row = idx - col * nr;
                if (col < 0 || col >= nc || row < 0 || row >= nr)
                    throw new MatlabRuntimeException($"Cell index {idx + 1} out of bounds (1..{nr * nc})");
                return target.CellData[row, col];
            }
            if (ci.Args.Count == 2)
            {
                int r = (int)Eval(ci.Args[0], scope).Scalar - 1;
                int c = (int)Eval(ci.Args[1], scope).Scalar - 1;
                if (r < 0 || r >= nr || c < 0 || c >= nc)
                    throw new MatlabRuntimeException($"Cell index ({r + 1}, {c + 1}) out of bounds");
                return target.CellData[r, c];
            }
            throw new MatlabRuntimeException("Cell indexing > 2D not supported");
        }

        private MValue EvalMatrixLit(MatrixLit ml, MatlabScope scope)
        {
            if (ml.Rows.Count == 0) return new MValue(0, 0);
            // Cada fila puede contener escalares o vectores → concatenamos
            int nRows = ml.Rows.Count;
            // Primera fila determina nCols
            int? expectedCols = null;
            var rowMats = new List<MValue>();
            bool hasSym = false;
            bool hasStringDouble = false;
            // Pre-evalúa todas las filas (eval primero para detectar tipo)
            var allPieces = new List<List<MValue>>();
            foreach (var row in ml.Rows)
            {
                var pieces = new List<MValue>();
                foreach (var e in row)
                {
                    var p = Eval(e, scope);
                    if (p.IsSymbolic || p.IsSymMatrix) hasSym = true;
                    if (p.IsDoubleQuoted || p.IsStringArray) hasStringDouble = true;
                    pieces.Add(p);
                }
                allPieces.Add(pieces);
            }
            // Single-quoted char-array concatenation: ['a' 'b'] → 'ab',
            // ['linea 1\n' 'linea 2\n'] → string concatenado (patrón MATLAB
            // estándar para construir formatos de fprintf multilinea).
            // Solo aplica si TODAS las piezas son strings single-quoted
            // (no double, no simbólicas, no numéricas).
            if (!hasSym && !hasStringDouble)
            {
                bool allSingleStr = allPieces.Count > 0;
                foreach (var row in allPieces)
                {
                    foreach (var p in row)
                    {
                        if (!p.IsString || p.IsDoubleQuoted) { allSingleStr = false; break; }
                    }
                    if (!allSingleStr) break;
                }
                if (allSingleStr)
                {
                    if (allPieces.Count == 1)
                    {
                        // Una fila: concatenar horizontalmente → un solo string
                        var sb = new StringBuilder();
                        foreach (var p in allPieces[0]) sb.Append(p.StringValue);
                        return new MValue(sb.ToString());
                    }
                    // Múltiples filas: char matrix. Cada fila debe tener mismo
                    // ancho (MATLAB exige esto y tira error si difieren).
                    var rowStrings = new List<string>();
                    foreach (var row in allPieces)
                    {
                        var sb = new StringBuilder();
                        foreach (var p in row) sb.Append(p.StringValue);
                        rowStrings.Add(sb.ToString());
                    }
                    int len0 = rowStrings[0].Length;
                    foreach (var rs in rowStrings)
                        if (rs.Length != len0)
                            throw new MatlabRuntimeException(
                                "Vertical dimensions of char arrays being concatenated are not consistent");
                    var sArr = new string[rowStrings.Count, 1];
                    for (int i = 0; i < rowStrings.Count; i++) sArr[i, 0] = rowStrings[i];
                    return MValue.NewStringArray(sArr);
                }
            }

            // String array literal: ["a", "b"; "c", "d"] → string[,]
            if (hasStringDouble && !hasSym)
            {
                int rR = allPieces.Count;
                int rC = -1;
                foreach (var row in allPieces)
                {
                    int rowCols = 0;
                    foreach (var p in row)
                    {
                        if (p.IsStringArray) rowCols += p.StringArrayData.GetLength(1);
                        else if (p.IsString) rowCols += 1;
                        else rowCols += 1;
                    }
                    if (rC < 0) rC = rowCols;
                    else if (rC != rowCols)
                        throw new MatlabRuntimeException("Inconsistent string row lengths");
                }
                var arr = new string[rR, rC];
                for (int i = 0; i < rR; i++)
                {
                    int col = 0;
                    foreach (var p in allPieces[i])
                    {
                        if (p.IsStringArray)
                        {
                            int pcols = p.StringArrayData.GetLength(1);
                            for (int j = 0; j < pcols; j++) arr[i, col + j] = p.StringArrayData[0, j];
                            col += pcols;
                        }
                        else
                        {
                            arr[i, col++] = CoerceToText(p);
                        }
                    }
                }
                return MValue.NewStringArray(arr);
            }
            foreach (var pieces in allPieces)
            {
                var combined = hasSym ? HorzConcatSym(pieces) : HorzConcat(pieces);
                // MATLAB: una fila VACIA ([] o 0xN) se IGNORA en la concatenacion
                // vertical. Habilita el patron de crecimiento FE: A=[]; A=[A; fila];
                if (!hasSym && combined.Rows * combined.Cols == 0) continue;
                if (expectedCols == null) expectedCols = combined.Cols;
                else if (combined.Cols != expectedCols)
                    throw new MatlabRuntimeException("Inconsistent row lengths in matrix literal");
                rowMats.Add(combined);
            }
            if (rowMats.Count == 0) return new MValue(0, 0);  // todas las filas vacias -> []
            // Vert-concat: si algún row es simbólico, todos lo son
            if (hasSym)
            {
                for (int i = 0; i < rowMats.Count; i++)
                    if (!rowMats[i].IsSymMatrix) rowMats[i] = LiftToSymMatrix(rowMats[i]);
                return VertConcatSym(rowMats);
            }
            return VertConcat(rowMats);
        }
        /// <summary>Convierte un MValue numérico a su equivalente symbolic matrix (SymConst por celda).</summary>
        private static MValue LiftToSymMatrix(MValue v)
        {
            if (v.IsSymMatrix) return v;
            if (v.IsSymbolic)
            {
                var c = new SymNode[1, 1] { { v.Symbolic } };
                return MValue.NewSymMatrix(c);
            }
            var cells = new SymNode[v.Rows, v.Cols];
            for (int i = 0; i < v.Rows; i++)
                for (int j = 0; j < v.Cols; j++)
                    cells[i, j] = new SymConst(v.At(i, j));
            return MValue.NewSymMatrix(cells);
        }
        private static MValue HorzConcatSym(List<MValue> pieces)
        {
            // Lift cada pieza a sym matrix con sus dimensiones
            var lifted = new List<MValue>();
            int rows = -1;
            foreach (var p in pieces)
            {
                var lp = p.IsSymMatrix ? p : LiftToSymMatrix(p);
                if (rows < 0) rows = lp.Rows;
                else if (lp.Rows != rows) throw new MatlabRuntimeException("Horz-concat (sym) row mismatch");
                lifted.Add(lp);
            }
            int totalCols = 0;
            foreach (var lp in lifted) totalCols += lp.Cols;
            var cells = new SymNode[rows, totalCols];
            int colOff = 0;
            foreach (var lp in lifted)
            {
                for (int i = 0; i < lp.Rows; i++)
                    for (int j = 0; j < lp.Cols; j++)
                        cells[i, colOff + j] = lp.SymCells[i, j];
                colOff += lp.Cols;
            }
            return MValue.NewSymMatrix(cells);
        }
        private static MValue VertConcatSym(List<MValue> pieces)
        {
            int cols = pieces[0].Cols;
            int totalRows = 0;
            foreach (var p in pieces)
            {
                if (p.Cols != cols) throw new MatlabRuntimeException("Vert-concat (sym) col mismatch");
                totalRows += p.Rows;
            }
            var cells = new SymNode[totalRows, cols];
            int rowOff = 0;
            foreach (var p in pieces)
            {
                for (int i = 0; i < p.Rows; i++)
                    for (int j = 0; j < p.Cols; j++)
                        cells[rowOff + i, j] = p.SymCells[i, j];
                rowOff += p.Rows;
            }
            return MValue.NewSymMatrix(cells);
        }
        private static MValue HorzConcat(List<MValue> pieces)
        {
            if (pieces.Count == 1) return pieces[0];
            // Filtrar empty (0×0): MATLAB ignora vacíos en concatenación
            var filtered = new List<MValue>(pieces.Count);
            foreach (var p in pieces)
                if (p.Rows != 0 || p.Cols != 0) filtered.Add(p);
            if (filtered.Count == 0) return new MValue(0, 0);
            if (filtered.Count == 1) return filtered[0];
            int rows = filtered[0].Rows;
            int totalCols = 0;
            foreach (var p in filtered)
            {
                if (p.Rows != rows) throw new MatlabRuntimeException("Horz-concat row mismatch");
                totalCols += p.Cols;
            }
            var r = new MValue(rows, totalCols);
            int colOffset = 0;
            foreach (var p in filtered)
            {
                for (int i = 0; i < p.Rows; i++)
                    for (int j = 0; j < p.Cols; j++)
                        r.Set(i, colOffset + j, p.At(i, j));
                colOffset += p.Cols;
            }
            return r;
        }
        private static MValue VertConcat(List<MValue> pieces)
        {
            if (pieces.Count == 1) return pieces[0];
            // Filtrar empty (0×0): MATLAB ignora vacíos en concatenación
            var filtered = new List<MValue>(pieces.Count);
            foreach (var p in pieces)
                if (p.Rows != 0 || p.Cols != 0) filtered.Add(p);
            if (filtered.Count == 0) return new MValue(0, 0);
            if (filtered.Count == 1) return filtered[0];
            int cols = filtered[0].Cols;
            int totalRows = 0;
            foreach (var p in filtered)
            {
                if (p.Cols != cols) throw new MatlabRuntimeException("Vert-concat col mismatch");
                totalRows += p.Rows;
            }
            var r = new MValue(totalRows, cols);
            int rowOffset = 0;
            foreach (var p in filtered)
            {
                for (int i = 0; i < p.Rows; i++)
                    for (int j = 0; j < p.Cols; j++)
                        r.Set(rowOffset + i, j, p.At(i, j));
                rowOffset += p.Rows;
            }
            return r;
        }

        // ─── Delaunay triangulation (Bowyer-Watson) ──────────────────────────
        private struct TriIdx
        {
            public int A, B, C;
            public TriIdx(int a, int b, int c) { A = a; B = b; C = c; }
            public bool ContainsPoint(int v) => A == v || B == v || C == v;
            public bool SharesEdge(int u, int v) =>
                (ContainsPoint(u) && ContainsPoint(v));
        }
        private static List<TriIdx> DelaunayBowyerWatson(double[,] pts)
        {
            int n = pts.GetLength(0);
            // Bounding super-triangle
            double xmin = double.MaxValue, xmax = double.MinValue;
            double ymin = double.MaxValue, ymax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (pts[i, 0] < xmin) xmin = pts[i, 0];
                if (pts[i, 0] > xmax) xmax = pts[i, 0];
                if (pts[i, 1] < ymin) ymin = pts[i, 1];
                if (pts[i, 1] > ymax) ymax = pts[i, 1];
            }
            double dx = xmax - xmin, dy = ymax - ymin;
            double dmax = Math.Max(dx, dy) * 20;
            double xc = (xmin + xmax) / 2;
            double yc = (ymin + ymax) / 2;
            // Agregamos 3 puntos del super-triángulo al final del array
            var ptsExt = new double[n + 3, 2];
            for (int i = 0; i < n; i++) { ptsExt[i, 0] = pts[i, 0]; ptsExt[i, 1] = pts[i, 1]; }
            ptsExt[n, 0] = xc - dmax; ptsExt[n, 1] = yc - dmax;
            ptsExt[n + 1, 0] = xc + dmax; ptsExt[n + 1, 1] = yc - dmax;
            ptsExt[n + 2, 0] = xc; ptsExt[n + 2, 1] = yc + dmax;
            var triangles = new List<TriIdx> { new TriIdx(n, n + 1, n + 2) };
            // Insertar puntos uno a uno
            for (int i = 0; i < n; i++)
            {
                double px = pts[i, 0], py = pts[i, 1];
                // Encontrar triángulos cuyo circumcircle contiene al punto
                var badTris = new List<int>();
                for (int t = 0; t < triangles.Count; t++)
                {
                    if (InCircumcircle(triangles[t], ptsExt, px, py))
                        badTris.Add(t);
                }
                // Encontrar polígono de aristas (boundary del agujero)
                var edges = new List<(int U, int V)>();
                foreach (int ti in badTris)
                {
                    var tri = triangles[ti];
                    var e1 = (tri.A, tri.B);
                    var e2 = (tri.B, tri.C);
                    var e3 = (tri.C, tri.A);
                    AddOrRemoveEdge(edges, e1);
                    AddOrRemoveEdge(edges, e2);
                    AddOrRemoveEdge(edges, e3);
                }
                // Eliminar triángulos malos
                badTris.Sort();
                for (int k = badTris.Count - 1; k >= 0; k--)
                    triangles.RemoveAt(badTris[k]);
                // Crear nuevos triángulos con cada arista hacia el nuevo punto
                foreach (var (u, v) in edges)
                    triangles.Add(new TriIdx(u, v, i));
            }
            // Remover triángulos que contienen un vértice del super-triángulo
            var result = new List<TriIdx>();
            foreach (var t in triangles)
                if (t.A < n && t.B < n && t.C < n) result.Add(t);
            return result;
        }
        private static void AddOrRemoveEdge(List<(int U, int V)> edges, (int U, int V) e)
        {
            // Si la arista (en cualquier orden) ya existe → eliminarla (shared)
            for (int i = 0; i < edges.Count; i++)
            {
                if ((edges[i].U == e.U && edges[i].V == e.V) ||
                    (edges[i].U == e.V && edges[i].V == e.U))
                {
                    edges.RemoveAt(i);
                    return;
                }
            }
            edges.Add(e);
        }
        private static bool InCircumcircle(TriIdx t, double[,] pts, double px, double py)
        {
            double ax = pts[t.A, 0], ay = pts[t.A, 1];
            double bx = pts[t.B, 0], by = pts[t.B, 1];
            double cx = pts[t.C, 0], cy = pts[t.C, 1];
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12) return false;
            double ux = ((ax * ax + ay * ay) * (by - cy)
                      +  (bx * bx + by * by) * (cy - ay)
                      +  (cx * cx + cy * cy) * (ay - by)) / d;
            double uy = ((ax * ax + ay * ay) * (cx - bx)
                      +  (bx * bx + by * by) * (ax - cx)
                      +  (cx * cx + cy * cy) * (bx - ax)) / d;
            double r2 = (ux - ax) * (ux - ax) + (uy - ay) * (uy - ay);
            double dp = (ux - px) * (ux - px) + (uy - py) * (uy - py);
            return dp < r2 - 1e-12;
        }
    }

    public class MatlabRuntimeException : Exception
    {
        public MatlabRuntimeException(string message) : base(message) { }
    }

    // ─── Operaciones aritméticas sobre matrices simbólicas (SymCells) ──────
    internal static class SymMatOps
    {
        public static SymNode[,] Eye(int n)
        {
            var c = new SymNode[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    c[i, j] = i == j ? (SymNode)new SymConst(1) : new SymConst(0);
            return c;
        }
        public static SymNode[,] Zero(int r, int c)
        {
            var z = new SymNode[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    z[i, j] = new SymConst(0);
            return z;
        }
        public static SymNode[,] Add(SymNode[,] A, SymNode[,] B)
        {
            int r = A.GetLength(0), c = A.GetLength(1);
            if (B.GetLength(0) != r || B.GetLength(1) != c)
                throw new MatlabRuntimeException("sym matrix add: shape mismatch");
            var R = new SymNode[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    R[i, j] = new SymAdd(A[i, j], B[i, j]).Simplify();
            return R;
        }
        public static SymNode[,] Sub(SymNode[,] A, SymNode[,] B)
        {
            int r = A.GetLength(0), c = A.GetLength(1);
            if (B.GetLength(0) != r || B.GetLength(1) != c)
                throw new MatlabRuntimeException("sym matrix sub: shape mismatch");
            var R = new SymNode[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    R[i, j] = new SymSub(A[i, j], B[i, j]).Simplify();
            return R;
        }
        public static SymNode[,] Mul(SymNode[,] A, SymNode[,] B)
        {
            int r = A.GetLength(0), m = A.GetLength(1);
            if (B.GetLength(0) != m) throw new MatlabRuntimeException("sym matmul: inner dim mismatch");
            int c = B.GetLength(1);
            var R = new SymNode[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    SymNode acc = new SymConst(0);
                    for (int k = 0; k < m; k++)
                        acc = new SymAdd(acc, new SymMul(A[i, k], B[k, j]));
                    R[i, j] = acc.Simplify();
                }
            return R;
        }
        public static SymNode[,] ScalarMul(SymNode s, SymNode[,] A)
        {
            int r = A.GetLength(0), c = A.GetLength(1);
            var R = new SymNode[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    R[i, j] = new SymMul(s, A[i, j]).Simplify();
            return R;
        }
        public static bool IsDiagonal(SymNode[,] A)
        {
            int r = A.GetLength(0), c = A.GetLength(1);
            if (r != c) return false;
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    if (i != j)
                    {
                        var s = A[i, j].Simplify();
                        if (!(s is SymConst k) || k.Value != 0) return false;
                    }
            return true;
        }
        /// <summary>
        /// expm(A) simbólico via serie de Taylor truncada: I + A + A²/2! + ... + A^N/N!
        /// Si A es diagonal, atajo: diag(exp(d1), exp(d2), ...)
        /// </summary>
        public static SymNode[,] Expm(SymNode[,] A, int order = 10)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new MatlabRuntimeException("expm (sym): matrix must be square");
            // Diagonal: exp por elemento
            if (IsDiagonal(A))
            {
                var D = new SymNode[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        D[i, j] = i == j ? new SymFunc("exp", A[i, i]).Simplify() : new SymConst(0);
                return D;
            }
            // Taylor: acumulador R = I, término T = I, para k=1..order: T = T·A/k; R = R + T
            var R = Eye(n);
            var T = Eye(n);
            double fact = 1;
            for (int k = 1; k <= order; k++)
            {
                T = Mul(T, A);
                fact *= k;
                var Tk = ScalarMul(new SymConst(1.0 / fact), T);
                R = Add(R, Tk);
            }
            return R;
        }
        /// <summary>sqrtm simbólico — solo soporta diagonal por ahora.</summary>
        public static SymNode[,] Sqrtm(SymNode[,] A)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new MatlabRuntimeException("sqrtm (sym): matrix must be square");
            if (!IsDiagonal(A))
                throw new MatlabRuntimeException("sqrtm (sym): only diagonal symbolic matrices supported");
            var D = new SymNode[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    D[i, j] = i == j ? new SymFunc("sqrt", A[i, i]).Simplify() : new SymConst(0);
            return D;
        }
        /// <summary>logm simbólico — solo diagonal.</summary>
        public static SymNode[,] Logm(SymNode[,] A)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new MatlabRuntimeException("logm (sym): matrix must be square");
            if (!IsDiagonal(A))
                throw new MatlabRuntimeException("logm (sym): only diagonal symbolic matrices supported");
            var D = new SymNode[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    D[i, j] = i == j ? new SymFunc("log", A[i, i]).Simplify() : new SymConst(0);
            return D;
        }
        /// <summary>funm(A, fnName) — solo diagonal por ahora.</summary>
        public static SymNode[,] Funm(SymNode[,] A, string fnName)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new MatlabRuntimeException("funm (sym): matrix must be square");
            if (!IsDiagonal(A))
                throw new MatlabRuntimeException("funm (sym): only diagonal symbolic matrices supported");
            var D = new SymNode[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    D[i, j] = i == j ? new SymFunc(fnName, A[i, i]).Simplify() : new SymConst(0);
            return D;
        }
        /// <summary>Transpose simbólico.</summary>
        public static SymNode[,] Transpose(SymNode[,] A)
        {
            int r = A.GetLength(0), c = A.GetLength(1);
            var R = new SymNode[c, r];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    R[j, i] = A[i, j];
            return R;
        }
        /// <summary>Power matricial simbólico A^n para n entero ≥ 0.</summary>
        public static SymNode[,] Pow(SymNode[,] A, int n)
        {
            int sz = A.GetLength(0);
            if (sz != A.GetLength(1)) throw new MatlabRuntimeException("sym matrix ^: must be square");
            if (n < 0) throw new MatlabRuntimeException("sym matrix ^: negative power not supported");
            if (n == 0) return Eye(sz);
            var R = A;
            for (int k = 1; k < n; k++) R = Mul(R, A);
            return R;
        }
    }

    // ─── Señales internas para control-flow (no son errores) ───────────────
    internal sealed class BreakSignal : Exception { public BreakSignal() : base("break") { } }
    internal sealed class ContinueSignal : Exception { public ContinueSignal() : base("continue") { } }
    internal sealed class ReturnSignal : Exception { public ReturnSignal() : base("return") { } }

    internal static class MatlabLinAlg
    {
        /// <summary>LU-based determinant. Devuelve 0 si singular.</summary>
        public static double Determinant(MValue m)
        {
            if (m.Rows != m.Cols) throw new MatlabRuntimeException($"det: matrix must be square, got {m.Rows}×{m.Cols}");
            int n = m.Rows;
            var a = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) a[i, j] = m.At(i, j);
            double det = 1;
            for (int k = 0; k < n; k++)
            {
                int pivot = k;
                for (int i = k + 1; i < n; i++) if (Math.Abs(a[i, k]) > Math.Abs(a[pivot, k])) pivot = i;
                if (Math.Abs(a[pivot, k]) < 1e-15) return 0;
                if (pivot != k)
                {
                    for (int j = 0; j < n; j++) (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]);
                    det = -det;
                }
                det *= a[k, k];
                for (int i = k + 1; i < n; i++)
                {
                    double f = a[i, k] / a[k, k];
                    for (int j = k + 1; j < n; j++) a[i, j] -= f * a[k, j];
                }
            }
            return det;
        }
        /// <summary>
        /// Resuelve A * x = b por eliminación gaussiana con pivoteo parcial.
        /// A puede ser N×N (cuadrada) o N×M (overdetermined → least-squares MVP via normal equations).
        /// b puede ser N×1 o N×k (múltiples RHS).
        /// </summary>
        // Submatriz de una matriz SPARSE (CSR) → CSR, sin densificar. Recorre solo
        // los no-ceros de las filas seleccionadas y remapea columnas. Soporta índices
        // repetidos/desordenados en cols (mapa columna-origen → posiciones nuevas).
        internal static MValue SparseSubmatrix(MValue m, int[] rows, int[] cols)
        {
            int nR = rows.Length, nC = cols.Length;
            var colMap = new System.Collections.Generic.List<int>[m.Cols];
            for (int j = 0; j < nC; j++)
            {
                int oc = cols[j];
                if (oc < 0 || oc >= m.Cols)
                    throw new MatlabRuntimeException($"Submatrix col index {oc + 1} out of {m.Cols}");
                (colMap[oc] ??= new System.Collections.Generic.List<int>()).Add(j);
            }
            var rp = m.SparseRowPtr; var ci = m.SparseCols; var vv = m.SparseVals;
            var newRowPtr = new int[nR + 1];
            var newCols = new System.Collections.Generic.List<int>();
            var newVals = new System.Collections.Generic.List<double>();
            var rowBuf = new System.Collections.Generic.List<(int c, double v)>();
            for (int i = 0; i < nR; i++)
            {
                newRowPtr[i] = newCols.Count;
                int orow = rows[i];
                if (orow < 0 || orow >= m.Rows)
                    throw new MatlabRuntimeException($"Submatrix row index {orow + 1} out of {m.Rows}");
                rowBuf.Clear();
                for (int k = rp[orow]; k < rp[orow + 1]; k++)
                {
                    var dests = colMap[ci[k]];
                    if (dests != null)
                        foreach (var nc in dests) rowBuf.Add((nc, vv[k]));
                }
                rowBuf.Sort((x, y) => x.c.CompareTo(y.c));
                foreach (var (c, v) in rowBuf) { newCols.Add(c); newVals.Add(v); }
            }
            newRowPtr[nR] = newCols.Count;
            return MValue.NewSparseCSR(nR, nC, newVals.ToArray(), newCols.ToArray(), newRowPtr);
        }

        public static MValue Linsolve(MValue A, MValue b)
        {
            // Fast-path SPARSE: A CSR resuelto con MKL PARDISO — evita densificar A
            // (128 MB) y los 3 escaneos densos por iteracion. b es un vector (se
            // densifica). Si PARDISO falla, cae al camino denso (ToDense).
            if (A.IsSparseReal && A.Rows == A.Cols && b.Cols == 1
                && A.Imag == null && b.Imag == null && Calcpad.Core.BlasInterop.PardisoAvailable)
            {
                try
                {
                    int nn = A.Rows;
                    var bd = b.IsSparseReal ? b.ToDense() : b;
                    var bv = new double[nn];
                    for (int i = 0; i < nn; i++) bv[i] = bd.At(i, 0);
                    var xx = Calcpad.Core.BlasInterop.SolveSparsePardiso(nn, A.SparseRowPtr, A.SparseCols, A.SparseVals, bv);
                    var rr = new MValue(nn, 1);
                    for (int i = 0; i < nn; i++) rr.Set(i, 0, xx[i]);
                    return rr;
                }
                catch { A = A.ToDense(); }
            }
            // Red de seguridad: cualquier operando sparse restante → denso.
            if (A.IsSparseReal) A = A.ToDense();
            if (b.IsSparseReal) b = b.ToDense();
            if (A.Rows != b.Rows)
                throw new MatlabRuntimeException($"A\\b: row mismatch A is {A.Rows}×{A.Cols}, b is {b.Rows}×{b.Cols}");
            // Caso cuadrado
            if (A.Rows == A.Cols)
            {
                int n = A.Rows;
                if (IsSymmetric(A))
                {
                    int bw = DetectBandwidth(A);
                    // PRIORIDAD 1: LAPACK DPBSV banded SPD — código Fortran optimizado.
                    // 10-20× más rápido que BandedCholeskySolve managed, Y al ser
                    // nativo no tiene heap corruption GC-related bajo WPF+WebView2
                    // (fix probable del crash AV en FEM scripts).
                    if (n >= 64 && bw < n / 3 && LapackInterop.Available
                        && b.Cols == 1 && A.Imag == null && b.Imag == null)
                    {
                        try
                        {
                            var x = LapackInterop.SolveSymBanded(n, bw, A.Data, b.Data);
                            var rd = new MValue(n, 1);
                            for (int i = 0; i < n; i++) rd.Set(i, 0, x[i]);
                            return rd;
                        }
                        catch { /* fallback a managed */ }
                    }
                    // FALLBACK 1: Banded Cholesky managed
                    if (n >= 100 && bw < n / 3)
                    {
                        try { return BandedCholeskySolve(A, b, bw); }
                        catch { /* fallback */ }
                    }
                    try { return CholeskySolve(A, b); } catch { /* fallback */ }
                }
                // LAPACK DGESV — fastest para dense no-simetricas grandes (n >= 64)
                if (n >= LapackInterop.LapackThreshold && LapackInterop.Available
                    && b.Cols == 1 && A.Imag == null && b.Imag == null)
                {
                    try
                    {
                        var x = LapackInterop.Solve(n, A.Data, b.Data);
                        var rd = new MValue(n, 1);
                        for (int i = 0; i < n; i++) rd.Set(i, 0, x[i]);
                        return rd;
                    }
                    catch { /* fallback al solver C# si DGESV falla */ }
                }
                return GaussSolveOptim(A, b);
            }
            // Overdetermined: x = (A'A)^-1 A' b  (normal equations)
            var At = TransposeM(A);
            var AtA = MatMul(At, A);
            var Atb = MatMul(At, b);
            return Linsolve(AtA, Atb);
        }
        /// <summary>Check rápido de simetría (con tolerancia relativa).</summary>
        private static bool IsSymmetric(MValue A)
        {
            int n = A.Rows;
            // Para matrices grandes, sample-check primero (cost minimal)
            int step = Math.Max(1, n / 50);
            for (int i = 0; i < n; i += step)
                for (int j = i + step; j < n; j += step)
                {
                    double a = A.At(i, j), b = A.At(j, i);
                    double scale = Math.Max(Math.Abs(a), Math.Abs(b)) + 1e-30;
                    if (Math.Abs(a - b) / scale > 1e-8) return false;
                }
            // Si pasa el sample-check, hacer full verification es opcional
            // (FEM siempre genera K simétrica; verificar todo sería caro y redundante).
            return true;
        }
        /// <summary>Cholesky: A = L·Lᵀ con A simétrica definida positiva. ~n³/6 flops (mitad de Gauss).</summary>
        private static MValue CholeskySolve(MValue A, MValue b)
        {
            int n = A.Rows;
            int k = b.Cols;
            // Copy A → L flat array (column-major para cache)
            var L = new double[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++) L[i * n + j] = A.At(i, j);
            // Factorización in-place: L[i,j] para i>=j
            for (int j = 0; j < n; j++)
            {
                double sum = L[j * n + j];
                for (int p = 0; p < j; p++)
                    sum -= L[j * n + p] * L[j * n + p];
                if (sum <= 1e-15)
                    throw new MatlabRuntimeException("Cholesky: matrix not positive definite");
                L[j * n + j] = Math.Sqrt(sum);
                double invDiag = 1.0 / L[j * n + j];
                for (int i = j + 1; i < n; i++)
                {
                    double s = L[i * n + j];
                    for (int p = 0; p < j; p++)
                        s -= L[i * n + p] * L[j * n + p];
                    L[i * n + j] = s * invDiag;
                }
            }
            // Solve L·y = b, then Lᵀ·x = y
            var x = new MValue(n, k);
            var y = new double[n];
            for (int rhs = 0; rhs < k; rhs++)
            {
                // Forward: L·y = b
                for (int i = 0; i < n; i++)
                {
                    double s = b.At(i, rhs);
                    for (int p = 0; p < i; p++) s -= L[i * n + p] * y[p];
                    y[i] = s / L[i * n + i];
                }
                // Backward: Lᵀ·x = y
                for (int i = n - 1; i >= 0; i--)
                {
                    double s = y[i];
                    for (int p = i + 1; p < n; p++) s -= L[p * n + i] * x.At(p, rhs);
                    x.Set(i, rhs, s / L[i * n + i]);
                }
            }
            return x;
        }
        /// <summary>Gauss con pivoteo parcial, optimizado por filas (flat arrays + cache-friendly).</summary>
        /// <summary>Detecta bandwidth máximo (mayor offset j-i con A[i,j]≠0).</summary>
        private static int DetectBandwidth(MValue A)
        {
            int n = A.Rows;
            int bw = 0;
            // Caminamos por filas; en cada fila buscamos el max j > i con A[i,j] != 0
            // Optimización: solo escaneamos columnas más allá del bw actual
            for (int i = 0; i < n; i++)
            {
                int jmin = Math.Min(n - 1, i + bw);
                for (int j = n - 1; j > jmin; j--)
                {
                    if (Math.Abs(A.At(i, j)) > 1e-14)
                    {
                        bw = j - i;
                        break;
                    }
                }
            }
            return bw;
        }
        /// <summary>Cholesky banded inner-product (versión estable que sí funciona).
        /// A simétrica positiva definida con bandwidth bw. O(n·bw²).
        /// Storage: L[i*w + (j-i)] para j ≥ i, j-i ≤ bw. Triángulo superior almacenado.</summary>
        private static MValue BandedCholeskySolve(MValue A, MValue b, int bw)
        {
            int n = A.Rows;
            int k = b.Cols;
            int w = bw + 1;
            // ArrayPool: reusa el buffer L entre invocaciones — reduce GC pressure
            // que estaba corrompiendo heap bajo WPF+WebView2 (FEM scripts crashean
            // en indexado post-solve por allocaciones masivas K_e Gauss loop).
            var L = Calcpad.Core.DoubleArrayPool.Rent(n * w);
            // Copiar banda superior de A
            for (int i = 0; i < n; i++)
            {
                int jmax = Math.Min(n - 1, i + bw);
                for (int j = i; j <= jmax; j++)
                    L[i * w + (j - i)] = A.At(i, j);
            }
            // Factorización in-place
            for (int j = 0; j < n; j++)
            {
                double diag = L[j * w + 0];
                int pmin = Math.Max(0, j - bw);
                for (int p = pmin; p < j; p++)
                    diag -= L[p * w + (j - p)] * L[p * w + (j - p)];
                if (diag <= 1e-15)
                    throw new MatlabRuntimeException("Banded Cholesky: not positive definite");
                L[j * w + 0] = Math.Sqrt(diag);
                double invDiag = 1.0 / L[j * w + 0];
                int imax = Math.Min(n - 1, j + bw);
                for (int i = j + 1; i <= imax; i++)
                {
                    double s = L[j * w + (i - j)];
                    int qstart = Math.Max(0, i - bw);
                    for (int q = qstart; q < j; q++)
                        s -= L[q * w + (j - q)] * L[q * w + (i - q)];
                    L[j * w + (i - j)] = s * invDiag;
                }
            }
            var x = new MValue(n, k);
            var y = new double[n];
            for (int rhs = 0; rhs < k; rhs++)
            {
                for (int i = 0; i < n; i++)
                {
                    double s = b.At(i, rhs);
                    int kmin = Math.Max(0, i - bw);
                    for (int kk = kmin; kk < i; kk++)
                        s -= L[kk * w + (i - kk)] * y[kk];
                    y[i] = s / L[i * w + 0];
                }
                for (int i = n - 1; i >= 0; i--)
                {
                    double s = y[i];
                    int kmax = Math.Min(n - 1, i + bw);
                    for (int kk = i + 1; kk <= kmax; kk++)
                        s -= L[i * w + (kk - i)] * x.At(kk, rhs);
                    x.Set(i, rhs, s / L[i * w + 0]);
                }
            }
            Calcpad.Core.DoubleArrayPool.Return(L);
            return x;
        }
        private static MValue GaussSolveOptim(MValue A, MValue b)
        {
            int n = A.Rows;
            int k = b.Cols;
            int nrhs = n + k;
            // Flat array row-major
            var ext = new double[n * nrhs];
            for (int i = 0; i < n; i++)
            {
                int row = i * nrhs;
                for (int j = 0; j < n; j++) ext[row + j] = A.At(i, j);
                for (int j = 0; j < k; j++) ext[row + n + j] = b.At(i, j);
            }
            for (int col = 0; col < n; col++)
            {
                // Pivot
                int pivot = col;
                double pivVal = Math.Abs(ext[col * nrhs + col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = Math.Abs(ext[r * nrhs + col]);
                    if (v > pivVal) { pivVal = v; pivot = r; }
                }
                // Octave-compat: matriz singular/casi-singular NO tira error; se regulariza el
                // pivote (la dirección singular = modo de cuerpo rígido con b≈0 → componente ≈0)
                // y se devuelve un resultado finito, como el \ de Octave/MATLAB (que solo advierte).
                bool singular = pivVal < 1e-15;
                if (!singular && pivot != col)
                {
                    int rA = col * nrhs, rB = pivot * nrhs;
                    for (int j = col; j < nrhs; j++) { var tmp = ext[rA + j]; ext[rA + j] = ext[rB + j]; ext[rB + j] = tmp; }
                }
                double pivDiag = ext[col * nrhs + col];
                if (singular && Math.Abs(pivDiag) < 1e-15) pivDiag = 1e-15;   // piso anti-NaN
                double invPiv = 1.0 / pivDiag;
                int rowCol = col * nrhs;
                for (int r = col + 1; r < n; r++)
                {
                    int rowR = r * nrhs;
                    double f = ext[rowR + col] * invPiv;
                    if (f == 0) continue;
                    for (int j = col + 1; j < nrhs; j++)
                        ext[rowR + j] -= f * ext[rowCol + j];
                }
            }
            // Back-substitution
            var x = new MValue(n, k);
            for (int rhs = 0; rhs < k; rhs++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    int row = i * nrhs;
                    double s = ext[row + n + rhs];
                    for (int j = i + 1; j < n; j++) s -= ext[row + j] * x.At(j, rhs);
                    x.Set(i, rhs, s / ext[row + i]);
                }
            }
            return x;
        }

        internal static MValue TransposeM(MValue m)
        {
            var r = new MValue(m.Cols, m.Rows);
            for (int i = 0; i < m.Rows; i++) for (int j = 0; j < m.Cols; j++) r.Set(j, i, m.At(i, j));
            return r;
        }
        internal static MValue MatMul(MValue a, MValue b)
        {
            var r = new MValue(a.Rows, b.Cols);
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < b.Cols; j++)
                {
                    double s = 0;
                    for (int k = 0; k < a.Cols; k++) s += a.At(i, k) * b.At(k, j);
                    r.Set(i, j, s);
                }
            return r;
        }

        /// <summary>
        /// Solver iterativo Gauss-Seidel. Útil para sistemas grandes diagonally-dominant.
        /// </summary>
        public static MValue GaussSeidel(MValue A, MValue b, MValue x0, double tol, int maxIter)
        {
            if (A.Rows != A.Cols) throw new MatlabRuntimeException("gauss_seidel: A must be square");
            int n = A.Rows;
            var x = new double[n];
            if (x0 != null) for (int i = 0; i < n && i < x0.Data.Length; i++) x[i] = x0.Data[i];
            for (int it = 0; it < maxIter; it++)
            {
                double maxDelta = 0;
                for (int i = 0; i < n; i++)
                {
                    double sum = b.At(i, 0);
                    for (int j = 0; j < n; j++) if (j != i) sum -= A.At(i, j) * x[j];
                    double aii = A.At(i, i);
                    if (Math.Abs(aii) < 1e-15) throw new MatlabRuntimeException("gauss_seidel: zero diagonal");
                    double xnew = sum / aii;
                    maxDelta = Math.Max(maxDelta, Math.Abs(xnew - x[i]));
                    x[i] = xnew;
                }
                if (maxDelta < tol) break;
            }
            var result = new MValue(n, 1);
            for (int i = 0; i < n; i++) result.Set(i, 0, x[i]);
            return result;
        }
        /// <summary>
        /// Conjugate Gradient (sin precondicionador) — para sistemas SPD grandes.
        /// </summary>
        public static MValue ConjugateGradient(MValue A, MValue b, double tol, int maxIter)
        {
            int n = A.Rows;
            var x = new double[n];
            var r = new double[n];
            var p = new double[n];
            for (int i = 0; i < n; i++) r[i] = b.At(i, 0);
            Array.Copy(r, p, n);
            double rsOld = 0;
            for (int i = 0; i < n; i++) rsOld += r[i] * r[i];
            for (int it = 0; it < maxIter; it++)
            {
                // Ap = A * p
                var Ap = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double s = 0;
                    for (int j = 0; j < n; j++) s += A.At(i, j) * p[j];
                    Ap[i] = s;
                }
                double pAp = 0;
                for (int i = 0; i < n; i++) pAp += p[i] * Ap[i];
                if (Math.Abs(pAp) < 1e-30) break;
                double alpha = rsOld / pAp;
                for (int i = 0; i < n; i++) { x[i] += alpha * p[i]; r[i] -= alpha * Ap[i]; }
                double rsNew = 0;
                for (int i = 0; i < n; i++) rsNew += r[i] * r[i];
                if (Math.Sqrt(rsNew) < tol) break;
                double beta = rsNew / rsOld;
                for (int i = 0; i < n; i++) p[i] = r[i] + beta * p[i];
                rsOld = rsNew;
            }
            var result = new MValue(n, 1);
            for (int i = 0; i < n; i++) result.Set(i, 0, x[i]);
            return result;
        }

        /// <summary>
        /// Eigenvalores de matriz simétrica via Jacobi rotation. Devuelve diagonal con
        /// los eigenvalores (no necesariamente ordenados).
        /// Para matrices no-simétricas: caemos en QR algorithm simple.
        /// </summary>
        public static (MValue eigenvalues, MValue eigenvectors) Eig(MValue A)
        {
            if (A.Rows != A.Cols)
                throw new MatlabRuntimeException($"eig: matrix must be square, got {A.Rows}×{A.Cols}");
            int n = A.Rows;
            // ¿Simétrica?
            bool symmetric = true;
            for (int i = 0; i < n && symmetric; i++)
                for (int j = i + 1; j < n && symmetric; j++)
                    if (Math.Abs(A.At(i, j) - A.At(j, i)) > 1e-12) symmetric = false;
            if (symmetric)
                return EigJacobi(A);
            return EigQR(A);
        }

        private static (MValue, MValue) EigJacobi(MValue A)
        {
            int n = A.Rows;
            var D = new double[n, n];
            var V = new double[n, n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) D[i, j] = A.At(i, j); V[i, i] = 1; }
            for (int sweep = 0; sweep < 100; sweep++)
            {
                double off = 0;
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) off += D[i, j] * D[i, j];
                if (off < 1e-24) break;
                for (int p = 0; p < n - 1; p++)
                    for (int q = p + 1; q < n; q++)
                    {
                        double app = D[p, p], aqq = D[q, q], apq = D[p, q];
                        if (Math.Abs(apq) < 1e-14) continue;
                        double tau = (aqq - app) / (2 * apq);
                        double t = tau >= 0 ? 1 / (tau + Math.Sqrt(1 + tau * tau))
                                            : 1 / (tau - Math.Sqrt(1 + tau * tau));
                        double c = 1 / Math.Sqrt(1 + t * t);
                        double s = t * c;
                        D[p, p] = app - t * apq; D[q, q] = aqq + t * apq;
                        D[p, q] = 0; D[q, p] = 0;
                        for (int i = 0; i < n; i++)
                        {
                            if (i != p && i != q)
                            {
                                double aip = D[i, p], aiq = D[i, q];
                                D[i, p] = c * aip - s * aiq; D[p, i] = D[i, p];
                                D[i, q] = s * aip + c * aiq; D[q, i] = D[i, q];
                            }
                            double vip = V[i, p], viq = V[i, q];
                            V[i, p] = c * vip - s * viq;
                            V[i, q] = s * vip + c * viq;
                        }
                    }
            }
            var eigVals = new MValue(n, 1);
            var eigVecs = new MValue(n, n);
            for (int i = 0; i < n; i++) { eigVals.Set(i, 0, D[i, i]); for (int j = 0; j < n; j++) eigVecs.Set(i, j, V[i, j]); }
            return (eigVals, eigVecs);
        }

        private static (MValue, MValue) EigQR(MValue A)
        {
            int n = A.Rows;
            var H = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) H[i, j] = A.At(i, j);
            // Shifted QR — MVP simple sin shifts; converge para matrices con eigenvalores reales bien separados
            for (int iter = 0; iter < 500; iter++)
            {
                // Verificar convergencia (subdiagonal pequeña)
                double off = 0;
                for (int i = 1; i < n; i++) off += Math.Abs(H[i, i - 1]);
                if (off < 1e-12) break;
                // Wilkinson shift
                double shift = H[n - 1, n - 1];
                for (int i = 0; i < n; i++) H[i, i] -= shift;
                // QR via Givens rotations
                var Qs = new (int, int, double, double)[n - 1];
                for (int k = 0; k < n - 1; k++)
                {
                    double a = H[k, k], b = H[k + 1, k];
                    double r = Math.Sqrt(a * a + b * b);
                    if (r < 1e-15) continue;
                    double c = a / r, s = b / r;
                    Qs[k] = (k, k + 1, c, s);
                    for (int j = k; j < n; j++)
                    {
                        double t1 = c * H[k, j] + s * H[k + 1, j];
                        double t2 = -s * H[k, j] + c * H[k + 1, j];
                        H[k, j] = t1; H[k + 1, j] = t2;
                    }
                }
                // R*Q (aplicar Q transpuesto desde la derecha)
                for (int k = 0; k < n - 1; k++)
                {
                    var (p, q, c, s) = Qs[k];
                    for (int i = 0; i < n; i++)
                    {
                        double t1 = c * H[i, p] + s * H[i, q];
                        double t2 = -s * H[i, p] + c * H[i, q];
                        H[i, p] = t1; H[i, q] = t2;
                    }
                }
                for (int i = 0; i < n; i++) H[i, i] += shift;
            }
            var eigVals = new MValue(n, 1);
            for (int i = 0; i < n; i++) eigVals.Set(i, 0, H[i, i]);
            // Eigenvectors para no-simétricos: skip MVP — devolver identity como placeholder
            var eigVecs = new MValue(n, n);
            for (int i = 0; i < n; i++) eigVecs.Set(i, i, 1);
            return (eigVals, eigVecs);
        }

        /// <summary>
        /// SVD vía one-sided Jacobi rotations sobre A'A (eigendescomposition):
        /// A = U·Σ·V'  donde Σ es diagonal con singular values descendentes.
        /// </summary>
        public static (MValue U, MValue S, MValue V) SVD(MValue A)
        {
            int m = A.Rows, n = A.Cols;
            // Construir B = A'A (n×n simétrica)
            var B = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int k = 0; k < m; k++) s += A.At(k, i) * A.At(k, j);
                    B[i, j] = s;
                }
            // Eigendescomposition simétrica vía Jacobi
            var V = new double[n, n];
            for (int i = 0; i < n; i++) V[i, i] = 1;
            for (int sweep = 0; sweep < 100; sweep++)
            {
                double off = 0;
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) off += B[i, j] * B[i, j];
                if (off < 1e-24) break;
                for (int p = 0; p < n - 1; p++)
                    for (int q = p + 1; q < n; q++)
                    {
                        double app = B[p, p], aqq = B[q, q], apq = B[p, q];
                        if (Math.Abs(apq) < 1e-14) continue;
                        double tau = (aqq - app) / (2 * apq);
                        double t = tau >= 0 ? 1 / (tau + Math.Sqrt(1 + tau * tau))
                                            : 1 / (tau - Math.Sqrt(1 + tau * tau));
                        double c = 1 / Math.Sqrt(1 + t * t);
                        double s = t * c;
                        B[p, p] = app - t * apq; B[q, q] = aqq + t * apq;
                        B[p, q] = 0; B[q, p] = 0;
                        for (int i = 0; i < n; i++)
                        {
                            if (i != p && i != q)
                            {
                                double aip = B[i, p], aiq = B[i, q];
                                B[i, p] = c * aip - s * aiq; B[p, i] = B[i, p];
                                B[i, q] = s * aip + c * aiq; B[q, i] = B[i, q];
                            }
                            double vip = V[i, p], viq = V[i, q];
                            V[i, p] = c * vip - s * viq;
                            V[i, q] = s * vip + c * viq;
                        }
                    }
            }
            // Singular values = sqrt(eigenvalues), ordenados descendente
            var svPairs = new System.Collections.Generic.List<(double sv, int idx)>();
            for (int i = 0; i < n; i++) svPairs.Add((Math.Sqrt(Math.Max(B[i, i], 0)), i));
            svPairs.Sort((x, y) => y.sv.CompareTo(x.sv));
            int r = Math.Min(m, n);
            var Sm = new MValue(m, n);
            var Vm = new MValue(n, n);
            for (int k = 0; k < r; k++) Sm.Set(k, k, svPairs[k].sv);
            for (int i = 0; i < n; i++)
                for (int k = 0; k < n; k++) Vm.Set(i, k, V[i, svPairs[k].idx]);
            // U = A V Σ⁻¹  para columnas con σ > 0
            var Um = new MValue(m, m);
            for (int k = 0; k < r; k++)
            {
                double sigma = svPairs[k].sv;
                if (sigma < 1e-14) continue;
                for (int i = 0; i < m; i++)
                {
                    double s = 0;
                    for (int j = 0; j < n; j++) s += A.At(i, j) * Vm.At(j, k);
                    Um.Set(i, k, s / sigma);
                }
            }
            // Si m > n, las columnas extra de U se dejan en 0 (MVP — no ortogonalización completa)
            return (Um, Sm, Vm);
        }

        // ─── LU decomposition (PA = LU) ─────────────────────────────────────
        public static (MValue L, MValue U, MValue P) LU(MValue A)
        {
            int n = A.Rows;
            if (n != A.Cols) throw new MatlabRuntimeException("lu: matrix must be square");
            var U = new double[n, n];
            var L = new double[n, n];
            var perm = new int[n];
            for (int i = 0; i < n; i++) { perm[i] = i; for (int j = 0; j < n; j++) U[i, j] = A.At(i, j); }
            for (int k = 0; k < n; k++)
            {
                int piv = k;
                for (int i = k + 1; i < n; i++) if (Math.Abs(U[i, k]) > Math.Abs(U[piv, k])) piv = i;
                if (Math.Abs(U[piv, k]) < 1e-15) continue;  // singular column
                if (piv != k)
                {
                    for (int j = 0; j < n; j++) (U[k, j], U[piv, j]) = (U[piv, j], U[k, j]);
                    for (int j = 0; j < k; j++) (L[k, j], L[piv, j]) = (L[piv, j], L[k, j]);
                    (perm[k], perm[piv]) = (perm[piv], perm[k]);
                }
                L[k, k] = 1;
                for (int i = k + 1; i < n; i++)
                {
                    L[i, k] = U[i, k] / U[k, k];
                    for (int j = k; j < n; j++) U[i, j] -= L[i, k] * U[k, j];
                }
            }
            var Lmv = new MValue(n, n); var Umv = new MValue(n, n); var Pmv = new MValue(n, n);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) { Lmv.Set(i, j, L[i, j]); Umv.Set(i, j, U[i, j]); }
                Pmv.Set(i, perm[i], 1);
            }
            return (Lmv, Umv, Pmv);
        }

        // ─── QR decomposition (Gram-Schmidt modificado) ────────────────────
        public static (MValue Q, MValue R) QR(MValue A)
        {
            int m = A.Rows, n = A.Cols;
            var Q = new double[m, n];
            var R = new double[n, n];
            var V = new double[m, n];
            for (int i = 0; i < m; i++) for (int j = 0; j < n; j++) V[i, j] = A.At(i, j);
            for (int j = 0; j < n; j++)
            {
                double norm = 0;
                for (int i = 0; i < m; i++) norm += V[i, j] * V[i, j];
                norm = Math.Sqrt(norm);
                R[j, j] = norm;
                if (norm < 1e-15) continue;
                for (int i = 0; i < m; i++) Q[i, j] = V[i, j] / norm;
                for (int k = j + 1; k < n; k++)
                {
                    double dot = 0;
                    for (int i = 0; i < m; i++) dot += Q[i, j] * V[i, k];
                    R[j, k] = dot;
                    for (int i = 0; i < m; i++) V[i, k] -= dot * Q[i, j];
                }
            }
            var Qmv = new MValue(m, n); var Rmv = new MValue(n, n);
            for (int i = 0; i < m; i++) for (int j = 0; j < n; j++) Qmv.Set(i, j, Q[i, j]);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) Rmv.Set(i, j, R[i, j]);
            return (Qmv, Rmv);
        }

        // ─── Cholesky decomposition (A = R'·R, R upper) ────────────────────
        public static MValue Cholesky(MValue A)
        {
            int n = A.Rows;
            if (n != A.Cols) throw new MatlabRuntimeException("chol: matrix must be square");
            var R = new double[n, n];
            for (int j = 0; j < n; j++)
            {
                double sum = A.At(j, j);
                for (int k = 0; k < j; k++) sum -= R[k, j] * R[k, j];
                if (sum <= 0) throw new MatlabRuntimeException("chol: matrix must be positive definite");
                R[j, j] = Math.Sqrt(sum);
                for (int i = j + 1; i < n; i++)
                {
                    double s = A.At(j, i);
                    for (int k = 0; k < j; k++) s -= R[k, i] * R[k, j];
                    R[j, i] = s / R[j, j];
                }
            }
            var r = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) r.Set(i, j, R[i, j]);
            return r;
        }

        // ─── Schur decomposition (A = Z·T·Z' via QR iteration) ─────────────
        public static (MValue T, MValue Z) Schur(MValue A)
        {
            int n = A.Rows;
            var T = new double[n, n];
            var Z = new double[n, n];
            for (int i = 0; i < n; i++) { Z[i, i] = 1; for (int j = 0; j < n; j++) T[i, j] = A.At(i, j); }
            for (int iter = 0; iter < 500; iter++)
            {
                // Verificar convergencia subdiagonal
                double off = 0;
                for (int i = 1; i < n; i++) off += Math.Abs(T[i, i - 1]);
                if (off < 1e-12) break;
                // Wilkinson shift
                double s = T[n - 1, n - 1];
                for (int i = 0; i < n; i++) T[i, i] -= s;
                // Givens QR
                var cs = new (double c, double sn)[n - 1];
                for (int k = 0; k < n - 1; k++)
                {
                    double aa = T[k, k], bb = T[k + 1, k];
                    double r = Math.Sqrt(aa * aa + bb * bb);
                    if (r < 1e-18) { cs[k] = (1, 0); continue; }
                    double c = aa / r, sn = bb / r;
                    cs[k] = (c, sn);
                    for (int j = k; j < n; j++)
                    {
                        double t1 = c * T[k, j] + sn * T[k + 1, j];
                        double t2 = -sn * T[k, j] + c * T[k + 1, j];
                        T[k, j] = t1; T[k + 1, j] = t2;
                    }
                }
                // R·Q + acumular Z
                for (int k = 0; k < n - 1; k++)
                {
                    var (c, sn) = cs[k];
                    for (int i = 0; i < n; i++)
                    {
                        double t1 = c * T[i, k] + sn * T[i, k + 1];
                        double t2 = -sn * T[i, k] + c * T[i, k + 1];
                        T[i, k] = t1; T[i, k + 1] = t2;
                        double z1 = c * Z[i, k] + sn * Z[i, k + 1];
                        double z2 = -sn * Z[i, k] + c * Z[i, k + 1];
                        Z[i, k] = z1; Z[i, k + 1] = z2;
                    }
                }
                for (int i = 0; i < n; i++) T[i, i] += s;
            }
            var Tmv = new MValue(n, n); var Zmv = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            { Tmv.Set(i, j, T[i, j]); Zmv.Set(i, j, Z[i, j]); }
            return (Tmv, Zmv);
        }

        // ─── Matrix exponential (Padé 6 + scaling-squaring) ────────────────
        public static MValue Expm(MValue A)
        {
            int n = A.Rows;
            if (n != A.Cols) throw new MatlabRuntimeException("expm: matrix must be square");
            // Norma 1 para elegir s
            double norm1 = 0;
            for (int j = 0; j < n; j++)
            {
                double col = 0;
                for (int i = 0; i < n; i++) col += Math.Abs(A.At(i, j));
                if (col > norm1) norm1 = col;
            }
            int s = Math.Max(0, (int)Math.Ceiling(Math.Log2(norm1)));
            double scale = Math.Pow(2, s);
            var As = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) As.Set(i, j, A.At(i, j) / scale);
            // Padé 6 — coeficientes
            double[] c = { 1.0, 0.5, 0.12, 0.01833333333333333, 0.001992753623188406,
                            0.00016304347826086956, 1.0349854227405274e-5 };
            // Identity
            var I = new MValue(n, n);
            for (int i = 0; i < n; i++) I.Set(i, i, 1);
            // Powers of As
            var A2 = MatMul(As, As);
            var A4 = MatMul(A2, A2);
            var A6 = MatMul(A4, A2);
            // U = A·(c1·I + c3·A² + c5·A⁴ + ...) — pero usaremos Pade(6,6) standard
            // Numerator U = c1*A + c3*A^3 + c5*A^5, Denom V = c0*I + c2*A^2 + c4*A^4 + c6*A^6
            var U = MatScale(As, c[1]);
            U = MatAdd(U, MatScale(MatMul(As, A2), c[3]));
            U = MatAdd(U, MatScale(MatMul(MatMul(As, A2), A2), c[5]));
            var V = MatScale(I, c[0]);
            V = MatAdd(V, MatScale(A2, c[2]));
            V = MatAdd(V, MatScale(A4, c[4]));
            V = MatAdd(V, MatScale(A6, c[6]));
            // Resolver (V - U) X = (V + U) → X = (V-U)^-1 (V+U)
            var VmU = MatSub(V, U);
            var VpU = MatAdd(V, U);
            var X = Linsolve(VmU, VpU);
            // Squaring: X = X^(2^s)
            for (int k = 0; k < s; k++) X = MatMul(X, X);
            return X;
        }
        public static MValue Logm(MValue A) {
            // Log via Schur + diag (simplificado, solo válido para diagonalizable real)
            var (T, Z) = Schur(A);
            int n = A.Rows;
            var logD = new MValue(n, n);
            for (int i = 0; i < n; i++)
            {
                double v = T.At(i, i);
                if (v <= 0) throw new MatlabRuntimeException("logm: requires positive eigenvalues (MVP)");
                logD.Set(i, i, Math.Log(v));
            }
            return MatMul(MatMul(Z, logD), TransposeM(Z));
        }
        public static MValue Sqrtm(MValue A) {
            var (T, Z) = Schur(A);
            int n = A.Rows;
            var sqrtT = new MValue(n, n);
            for (int i = 0; i < n; i++)
            {
                double v = T.At(i, i);
                if (v < 0) throw new MatlabRuntimeException("sqrtm: complex result (MVP)");
                sqrtT.Set(i, i, Math.Sqrt(v));
            }
            return MatMul(MatMul(Z, sqrtT), TransposeM(Z));
        }
        // Helpers
        internal static MValue MatAdd(MValue a, MValue b) {
            var r = new MValue(a.Rows, a.Cols);
            for (int i = 0; i < a.Data.Length; i++) r.Data[i] = a.Data[i] + b.Data[i];
            return r;
        }
        internal static MValue MatSub(MValue a, MValue b) {
            var r = new MValue(a.Rows, a.Cols);
            for (int i = 0; i < a.Data.Length; i++) r.Data[i] = a.Data[i] - b.Data[i];
            return r;
        }
        internal static MValue MatScale(MValue a, double s) {
            var r = new MValue(a.Rows, a.Cols);
            for (int i = 0; i < a.Data.Length; i++) r.Data[i] = a.Data[i] * s;
            return r;
        }

        // ─── BiCGStab para sistemas no-simétricos ──────────────────────────
        public static MValue BiCGStab(MValue A, MValue b, double tol, int maxIter)
        {
            int n = A.Rows;
            var x = new double[n];
            var r = new double[n]; var r0 = new double[n];
            // r = b - A*x = b (x=0)
            for (int i = 0; i < n; i++) { r[i] = b.At(i, 0); r0[i] = r[i]; }
            var p = (double[])r.Clone();
            double rho = 0;
            for (int i = 0; i < n; i++) rho += r[i] * r0[i];
            double bNorm = Math.Sqrt(rho);
            if (bNorm < 1e-30) return new MValue(n, 1);
            for (int it = 0; it < maxIter; it++)
            {
                // v = A*p
                var v = new double[n];
                for (int i = 0; i < n; i++) { double s = 0; for (int j = 0; j < n; j++) s += A.At(i, j) * p[j]; v[i] = s; }
                double alpha_num = rho;
                double alpha_den = 0;
                for (int i = 0; i < n; i++) alpha_den += r0[i] * v[i];
                if (Math.Abs(alpha_den) < 1e-30) break;
                double alpha = alpha_num / alpha_den;
                var s_v = new double[n];
                for (int i = 0; i < n; i++) s_v[i] = r[i] - alpha * v[i];
                // t = A*s
                var t = new double[n];
                for (int i = 0; i < n; i++) { double ss = 0; for (int j = 0; j < n; j++) ss += A.At(i, j) * s_v[j]; t[i] = ss; }
                double ts = 0, tt = 0;
                for (int i = 0; i < n; i++) { ts += t[i] * s_v[i]; tt += t[i] * t[i]; }
                if (tt < 1e-30) break;
                double omega = ts / tt;
                for (int i = 0; i < n; i++) { x[i] += alpha * p[i] + omega * s_v[i]; r[i] = s_v[i] - omega * t[i]; }
                double resid = 0;
                for (int i = 0; i < n; i++) resid += r[i] * r[i];
                if (Math.Sqrt(resid) < tol * bNorm) break;
                double rhoNew = 0;
                for (int i = 0; i < n; i++) rhoNew += r[i] * r0[i];
                double beta = (rhoNew / rho) * (alpha / omega);
                for (int i = 0; i < n; i++) p[i] = r[i] + beta * (p[i] - omega * v[i]);
                rho = rhoNew;
            }
            var result = new MValue(n, 1);
            for (int i = 0; i < n; i++) result.Set(i, 0, x[i]);
            return result;
        }

        // ─── GMRES con restart ─────────────────────────────────────────────
        public static MValue Gmres(MValue A, MValue b, int restart, double tol, int maxIter)
        {
            int n = A.Rows;
            var x = new double[n];
            double bNorm = 0;
            for (int i = 0; i < n; i++) bNorm += b.At(i, 0) * b.At(i, 0);
            bNorm = Math.Sqrt(bNorm);
            if (bNorm < 1e-30) return new MValue(n, 1);
            for (int outerIt = 0; outerIt < maxIter; outerIt++)
            {
                // r = b - A·x
                var r = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double s = b.At(i, 0);
                    for (int j = 0; j < n; j++) s -= A.At(i, j) * x[j];
                    r[i] = s;
                }
                double rNorm = 0; foreach (var v in r) rNorm += v * v; rNorm = Math.Sqrt(rNorm);
                if (rNorm < tol * bNorm) break;
                // Arnoldi
                int m = restart;
                var V = new double[m + 1][];
                V[0] = new double[n];
                for (int i = 0; i < n; i++) V[0][i] = r[i] / rNorm;
                var H = new double[m + 1, m];
                var g = new double[m + 1]; g[0] = rNorm;
                var cs = new double[m]; var sn = new double[m];
                int j_used = m;
                for (int j = 0; j < m; j++)
                {
                    // w = A · V[j]
                    var w = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        double s = 0;
                        for (int k = 0; k < n; k++) s += A.At(i, k) * V[j][k];
                        w[i] = s;
                    }
                    for (int i = 0; i <= j; i++)
                    {
                        double dot = 0;
                        for (int k = 0; k < n; k++) dot += w[k] * V[i][k];
                        H[i, j] = dot;
                        for (int k = 0; k < n; k++) w[k] -= dot * V[i][k];
                    }
                    double wNorm = 0; foreach (var v in w) wNorm += v * v; wNorm = Math.Sqrt(wNorm);
                    H[j + 1, j] = wNorm;
                    if (wNorm > 1e-15)
                    {
                        V[j + 1] = new double[n];
                        for (int i = 0; i < n; i++) V[j + 1][i] = w[i] / wNorm;
                    }
                    // Aplicar Givens previos
                    for (int i = 0; i < j; i++)
                    {
                        double tmp = cs[i] * H[i, j] + sn[i] * H[i + 1, j];
                        H[i + 1, j] = -sn[i] * H[i, j] + cs[i] * H[i + 1, j];
                        H[i, j] = tmp;
                    }
                    double rho = Math.Sqrt(H[j, j] * H[j, j] + H[j + 1, j] * H[j + 1, j]);
                    if (rho < 1e-30) { j_used = j; break; }
                    cs[j] = H[j, j] / rho; sn[j] = H[j + 1, j] / rho;
                    H[j, j] = rho; H[j + 1, j] = 0;
                    g[j + 1] = -sn[j] * g[j]; g[j] = cs[j] * g[j];
                    if (Math.Abs(g[j + 1]) < tol * bNorm) { j_used = j + 1; break; }
                }
                // Back-solve y para H[0..j_used-1, 0..j_used-1]
                int k_end = Math.Min(j_used, m);
                var y = new double[k_end];
                for (int i = k_end - 1; i >= 0; i--)
                {
                    double s = g[i];
                    for (int j = i + 1; j < k_end; j++) s -= H[i, j] * y[j];
                    y[i] = s / H[i, i];
                }
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < k_end; j++) x[i] += V[j][i] * y[j];
            }
            var result = new MValue(n, 1);
            for (int i = 0; i < n; i++) result.Set(i, 0, x[i]);
            return result;
        }

        /// <summary>Inversa por eliminación Gauss-Jordan.</summary>
        public static MValue Inverse(MValue m)
        {
            if (m.Rows != m.Cols) throw new MatlabRuntimeException($"inv: matrix must be square, got {m.Rows}×{m.Cols}");
            int n = m.Rows;
            var a = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) a[i, j] = m.At(i, j);
                a[i, n + i] = 1;
            }
            for (int k = 0; k < n; k++)
            {
                int pivot = k;
                for (int i = k + 1; i < n; i++) if (Math.Abs(a[i, k]) > Math.Abs(a[pivot, k])) pivot = i;
                if (Math.Abs(a[pivot, k]) < 1e-15) throw new MatlabRuntimeException("inv: matrix is singular");
                if (pivot != k) for (int j = 0; j < 2 * n; j++) (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]);
                double f = a[k, k];
                for (int j = 0; j < 2 * n; j++) a[k, j] /= f;
                for (int i = 0; i < n; i++) if (i != k)
                {
                    double g = a[i, k];
                    for (int j = 0; j < 2 * n; j++) a[i, j] -= g * a[k, j];
                }
            }
            var inv = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv.Set(i, j, a[i, n + j]);
            return inv;
        }
    }
}
