using System;
using ICSharpCode.AvalonEdit.Document;

namespace Calcpad.Wpf
{
    /// <summary>
    /// LOS BOTONES DE MARCADO (B, I, U, S, x₂, x², p, colores...) EN EL EDITOR PLEGABLE.
    ///
    /// Eran los ultimos que seguian escribiendo en el RichTextBox OCULTO: el texto aparecia
    /// donde estaba SU cursor, no donde el usuario ve el suyo (mismo fallo que ya se arreglo
    /// con InsertManager.Desvio para los botones de insertar).
    ///
    /// Aqui no se reusa <see cref="InsertManager"/> porque su logica es de CALCPAD: alli el
    /// comentario se abre y se CIERRA con <c>'</c> a media linea. En MATLAB el <c>%</c> llega
    /// hasta el final de la linea y no se cierra, asi que la regla es otra:
    ///
    ///   - El cursor esta DENTRO de un comentario  -> se envuelve la seleccion ahi mismo.
    ///   - La linea esta vacia                     -> se convierte en linea de documento `%'`.
    ///   - La linea tiene CODIGO                   -> no se toca: la marca baja a una linea
    ///                                                `%'` nueva debajo (envolver codigo en
    ///                                                &lt;strong&gt; lo romperia en silencio).
    ///
    /// El boton es un interruptor: si las etiquetas YA estan puestas alrededor, las quita.
    /// </summary>
    public partial class MainWindow
    {
        private const StringComparison Ign = StringComparison.OrdinalIgnoreCase;

        /// <summary>Botones con <c>‖</c> (la marca separa la etiqueta de apertura de la de
        /// cierre). Devuelve false si el editor plegable no esta activo: entonces sigue el
        /// camino clasico del RichTextBox.</summary>
        private bool MarcarEnAvalon(string tag)
        {
            if (!EditorPlegableActivo || !_avalonListo || AvalonEditor is null) return false;

            var parts = tag.Split('‖');
            var t1 = parts[0];
            var t2 = parts.Length > 1 ? parts[1] : string.Empty;

            using (AvalonEditor.Document.RunUpdate())
            {
                if (tag.StartsWith("‖#", StringComparison.Ordinal))
                    EncabezadoMarkdown(t2);
                else if (t1.StartsWith("<p>", Ign) ||
                         t1.StartsWith("<h", Ign) && !t1.Equals("<hr/>", Ign))
                    EncabezadoHtml(t1, t2);
                else
                    MarcarSeleccion(t1, t2);
            }
            AvalonEditor.Focus();
            return true;
        }

        /// <summary>Botones de varias lineas (separadas por <c>§</c>): div, listas, tabla, y
        /// las plantillas de codigo de los menus. Van a una linea nueva debajo, sin tocar la
        /// linea actual.</summary>
        private bool InsertarLineasEnAvalon(string tag)
        {
            if (!EditorPlegableActivo || !_avalonListo || AvalonEditor is null) return false;

            var doc = AvalonEditor.Document;
            var bloque = string.Join(Environment.NewLine, tag.Split('§'));
            using (doc.RunUpdate())
            {
                var linea = doc.GetLineByOffset(AvalonEditor.CaretOffset);
                var vacia = string.IsNullOrWhiteSpace(doc.GetText(linea));
                var pos = vacia ? linea.Offset : linea.EndOffset;
                var texto = vacia ? bloque : Environment.NewLine + bloque;
                if (vacia) doc.Remove(linea.Offset, linea.Length);
                doc.Insert(pos, texto);
                AvalonEditor.CaretOffset = pos + texto.Length;
            }
            AvalonEditor.Focus();
            return true;
        }

        // ---------- marcado en linea (negrita, cursiva, sub/superindice, colores) ----------

        private void MarcarSeleccion(string t1, string t2)
        {
            var doc = AvalonEditor.Document;
            var ini = AvalonEditor.SelectionStart;
            var len = AvalonEditor.SelectionLength;
            if (len == 0 && t1.Length > 0 && t2.Length > 0)
                (ini, len) = PalabraEn(AvalonEditor.CaretOffset);

            var linea = doc.GetLineByOffset(ini);
            // Una etiqueta en linea no puede cruzar lineas: se recorta a la del principio.
            if (ini + len > linea.EndOffset) len = linea.EndOffset - ini;

            if (QuitarMarcas(ini, len, t1, t2)) return;     // ya estaban: el boton las quita

            var com = InicioComentario(doc.GetText(linea));
            if (com < 0 || ini - linea.Offset <= com)       // el cursor NO esta en el comentario
            {
                NuevaLineaDoc(linea, t1, t2);
                return;
            }

            var sel = doc.GetText(ini, len);
            doc.Replace(ini, len, t1 + sel + t2);
            if (len > 0)
                AvalonEditor.Select(ini + t1.Length, len);
            else
                AvalonEditor.CaretOffset = ini + t1.Length;
        }

