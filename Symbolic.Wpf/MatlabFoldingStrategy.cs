using System;
using System.Collections.Generic;
using System.Text;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace Calcpad.Wpf;

/// <summary>
/// LO QUE APORTA: carpetas colapsables en el codigo MATLAB.
/// Hekatan Lab usa un <c>RichTextBox</c>, que no sabe plegar; AvalonEdit si, pero hay
/// que decirle DONDE empieza y termina cada bloque. Eso es esta clase.
///
/// Se pliegan:
///   - secciones <c>%%</c> (celdas de MATLAB): de una marca a la siguiente
///   - bloques <c>function / for / parfor / while / if / switch / try / do /
///     classdef / properties / methods</c> hasta su <c>end</c>
///   - <c>function</c> al estilo clasico (sin <c>end</c>): hasta la siguiente
///     <c>function</c> o el fin del archivo
///   - comentarios en bloque <c>%{ … %}</c>
///
/// Las dos trampas de MATLAB que hay que respetar:
///   1. <c>end</c> tambien es un INDICE (<c>v(end)</c>, <c>A{end}</c>). Solo cierra
///      bloque si esta fuera de todo parentesis/corchete/llave.
///   2. La comilla simple es TRANSPUESTA (<c>A'</c>) o cadena (<c>'hola'</c>) segun lo
///      que venga justo antes. Si se confunden, un <c>%</c> dentro de comillas se toma
///      por comentario y el bloque entero se pliega mal.
/// </summary>
internal sealed class MatlabFoldingStrategy
{
    /// <summary>Palabras que ABREN bloque (fuera de parentesis).</summary>
    private static readonly HashSet<string> Abren = new(StringComparer.Ordinal)
    {
        "function", "for", "parfor", "while", "if", "switch", "try", "do", "spmd",
        "classdef", "properties", "methods", "events", "enumeration", "arguments",
    };

    /// <summary>Palabras que CIERRAN bloque. Octave admite los <c>endX</c> explicitos.</summary>
    private static readonly HashSet<string> Cierran = new(StringComparer.Ordinal)
    {
        "end", "endfunction", "endfor", "endparfor", "endwhile", "endif",
        "endswitch", "end_try_catch", "endclassdef", "endproperties", "endmethods",
        "endevents", "endenumeration", "until",
    };

    private readonly record struct Abierto(string Palabra, int Linea, int FinDeLinea);

