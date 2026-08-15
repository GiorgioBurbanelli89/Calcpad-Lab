using Calcpad.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace Calcpad.Cli
{
    class Program
    {   
        private static readonly string _currentCultureName = "en"; //en, bg or zh
        private static readonly char _dirSeparator = Path.DirectorySeparatorChar;
        const string Prompt = " |> ";
        private static int _width;

        internal static readonly string AppPath = AppContext.BaseDirectory;
        struct Line
        {
            private static readonly char[] GreekLetters = ['α', 'β', 'χ', 'δ', 'ε', 'φ', 'γ', 'η', 'ι', 'ø', 'κ', 'λ', 'μ', 'ν', 'ο', 'π', 'θ', 'ρ', 'σ', 'τ', 'υ', 'ϑ', 'ω', 'ξ', 'ψ', 'ζ'];
            private readonly StringBuilder _sb = new(80);
            public string Input, Output;
            public Line(string Input)
            {
                this.Input = LatinToGreek(Input);
                Output = string.Empty;
            }

            private string LatinToGreek(string input)
            { 
                var i = input.IndexOf('`');
                if (i == -1)
                    return input;

                _sb.Clear();
                var n = 0;
                while (i >= 0) 
                {
                    if (i > 0)
                        _sb.Append(input[n..i]);

                    n = i + 1;                    
                    _sb.Append(LatinToGreekChar(input[n]));
                    i = input.IndexOf('`', n);
                    ++n;
                }
                if (n < input.Length)
                    _sb.Append(input[n..]);

                return _sb.ToString();
            }
            private static char LatinToGreekChar(char c) => c switch
            {
                >= 'a' and <= 'z' => GreekLetters[c - 'a'],
                'V' => '∡',
                'J' => 'Ø',
                >= 'A' and <= 'Z' => (char) (GreekLetters[c - 'A'] + 'Α' - 'α'),
                '@' => '°',
                '\'' => '′',
                '"' => '″',
                _ => c
            };
        }

        static void Main()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(_currentCultureName);
            try
            {
                _width = Math.Min(Math.Min(Console.WindowWidth, Console.BufferWidth), 85);
            }
            catch 
            { 
                _width = 85; 
            }
            Settings settings = GetSettings();
            if (TryConvertOnStartup(settings))
                return;
            
            MathParser mp = new(settings.Math);
            
            if (OperatingSystem.IsWindows())
            {
                Console.OutputEncoding = Encoding.Unicode;
                Console.InputEncoding = Encoding.Unicode;  
            }
            else
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;  
            }
            
            //Console.WindowWidth = 85;
            List<Line> Lines = [];
            var Title = TryOpenOnStartup(Lines);
            Header(Title, settings.Math.Degrees);
            if (Title.Length > 0)
                Render(mp, Lines, true);

            while (true)
            {
                var LineNo = (Lines.Count + 1).ToString().PadLeft(3) + Prompt;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(LineNo);
                Console.ResetColor();
                var s = Console.ReadLine();
                if (s.Length == 0)
                {
                    Header(Title, settings.Math.Degrees);
                    Render(mp, Lines, true);
                }
                else
                {
                    string sCaps = s.ToUpper().Trim();
                    switch (sCaps)
                    {
                        case "NEW":
                            Title = string.Empty;
                            mp = new(settings.Math);
                            Lines.Clear();
                            Header(Title, settings.Math.Degrees);
                            break;
                        case "OPEN":
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                            var t = Open(LineNo, Lines);
                            if (!string.IsNullOrEmpty(t))
                            {
                                Title = t;
                                mp = new(settings.Math);
                                Header(Title, settings.Math.Degrees);
                                Render(mp, Lines, true);
                            }
                            break;
                        case "SAVE":
                            Title = Save(Title, LineNo, Lines);
                            Header(Title, settings.Math.Degrees);
                            Render(mp, Lines, false);
                            break;
                        case "EXIT":
                            return;
                        case "CLS":
                        case "DEL":
                        case "RESET":
                            Header(Title, settings.Math.Degrees);
                            if (sCaps == "DEL" && Lines.Count > 0)
                                Lines.RemoveAt(Lines.Count - 1);

                            if (sCaps != "CLS")
                                Render(mp, Lines, sCaps == "RESET");

                            break;
                        case "LIST":
                            List(LineNo);
                            break;
                        case "DEG":
                        case "RAD":
                        case "GRA":
                            settings.Math.Degrees = sCaps == "DEG" ? 0: sCaps == "RAD" ? 1 : 2;
                            mp.Degrees = settings.Math.Degrees;
                            Header(Title, settings.Math.Degrees);
                            Render(mp, Lines, true);
                            break;
                        case "SETTINGS":
                        case "OPTIONS":
                            if (OperatingSystem.IsWindows())
                            {
                                if (Execute("NOTEPAD", AppPath + "Settings.xml"))
                                {
                                    settings = GetSettings();
                                    mp = new(settings.Math);
                                    Header(Title, settings.Math.Degrees);
                                    Render(mp, Lines, true);
                                }
                            }
                            else
                            {
                                var settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                                                   $"{_dirSeparator}.config{_dirSeparator}calcpad{_dirSeparator}Settings.xml";
                                File.SetUnixFileMode(settingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                                Execute("/bin/bash", $"-c \"nano {settingsPath}\"");
                                Console.Write(Messages.Press_Any_Key_When_Ready);
                                Console.ReadKey();
                                settings = GetSettings();
                                mp = new(settings.Math);
                                Header(Title, settings.Math.Degrees);
                                Render(mp, Lines, true);
                            }
                            break;
                        case "LICENSE":
                        case "HELP":
                            var fileName = $"{AppPath}doc{_dirSeparator}{sCaps}{AddCultureExt("TXT")}";
                            if (!File.Exists(fileName))
                                fileName = $"{AppPath}doc{_dirSeparator}{sCaps}.TXT";

                            RenderFile(fileName);
                            break;
                        default:
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                            Line L = new(s);
                            if (Calculate(mp, LineNo, ref L))
                                Lines.Add(L);

                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Quita un flag del final de la cola de argumentos. Acepta las dos formas:
        /// con archivo de salida delante (<c>"out.html --png"</c>) y sin él
        /// (<c>"--png"</c> solo, cuando el usuario no dio salida). Antes sólo se
        /// reconocía la primera, así que `script.m --latex` moría con
        /// "Invalid output extension".
        /// </summary>
        private static bool StripFlag(ref string args, string flag)
        {
            if (args.EndsWith(' ' + flag, StringComparison.Ordinal))
            {
                args = args[..^(flag.Length + 1)].TrimEnd();
                return true;
            }
            if (string.Equals(args, flag, StringComparison.Ordinal))
            {
                args = string.Empty;
                return true;
            }
            return false;
        }

        internal static string AddCultureExt(string ext) => string.Equals(_currentCultureName, "en", StringComparison.Ordinal) ?
                $".{ext}" :
                $".{_currentCultureName}.{ext}";

        static Settings GetSettings()
        {
                Settings settings = new(); 
                settings.Math.Decimals = 6;
                XmlSerializer writer = new(settings.GetType());
                var path = OperatingSystem.IsWindows() ?
                    AppPath:
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"{_dirSeparator}.config{_dirSeparator}calcpad{_dirSeparator}";

                var fileName = path + "Settings.xml";
                FileStream fileStream = null;
                try
                {
                    if (Path.Exists(fileName))
                    {
                        fileStream = File.OpenRead(fileName);
                        settings = (Settings)writer.Deserialize(fileStream);
                    }
                    else if(Path.Exists(path))
                    {
                        fileStream = File.Create(fileName);
                        writer.Serialize(fileStream, settings);
                    }
                }
            catch (Exception ex)
            {
                fileStream?.Close();
                var key = WriteErrorAndWait(ex.Message, Messages.WouldYouLikeToRestoreThePreviousSettingsYN);
                if (key.Key == ConsoleKey.Y)
                    TryRestoreSettings(settings, writer, path);
            }
            finally
            {
                fileStream?.Close();
            }
            return settings;
        }

        private static void TryRestoreSettings(Settings settings, XmlSerializer writer, string path)
        {
            try
            {
                if (Path.Exists(path))
                {
                    FileStream file = File.OpenWrite(path);
                    writer.Serialize(file, settings);
                    file.Close();
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                WriteErrorAndWait(ex.Message);
            }
        }

        static void RenderFile(string path)
        {
            try
            {
                Console.Write(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);    
            }
            Console.WriteLine();
        }

        static bool TryConvertOnStartup(Settings settings)
        {
            var args = Environment.GetCommandLineArgs();
            var n = args.Length;
            if (n <= 1)
                return false;

            var fileName = string.Join(" ", args, 1, n - 1).Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (OperatingSystem.IsWindows())
                fileName = fileName.ToLower();

            // UTF-8 en la consola: en modo conversión los mensajes salen ANTES de que Main
            // fije la codificación, y los ✓/⚠ se veían como '?'. UTF-8 (no UTF-16) para
            // que además funcione con la salida redirigida a un archivo o a otro proceso.
            try { Console.OutputEncoding = Encoding.UTF8; } catch { /* consola sin soporte */ }

            // Calcpad Lab es MATLAB-only: SÓLO acepta .m. Cualquier otra extensión es error.
            int extLen = 2; // ".m"
            int i = fileName.IndexOf(".m ", StringComparison.OrdinalIgnoreCase);
            if (i < 0 && fileName.EndsWith(".m", StringComparison.OrdinalIgnoreCase))
                i = fileName.Length - 2;
            if (i < 0)
            {
                WriteErrorAndWait("Calcpad Lab solo procesa scripts MATLAB (.m). Recibido: " + fileName);
                return true;
            }
            i += extLen;
            var outFile = fileName[i..].Trim();
            // Parse trailing flags en cualquier orden: -s (silent) y --legacy (forzar
            // ExpressionParser viejo solo para debug; nadie deberia usarlo).
            // DEFAULT para .m: SIEMPRE MatlabPipeline puro — sin traduccion a Calcpad.
            bool isSilent = false;
            bool isPureMatlab = true;   // DEFAULT: motor MATLAB puro
            bool isStreamDebug = false; // --stream-debug: imprime chunks via StatementCompleted
            bool isTaskRun = false;     // --task-run: ejecuta pipeline en Task.Run (replica WPF)
            bool wantPng = false;       // --png: renderiza el HTML headless -> PNG + .errores.txt
            bool wantLatex = false;     // --latex/--tex: exporta .tex (ecuaciones LaTeX + PNG)
            while (true)
            {
                if (StripFlag(ref outFile, "--png")) { wantPng = true; continue; }
                if (StripFlag(ref outFile, "--latex")) { wantLatex = true; continue; }
                if (StripFlag(ref outFile, "--tex")) { wantLatex = true; continue; }
                if (StripFlag(ref outFile, "--task-run")) { isTaskRun = true; continue; }
                if (StripFlag(ref outFile, "--stream-debug")) { isStreamDebug = true; continue; }
                if (StripFlag(ref outFile, "-s")) { isSilent = true; continue; }
                if (StripFlag(ref outFile, "--pure")) { isPureMatlab = true; continue; }
                if (StripFlag(ref outFile, "-p")) { isPureMatlab = true; continue; }
                if (StripFlag(ref outFile, "--legacy")) { isPureMatlab = false; continue; }
                break;
            }

            fileName = fileName[..i].Trim();
            if (!File.Exists(fileName))
            {
                WriteErrorAndWait(Messages.InputFileDoesNotExist);
                return true;
            }

            if (string.IsNullOrWhiteSpace(outFile))
                outFile = Path.ChangeExtension(fileName, ".html");
            else if (Directory.Exists(outFile))
                outFile += Path.GetFileNameWithoutExtension(fileName) + ".html";
            else if (string.Equals(outFile, "html") ||
                     string.Equals(outFile, "htm") ||
                     string.Equals(outFile, "docx") ||
                     string.Equals(outFile, "pdf") ||
                     string.Equals(outFile, "tex") ||
                     string.Equals(outFile, "txt"))
                outFile = Path.ChangeExtension(fileName, "." + outFile);

            // --latex fuerza la extension .tex; y pedir un `.tex` de salida implica --latex.
            if (wantLatex && !string.Equals(Path.GetExtension(outFile), ".tex", StringComparison.OrdinalIgnoreCase))
                outFile = Path.ChangeExtension(outFile, ".tex");

            var ext = Path.GetExtension(outFile);
            if (string.Equals(ext, ".tex", StringComparison.OrdinalIgnoreCase))
                wantLatex = true;
            bool isTxt = ext == ".txt";
            // Modo TXT → gráficas a PNG (sin navegador): activar la captura antes de correr.
            if (isTxt)
            {
                Calcpad.Core.Matlab.MatlabPipeline.PngExportMode = true;
                Calcpad.Core.Matlab.MatlabPipeline.ExportedPngs.Clear();
            }
            try
            {
                // Resolve to absolute paths BEFORE changing cwd; otherwise a
                // relative fileName like "Examples/x.cpd" gets re-prefixed
                // when the cwd changes to its containing folder, producing
                // "Examples/Examples/x.cpd" and failing.
                var absFileName = Path.GetFullPath(fileName);
                var absOutFile = Path.GetFullPath(outFile);
                var path = Path.GetDirectoryName(absFileName);
                if (!string.IsNullOrWhiteSpace(path))
                    Directory.SetCurrentDirectory(path);
                fileName = absFileName;
                outFile = absOutFile;

                // Calcpad Lab: solo MATLAB. Leer el archivo directamente.
                // MATLAB usa radianes por default.
                if (settings.Math.Degrees == 0)
                    settings.Math.Degrees = 1; // rad
                string unwrappedCode = File.ReadAllText(fileName);
                // MATLAB path resolution: auto-incluir function-files de la misma carpeta.
                unwrappedCode = MatlabFolderLoader.Load(unwrappedCode, fileName);

                // ─── EXPORT LaTeX (--latex / --tex / salida .tex) ───
                // El motor ya lo tiene (MatlabLatexWriter): texto + ecuaciones LaTeX reales
                // y las figuras como PNG externos junto al .tex. Headless: sin navegador.
                if (wantLatex)
                {
                    ExportLatex(unwrappedCode, fileName, outFile, isSilent);
                    return true;
                }

                string htmlResult;
                // Con --png no se abre el navegador: la salida visual es el PNG.
                Converter converter = new(isSilent || wantPng);
                if (isPureMatlab)
                {
                    // ─── PIPELINE MATLAB-PURO (sin ExpressionParser/MathParser de Calcpad) ───
                    // Tokenizer + Parser + Evaluator + HtmlWriter propios. Sólo se reutiliza
                    // el CSS template de Calcpad (clases matrix/tr/td/var/eq/b).
                    var pipeline = new Calcpad.Core.Matlab.MatlabPipeline();
                    // Auto-run de archivo-función: si el .m es solo funciones, invocar la
                    // primaria (la que se llama igual que el archivo), como MATLAB al pulsar Run.
                    pipeline.EntryFunctionHint = Path.GetFileNameWithoutExtension(fileName);
                    if (isStreamDebug)
                    {
                        pipeline.StreamingMode = true;
                        int chunkNum = 0;
                        pipeline.StatementCompleted += (line, html) =>
                        {
                            chunkNum++;
                            Console.Error.WriteLine($"[STREAM CHUNK #{chunkNum} line={line}] {html.TrimEnd()}");
                        };
                    }
                    string html; string err; int errLine;
                    if (isTaskRun)
                    {
                        // --task-run: replicar WPF (Task.Run en ThreadPool)
                        Console.Error.WriteLine("[--task-run] running pipeline inside Task.Run...");
                        var task = System.Threading.Tasks.Task.Run(() => pipeline.RunLine(unwrappedCode));
                        var r = task.GetAwaiter().GetResult();
                        html = r.Html; err = r.Error; errLine = r.ErrorLine;
                        Console.Error.WriteLine($"[--task-run] returned: htmlLen={html?.Length ?? 0}, err={err}");
                    }
                    else
                    {
                        var r = pipeline.RunLine(unwrappedCode);
                        html = r.Html; err = r.Error; errLine = r.ErrorLine;
                    }
                    if (err != null)
                        htmlResult = $"<p class=\"err\">Error on line {errLine}: {System.Net.WebUtility.HtmlEncode(err)}</p>";
                    else
                        htmlResult = html;
                }
                else
                {
                    // MATLAB pre-processing: %% headings, ; suppression, for/end → #for/#loop,
                    // function out = fn(args) → #function fn(args), `,` dentro de paréntesis → `;`,
                    // notación científica 25e6 → (25*10^6), delaunay/trimesh reservados, etc.
                    unwrappedCode = MatlabPreprocessor.Process(unwrappedCode);
                    ExpressionParser parser = new() { Settings = settings };
                    parser.Parse(unwrappedCode, true, ext == ".docx");
                    htmlResult = parser.HtmlResult;
                }
                if (ext == ".html" || ext == ".htm")
                    converter.ToHtml(htmlResult, outFile);
                else if (ext == ".docx" && !isPureMatlab)
                    converter.ToOpenXml(htmlResult, outFile, new List<string>());  // pure mode: sin OpenXml por ahora
                else if (ext == ".pdf")
                    converter.ToPdf(htmlResult, outFile);
                else if (isTxt)
                {
                    // Export TEXTO PLANO (Unicode) — sin HTML, sin navegador. Numerico y
                    // ecuaciones en Unicode; los errores se incluyen (van dentro de htmlResult).
                    File.WriteAllText(outFile, HtmlToPlainText(htmlResult), new System.Text.UTF8Encoding(false));
                    // Graficas → PNG (SkiaSharp, sin navegador). 1 figura → base.png; varias → base_1.png…
                    Calcpad.Core.Matlab.MatlabPipeline.PngExportMode = false;
                    var pngs = Calcpad.Core.Matlab.MatlabPipeline.ExportedPngs;
                    string baseNoExt = Path.Combine(Path.GetDirectoryName(outFile) ?? ".", Path.GetFileNameWithoutExtension(outFile));
                    for (int pi = 0; pi < pngs.Count; pi++)
                    {
                        string pngPath = pngs.Count == 1 ? baseNoExt + ".png" : $"{baseNoExt}_{pi + 1}.png";
                        File.WriteAllBytes(pngPath, pngs[pi]);
                    }
                    if (pngs.Count > 0 && !isSilent) Console.WriteLine($"✓ {pngs.Count} PNG generado(s)");
                }
                else
                    WriteErrorAndWait(Messages.InvalidOutputExtensionMustBeHtmlDocxOrPdf);

                // ── Render headless a PNG (--png) ──
                // Chromium (Playwright) abre el HTML, deja dibujar el JS y captura lo que se
                // VE de verdad + la consola. Es la unica forma de aprobar un reporte con
                // graficas desde el CLI: el HTML crudo no dice si el JS dibujo o no.
                if (wantPng && !isTxt && File.Exists(outFile))
                {
                    Console.WriteLine($"✓ Reporte generado: {outFile}");
                    RenderPngHeadless(outFile);
                }
                // ── Abrir el reporte en el navegador ──
                // NUNCA para .txt (texto plano, sin navegador). Tampoco con -s (silencioso).
                else if (!isSilent && !isTxt && File.Exists(outFile))
                {
                    Console.WriteLine($"✓ Reporte generado: {outFile}");
                    OpenInBrowser(outFile);
                }
                else if (isTxt && !isSilent && File.Exists(outFile))
                    Console.WriteLine($"✓ TXT generado: {outFile}");

                return true;
            }
            catch (Exception ex) 
            {
                WriteErrorAndWait(ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Exporta el `.m` a un `.tex` (texto + ecuaciones LaTeX reales + figuras como PNG
        /// externos junto al `.tex`). Reusa el motor: MatlabPipeline.RunLatex →
        /// MatlabLatexWriter. Headless — no necesita navegador ni WebView2.
        /// </summary>
        private static void ExportLatex(string source, string mFile, string texPath, bool isSilent)
        {
            var pipeline = new Calcpad.Core.Matlab.MatlabPipeline
            {
                // Auto-run de archivo-función, igual que en el modo HTML.
                EntryFunctionHint = Path.GetFileNameWithoutExtension(mFile)
            };
            var scriptDir = Path.GetDirectoryName(Path.GetFullPath(mFile));
            if (!string.IsNullOrEmpty(scriptDir))
                pipeline.SetScriptDirectory(scriptDir, mFile);

            var written = pipeline.RunLatex(source, texPath);
            if (isSilent)
                return;

            Console.WriteLine($"✓ LaTeX generado: {written}");
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(written));
                var pngs = Directory.GetFiles(dir ?? ".", Path.GetFileNameWithoutExtension(written) + "_img*.png");
                if (pngs.Length > 0)
                    Console.WriteLine($"  {pngs.Length} figura(s) PNG junto al .tex");
            }
            catch { /* el .tex ya está escrito; el conteo de figuras es informativo */ }
        }

        /// <summary>
        /// Renderiza el HTML generado en Chromium headless (Playwright, vía
        /// <c>render_html.py</c>) y deja `&lt;salida&gt;.png` + `&lt;salida&gt;.errores.txt`.
        /// Es lo que le falta al CLI frente al WPF (WebView2): ver si el JS dibujó de
        /// verdad las gráficas/ecuaciones y qué errores soltó la consola.
        /// </summary>
        private static void RenderPngHeadless(string htmlFile)
        {
            var script = FindRenderScript();
            if (script is null)
            {
                WriteError("⚠  --png: no encuentro render_html.py (busqué en HEKATAN_RENDER_HTML, " +
                           $"{AppPath}, {AppPath}doc y %USERPROFILE%\\.claude).", true);
                return;
            }
            var png = Path.ChangeExtension(htmlFile, ".png");
            var errLog = Path.ChangeExtension(png, ".errores.txt");
            foreach (var python in new[] { "python", "py", "python3" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = python,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };
                    psi.ArgumentList.Add(script);
                    psi.ArgumentList.Add(htmlFile);
                    psi.ArgumentList.Add(png);
                    using var proc = Process.Start(psi);
                    if (proc is null)
                        continue;

                    var stdout = proc.StandardOutput.ReadToEnd();
                    var stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        WriteError($"⚠  --png: {python} falló (código {proc.ExitCode}).", true);
                        if (!string.IsNullOrWhiteSpace(stderr))
                            WriteError(stderr.TrimEnd(), true);
                        if (stderr.Contains("playwright", StringComparison.OrdinalIgnoreCase))
                            Console.WriteLine("   Instalar:  pip install playwright  &&  python -m playwright install chromium");
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Console.Write(stdout);

                    ReportRenderErrors(png, errLog);
                    return;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // ese intérprete no está en el PATH — probar el siguiente
                }
                catch (Exception ex)
                {
                    WriteError($"⚠  --png: {ex.Message}", true);
                    return;
                }
            }
            WriteError("⚠  --png: no encontré Python en el PATH (probé python, py, python3).", true);
        }

        /// <summary>Veredicto corto: ¿salió el PNG? ¿la consola tiró errores?</summary>
        private static void ReportRenderErrors(string png, string errLog)
        {
            if (!File.Exists(png))
            {
                WriteError($"⚠  --png: no se generó {png}", true);
                return;
            }
            var errors = 0;
            if (File.Exists(errLog))
            {
                foreach (var line in File.ReadLines(errLog))
                {
                    if (line.StartsWith("[error", StringComparison.Ordinal) ||
                        line.StartsWith("[pageerror", StringComparison.Ordinal) ||
                        line.StartsWith("[reqfail", StringComparison.Ordinal))
                        ++errors;
                }
            }
            if (errors > 0)
                WriteError($"⚠  {errors} error(es) de consola/JS — revisar {errLog}", true);
            else
                Console.WriteLine("✓ Render sin errores de consola/JS.");
        }

        /// <summary>Localiza render_html.py: variable de entorno → junto al .exe → doc\ → ~/.claude.</summary>
        private static string FindRenderScript()
        {
            var candidates = new List<string>();
            var fromEnv = Environment.GetEnvironmentVariable("HEKATAN_RENDER_HTML");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                candidates.Add(fromEnv);

            candidates.Add($"{AppPath}render_html.py");
            candidates.Add($"{AppPath}doc{_dirSeparator}render_html.py");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
                candidates.Add($"{home}{_dirSeparator}.claude{_dirSeparator}render_html.py");

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }
            return null;
        }

        // Sub/super-índices a Unicode (para ecuaciones en TXT sin HTML).
        private static readonly System.Collections.Generic.Dictionary<char, char> _subMap = new()
        { ['0']='₀',['1']='₁',['2']='₂',['3']='₃',['4']='₄',['5']='₅',['6']='₆',['7']='₇',['8']='₈',['9']='₉',
          ['+']='₊',['-']='₋',['=']='₌',['(']='₍',[')']='₎',['a']='ₐ',['e']='ₑ',['i']='ᵢ',['j']='ⱼ',['n']='ₙ',['x']='ₓ' };
        private static readonly System.Collections.Generic.Dictionary<char, char> _supMap = new()
        { ['0']='⁰',['1']='¹',['2']='²',['3']='³',['4']='⁴',['5']='⁵',['6']='⁶',['7']='⁷',['8']='⁸',['9']='⁹',
          ['+']='⁺',['-']='⁻',['=']='⁼',['(']='⁽',[')']='⁾',['n']='ⁿ',['i']='ⁱ' };

        private static string MapScript(string s, System.Collections.Generic.Dictionary<char, char> map, char fallbackPrefix)
        {
            var sb = new System.Text.StringBuilder();
            bool allMapped = true;
            foreach (var c in s) if (!map.ContainsKey(c)) { allMapped = false; break; }
            if (allMapped) { foreach (var c in s) sb.Append(map[c]); return sb.ToString(); }
            return fallbackPrefix + s;   // p.ej. x_max → x_max, y^2n → y^2n (no todo mapeable)
        }

        /// <summary>Convierte el HTML del reporte a TEXTO PLANO Unicode: sin markup, sin
        /// navegador. Preserva saltos de linea/tablas, pasa sub/sup a Unicode y decodifica
        /// entidades. Es el modo de salida numerico/ecuaciones que pidio Jorge (no HTML).</summary>
        private static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var s = html;
            // Fuera el JS/CSS embebido (Plotly, MathJax… pesan MBs y no son texto)
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<script\b[^>]*>.*?</script>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<style\b[^>]*>.*?</style>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // sub/sup → Unicode
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<sub>(.*?)</sub>",
                m => MapScript(System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, "<[^>]+>", ""), _subMap, '_'),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<sup>(.*?)</sup>",
                m => MapScript(System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, "<[^>]+>", ""), _supMap, '^'),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // saltos de linea a partir de la estructura
            s = System.Text.RegularExpressions.Regex.Replace(s, @"</(p|div|tr|h[1-6]|li)>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"</(td|th)>", "  ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // quitar el resto de tags
            s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");
            s = System.Net.WebUtility.HtmlDecode(s);
            // colapsar espacios y lineas en blanco excesivas
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[ \t]+", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, @" *\n *", "\n");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim() + "\n";
        }

        /// <summary>
        /// Abre el archivo generado (HTML / PDF) en el navegador o aplicación
        /// default del sistema operativo. Usa <c>UseShellExecute = true</c>
        /// que invoca al handler asociado en Windows/macOS/Linux.
        /// </summary>
        private static void OpenInBrowser(string filePath)
        {
            try
            {
                var absPath = Path.GetFullPath(filePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = absPath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                // No fallar si el navegador no abre — al menos avisamos.
                Console.WriteLine($"⚠  No se pudo abrir el navegador: {ex.Message}");
                Console.WriteLine($"   Abrir manualmente: {filePath}");
            }
        }

        private static ConsoleKeyInfo WriteErrorAndWait(string message, string prompt = null)
        {
            WriteError(message, true);
            prompt ??= Messages.PressAnyKeyToContinue;
            Console.WriteLine(prompt);
            // Skip ReadKey when stdin is redirected (batch / piped invocation)
            // — otherwise InvalidOperationException kills batch runs.
            if (Console.IsInputRedirected)
                return default;
            try { return Console.ReadKey(); }
            catch (InvalidOperationException) { return default; }
        }

        static string TryOpenOnStartup(List<Line> Lines)
        {
            var args = Environment.GetCommandLineArgs();
            var n = args.Length;
            if (n > 1)
            {
                var fileName = string.Join(" ", args, 1, n - 1); //.ToLower(); cannot be used in linux due to case sensitive file system
            
                if (OperatingSystem.IsWindows())
                {
                    fileName = fileName.ToLower();
                }
                
                if (File.Exists(fileName))
                {
                    if (Path.GetExtension(fileName) == ".cpc")
                    {
                        Lines.Clear();
                        using (StreamReader sr = new(fileName))
                            while (!sr.EndOfStream)
                                Lines.Add(new Line(sr.ReadLine()));

                        return Path.GetFileNameWithoutExtension(fileName);
                    }
                }
            }
            return string.Empty;
        }

        static void Header(string Title, int drg)
        {
            Console.Clear();
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine(new string('—', _width));
            Console.WriteLine(string.Format(Messages.Welcome_To_Calcpad_Command_Line_Interpreter, ver.Major, ver.Minor, ver.Build));
            Console.WriteLine(Messages.Copyright_2023_By_Proektsoft_EOOD);
            Console.Write($"\r\n {Messages.Commands}: NEW OPEN SAVE LIST EXIT RESET CLS DEL ");
            switch (drg)
            {
                case 0:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("DEG ");
                    Console.ResetColor();
                    Console.Write("RAD ");
                    Console.Write("GRA ");
                    break;
                case 1:
                    Console.Write("DEG ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("RAD ");
                    Console.ResetColor();
                    Console.Write("GRA ");
                    break;
                default:
                    Console.Write("DEG ");
                    Console.Write("RAD ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("GRA ");
                    Console.ResetColor();
                    break;
            }
            Console.Write("SETTINGS LICENSE HELP\r\n");
            Console.WriteLine(new string('—', _width));
            if (Title.Length > 0)
                Console.WriteLine(" " + Title + ":\n");
            else
                Console.WriteLine($" {Messages.Enter_Math_Expressions_Or_Commands_Or_Type_HELP_For_Further_Instructions}:\n");
        }

        static bool Calculate(MathParser mp, string Prompt, ref Line L)
        {
            try
            {
                var Buffer = GetVariables(Prompt, L.Input);
                var Tokens = Buffer.Split('\'');
                L.Output = string.Empty;
                for (int i = 0; i < Tokens.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        if (Tokens[i].Length > 0)
                        {
                            var s = Tokens[i]
                                .Replace(" ", "")
                                .Replace("==", "≡")
                                .Replace("!=", "≠")
                                .Replace("<=", "≤")
                                .Replace(">=", "≥")
                                .Replace("||", "∨")
                                .Replace("&&", "∧")
                                .Replace("%%", "⦼");
                            mp.Parse(s);
                            mp.Calculate();
                            L.Output += mp.ToString().Trim() + ' ';
                        }
                    }
                    else
                        L.Output += Tokens[i].Trim() + ' ';
                }
                var Output = Prompt + L.Output.PadRight(_width - 8);
                Console.WriteLine(Output);
                mp.SaveAnswer();
                return true;
            }
            catch (Exception ex)
            {
                WriteError($"{Prompt + L.Input} {Messages.Error}: {ex.Message}", true);
                return false;
            }
        }

        static void Render(MathParser mp, List<Line> Lines, bool Reset)
        {
            if (Reset)
                mp.ClearCustomUnits();

            for (int i = 0; i < Lines.Count; i++)
            {
                var LineNo = (i + 1).ToString().PadLeft(3) + Prompt;
                if (Reset)
                {
                    Line L = Lines[i];
                    Calculate(mp, LineNo, ref L);
                    Lines[i] = L;
                }
                else
                    Console.WriteLine(LineNo + Lines[i].Output);

            }
        }

        static string GetVariables(string Prompt, string Input)
        {
            var i = 0;
            while (i >= 0)
            {
                i = Input.IndexOf('?');
                if (i >= 0)
                {
                    Console.Write(Prompt + Input[..i].Replace("\'", string.Empty));
                    var Variable = Console.ReadLine();
                    Input = Input[..i] + Variable + Input[(i + 1)..];
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
            }
            return Input;
        }

        static string Open(string Prompt, List<Line> Lines)
        {
            var FilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"{_dirSeparator}cpc";
            if (!Directory.Exists(FilePath))
            {
                WriteError($"{Prompt}OPEN {Messages.There_Are_No_Saved_Problems}\r\n", false);
                return null;
            }
            Console.Write($"{Prompt}OPEN {Messages.Problem_Title} ");
            var Title = Console.ReadLine();
            var FileName = FilePath + _dirSeparator + Title + ".cpc";
            if (File.Exists(FileName))
            {
                Lines.Clear();
                using StreamReader sr = new(FileName);
                while (!sr.EndOfStream)
                    Lines.Add(new Line(sr.ReadLine()));

                return Title;
            }
            else
            {
                WriteError(Prompt + string.Format(Messages.Problem_0_Does_Not_Exist, Title), true);
                return null;
            }
        }

        static string Save(string Title, string Prompt, List<Line> Lines)
        {
            var FilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"{_dirSeparator}cpc";
            if (!Directory.Exists(FilePath))
                Directory.CreateDirectory(FilePath);

            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Prompt += "SAVE" + Messages.Problem_Title;
            if (Title.Length > 0 )
                Prompt += $" ({Title}): ";
            else
                Prompt += ": ";
            Console.Write(Prompt);
            var NewTitle = Console.ReadLine();
            if (NewTitle.Length == 0)
                NewTitle = Title;

            if (NewTitle.Length > 0)
            {
                var FileName = FilePath + _dirSeparator + NewTitle + ".cpc";
                using StreamWriter sw = new(FileName);
                foreach (Line L in Lines)
                    sw.WriteLine(L.Input);
            }
            return NewTitle;
        }

        static void List(string Prompt)
        {
            string FilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"{_dirSeparator}cpc";
            if (!Directory.Exists(FilePath))
            {
                WriteError(Prompt + Messages.There_Are_No_Saved_Problems, true);
                return;
            }
            List<string> Lines = Directory.EnumerateFiles(FilePath).ToList();
            foreach (string s in Lines)
                Console.WriteLine(Path.GetFileNameWithoutExtension(s));

            Console.WriteLine();
        }

        private static void WriteError(string message, bool line)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (line)
                Console.WriteLine(message);
            else
                Console.Write(message);

            Console.ResetColor();
        }
        private static bool Execute(string fileName, string args = "")
        {
            var proc = new Process();
            var psi = new ProcessStartInfo
            {
                UseShellExecute = OperatingSystem.IsWindows(),
                FileName = fileName,
                Arguments = args,
                Verb = "runas"
            };
            proc.StartInfo = psi;
            try
            {
                Console.WriteLine(Messages.Loading_The_Settings_File);
                var result = proc.Start();
                proc.WaitForExit();
                return result;
            }
            catch (Exception Ex)
            {
                WriteError(Ex.Message, true);
                return false;
            }
        }
    }
}