        /// <summary>Si la seleccion ya viene envuelta por estas etiquetas, las borra y devuelve
        /// true. Los espacios que hubiera entre la etiqueta y el texto se respetan.</summary>
        private bool QuitarMarcas(int ini, int len, string t1, string t2)
        {
            if (t1.Length == 0 && t2.Length == 0) return false;

            var doc = AvalonEditor.Document;
            var linea = doc.GetLineByOffset(ini);
            var fin = ini + len;
            var antes = doc.GetText(linea.Offset, ini - linea.Offset);
            var despues = doc.GetText(fin, Math.Max(0, linea.EndOffset - fin));
            var a = antes.TrimEnd();
            var d = despues.TrimStart();

            if (t1.Length > 0 && !a.EndsWith(t1, Ign)) return false;
            if (t2.Length > 0 && !d.StartsWith(t2, Ign)) return false;

            // Primero por detras: asi los offsets de delante siguen valiendo.
            if (t2.Length > 0)
                doc.Remove(fin + (despues.Length - d.Length), t2.Length);
            if (t1.Length > 0)
                doc.Remove(linea.Offset + a.Length - t1.Length, t1.Length);

            AvalonEditor.Select(ini - t1.Length, len);
            return true;
        }

        // ---------- marcado de linea entera (p, h1..h6, encabezados markdown) ----------

        private void EncabezadoHtml(string t1, string t2)
        {
            var doc = AvalonEditor.Document;
            var linea = doc.GetLineByOffset(AvalonEditor.CaretOffset);
            var texto = doc.GetText(linea);
            var com = InicioComentario(texto);
            if (com < 0)
            {
                NuevaLineaDoc(linea, t1, t2);
                return;
            }

            var (pre, cont) = PartirComentario(texto, com);
            string nuevo;
            if (cont.Length >= t1.Length + t2.Length &&
                cont.StartsWith(t1, Ign) && cont.EndsWith(t2, Ign))
                nuevo = cont[t1.Length..^t2.Length];                 // ya estaba: quitar
            else
                nuevo = t1 + QuitarEncabezado(cont) + t2;

            doc.Replace(linea.Offset, linea.Length, pre + nuevo);
            AvalonEditor.CaretOffset = linea.Offset + pre.Length + t1.Length;
        }

        /// <summary>Encabezado markdown (<c>#</c>, <c>##</c>...): la almohadilla manda toda la
        /// linea, asi que se cambian las que hubiera. Pulsar el mismo nivel lo quita.</summary>
        private void EncabezadoMarkdown(string almohadillas)
        {
            var doc = AvalonEditor.Document;
            var linea = doc.GetLineByOffset(AvalonEditor.CaretOffset);
            var texto = doc.GetText(linea);
            var com = InicioComentario(texto);
            if (com < 0)
            {
                NuevaLineaDoc(linea, almohadillas + " ", string.Empty);
                return;
            }

            var (pre, cont) = PartirComentario(texto, com);
            var n = 0;
            while (n < cont.Length && cont[n] == '#') ++n;
            var resto = cont[n..].TrimStart();
            var nuevo = n == almohadillas.Length ? resto : almohadillas + " " + resto;
            doc.Replace(linea.Offset, linea.Length, pre + nuevo);
            AvalonEditor.CaretOffset = linea.Offset + (pre + nuevo).Length;
        }

        /// <summary>Parte la linea en (lo que la hace comentario, el contenido). El prefijo se
        /// queda con el <c>%</c> y con el <c>'</c> o el <c>"</c> que lo hacen visible en el
        /// reporte, para no cambiar de tipo de comentario sin que el usuario lo pida.</summary>
        private static (string, string) PartirComentario(string texto, int com)
        {
            var pre = texto[..(com + 1)];
            var resto = texto[(com + 1)..];
            if (resto.Length > 0 && (resto[0] == '\'' || resto[0] == '"'))
            {
                pre += resto[0];
                resto = resto[1..];
            }
            var cont = resto.TrimStart();
            pre += resto[..(resto.Length - cont.Length)];    // los espacios se quedan en el prefijo
            return (pre, cont.TrimEnd());
        }

        /// <summary>Quita el <c>&lt;p&gt;</c> o el <c>&lt;hN&gt;</c> que ya envolviera la linea,
        /// para que los encabezados se sustituyan en vez de anidarse.</summary>
        private static string QuitarEncabezado(string s)
        {
            if (!s.StartsWith('<')) return s;
            var i = s.IndexOf('>');
            if (i < 2) return s;
            var etq = s[1..i];
            var esEncabezado = etq.Equals("p", Ign) ||
                etq.Length == 2 && char.ToLowerInvariant(etq[0]) == 'h' && char.IsDigit(etq[1]);
            if (!esEncabezado) return s;
            var cierre = $"</{etq}>";
            if (!s.EndsWith(cierre, Ign)) return s;
            return s[(i + 1)..^cierre.Length].Trim();
        }

        // ---------- utilidades ----------

