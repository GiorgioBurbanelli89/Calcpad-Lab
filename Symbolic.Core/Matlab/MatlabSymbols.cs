using System;
using System.Collections.Generic;

namespace Calcpad.Core.Matlab
{
    /// <summary>Que es cada nombre encontrado en el codigo del usuario.</summary>
    public enum SimboloTipo
    {
        /// <summary>Variable asignada: <c>x = ...</c>, <c>[a, b] = f(...)</c>, <c>for i = ...</c>.</summary>
        Variable,
        /// <summary>Funcion declarada con <c>function</c>.</summary>
        Funcion,
        /// <summary>Argumento de una <c>function</c> (existe dentro de ella).</summary>
        Parametro,
    }

    /// <summary>Un nombre declarado por el usuario y donde se declaro.
    /// <paramref name="Firma"/> solo lo llevan las funciones: <c>momento(q, L)</c>.</summary>
    public readonly record struct Simbolo(string Nombre, int Linea, SimboloTipo Tipo, string Firma = null);

    /// <summary>
    /// Lee el codigo y devuelve QUE declaro el usuario y en que linea: variables, funciones y
    /// sus argumentos. Lo usan el autocompletado y el coloreado del editor.
    ///
    /// POR QUE EXISTE: antes esto lo hacia <c>UserDefined</c>, que es el lector de CALCPAD.
    /// Ahi un <c>;</c> al final de linea significa "la linea SIGUE", asi que en MATLAB —donde
    /// el <c>;</c> es el final normal— pegaba todas las lineas en una y solo veia la PRIMERA
    /// variable de cada bloque. Con 4 asignaciones seguidas detectaba 1.
    ///
    /// Como <see cref="MatlabBlocks"/>, vive en el MOTOR y es texto puro: la piel WPF, una
    /// piel Avalonia o el CLI ven los mismos simbolos.
    ///
    /// Lo que reconoce:
    ///   - <c>x = ...</c> y <c>x(3) = ...</c> / <c>s.campo = ...</c> (queda la variable base)
    ///   - <c>[a, b] = size(A)</c> — todas las salidas
    ///   - <c>function [M, V] = momento(q, L)</c> — la funcion y sus argumentos
    ///   - <c>for i = 1:n</c>, <c>parfor</c>
    ///   - <c>global a b</c>, <c>persistent c</c>
    /// Lo que NO cuenta como asignacion: <c>==</c>, <c>~=</c>, <c>&lt;=</c>, <c>&gt;=</c>, y
    /// cualquier <c>=</c> dentro de parentesis (es un argumento con nombre, no una variable).
    /// </summary>
    public static class MatlabSymbols
    {
        /// <summary>Palabras del lenguaje que nunca son un nombre del usuario.</summary>
        private static readonly HashSet<string> Reservadas = new(StringComparer.Ordinal)
        {
            "function", "end", "for", "parfor", "while", "if", "elseif", "else", "switch",
            "case", "otherwise", "break", "continue", "return", "try", "catch", "do", "until",
            "global", "persistent", "classdef", "properties", "methods", "events", "spmd",
            "enumeration", "arguments", "true", "false", "pi", "Inf", "inf", "NaN", "nan", "eps",
        };

        public static List<Simbolo> Find(string texto)
        {
            var simbolos = new List<Simbolo>();
            if (string.IsNullOrEmpty(texto)) return simbolos;

            var n = 0;
            foreach (var lineaCruda in texto.Split('\n'))
            {
                ++n;
                // Fuera comentarios y contenido de cadenas: se reusa el mismo recorte que el
                // plegado, que ya sabe que A' es transpuesta y no cadena.
                var codigo = MatlabBlocks.SoloCodigo(lineaCruda.TrimEnd('\r')).Trim();
                if (codigo.Length == 0) continue;

                var palabra = PrimeraPalabra(codigo);

                if (palabra == "function") { Funcion(simbolos, codigo, n); continue; }
                if (palabra is "global" or "persistent") { Declaradas(simbolos, codigo, n); continue; }
                if (palabra is "for" or "parfor") { codigo = QuitarPrimeraPalabra(codigo); }
                else if (Reservadas.Contains(palabra)) continue;   // if / while / case…: no asignan

                Asignacion(simbolos, codigo, n);
            }
            return simbolos;
        }

        // ---------- casos ----------

