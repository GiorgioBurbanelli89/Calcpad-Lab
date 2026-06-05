// EtabsBridge — Calcpad Lab (.NET 10) corre ETABS y trae resultados al documento.
// NOTA: el OAPI de ETABS es .NET Framework y NO coexiste con .NET 10 en el mismo proceso
// (ni por Remoting ni por COM in-proc). Por eso se delega a EtabsRunner.exe, un helper net48
// que SÍ habla el dialecto del OAPI, y se lee su JSON por stdout. Sin Python.
// Uso en .m:  r = etabs_run("mesa.edb", "Live")
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Calcpad.Core.Matlab
{
    internal static class EtabsBridge
    {
        // Carpeta de ETABS (donde está ETABS.exe). El helper la recibe como 3er argumento.
        private static readonly string[] EtabsDirs =
        {
            @"C:\Program Files\Computers and Structures\ETABS 19",
            @"C:\Program Files\Computers and Structures\ETABS 22",
            @"C:\Program Files\Computers and Structures\ETABS 21",
        };

        public static Dictionary<string, double> Run(string model, string loadCase, string ver = "19")
        {
            string runner = FindRunner();
            if (runner == null)
                throw new Exception("etabs_run: no encuentro EtabsRunner.exe (helper net48 del OAPI). " +
                                    "Debe estar junto a CalcpadLab o en EtabsRunner\\.");

            string dir = ResolveEtabsDir(ver);

            var psi = new ProcessStartInfo
            {
                FileName = runner,
                UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(model);
            psi.ArgumentList.Add(loadCase);
            if (dir != null) psi.ArgumentList.Add(dir);

            string stdout, stderr;
            using (var p = Process.Start(psi))
            {
                stdout = p.StandardOutput.ReadToEnd();
                stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } throw new Exception("etabs_run: timeout (ETABS tardó demasiado)."); }
                if (p.ExitCode != 0)
                    throw new Exception("etabs_run: " + (string.IsNullOrWhiteSpace(stderr) ? "el helper falló (código " + p.ExitCode + ")." : Trim(stderr)));
            }

            if (string.IsNullOrWhiteSpace(stdout))
                throw new Exception("etabs_run: el helper no devolvió resultados. " + Trim(stderr));

            var res = new Dictionary<string, double>();
            try
            {
                using var doc = JsonDocument.Parse(stdout.Trim());
                foreach (var kv in doc.RootElement.EnumerateObject())
                    if (kv.Value.ValueKind == JsonValueKind.Number)
                        res[kv.Name] = kv.Value.GetDouble();
            }
            catch (Exception ex)
            {
                throw new Exception("etabs_run: respuesta no válida del helper. " + ex.Message + " | " + Trim(stdout));
            }

            if (res.Count == 0) throw new Exception("etabs_run: sin resultados (¿caso '" + loadCase + "' válido?). " + Trim(stderr));
            return res;
        }

        private static string FindRunner()
        {
            var bases = new List<string>();
            try { bases.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)); } catch { }
            try { bases.Add(AppContext.BaseDirectory); } catch { }
            bases.Add(Directory.GetCurrentDirectory());

            foreach (var b in bases)
            {
                if (string.IsNullOrEmpty(b)) continue;
                foreach (var rel in new[] { "EtabsRunner.exe", @"EtabsRunner\EtabsRunner.exe" })
                {
                    var p = Path.Combine(b, rel);
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        private static string ResolveEtabsDir(string ver)
        {
            // si ver es una ruta a carpeta, úsala directo
            if (!string.IsNullOrEmpty(ver) && Directory.Exists(ver)) return ver;
            // si ver es "19"/"22"/... busca esa versión primero
            if (!string.IsNullOrEmpty(ver))
            {
                var byVer = $@"C:\Program Files\Computers and Structures\ETABS {ver}";
                if (Directory.Exists(byVer)) return byVer;
            }
            foreach (var d in EtabsDirs) if (Directory.Exists(d)) return d;
            return null; // el helper usará su default
        }

        private static string Trim(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) : s);
    }
}
