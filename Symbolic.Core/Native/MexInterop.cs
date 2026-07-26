using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    /// <summary>
    /// MEX MVP nativo para Hekatan Lab: compila una funcion C++ (.cpp) a DLL con g++
    /// (MinGW) y la invoca desde un script .m pasando matrices por memoria (row-major).
    ///
    /// Firma nativa fija (Hekatan-nativa, NO la mxArray de MATLAB):
    ///
    ///   typedef double* (*hkmex_alloc_t)(int index, int rows, int cols);
    ///   extern "C" __declspec(dllexport) void hkmex(
    ///       int nin, const double* const* in, const int* rows, const int* cols,
    ///       int nout, hkmex_alloc_t alloc, int* outRows, int* outCols);
    ///
    /// El codigo nativo recibe `nin` matrices por puntero (row-major) con sus dims en
    /// rows[]/cols[]. Para cada salida i pone outRows[i]/outCols[i] y pide el buffer con
    /// `double* p = alloc(i, r, c);` (memoria gestionada por el host, sin free cruzado
    /// entre runtimes). El host copia esos buffers a MValue y los libera.
    /// </summary>
    internal static class MexInterop
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr HkmexAllocDelegate(int index, int rows, int cols);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void HkmexEntryDelegate(
            int nin, IntPtr inPtrs, IntPtr rows, IntPtr cols,
            int nout, IntPtr allocCb, IntPtr outRows, IntPtr outCols);

        // ABI string (opt-in): const char* hkmex_str(int nin, const char* const* in).
        // El DLL retorna un const char* (buffer propio, p.ej. std::string static) que el host
        // NO libera. Se usa cuando los argumentos del MValue son strings (CAS simbolico, etc.).
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr HkmexStrDelegate(int nin, IntPtr inPtrs);

        /// <summary>Localiza una herramienta MinGW 64-bit (g++/gcc/gfortran): primero PATH,
        /// luego rutas conocidas (Octave, CodeBlocks, "Assistant"/"Designer" MinGW).
        /// Devuelve null si no existe.</summary>
        public static string FindTool(string toolName)
        {
            // 1) PATH (rapido: si `<tool> --version` corre, existe)
            if (TryRun(toolName, "--version", out _)) return toolName;

            // 2) Rutas conocidas en esta clase de maquinas (Octave trae MinGW 64-bit)
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string exeName = toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? toolName : toolName + ".exe";
            var candidates = new List<string>
            {
                Path.Combine(pf ?? "", "GNU Octave", "Octave-10.1.0", "mingw64", "bin", exeName),
            };
            // Escaneo generico: cualquier .../bin/<tool>.exe bajo carpetas MinGW conocidas
            foreach (var root in new[] { pf, pfx86 })
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                foreach (var sub in new[] { "GNU Octave", "CodeBlocks", "Assistant 6.9.1 (MinGW 13.1.0 64-bit)", "Designer (MinGW)" })
                {
                    var baseDir = Path.Combine(root, sub);
                    if (!Directory.Exists(baseDir)) continue;
                    try
                    {
                        foreach (var hit in Directory.EnumerateFiles(baseDir, exeName, SearchOption.AllDirectories))
                            candidates.Add(hit);
                    }
                    catch { /* permisos: ignorar */ }
                }
            }
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;

            return null;
        }

        /// <summary>Compila un fuente nativo a DLL 64-bit junto al fuente, eligiendo el
        /// compilador MinGW por extension: .cpp/.cc/.cxx -> g++, .c -> gcc, .f/.f90/.f95 ->
        /// gfortran. Todos exponen la MISMA ABI `hkmex`. Devuelve la ruta del DLL. Lanza con
        /// el stderr real del compilador si falla.</summary>
        public static string Compile(string srcPath)
        {
            string full = Path.GetFullPath(srcPath);
            if (!File.Exists(full))
                throw new Exception($"archivo no encontrado: {full}");

            string ext = Path.GetExtension(full).ToLowerInvariant();
            string tool, langArgs, lang;
            switch (ext)
            {
                case ".cpp": case ".cc": case ".cxx": case ".c++":
                    tool = "g++"; lang = "C++";
                    langArgs = "-static-libgcc -static-libstdc++";
                    break;
                case ".c":
                    tool = "gcc"; lang = "C";
                    langArgs = "-static-libgcc";
                    break;
                case ".f": case ".for": case ".f77": case ".f90": case ".f95": case ".f03": case ".f08":
                    tool = "gfortran"; lang = "Fortran";
                    langArgs = "-static-libgcc -static-libgfortran -static-libquadmath";
                    break;
                default:
                    throw new Exception($"extension no soportada para mex: '{ext}' (usa .cpp/.cc/.cxx, .c, o .f/.f90/.f95)");
            }

            string gpp = FindTool(tool);
            if (gpp == null)
                throw new Exception($"no se encontro {tool} (MinGW 64-bit para {lang}) en PATH ni en rutas conocidas.");

            string dll = Path.ChangeExtension(full, ".dll");

            string srcTxt = "";
            try { srcTxt = File.ReadAllText(full); } catch { /* el compilador reportara si no existe */ }

            // Include del shim MEX/mxArray: si el fuente usa mexFunction, se agrega -I<shim> para
            // que `#include "mex.h"` resuelva a NUESTRO mex.h (capa fiel a MATLAB). En MATLAB
            // resuelve al de MathWorks; asi el MISMO .cpp compila en ambos.
            string incExtra = "";
            if (srcTxt.Contains("mexFunction", StringComparison.Ordinal))
                incExtra = $" -I\"{EnsureShimDir()}\"";

            // Auto-enlace de giac (CAS): si el fuente menciona 'giac' y hay un giac.dll junto
            // al host (mismo dir del exe), se pasa su ruta como input al linker. MinGW ld genera
            // los imports directo del .dll (no hace falta lib de import). Solo se enlaza cuando el
            // fuente lo usa: un mex numerico normal NO arrastra la dependencia.
            string linkExtra = "";
            if (srcTxt.Contains("giac", StringComparison.OrdinalIgnoreCase))
            {
                string giac = Path.Combine(AppContext.BaseDirectory, "giac.dll");
                if (File.Exists(giac))
                    linkExtra = $" \"{giac}\"";
            }

            // -static-lib* para no depender de las DLL de runtime de MinGW en el PATH.
            // (NO -static a secas: forzaria -lpthread estatico, que MinGW-Octave no trae.)
            string args = $"-shared -O2 {langArgs}{incExtra} -o \"{dll}\" \"{full}\"{linkExtra}";

            var psi = new ProcessStartInfo
            {
                FileName = gpp,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(full) ?? Environment.CurrentDirectory,
            };

            using var p = Process.Start(psi)
                ?? throw new Exception($"no se pudo lanzar el compilador: {gpp}");
            string stderr = p.StandardError.ReadToEnd();
            string stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                string msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new Exception($"{tool} fallo (exit {p.ExitCode}):\n{msg.Trim()}");
            }
            if (!File.Exists(dll))
                throw new Exception($"{tool} termino OK pero no se genero el DLL: {dll}");
            return dll;
        }

        /// <summary>Carga el DLL dinamicamente, marshala las entradas row-major, llama a
        /// hkmex y devuelve las salidas (data row-major + dims). El host asigna/libera los
        /// buffers de salida via callback (sin free entre runtimes distintos).</summary>
        public static List<(double[] data, int rows, int cols)> Call(
            string dllPath, double[][] inputs, int[] inRows, int[] inCols, int nout)
        {
            IntPtr h = NativeLibrary.Load(dllPath);
            try
            {
                if (!NativeLibrary.TryGetExport(h, "hkmex", out IntPtr entryPtr))
                    throw new Exception($"el DLL no exporta 'hkmex': {Path.GetFileName(dllPath)}");
                var entry = Marshal.GetDelegateForFunctionPointer<HkmexEntryDelegate>(entryPtr);

                int nin = inputs.Length;
                var handles = new List<GCHandle>();
                var inAddr = new IntPtr[Math.Max(nin, 1)];
                for (int i = 0; i < nin; i++)
                {
                    var gh = GCHandle.Alloc(inputs[i], GCHandleType.Pinned);
                    handles.Add(gh);
                    inAddr[i] = gh.AddrOfPinnedObject();
                }
                var inPtrsH = GCHandle.Alloc(inAddr, GCHandleType.Pinned);
                var rowsH = GCHandle.Alloc(inRows, GCHandleType.Pinned);
                var colsH = GCHandle.Alloc(inCols, GCHandleType.Pinned);

                var outRows = new int[nout];
                var outCols = new int[nout];
                var outRowsH = GCHandle.Alloc(outRows, GCHandleType.Pinned);
                var outColsH = GCHandle.Alloc(outCols, GCHandleType.Pinned);

                var outBufs = new IntPtr[nout];
                HkmexAllocDelegate alloc = (index, r, c) =>
                {
                    long n = (long)r * c;
                    if (n < 0) n = 0;
                    IntPtr buf = Marshal.AllocHGlobal((IntPtr)(n * sizeof(double)));
                    if (index >= 0 && index < nout) outBufs[index] = buf;
                    return buf;
                };
                IntPtr allocPtr = Marshal.GetFunctionPointerForDelegate(alloc);

                try
                {
                    entry(nin, inPtrsH.AddrOfPinnedObject(), rowsH.AddrOfPinnedObject(),
                        colsH.AddrOfPinnedObject(), nout, allocPtr,
                        outRowsH.AddrOfPinnedObject(), outColsH.AddrOfPinnedObject());
                    GC.KeepAlive(alloc);

                    var results = new List<(double[], int, int)>(nout);
                    for (int i = 0; i < nout; i++)
                    {
                        int r = outRows[i], c = outCols[i];
                        int len = r * c;
                        var data = new double[len < 0 ? 0 : len];
                        if (outBufs[i] != IntPtr.Zero && len > 0)
                            Marshal.Copy(outBufs[i], data, 0, len);
                        results.Add((data, r, c));
                    }
                    return results;
                }
                finally
                {
                    foreach (var b in outBufs)
                        if (b != IntPtr.Zero) Marshal.FreeHGlobal(b);
                    foreach (var gh in handles) gh.Free();
                    inPtrsH.Free(); rowsH.Free(); colsH.Free();
                    outRowsH.Free(); outColsH.Free();
                }
            }
            finally
            {
                NativeLibrary.Free(h);
            }
        }

        /// <summary>Ruta string de la ABI: carga el DLL, marshala los argumentos como
        /// const char* (UTF-8), invoca `hkmex_str(nin, in)` y devuelve el const char* de
        /// retorno como string .NET. El buffer de retorno es propiedad del DLL (no se libera).
        /// Lanza si el DLL no exporta 'hkmex_str'.</summary>
        public static string CallStr(string dllPath, string[] inputs)
        {
            IntPtr h = NativeLibrary.Load(dllPath);
            try
            {
                if (!NativeLibrary.TryGetExport(h, "hkmex_str", out IntPtr entryPtr))
                    throw new Exception($"el DLL no exporta 'hkmex_str' (ABI string): {Path.GetFileName(dllPath)}");
                var entry = Marshal.GetDelegateForFunctionPointer<HkmexStrDelegate>(entryPtr);

                int nin = inputs.Length;
                var strPtrs = new IntPtr[nin];
                var addrArr = new IntPtr[Math.Max(nin, 1)];
                GCHandle addrH = default;
                try
                {
                    for (int i = 0; i < nin; i++)
                    {
                        strPtrs[i] = Marshal.StringToCoTaskMemUTF8(inputs[i] ?? "");
                        addrArr[i] = strPtrs[i];
                    }
                    addrH = GCHandle.Alloc(addrArr, GCHandleType.Pinned);

                    IntPtr retPtr = entry(nin, addrH.AddrOfPinnedObject());
                    // giac/caseval devuelve UTF-8; buffer propiedad del DLL (no liberar).
                    return retPtr == IntPtr.Zero ? "" : (Marshal.PtrToStringUTF8(retPtr) ?? "");
                }
                finally
                {
                    if (addrH.IsAllocated) addrH.Free();
                    for (int i = 0; i < nin; i++)
                        if (strPtrs[i] != IntPtr.Zero) Marshal.ZeroFreeCoTaskMemUTF8(strPtrs[i]);
                }
            }
            finally
            {
                NativeLibrary.Free(h);
            }
        }

        // ─── Capa MEX/mxArray FIEL a MATLAB ────────────────────────────────────
        // mxArray concreto (en MATLAB es opaco). Layout ESPEJADO por el header shim
        // (Symbolic.Core/Native/mex_shim/mex.h): pr, pi, m, n, classID, complexflag = 40 bytes.
        [StructLayout(LayoutKind.Sequential)]
        private struct MxArray
        {
            public IntPtr pr;          // datos reales, COLUMN-MAJOR
            public IntPtr pi;          // imaginarios (NULL si real)
            public ulong  m;           // filas   (size_t)
            public ulong  n;           // columnas(size_t)
            public int    classID;     // mxDOUBLE_CLASS = 6
            public int    complexflag; // 0 = real
        }

        // void mexFunction(int nlhs, mxArray* plhs[], int nrhs, const mxArray* prhs[])
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MexFunctionDelegate(int nlhs, IntPtr plhs, int nrhs, IntPtr prhs);

        /// <summary>Contenido del header shim mex.h (fiel a MATLAB R2017a, subset). Se escribe
        /// a un dir temporal cacheado para que `#include "mex.h"` resuelva a este via -I.</summary>
        private const string ShimHeader =