        /// <summary><c>function [M, V] = momento(q, L)</c> → funcion "momento" con firma, mas
        /// M, V (salidas) y q, L (argumentos) como nombres vivos dentro de ella.</summary>
        private static void Funcion(List<Simbolo> salida, string codigo, int linea)
        {
            var resto = QuitarPrimeraPalabra(codigo);
            var igual = IgualDeAsignacion(resto);
            var cabecera = resto;

            if (igual >= 0)
            {
                foreach (var s in Nombres(resto[..igual]))
                    salida.Add(new Simbolo(s, linea, SimboloTipo.Variable));
                cabecera = resto[(igual + 1)..];
            }

            cabecera = cabecera.Trim();
            var par = cabecera.IndexOf('(');
            var nombre = (par < 0 ? cabecera : cabecera[..par]).Trim();
            if (nombre.Length == 0 || !EsIdentificador(nombre)) return;

            var firma = par < 0 ? nombre + "()" : cabecera.TrimEnd();
            salida.Add(new Simbolo(nombre, linea, SimboloTipo.Funcion, firma));

            if (par >= 0)
            {
                var cierre = cabecera.LastIndexOf(')');
                if (cierre > par)
                    foreach (var s in Nombres(cabecera[(par + 1)..cierre]))
                        salida.Add(new Simbolo(s, linea, SimboloTipo.Parametro));
            }
        }

        /// <summary><c>global a b c</c> — separadas por espacios o comas.</summary>
        private static void Declaradas(List<Simbolo> salida, string codigo, int linea)
        {
            foreach (var s in Nombres(QuitarPrimeraPalabra(codigo)))
                salida.Add(new Simbolo(s, linea, SimboloTipo.Variable));
        }

        /// <summary><c>x = …</c> o <c>[a, b] = …</c>.</summary>
        private static void Asignacion(List<Simbolo> salida, string codigo, int linea)
        {
            var igual = IgualDeAsignacion(codigo);
            if (igual < 0) return;
            foreach (var s in Nombres(codigo[..igual]))
                salida.Add(new Simbolo(s, linea, SimboloTipo.Variable));
        }

        // ---------- ayudas ----------

        /// <summary>Posicion del <c>=</c> que ASIGNA: el primero de nivel 0 que no forma parte
        /// de <c>==</c>, <c>~=</c>, <c>!=</c>, <c>&lt;=</c> ni <c>&gt;=</c>. Devuelve -1 si no hay.</summary>
        private static int IgualDeAsignacion(string codigo)
        {
            var nivel = 0;
            for (var i = 0; i < codigo.Length; i++)
            {
                var c = codigo[i];
                if (c is '(' or '{') nivel++;
                else if (c is ')' or '}') { if (nivel > 0) nivel--; }
                else if (c == '[') { if (i > 0 || nivel > 0) nivel++; }   // [a,b] = … : el de fuera no cuenta
                else if (c == ']') { if (nivel > 0) nivel--; }
                else if (c == '=' && nivel == 0)
                {
                    if (i + 1 < codigo.Length && codigo[i + 1] == '=') { i++; continue; }   // ==
                    if (i > 0 && codigo[i - 1] is '=' or '~' or '!' or '<' or '>') continue;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Los nombres de un lado izquierdo: <c>[a, b]</c>, <c>x</c>, <c>v(3)</c> (queda
        /// <c>v</c>), <c>s.campo</c> (queda <c>s</c>), <c>~</c> (se descarta).</summary>
        private static IEnumerable<string> Nombres(string trozo)
        {
            trozo = trozo.Trim().Trim('[', ']').Trim();
            foreach (var parte in trozo.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = parte.Trim();
                var corte = p.IndexOfAny(new[] { '(', '{', '.' });
                if (corte >= 0) p = p[..corte];
                p = p.Trim();
                if (EsIdentificador(p) && !Reservadas.Contains(p)) yield return p;
            }
        }

        private static bool EsIdentificador(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (var c in s)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }

        private static string PrimeraPalabra(string codigo)
        {
            var i = 0;
            while (i < codigo.Length && (char.IsLetterOrDigit(codigo[i]) || codigo[i] == '_')) i++;
            return codigo[..i];
        }

        private static string QuitarPrimeraPalabra(string codigo) =>
            codigo[PrimeraPalabra(codigo).Length..].Trim();
    }
}