        /// <summary>La linea no admite la marca (es codigo, o esta vacia): la marca vacia se
        /// pone en una linea de documento <c>%'</c>, con el cursor entre las dos etiquetas.
        /// Si la linea esta vacia se aprovecha esa; si tiene codigo, se abre una debajo.</summary>
        private void NuevaLineaDoc(DocumentLine linea, string t1, string t2)
        {
            var doc = AvalonEditor.Document;
            var marca = "%'" + t1 + t2;
            if (string.IsNullOrWhiteSpace(doc.GetText(linea)))
            {
                var pos = linea.Offset;
                doc.Replace(pos, linea.Length, marca);
                AvalonEditor.CaretOffset = pos + 2 + t1.Length;
            }
            else
            {
                var fin = linea.EndOffset;
                doc.Insert(fin, Environment.NewLine + marca);
                AvalonEditor.CaretOffset = fin + Environment.NewLine.Length + 2 + t1.Length;
            }
        }

        /// <summary>La palabra que hay bajo el cursor (inicio, largo). Sin seleccion, los
        /// botones de negrita/cursiva marcan la palabra entera, como en el editor clasico.</summary>
        private (int, int) PalabraEn(int offset)
        {
            var doc = AvalonEditor.Document;
            int ini = offset, fin = offset;
            while (ini > 0 && EsDePalabra(doc.GetCharAt(ini - 1))) --ini;
            while (fin < doc.TextLength && EsDePalabra(doc.GetCharAt(fin))) ++fin;
            return (ini, fin - ini);

            static bool EsDePalabra(char c) => char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>Donde empieza el comentario de esta linea, o -1 si es toda codigo. El
        /// <c>%</c> dentro de una cadena NO abre comentario (<c>'100 % de la carga'</c>), y la
        /// comilla que va detras de un nombre, <c>)</c>, <c>]</c> o <c>.</c> es la TRASPUESTA
        /// de MATLAB (<c>A'</c>), no una cadena. Es la misma regla del resaltado (Matlab.xshd).
        /// </summary>
        internal static int InicioComentario(string linea)
        {
            var cadena = false;
            for (var i = 0; i < linea.Length; ++i)
            {
                var c = linea[i];
                if (cadena)
                {
                    if (c != '\'') continue;
                    if (i + 1 < linea.Length && linea[i + 1] == '\'') ++i;   // '' = comilla escapada
                    else cadena = false;
                    continue;
                }
                if (c == '\'')
                {
                    var a = i > 0 ? linea[i - 1] : ' ';
                    if (!(char.IsLetterOrDigit(a) || a == '_' || a == ')' || a == ']' || a == '.'))
                        cadena = true;
                }
                else if (c == '%')
                    return i;
            }
            return -1;
        }

        /// <summary><c>--marcar &lt;tag&gt; [--enlinea N[,C]] [--doble] [--cshot &lt;png&gt;]</c>: aplica un
        /// boton de marcado, para comprobar en PNG que la marca cae en el editor que se VE.
        /// Sin <c>--enlinea</c> se anade una linea de prueba y se marca una palabra suya; con
        /// <c>--enlinea</c> se pone el cursor en esa linea y columna del archivo abierto.
        /// Con <c>--doble</c> lo pulsa dos veces: la segunda tiene que dejar la linea como
        /// estaba (el boton es un interruptor).</summary>
        private void PrepararCapturaMarcado()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "--marcar");
            if (i < 0 || i + 1 >= args.Length) return;
            var tag = args[i + 1];
            var doble = Array.IndexOf(args, "--doble") >= 0;
            var iLin = Array.IndexOf(args, "--enlinea");
            var donde = iLin >= 0 && iLin + 1 < args.Length ? args[iLin + 1] : null;
            var iPng = Array.IndexOf(args, "--cshot");
            var png = iPng >= 0 && iPng + 1 < args.Length ? args[iPng + 1] : null;

            TrasUnRato(1400, () =>
            {
                try
                {
                    AvalonEditor.Focus();
                    var doc = AvalonEditor.Document;
                    if (donde is null)
                    {
                        var texto = Environment.NewLine + "%' marca de prueba";
                        doc.Insert(doc.TextLength, texto);
                        // El cursor, sobre la palabra "prueba" (sin seleccionarla: asi tambien se
                        // comprueba que el boton coge la palabra entera el solo).
                        AvalonEditor.CaretOffset = doc.TextLength - 3;
                    }
                    else
                    {
                        var nc = donde.Split(',');
                        var linea = doc.GetLineByNumber(int.Parse(nc[0]));
                        AvalonEditor.CaretOffset = nc.Length > 1
                            ? Math.Min(linea.Offset + int.Parse(nc[1]) - 1, linea.EndOffset)
                            : linea.EndOffset;
                    }
                    Pulsar();
                    if (doble) Pulsar();

                    void Pulsar()
                    {
                        if (tag.Contains('§')) InsertarLineasEnAvalon(tag);
                        else MarcarEnAvalon(tag);
                    }
                }
                catch { }
                if (png is null) return;
                TrasUnRato(900, () => { CapturarPantalla(png); Environment.Exit(0); });
            });
        }
    }
}
