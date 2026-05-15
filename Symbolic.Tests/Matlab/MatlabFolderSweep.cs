// Sweep masivo del directorio C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Matlab\
// para detectar qué scripts MATLAB del usuario pasan por el parser sin errores.
//
// Este test es DATA-DRIVEN: descubre los .m en tiempo de ejecución, los corre
// uno por uno, y para cada uno emite [PASS]/[FAIL] con el primer error.
//
// NO falla el build si hay scripts con errores: emite un summary al output.
// El objetivo es identificar gaps del parser para iterar fixes después.
//
// Trait("Category", "Sweep") permite filtrar/excluir si lentea CI.

using Xunit.Abstractions;

namespace Calcpad.Lab.Tests
{
    public class MatlabFolderSweep
    {
        private readonly ITestOutputHelper _o;
        public MatlabFolderSweep(ITestOutputHelper o) { _o = o; }

        private const string MatlabFolder = @"C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Matlab";

        [Fact]
        [Trait("Category", "Sweep")]
        public void Sweep_AllUserMatlabScripts_Report()
        {
            int pass = 0, fail = 0, crash = 0;
            var passList = new List<string>();
            var failList = new List<(string file, int errors, string firstErr)>();
            var reportLines = new List<string>();
            void Log(string s) { _o.WriteLine(s); reportLines.Add(s); }

            if (!Directory.Exists(MatlabFolder))
            {
                Log($"Skip: folder no existe: {MatlabFolder}");
                return;
            }

            var files = Directory.GetFiles(MatlabFolder, "*.m", SearchOption.TopDirectoryOnly);
            Log($"Encontrados {files.Length} scripts .m en {MatlabFolder}");
            Log("");

            foreach (var file in files.OrderBy(f => f))
            {
                var name = Path.GetFileName(file);
                try
                {
                    var script = File.ReadAllText(file);
                    // Limitar tamaño para evitar bloquear el test
                    if (script.Length > 200_000)
                    {
                        Log($"[SKIP-BIG] {name} ({script.Length} bytes)");
                        continue;
                    }
                    var lab = new TestLab();
                    var html = lab.Run(script);
                    var errs = lab.CountErrors(html);
                    if (errs == 0)
                    {
                        pass++;
                        passList.Add(name);
                        Log($"[PASS] {name}");
                    }
                    else
                    {
                        fail++;
                        // Extraer primer error del HTML
                        var idx = html.IndexOf("class=\"err");
                        string firstErr = "?";
                        if (idx >= 0)
                        {
                            var endTag = html.IndexOf("</span>", idx);
                            if (endTag > idx)
                            {
                                firstErr = html[idx..endTag];
                                // limpiar tags HTML para legibilidad
                                firstErr = System.Text.RegularExpressions.Regex.Replace(firstErr, "<.*?>", "");
                                if (firstErr.Length > 200) firstErr = firstErr[..200] + "...";
                            }
                        }
                        failList.Add((name, errs, firstErr));
                        Log($"[FAIL {errs}] {name} :: {firstErr}");
                    }
                }
                catch (Exception ex)
                {
                    crash++;
                    Log($"[CRASH] {name} :: {ex.GetType().Name}: {ex.Message}");
                }
            }

            _o.WriteLine("");
            _o.WriteLine("===== SUMMARY =====");
            _o.WriteLine($"PASS  : {pass}");
            _o.WriteLine($"FAIL  : {fail}");
            _o.WriteLine($"CRASH : {crash}");
            _o.WriteLine($"TOTAL : {files.Length}");

            if (passList.Count > 0)
            {
                Log("");
                Log("--- PASS list (candidatos para Examples/from-matlab-prueba/) ---");
                foreach (var p in passList) _o.WriteLine("  " + p);
            }
            if (failList.Count > 0)
            {
                Log("");
                Log("--- FAIL top-10 errores más frecuentes ---");
                var grp = failList
                    .GroupBy(f => f.firstErr)
                    .OrderByDescending(g => g.Count())
                    .Take(10);
                foreach (var g in grp)
                    Log($"  ({g.Count()}×) {g.Key}");
            }

            // Dump report a disco
            var reportPath = @"C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Lab\Symbolic.Tests\matlab-sweep-report.txt";
            File.WriteAllLines(reportPath, reportLines);
            _o.WriteLine($"\nReporte completo escrito a: {reportPath}");

            // No assert que falle — esto es informativo.
            Assert.True(true);
        }
    }
}