    internal void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var pliegues = CrearPliegues(document);
        manager.UpdateFoldings(pliegues, -1);
    }

    internal IEnumerable<NewFolding> CrearPliegues(TextDocument doc)
    {
        var pliegues = new List<NewFolding>();
        var pila = new Stack<Abierto>();

        int seccionLinea = -1, seccionFin = -1;   // seccion %% abierta
        int bloqueComentario = -1;                 // linea del %{ abierto
        int nivelCorchetes = 0;                    // ( [ { que siguen abiertos entre lineas

        for (var n = 1; n <= doc.LineCount; n++)
        {
            var linea = doc.GetLineByNumber(n);
            var texto = doc.GetText(linea.Offset, linea.Length);
            var recortado = texto.Trim();

            // --- comentario en bloque %{ … %} ---
            if (bloqueComentario >= 0)
            {
                if (recortado == "%}")
                {
                    Anadir(pliegues, doc, bloqueComentario, n);
                    bloqueComentario = -1;
                }
                continue;
            }
            if (recortado == "%{") { bloqueComentario = n; continue; }

            // --- seccion %% (celda) ---
            if (recortado.StartsWith("%%", StringComparison.Ordinal))
            {
                if (seccionLinea >= 0 && seccionFin > seccionLinea)
                    Anadir(pliegues, doc, seccionLinea, seccionFin);
                seccionLinea = n;
                seccionFin = n;
                continue;
            }
            if (seccionLinea >= 0 && recortado.Length > 0) seccionFin = n;

            // --- codigo: fuera de comentarios y cadenas ---
            var codigo = SoloCodigo(texto);
            if (codigo.Length == 0) continue;

            foreach (var t in Tokens(codigo, ref nivelCorchetes))
            {
                var palabra = t.Palabra;
                if (t.Profundidad != 0) continue;        // v(end) es un INDICE, no un cierre

                // Abre bloque solo si es la PRIMERA palabra de la linea y no es una
                // asignacion: `properties`, `methods`, `arguments`… tambien valen como
                // nombre de variable, y `properties = 3;` no abre nada.
                if (Abren.Contains(palabra) && t.PrimeraDeLinea && !t.EsAsignacion)
                {
                    // function al estilo clasico: la anterior no tenia `end` → cerrarla aqui
                    if (palabra == "function" && pila.Count > 0 && pila.Peek().Palabra == "function")
                    {
                        var previa = pila.Pop();
                        CerrarEn(pliegues, doc, previa, n - 1);
                    }
                    pila.Push(new Abierto(palabra, n, linea.EndOffset));
                }
                else if (Cierran.Contains(palabra) && pila.Count > 0)
                {
                    var abierto = pila.Pop();
                    if (n > abierto.Linea)                // un bloque de una sola linea no se pliega
                        pliegues.Add(new NewFolding(abierto.FinDeLinea, linea.EndOffset)
                        {
                            Name = Etiqueta(n - abierto.Linea),
                        });
                }
            }
        }

        // lo que quedo abierto al final (function clasica, o bloque a medio escribir)
        while (pila.Count > 0)
            CerrarEn(pliegues, doc, pila.Pop(), doc.LineCount);
        if (seccionLinea >= 0 && seccionFin > seccionLinea)
            Anadir(pliegues, doc, seccionLinea, seccionFin);
        if (bloqueComentario >= 0)
            Anadir(pliegues, doc, bloqueComentario, doc.LineCount);

        // AvalonEdit exige los pliegues ordenados por offset de inicio
        pliegues.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return pliegues;
    }

    // ---------- ayudas ----------

    private static string Etiqueta(int lineas) =>
        lineas == 1 ? " ⋯ 1 línea" : $" ⋯ {lineas} líneas";

    private static void Anadir(List<NewFolding> pliegues, TextDocument doc, int desde, int hasta)
    {
        if (hasta <= desde) return;
        var a = doc.GetLineByNumber(desde).EndOffset;
        var b = doc.GetLineByNumber(hasta).EndOffset;
        if (b > a) pliegues.Add(new NewFolding(a, b) { Name = Etiqueta(hasta - desde) });
    }

    private static void CerrarEn(List<NewFolding> pliegues, TextDocument doc, Abierto abierto, int lineaFinal)
    {
        if (lineaFinal <= abierto.Linea) return;
        var fin = doc.GetLineByNumber(lineaFinal).EndOffset;
        if (fin > abierto.FinDeLinea)
            pliegues.Add(new NewFolding(abierto.FinDeLinea, fin)
            {
                Name = Etiqueta(lineaFinal - abierto.Linea),
            });
    }

    /// <summary>Devuelve la linea sin comentario ni contenido de cadenas, conservando las
    /// posiciones de los parentesis. Distingue la comilla de transpuesta de la de cadena.</summary>
    internal static string SoloCodigo(string linea)
    {
        var sb = new StringBuilder(linea.Length);
        char anteriorUtil = '\0';
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];

            if (c == '%')  break;                       // comentario hasta fin de linea
            if (c == '.' && i + 2 < linea.Length && linea[i + 1] == '.' && linea[i + 2] == '.')
                break;                                   // continuacion: lo que sigue es comentario

            if (c == '"')                                // cadena de comillas dobles
            {
                i++;
                while (i < linea.Length && linea[i] != '"') i++;
                sb.Append("\"\"");
                anteriorUtil = '"';
                continue;
            }

            if (c == '\'')
            {
                // transpuesta si viene pegada a un identificador, cierre o punto
                var esTranspuesta = char.IsLetterOrDigit(anteriorUtil) || anteriorUtil is '_' or ')' or ']' or '}' or '.' or '\'';
                if (esTranspuesta) { sb.Append('\''); anteriorUtil = '\''; continue; }
                i++;
                while (i < linea.Length)
                {
                    if (linea[i] == '\'')
                    {
                        if (i + 1 < linea.Length && linea[i + 1] == '\'') { i += 2; continue; }  // '' escapada
                        break;
                    }
                    i++;
                }
                sb.Append("''");
                anteriorUtil = '\'';
                continue;
            }

            sb.Append(c);
            if (!char.IsWhiteSpace(c)) anteriorUtil = c;
        }
        return sb.ToString();
    }

    private readonly record struct Token(string Palabra, int Profundidad, bool PrimeraDeLinea, bool EsAsignacion);

    /// <summary>Palabras del codigo con la profundidad de parentesis en que aparecen.
    /// <paramref name="nivel"/> entra y sale para arrastrar corchetes abiertos entre lineas.</summary>
    private static List<Token> Tokens(string codigo, ref int nivel)
    {
        var salida = new List<Token>();
        var primera = true;
        for (var i = 0; i < codigo.Length; i++)
        {
            var c = codigo[i];
            if (c is '(' or '[' or '{') { nivel++; primera = false; continue; }
            if (c is ')' or ']' or '}') { if (nivel > 0) nivel--; primera = false; continue; }
            if (!char.IsLetter(c) && c != '_')
            {
                if (!char.IsWhiteSpace(c)) primera = false;
                continue;
            }

            var j = i;
            while (j < codigo.Length && (char.IsLetterOrDigit(codigo[j]) || codigo[j] == '_')) j++;

            // ¿le sigue un `=` que no sea `==`? entonces la palabra es el destino de una asignacion
            var k = j;
            while (k < codigo.Length && char.IsWhiteSpace(codigo[k])) k++;
            var asignacion = k < codigo.Length && codigo[k] == '='
                             && (k + 1 >= codigo.Length || codigo[k + 1] != '=');

            salida.Add(new Token(codigo[i..j], nivel, primera && nivel == 0, asignacion));
            primera = false;
            i = j - 1;
        }
        return salida;
    }
}
