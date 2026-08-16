using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Calcpad.Wpf
{
    /// <summary>
    /// EL EDITOR DE CODIGO DE HEKATAN LAB, sobre AvalonEdit.
    ///
    /// El RichTextBox de siempre SIGUE AHI, oculto, y se le escribe con
    /// <c>SetInputText</c> en cada tecla. Asi todo lo demas de la app —autorun, calcular,
    /// guardar, los botones de insertar, el teclado, MathCanvas— funciona igual que
    /// siempre, sin tocar sus 158 llamadas al RichTextBox. Es el mismo patron que ya usa
    /// MathCanvas para ser un segundo editor (OnMathCanvasTextChanged/_syncingFromMathCanvas).
    ///
    /// Lo que AvalonEdit aporta: CARPETAS PLEGABLES (+/-) en %% , function, for, if,
    /// while, switch, try... como MATLAB. Ver <see cref="MatlabFoldingStrategy"/>.
    ///
    /// La casilla "Plegado" alterna entre este editor y el clasico, para compararlos.
    /// </summary>
    public partial class MainWindow
    {
        private FoldingManager _foldingManager;
        private CompletionWindow _avalonCompletion;
        private bool _desdeAvalon;      // el cambio de texto lo origino AvalonEdit
        private bool _haciaAvalon;      // estamos escribiendo EN AvalonEdit desde la app
        private bool _avalonListo;

        /// <summary>Llamar una vez, cuando la ventana ya cargo.</summary>
        private void PrepararAvalon()
        {
            if (_avalonListo) return;
            _avalonListo = true;

            CargarResaltadoMatlab();
            _foldingManager = FoldingManager.Install(AvalonEditor.TextArea);

            AvalonEditor.TextChanged += AvalonEditor_TextChanged;
            AvalonEditor.TextArea.TextEntered += AvalonEditor_TextEntered;
            AvalonEditor.PreviewKeyDown += AvalonEditor_PreviewKeyDown;

            AplicarModoEditor();
            SincronizarHaciaAvalon();

            // --plegar: arrancar con todo plegado (para capturar el PNG del plegado cerrado).
            // Con retardo: el archivo entra al editor DESPUES, al final de GetInputTextFromFile.
            if (Environment.GetCommandLineArgs().Any(a => a == "--plegar"))
            {
                var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(900) };
                t.Tick += (_, _) => { t.Stop(); PlegarTodo_Click(null, null); };
                t.Start();
            }

            PrepararCapturaAutocompletado();
        }

        /// <summary>
        /// <c>--completar &lt;prefijo&gt; [--cshot &lt;png&gt;]</c>: escribe el prefijo al final del
        /// codigo y abre el popup de autocompletado, para poder REVISARLO en un PNG.
        ///
        /// Por que no vale <c>--wshot</c>: el popup de AvalonEdit es OTRA ventana, y --wshot
        /// dibuja solo esta (RenderTargetBitmap/PrintWindow). Para que salga el popup hay que
        /// copiar de la PANTALLA, que es lo que hace <see cref="CapturarPantalla"/>.
        /// </summary>
        private void PrepararCapturaAutocompletado()
        {
            var args = Environment.GetCommandLineArgs();
            var iPre = Array.IndexOf(args, "--completar");
            if (iPre < 0 || iPre + 1 >= args.Length) return;
            var prefijo = args[iPre + 1];
            var iPng = Array.IndexOf(args, "--cshot");
            var png = iPng >= 0 && iPng + 1 < args.Length ? args[iPng + 1] : null;

            var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(1400) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                try
                {
                    WindowState = WindowState.Normal;
                    AvalonEditor.Focus();
                    AvalonEditor.AppendText("\n" + prefijo);
                    AvalonEditor.CaretOffset = AvalonEditor.Document.TextLength;
                    MostrarAutocompletado(prefijo);
                    // --aceptar: ademas, mete el item seleccionado (para ver el SNIPPET ya
                    // insertado con sus huecos, que es lo que hace Tab).
                    if (args.Any(a => a == "--aceptar"))
                        _avalonCompletion?.CompletionList.RequestInsertion(EventArgs.Empty);
                }
                catch { }

                if (png is null) return;
                var t2 = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(900) };
                t2.Tick += (_, _) =>
                {
                    t2.Stop();
                    CapturarPantalla(png);
                    // Salida DURA: Shutdown() dispara el "File not saved. Save?" (el snippet
                    // ensucio el documento) y en headless ese dialogo se queda ahi para siempre.
                    Environment.Exit(0);
                };
                t2.Start();
            };
            t.Start();
        }

        /// <summary>Dibuja esta ventana Y las ventanas hijas que tenga encima (el popup del
        /// autocompletado), cada una por su HANDLE con PrintWindow, y las pega en su sitio.
        ///
        /// Por que no <c>CopyFromScreen</c>: copiar de la pantalla trae lo que este delante en
        /// el monitor (otra terminal, el navegador...). Por handle sale SIEMPRE la app, aunque
        /// este tapada.</summary>
        private void CapturarPantalla(string ruta)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
                if (!GetWindowRect(hwnd, out var r)) return;
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w <= 0 || h <= 0) return;

                using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    Pintar(g, hwnd, 0, 0, w, h);

                    foreach (Window otra in Application.Current.Windows)
                    {
                        if (ReferenceEquals(otra, this) || !otra.IsVisible) continue;
                        var h2 = new System.Windows.Interop.WindowInteropHelper(otra).Handle;
                        if (h2 == IntPtr.Zero || !GetWindowRect(h2, out var r2)) continue;
                        Pintar(g, h2, r2.Left - r.Left, r2.Top - r.Top, r2.Right - r2.Left, r2.Bottom - r2.Top);
                    }
                }

                var dir = System.IO.Path.GetDirectoryName(ruta);
                if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
                bmp.Save(ruta, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch { }

            static void Pintar(System.Drawing.Graphics destino, IntPtr hwnd, int x, int y, int w, int h)
            {
                if (w <= 0 || h <= 0) return;
                using var trozo = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(trozo))
                {
                    var hdc = g.GetHdc();
                    try { PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT); }
                    finally { g.ReleaseHdc(hdc); }
                }
                destino.DrawImage(trozo, x, y);
            }
        }

        // ---------- alternar editor plegable / clasico ----------

        private bool EditorPlegableActivo => EditorPlegableChk?.IsChecked == true;

        private void EditorPlegable_Changed(object sender, RoutedEventArgs e)
        {
            if (!_avalonListo) return;
            AplicarModoEditor();
            if (EditorPlegableActivo) SincronizarHaciaAvalon();
        }

        private void AplicarModoEditor()
        {
            var plegable = EditorPlegableActivo;
            AvalonEditor.Visibility = plegable ? Visibility.Visible : Visibility.Collapsed;
            PlegarTodoBtn.Visibility = plegable ? Visibility.Visible : Visibility.Collapsed;
            DesplegarTodoBtn.Visibility = plegable ? Visibility.Visible : Visibility.Collapsed;
            // El canal de numeros de linea del Lab estorba: AvalonEdit trae el suyo.
            LineNumbers.Visibility = plegable ? Visibility.Collapsed : Visibility.Visible;
            LineNumbersBorder.Visibility = plegable ? Visibility.Collapsed : Visibility.Visible;
            if (plegable) AvalonEditor.Focus();
        }

        // ---------- sincronizacion en los dos sentidos ----------

        /// <summary>AvalonEdit -> RichTextBox oculto. Dispara la cadena normal de la app
        /// (RichTextBox_TextChanged: autorun, resaltado, guardado sucio...).</summary>
        private void AvalonEditor_TextChanged(object sender, EventArgs e)
        {
            if (_haciaAvalon || !EditorPlegableActivo) return;
            _desdeAvalon = true;
            try { SetInputText(AvalonEditor.Text); }
            catch { }
            finally { _desdeAvalon = false; }
            ActualizarPlegado();
        }

        /// <summary>RichTextBox -> AvalonEdit. Se llama al abrir archivo, al limpiar, y
        /// tras cualquier cambio que NO haya salido de AvalonEdit (botones de insertar,
        /// teclado de simbolos, MathCanvas).</summary>
        private void SincronizarHaciaAvalon()
        {
            if (!_avalonListo || _desdeAvalon || AvalonEditor is null) return;
            string codigo;
            try { codigo = GetInputText(); }
            catch { return; }
            if (codigo == AvalonEditor.Text) return;

            var caret = AvalonEditor.CaretOffset;
            _haciaAvalon = true;
            try
            {
                AvalonEditor.Text = codigo;
                AvalonEditor.CaretOffset = Math.Min(caret, AvalonEditor.Document.TextLength);
            }
            finally { _haciaAvalon = false; }
            ActualizarPlegado();
        }

        // ---------- plegado ----------

        /// <summary>Quien SABE donde estan los bloques es el motor
        /// (<see cref="Calcpad.Core.Matlab.MatlabBlocks"/>); aqui solo se traducen sus
        /// tramos al +/- de AvalonEdit. Asi otra piel (Avalonia) pliega identico.
        /// UpdateFoldings conserva los que ya estaban cerrados: plegar no se deshace al escribir.</summary>
        private void ActualizarPlegado()
        {
            if (_foldingManager is null) return;
            try
            {
                var tramos = Calcpad.Core.Matlab.MatlabBlocks.Find(AvalonEditor.Text);
                var pliegues = tramos
                    .Select(t => new ICSharpCode.AvalonEdit.Folding.NewFolding(t.Start, t.End) { Name = t.Label })
                    .ToList();
                _foldingManager.UpdateFoldings(pliegues, -1);
            }
            catch { /* con un bloque a medio escribir puede no cuadrar: no es critico */ }
        }

        private void PlegarTodo_Click(object sender, RoutedEventArgs e)
        {
            ActualizarPlegado();
            if (_foldingManager is null) return;
            foreach (var f in _foldingManager.AllFoldings) f.IsFolded = true;
        }

        private void DesplegarTodo_Click(object sender, RoutedEventArgs e)
        {
            if (_foldingManager is null) return;
            foreach (var f in _foldingManager.AllFoldings) f.IsFolded = false;
        }

        // ---------- resaltado ----------

        private void CargarResaltadoMatlab()
        {
            try
            {
                using var stream = typeof(MainWindow).Assembly
                    .GetManifestResourceStream("Calcpad.Wpf.Matlab.xshd");
                if (stream is null) return;
                using var reader = XmlReader.Create(stream);
                AvalonEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch { /* sin resaltado no es critico */ }
        }

        /// <summary>Re-tine el resaltado segun el tema del Lab (Oscuro / Oro).</summary>
        private void AplicarColoresAvalon(bool oscuro)
        {
            var hl = AvalonEditor?.SyntaxHighlighting;
            if (hl is null) return;

            void C(string nombre, string hexOscuro, string hexClaro)
            {
                var col = hl.GetNamedColor(nombre);
                if (col is not null)
                    col.Foreground = new SimpleHighlightingBrush(
                        (Color)ColorConverter.ConvertFromString(oscuro ? hexOscuro : hexClaro));
            }

            C("Comment",   "#5AC37E", "#0A7F3F");
            C("Section",   "#7BD79A", "#046A38");
            C("TituloDoc", "#F5C043", "#8A5A00");   // %" titulo visible en el reporte
            C("TextoDoc",  "#9DB8D2", "#31506E");   // %' texto visible en el reporte
            C("String",    "#E5C07B", "#A15C00");
            C("Number",    "#9ECBFF", "#0B66C3");
            C("Keyword",   "#C678DD", "#A626A4");
            C("Constant",  "#56B6C2", "#0B7285");
            C("Builtin",   "#61AFEF", "#1A56C4");
            AvalonEditor.TextArea.TextView.Redraw();
        }

        // ---------- autocompletado ----------

        private void AvalonEditor_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_avalonCompletion is not null) return;    // ya abierto: AvalonEdit filtra solo
            if (e.Text.Length == 1 && (char.IsLetter(e.Text[0]) || e.Text[0] == '_'))
            {
                var prefijo = PalabraActual();
                if (prefijo.Length >= 2) MostrarAutocompletado(prefijo);
            }
        }

        private void AvalonEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                MostrarAutocompletado(PalabraActual());
                e.Handled = true;
            }
        }

        private string PalabraActual()
        {
            var doc = AvalonEditor.Document;
            int caret = AvalonEditor.CaretOffset, ini = caret;
            while (ini > 0)
            {
                var c = doc.GetCharAt(ini - 1);
                if (char.IsLetterOrDigit(c) || c == '_') ini--;
                else break;
            }
            return doc.GetText(ini, caret - ini);
        }

        private void MostrarAutocompletado(string prefijo)
        {
            var items = MatlabLang.Items(prefijo).ToList();
            if (items.Count == 0) return;

            _avalonCompletion = new CompletionWindow(AvalonEditor.TextArea) { CloseWhenCaretAtBeginning = true };
            _avalonCompletion.StartOffset = AvalonEditor.CaretOffset - prefijo.Length;
            VestirPopup(_avalonCompletion);
            foreach (var it in items) _avalonCompletion.CompletionList.CompletionData.Add(it);
            if (!string.IsNullOrEmpty(prefijo)) _avalonCompletion.CompletionList.SelectItem(prefijo);
            _avalonCompletion.Closed += (_, _) => _avalonCompletion = null;
            _avalonCompletion.Show();
        }

        /// <summary>El popup de AvalonEdit nace BLANCO (sus colores son fijos, no heredan del
        /// tema): en el tema Oscuro daba un cuadro blanco en medio del editor negro. Se le
        /// pasan los mismos brushes del Lab, que ya cambian solos al alternar Oscuro/Oro.</summary>
        private void VestirPopup(CompletionWindow w)
        {
            try
            {
                var fondo = (System.Windows.Media.Brush)FindResource("ThemeEditorBg");
                var texto = (System.Windows.Media.Brush)FindResource("ThemeText");
                var borde = (System.Windows.Media.Brush)FindResource("ThemeButtonBorder");

                w.Background = fondo;
                w.Foreground = texto;
                w.BorderBrush = borde;
                w.CompletionList.Background = fondo;
                w.CompletionList.Foreground = texto;
                w.CompletionList.ListBox.Background = fondo;
                w.CompletionList.ListBox.Foreground = texto;
                w.CompletionList.ListBox.BorderBrush = borde;
                w.FontFamily = AvalonEditor.FontFamily;
                w.FontSize = AvalonEditor.FontSize;
            }
            catch { /* si falta un brush, el popup se queda con su look de fabrica */ }
        }
    }
}
