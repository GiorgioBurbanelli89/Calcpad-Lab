using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core.Matlab
{
    /// <summary>Un bloque plegable: del final de la linea que ABRE al final de la que CIERRA.</summary>
    public readonly record struct FoldSpan(int Start, int End, string Label);

    /// <summary>
    /// Encuentra los bloques de MATLAB (%% , function, for, if, while, switch, try...) para
    /// el +/- del margen del editor.
    ///
    /// POR QUE VIVE EN EL MOTOR y no en la ventana: es analisis de TEXTO puro, sin una sola
    /// referencia al editor. Asi la piel WPF (AvalonEdit), una futura piel Avalonia
    /// (AvaloniaEdit) y el CLI pliegan IDENTICO, porque leen la misma funcion. Es la misma
    /// receta que ya usa Hekatan Fortran con FortranBlocks.
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
    public static class MatlabBlocks
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

        /// <summary>Los pliegues del texto, ordenados por donde empiezan.</summary>
        public static List<FoldSpan> Find(string texto)
        {
            var pliegues = new List<FoldSpan>();
            if (string.IsNullOrEmpty(texto)) return pliegues;

            var fin = FinesDeLinea(texto, out var inicios);
            var pila = new Stack<Abierto>();

            int seccionLinea = -1, seccionFin = -1;   // seccion %% abierta
            int bloqueComentario = -1;                 // linea del %{ abierto
            int nivelCorchetes = 0;                    // ( [ { que siguen abiertos entre lineas

            for (var n = 1; n <= fin.Count; n++)
            {
                var linea = texto[inicios[n - 1]..fin[n - 1]];
                var recortado = linea.Trim();

                // --- comentario en bloque %{ … %} ---
                if (bloqueComentario >= 0)
                {
                    if (recortado == "%}")
                    {
                        Anadir(pliegues, fin, bloqueComentario, n);
                        bloqueComentario = -1;
                    }
                    continue;
                }
                if (recortado == "%{") { bloqueComentario = n; continue; }

                // --- seccion %% (celda) ---
                if (recortado.StartsWith("%%", StringComparison.Ordinal))
                {
                    if (seccionLinea >= 0 && seccionFin > seccionLinea)
                        Anadir(pliegues, fin, seccionLinea, seccionFin);
                    seccionLinea = n;
                    seccionFin = n;
                    continue;
                }
                if (seccionLinea >= 0 && recortado.Length > 0) seccionFin = n;

                // --- codigo: fuera de comentarios y cadenas ---
                var codigo = SoloCodigo(linea);
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
                            CerrarEn(pliegues, fin, pila.Pop(), n - 1);

                        pila.Push(new Abierto(palabra, n, fin[n - 1]));
                    }
                    else if (Cierran.Contains(palabra) && pila.Count > 0)
                    {
                        var abierto = pila.Pop();
                        if (n > abierto.Linea)                // un bloque de una sola linea no se pliega
                            pliegues.Add(new FoldSpan(abierto.FinDeLinea, fin[n - 1], Etiqueta(n - abierto.Linea)));
                    }
                }
            }

            // lo que quedo abierto al final (function clasica, o bloque a medio escribir)
            while (pila.Count > 0)
                CerrarEn(pliegues, fin, pila.Pop(), fin.Count);
            if (seccionLinea >= 0 && seccionFin > seccionLinea)
                Anadir(pliegues, fin, seccionLinea, seccionFin);
            if (bloqueComentario >= 0)
                Anadir(pliegues, fin, bloqueComentario, fin.Count);

            // el editor exige los pliegues ordenados por offset de inicio
            pliegues.Sort((a, b) => a.Start.CompareTo(b.Start));
            return pliegues;
        }

        // ---------- ayudas ----------

        /// <summary>Fin de cada linea SIN el salto (\r\n o \n), y donde empieza cada una.
        /// Son los mismos offsets que usa el editor, por eso se calculan aqui una sola vez.</summary>
        private static List<int> FinesDeLinea(string texto, out List<int> inicios)
        {
            var fines = new List<int>();
            inicios = new List<int>();
            var pos = 0;
            while (true)
            {
                var salto = texto.IndexOf('\n', pos);
                var fin = salto < 0 ? texto.Length : salto;
                inicios.Add(pos);
                fines.Add(fin > pos && texto[fin - 1] == '\r' ? fin - 1 : fin);
                if (salto < 0) break;
                pos = salto + 1;
            }
            return fines;
        }

        private static string Etiqueta(int lineas) =>
            lineas == 1 ? " ⋯ 1 línea" : $" ⋯ {lineas} líneas";

        private static void Anadir(List<FoldSpan> pliegues, List<int> fin, int desde, int hasta)
        {
            if (hasta <= desde) return;
            int a = fin[desde - 1], b = fin[hasta - 1];
            if (b > a) pliegues.Add(new FoldSpan(a, b, Etiqueta(hasta - desde)));
        }

        private static void CerrarEn(List<FoldSpan> pliegues, List<int> fin, Abierto abierto, int lineaFinal)
        {
            if (lineaFinal <= abierto.Linea) return;
            var b = fin[lineaFinal - 1];
            if (b > abierto.FinDeLinea)
                pliegues.Add(new FoldSpan(abierto.FinDeLinea, b, Etiqueta(lineaFinal - abierto.Linea)));
        }

        /// <summary>Devuelve la linea sin comentario ni contenido de cadenas, conservando las
        /// posiciones de los parentesis. Distingue la comilla de transpuesta de la de cadena.</summary>
        public static string SoloCodigo(string linea)
        {
            var sb = new StringBuilder(linea.Length);
            char anteriorUtil = '\0';
            for (var i = 0; i < linea.Length; i++)
            {
                var c = linea[i];

                if (c == '%') break;                        // comentario hasta fin de linea
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
}
