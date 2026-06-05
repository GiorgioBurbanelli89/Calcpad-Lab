// EtabsRunner — helper .NET Framework 4.8 que maneja el OAPI de ETABS (mismo "dialecto" que ETABS)
// y escupe JSON. Lo llama Calcpad Lab (.NET 10) por fuera, salvando el choque de runtimes.
// Uso:  EtabsRunner.exe "C:\...\mesa.edb" "Live" [verDir]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

class Program
{
    const double G = 9.80665;

    // Carpetas candidatas de ETABS (de donde se carga ETABSv1.dll en runtime; NO se redistribuye).
    static readonly string[] EtabsDirs =
    {
        @"C:\Program Files\Computers and Structures\ETABS 19",
        @"C:\Program Files\Computers and Structures\ETABS 22",
        @"C:\Program Files\Computers and Structures\ETABS 21",
        @"C:\Program Files\Computers and Structures\ETABS 20",
    };
    static string _etabsDir;

    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("uso: EtabsRunner modelo.edb [caso] [verDir]"); return 2; }
        string dir = args.Length >= 3 && Directory.Exists(args[2]) ? args[2] : null;
        if (dir == null) foreach (var d in EtabsDirs) if (Directory.Exists(d)) { dir = d; break; }
        if (dir == null || !File.Exists(Path.Combine(dir, "ETABSv1.dll")))
        {
            Console.Error.WriteLine("ERROR: no encuentro ETABSv1.dll. Instalá ETABS o pasá la carpeta como 3er argumento.");
            return 3;
        }
        _etabsDir = dir;
        // Cargar ETABSv1.dll desde la instalación local de ETABS (sin redistribuirlo).
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            var name = new AssemblyName(e.Name).Name;
            var p = Path.Combine(_etabsDir, name + ".dll");
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        return RunCore(args[0], args.Length >= 2 ? args[1] : "Live", dir);
    }

    // Aislado en su propio método: el JIT resuelve los tipos ETABSv1 recién al entrar acá,
    // cuando el AssemblyResolve ya está registrado.
    static int RunCore(string model, string caseN, string dir)
    {
        try
        {
            string exe = Path.Combine(dir, "ETABS.exe");

            ETABSv1.cHelper helper = new ETABSv1.Helper();
            ETABSv1.cOAPI etabs = helper.CreateObject(exe);
            etabs.ApplicationStart();
            try { etabs.Hide(); } catch { }
            ETABSv1.cSapModel sm = etabs.SapModel;
            sm.File.OpenFile(model);
            sm.SetPresentUnits(ETABSv1.eUnits.kN_m_C);
            try { sm.SetModelIsLocked(false); } catch { }
            sm.Analyze.RunAnalysis();
            sm.Results.Setup.DeselectAllCasesAndCombosForOutput();
            sm.Results.Setup.SetCaseSelectedForOutput(caseN);

            var res = new Dictionary<string, double>();

            // ---- FRAMES (grupo "All") ----
            int nf = 0;
            string[] o1 = null, e1 = null, lc = null, st = null;
            double[] os = null, es = null, sn = null, P = null, V2 = null, V3 = null, T = null, M2 = null, M3 = null;
            sm.Results.FrameForce("All", ETABSv1.eItemTypeElm.GroupElm, ref nf,
                ref o1, ref os, ref e1, ref es, ref lc, ref st, ref sn,
                ref P, ref V2, ref V3, ref T, ref M2, ref M3);
            if (nf > 0)
            {
                var agg = new Dictionary<string, double[]>();
                for (int i = 0; i < nf; i++)
                {
                    if (!agg.TryGetValue(o1[i], out var g)) { g = new double[6]; agg[o1[i]] = g; }
                    g[0]=Math.Max(g[0],Math.Abs(P[i])); g[1]=Math.Max(g[1],Math.Abs(V2[i]));
                    g[2]=Math.Max(g[2],Math.Abs(V3[i])); g[3]=Math.Max(g[3],Math.Abs(T[i]));
                    g[4]=Math.Max(g[4],Math.Abs(M2[i])); g[5]=Math.Max(g[5],Math.Abs(M3[i]));
                }
                double[] col=null, beam=null; double cm3=-1, bm2=-1;
                foreach (var g in agg.Values) if (g[5] > cm3) { cm3=g[5]; col=g; }
                foreach (var g in agg.Values) { if (g==col) continue; if (g[4] > bm2) { bm2=g[4]; beam=g; } }
                if (col!=null){ res["col_P"]=col[0]/G; res["col_V2"]=col[1]/G; res["col_V3"]=col[2]/G;
                                res["col_T"]=col[3]/G; res["col_M2"]=col[4]/G; res["col_M3"]=col[5]/G; }
                if (beam!=null){ res["beam_V3"]=beam[2]/G; res["beam_T"]=beam[3]/G; res["beam_M2"]=beam[4]/G; }
            }

            // ---- SHELLS losa (grupo "All") ----
            int ns = 0;
            string[] so1=null, se=null, sp=null, slc=null, sst=null;
            double[] ssn=null, F11=null, F22=null, F12=null, FMax=null, FMin=null, FAng=null, FVM=null,
                     M11=null, M22=null, M12=null, MMax=null, MMin=null, MAng=null, V13=null, V23=null, VMax=null, VAng=null;
            sm.Results.AreaForceShell("All", ETABSv1.eItemTypeElm.GroupElm, ref ns,
                ref so1, ref se, ref sp, ref slc, ref sst, ref ssn,
                ref F11, ref F22, ref F12, ref FMax, ref FMin, ref FAng, ref FVM,
                ref M11, ref M22, ref M12, ref MMax, ref MMin, ref MAng, ref V13, ref V23, ref VMax, ref VAng);
            if (ns > 0)
            {
                double a=0,b=0,c=0;
                for (int i=0;i<ns;i++){ a=Math.Max(a,Math.Abs(M11[i])); b=Math.Max(b,Math.Abs(M22[i])); c=Math.Max(c,Math.Abs(M12[i])); }
                res["slab_Mxx"]=a/G; res["slab_Myy"]=b/G; res["slab_Mxy"]=c/G;
            }

            try { etabs.ApplicationExit(false); } catch { }

            // ---- JSON manual (sin dependencias) ----
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in res)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(kv.Key).Append("\":")
                  .Append(kv.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append("}");
            Console.Out.Write(sb.ToString());
            return 0;
        }
        catch (Exception ex)
        {
            var inner = ex; while (inner.InnerException != null) inner = inner.InnerException;
            Console.Error.WriteLine("ERROR: " + inner.Message);
            return 1;
        }
    }
}