@"// hekatan mex.h - capa MEX/mxArray fiel a MATLAB (subset). Autogenerado por Hekatan Lab.
// El MISMO .cpp de un MEX real (mexFunction + mxArray) corre en MATLAB R2017a y en Hekatan.
// MATLAB usa COLUMN-MAJOR: pr[i + j*m] = A(i+1, j+1). Firmas replican matrix.h/mex.h de R2017a.
#ifndef HEKATAN_MEX_H
#define HEKATAN_MEX_H
#include <stddef.h>
#include <stdlib.h>
#ifdef __cplusplus
extern ""C"" {
#endif
typedef size_t mwSize;
typedef size_t mwIndex;
typedef enum { mxREAL = 0, mxCOMPLEX = 1 } mxComplexity;
typedef enum {
    mxUNKNOWN_CLASS = 0, mxCELL_CLASS, mxSTRUCT_CLASS, mxLOGICAL_CLASS,
    mxCHAR_CLASS, mxVOID_CLASS, mxDOUBLE_CLASS, mxSINGLE_CLASS,
    mxINT8_CLASS, mxUINT8_CLASS, mxINT16_CLASS, mxUINT16_CLASS,
    mxINT32_CLASS, mxUINT32_CLASS, mxINT64_CLASS, mxUINT64_CLASS
} mxClassID;
typedef struct hk_mxArray {
    double*   pr;
    double*   pi;
    size_t    m;
    size_t    n;
    int       classID;
    int       complexflag;
} mxArray;
static mxArray* mxCreateDoubleMatrix(mwSize m, mwSize n, mxComplexity cx) {
    mxArray* a = (mxArray*)malloc(sizeof(mxArray));
    size_t nel = (size_t)m * (size_t)n;
    a->m = (size_t)m; a->n = (size_t)n;
    a->classID = (int)mxDOUBLE_CLASS;
    a->complexflag = (cx == mxCOMPLEX) ? 1 : 0;
    a->pr = (double*)calloc(nel ? nel : 1, sizeof(double));
    a->pi = (cx == mxCOMPLEX) ? (double*)calloc(nel ? nel : 1, sizeof(double)) : NULL;
    return a;
}
static mxArray* mxCreateDoubleScalar(double v) {
    mxArray* a = mxCreateDoubleMatrix(1, 1, mxREAL); a->pr[0] = v; return a;
}
static double* mxGetPr(const mxArray* a)   { return a ? a->pr : NULL; }
static double* mxGetPi(const mxArray* a)   { return a ? a->pi : NULL; }
static void*   mxGetData(const mxArray* a) { return a ? (void*)a->pr : NULL; }
static mwSize  mxGetM(const mxArray* a)    { return a ? (mwSize)a->m : 0; }
static mwSize  mxGetN(const mxArray* a)    { return a ? (mwSize)a->n : 0; }
static size_t  mxGetNumberOfElements(const mxArray* a) { return a ? a->m * a->n : 0; }
static double  mxGetScalar(const mxArray* a){ return (a && a->pr) ? a->pr[0] : 0.0; }
static int     mxGetClassID(const mxArray* a){ return a ? a->classID : (int)mxUNKNOWN_CLASS; }
static int     mxIsDouble(const mxArray* a) { return a && a->classID == (int)mxDOUBLE_CLASS; }
static int     mxIsComplex(const mxArray* a){ return a && a->complexflag != 0; }
static void    mxDestroyArray(mxArray* a)   { if (a) { free(a->pr); free(a->pi); free(a); } }
extern void mexFunction(int nlhs, mxArray* plhs[], int nrhs, const mxArray* prhs[]);
#ifdef __cplusplus
}
#endif
#endif
";

        private static string _shimDir;
        /// <summary>Escribe el header shim a %TEMP%/hekatan_mex_shim/mex.h (una vez) y devuelve
        /// el directorio para pasarlo como -I al compilador.</summary>
        public static string EnsureShimDir()
        {
            if (_shimDir != null) return _shimDir;
            string dir = Path.Combine(Path.GetTempPath(), "hekatan_mex_shim");
            Directory.CreateDirectory(dir);
            string hdr = Path.Combine(dir, "mex.h");
            try
            {
                if (!File.Exists(hdr) || File.ReadAllText(hdr) != ShimHeader)
                    File.WriteAllText(hdr, ShimHeader);
            }
            catch { /* si otra instancia lo escribe en paralelo, sirve igual */ }
            _shimDir = dir;
            return dir;
        }

        /// <summary>Devuelve que entradas exporta el DLL: mexFunction (MATLAB), hkmex (numerica
        /// MVP), hkmex_str (string). El host elige la ruta segun esto y el tipo de argumentos.</summary>
        public static (bool hasMex, bool hasHk, bool hasStr) Probe(string dllPath)
        {
            IntPtr h = NativeLibrary.Load(dllPath);
            try
            {
                bool mex = NativeLibrary.TryGetExport(h, "mexFunction", out _);
                bool hk  = NativeLibrary.TryGetExport(h, "hkmex", out _);
                bool str = NativeLibrary.TryGetExport(h, "hkmex_str", out _);
                return (mex, hk, str);
            }
            finally { NativeLibrary.Free(h); }
        }

        /// <summary>Ruta mxArray (fiel a MATLAB): envuelve las entradas row-major como mxArray
        /// COLUMN-MAJOR, invoca `mexFunction(nlhs, plhs, nrhs, prhs)` y lee plhs[0..nlhs-1] como
        /// matrices (convertidas de vuelta a row-major). Lanza si el DLL no exporta 'mexFunction'.</summary>
        public static List<(double[] data, int rows, int cols)> CallMx(
            string dllPath, double[][] inputsRowMajor, int[] inRows, int[] inCols, int nlhs)
        {
            IntPtr h = NativeLibrary.Load(dllPath);
            try
            {
                if (!NativeLibrary.TryGetExport(h, "mexFunction", out IntPtr ep))
                    throw new Exception($"el DLL no exporta 'mexFunction': {Path.GetFileName(dllPath)}");
                var mexfn = Marshal.GetDelegateForFunctionPointer<MexFunctionDelegate>(ep);

                int nrhs = inputsRowMajor.Length;
                int mxSize = Marshal.SizeOf<MxArray>();
                var allocated = new List<IntPtr>();
                var dataHandles = new List<GCHandle>();
                var prhsPtrs = new IntPtr[Math.Max(nrhs, 1)];
                for (int k = 0; k < nrhs; k++)
                {
                    int m = inRows[k], n = inCols[k];
                    // row-major (host) -> column-major (MATLAB): col[j*m + i] = row[i*n + j]
                    var col = new double[m * n];
                    for (int i = 0; i < m; i++)
                        for (int j = 0; j < n; j++)
                            col[j * m + i] = inputsRowMajor[k][i * n + j];
                    var gh = GCHandle.Alloc(col, GCHandleType.Pinned);
                    dataHandles.Add(gh);
                    var mx = new MxArray
                    {
                        pr = gh.AddrOfPinnedObject(), pi = IntPtr.Zero,
                        m = (ulong)m, n = (ulong)n, classID = 6 /*mxDOUBLE_CLASS*/, complexflag = 0
                    };
                    IntPtr mxPtr = Marshal.AllocHGlobal(mxSize);
                    allocated.Add(mxPtr);
                    Marshal.StructureToPtr(mx, mxPtr, false);
                    prhsPtrs[k] = mxPtr;
                }
                var prhsArrH = GCHandle.Alloc(prhsPtrs, GCHandleType.Pinned);
                var plhs = new IntPtr[Math.Max(nlhs, 1)];
                var plhsArrH = GCHandle.Alloc(plhs, GCHandleType.Pinned);
                try
                {
                    mexfn(nlhs, plhsArrH.AddrOfPinnedObject(), nrhs, prhsArrH.AddrOfPinnedObject());
                    var results = new List<(double[], int, int)>(nlhs);
                    for (int k = 0; k < nlhs; k++)
                    {
                        IntPtr op = plhs[k];
                        if (op == IntPtr.Zero) { results.Add((Array.Empty<double>(), 0, 0)); continue; }
                        var omx = Marshal.PtrToStructure<MxArray>(op);
                        int m = (int)omx.m, n = (int)omx.n, len = m * n;
                        var col = new double[len < 0 ? 0 : len];
                        if (omx.pr != IntPtr.Zero && len > 0) Marshal.Copy(omx.pr, col, 0, len);
                        // column-major -> row-major
                        var row = new double[col.Length];
                        for (int i = 0; i < m; i++)
                            for (int j = 0; j < n; j++)
                                row[i * n + j] = col[j * m + i];
                        results.Add((row, m, n));
                    }
                    return results;
                }
                finally
                {
                    plhsArrH.Free(); prhsArrH.Free();
                    foreach (var gh in dataHandles) gh.Free();
                    foreach (var p in allocated) Marshal.FreeHGlobal(p);
                    // NOTA: los buffers de plhs los asigno el .cpp (malloc del runtime MinGW);
                    // no se liberan aqui (fuga acotada por llamada, aceptable en el MVP).
                }
            }
            finally { NativeLibrary.Free(h); }
        }

        private static bool TryRun(string file, string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
