using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Calcpad.Core.Matlab;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Calcpad.Wpf;

/// <summary>
/// Pinta lo que el usuario declaro EN SU ARCHIVO: sus variables con color propio y sus
/// funciones en negrita. El <c>Matlab.xshd</c> no puede hacerlo: solo conoce listas fijas
/// de palabras (keywords, constantes, las 577 del motor). Esto es lo que el editor clasico
/// hacia con <c>HighLighter</c> + <c>UserDefined</c>.
///
/// Se aplica DESPUES del resaltado de sintaxis, asi que solo toca los nombres que el .xshd
/// dejo sin clasificar: si la palabra es keyword, constante o funcion del motor, no se toca.
///
/// Quien lee los simbolos es el MOTOR (<see cref="MatlabSymbols"/>), no esta clase: la piel
/// solo elige colores.
/// </summary>
internal sealed class MatlabSemanticColorizer : DocumentColorizingTransformer
{
    /// <summary>Lo que el .xshd ya pinta (keywords, constantes y las 577 del motor). Se arma
    /// una vez y como conjunto: mirarlo por cada palabra de cada linea tiene que ser barato.</summary>
    private static readonly HashSet<string> YaPintadas = new(
        System.Linq.Enumerable.Concat(
            System.Linq.Enumerable.Concat(MatlabBuiltins.All, MatlabBuiltins.Keywords),
            MatlabBuiltins.Constants),
        StringComparer.Ordinal);

    private Dictionary<string, SimboloTipo> _mapa = new(StringComparer.Ordinal);
    private Brush _variable, _funcion, _parametro;

    internal MatlabSemanticColorizer() => AplicarTema(true);

    /// <summary>Mismos colores que usa el popup del autocompletado, para que lo que ves en la
    /// lista y lo que ves en el codigo sean lo mismo.</summary>
    internal void AplicarTema(bool oscuro)
    {
        _variable  = Pincel(oscuro ? "#98C379" : "#3F7A2E");
        _funcion   = Pincel(oscuro ? "#E5E5E5" : "#1A1A1A");
        _parametro = Pincel(oscuro ? "#D19A66" : "#8A5A00");
    }

    /// <summary>Cambia la tabla de nombres. La ultima declaracion gana (una variable
    /// reasignada sigue siendo variable).</summary>
    internal void Actualizar(IReadOnlyList<Simbolo> simbolos)
    {
        var mapa = new Dictionary<string, SimboloTipo>(StringComparer.Ordinal);
        if (simbolos is not null)
            foreach (var s in simbolos)
            {
                // Una funcion manda sobre una variable del mismo nombre: es lo que se llama.
                if (mapa.TryGetValue(s.Nombre, out var previo) && previo == SimboloTipo.Funcion) continue;
                mapa[s.Nombre] = s.Tipo;
            }
        _mapa = mapa;
    }

    protected override void ColorizeLine(DocumentLine linea)
    {
        if (_mapa.Count == 0 || linea.Length == 0) return;

        var texto = CurrentContext.Document.GetText(linea);
        foreach (var p in MatlabSymbols.Identificadores(texto))
        {
            if (!_mapa.TryGetValue(p.Nombre, out var tipo)) continue;
            if (YaPintadas.Contains(p.Nombre)) continue;   // ya lo pinto el .xshd

            var (color, negrita) = tipo switch
            {
                SimboloTipo.Funcion   => (_funcion, true),
                SimboloTipo.Parametro => (_parametro, false),
                _                     => (_variable, false),
            };

            ChangeLinePart(linea.Offset + p.Inicio, linea.Offset + p.Inicio + p.Largo, e =>
            {
                e.TextRunProperties.SetForegroundBrush(color);
                if (negrita)
                    e.TextRunProperties.SetTypeface(new Typeface(
                        e.TextRunProperties.Typeface.FontFamily, FontStyles.Normal,
                        FontWeights.Bold, FontStretches.Normal));
            });
        }
    }

    private static Brush Pincel(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
