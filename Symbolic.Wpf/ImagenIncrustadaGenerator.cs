using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Calcpad.Wpf;

/// <summary>
/// Enseña las imágenes pegadas en el código. Al pegar un recorte, Hekatan Lab guarda una
/// linea larguisima <c>% #img data:image/png;base64,iVBORw0…</c>; el editor clasico
/// (RichTextBox) la tapaba con una miniatura, y AvalonEdit es texto plano, asi que sin esto
/// se veria el chorro de base64.
///
/// Aqui se hace con el mecanismo propio de AvalonEdit: un generador que, en esas lineas,
/// mete un elemento visual en lugar del texto. La linea sigue existiendo tal cual en el
/// documento —al guardar y al calcular se manda el base64 completo—, solo cambia lo que se
/// DIBUJA.
/// </summary>
internal sealed class ImagenIncrustadaGenerator : VisualLineElementGenerator
{
    private const string Marca = "base64,";

    private Brush _texto = Brushes.Gray;

    internal void AplicarTema(bool oscuro) =>
        _texto = new SolidColorBrush((Color)ColorConverter.ConvertFromString(oscuro ? "#5AC37E" : "#0A7F3F"));

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var linea = CurrentContext.VisualLine.LastDocumentLine;
        if (startOffset > linea.Offset) return -1;          // ya se genero para esta linea
        return EsLineaDeImagen(CurrentContext.Document, linea, out _) ? linea.Offset : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var linea = CurrentContext.Document.GetLineByOffset(offset);
        if (!EsLineaDeImagen(CurrentContext.Document, linea, out var texto)) return null;

        // SOLO la etiqueta. La imagen se ve en el REPORTE (WebView2), no en el codigo: el
        // editor es para escribir, y una miniatura ahi estorba y descuadra las lineas.
        var kb = Math.Max(1, (texto.Length - texto.IndexOf(Marca, StringComparison.OrdinalIgnoreCase)) * 3 / 4096);
        var etiqueta = new TextBlock
        {
            Text = $"% imagen incrustada  ({kb} KB — se ve en el reporte)",
            Foreground = _texto,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // La linea entera del documento se sustituye por este elemento (el texto sigue ahi:
        // al guardar y al calcular se manda el base64 completo).
        return new InlineObjectElement(linea.Length, etiqueta);
    }

    private static bool EsLineaDeImagen(TextDocument doc, DocumentLine linea, out string texto)
    {
        texto = null;
        if (linea.Length < 32 || linea.Length > 20_000_000) return false;
        var s = doc.GetText(linea);
        if (s.Length == 0 || s[0] != '%') return false;
        if (s.IndexOf(Marca, StringComparison.OrdinalIgnoreCase) < 0) return false;
        texto = s;
        return true;
    }

}
