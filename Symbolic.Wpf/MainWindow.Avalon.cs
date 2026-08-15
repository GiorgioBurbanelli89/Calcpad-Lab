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
        private readonly MatlabFoldingStrategy _foldingStrategy = new();
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

        private void ActualizarPlegado()
        {
            if (_foldingManager is null) return;
            try { _foldingStrategy.UpdateFoldings(_foldingManager, AvalonEditor.Document); }
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
            foreach (var it in items) _avalonCompletion.CompletionList.CompletionData.Add(it);
            if (!string.IsNullOrEmpty(prefijo)) _avalonCompletion.CompletionList.SelectItem(prefijo);
            _avalonCompletion.Closed += (_, _) => _avalonCompletion = null;
            _avalonCompletion.Show();
        }
    }
}
