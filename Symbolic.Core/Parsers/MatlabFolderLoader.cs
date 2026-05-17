using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Calcpad Lab — Resolver MATLAB-path. Cuando un script <c>main.m</c> llama a
    /// <c>helper(x)</c> y <c>helper</c> está definida en <c>helper.m</c> en la
    /// misma carpeta, este loader escanea la carpeta, extrae todas las
    /// definiciones de función <c>function ...</c> de los archivos <c>.m</c>,
    /// y las antepone al script principal antes de pasarlo al preprocessor.
    ///
    /// Igual que MATLAB en su current directory + path.
    ///
    /// IMPORTANTE:
    ///   - Solo se anexan los archivos cuya PRIMERA línea ejecutable es
    ///     <c>function ...</c> (estilo "function file" de MATLAB). Los scripts
    ///     puros NO se incluyen.
    ///   - El propio archivo principal NO se incluye dos veces.
    ///   - Se preserva el orden alfabético para reproducibilidad.
    /// </summary>
    public static class MatlabFolderLoader
    {
        /// <summary>
        /// Carga el script principal + todas las "function files" de la misma carpeta.
        /// Si <paramref name="mainFilePath"/> es null o no existe, devuelve
        /// <paramref name="mainScript"/> sin cambios.
        /// </summary>
        /// <param name="mainScript">Contenido del script principal (.m).</param>
        /// <param name="mainFilePath">Ruta absoluta al archivo principal (para
        /// resolver la carpeta). Puede ser null si no aplica.</param>
        /// <returns>Script combinado: function-files anexados + main al final.</returns>
        public static string Load(string mainScript, string mainFilePath)
        {
            if (string.IsNullOrEmpty(mainFilePath)) return mainScript;
            string folder;
            try { folder = Path.GetDirectoryName(Path.GetFullPath(mainFilePath)); }
            catch { return mainScript; }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return mainScript;

            return LoadFromFolder(mainScript, folder,
                                  Path.GetFileName(mainFilePath));
        }

        /// <summary>
        /// Como <see cref="Load"/> pero el caller especifica la carpeta y el
        /// nombre del archivo principal directamente (útil para tests).
        /// </summary>
        public static string LoadFromFolder(string mainScript, string folder, string mainFileName)
        {
            if (!Directory.Exists(folder)) return mainScript;

            // Encontrar todos los .m en la carpeta y subcarpetas (MATLAB path-like).
            string[] mFiles;
            try { mFiles = Directory.GetFiles(folder, "*.m", SearchOption.AllDirectories); }
            catch { return mainScript; }

            // Ordenar para reproducibilidad
            Array.Sort(mFiles, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            var includedFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in mFiles)
            {
                var name = Path.GetFileName(path);
                if (string.Equals(name, mainFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                string content;
                try { content = File.ReadAllText(path); }
                catch { continue; }

                if (!IsFunctionFile(content, out var fnName)) continue;
                // Eager-load: incluir TODAS las function-files del path (como MATLAB
                // real, que tiene todas las funciones del current directory + path
                // disponibles). Esto resuelve cadenas de dispatch transitivas donde
                // el main llama f1() y f1() llama internamente f2() en otro archivo.
                // Evitar duplicados (mismo nombre de funcion en archivos distintos).
                if (!includedFunctions.Add(fnName)) continue;

                // Auto-include silencioso: sin comentarios visibles en el output.
                sb.AppendLine(content.TrimEnd());
                sb.AppendLine();
            }

            // Anexar el script principal al final (las funciones quedan disponibles).
            sb.Append(mainScript);
            return sb.ToString();
        }

        /// <summary>
        /// Determina si <paramref name="script"/> contiene el identificador
        /// <paramref name="name"/> como token (con word boundaries). Salta líneas
        /// de comentario completas y STRIPea comentarios inline (% ... fin de línea),
        /// considerando que el % puede aparecer dentro de strings literales — los
        /// strings se mantienen y solo se quita el % no enmascarado.
        ///
        /// Justificación: un helper como `longitud.m` se incluiría por la sola
        /// mención de "longitud" en un comentario, lo que provoca parse-errors
        /// si el helper no cumple las reglas estrictas del parser (e.g. function
        /// sin end). Strippear comentarios inline elimina ese acoplamiento.
        /// </summary>
        public static bool ReferencesIdentifier(string script, string name)
        {
            if (string.IsNullOrEmpty(script) || string.IsNullOrEmpty(name))
                return false;
            int nameLen = name.Length;
            foreach (var rawLine in script.Split('\n'))
            {
                var trimmed = rawLine.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == '%') continue;
                // Stripear comentario inline (todo desde el primer % NO enmascarado)
                var line = StripInlineComment(rawLine);
                int idx = 0;
                while ((idx = line.IndexOf(name, idx, StringComparison.Ordinal)) >= 0)
                {
                    // Word boundaries: chars antes/después no son identifier chars.
                    bool leftOk = idx == 0 || !IsIdentChar(line[idx - 1]);
                    int after = idx + nameLen;
                    bool rightOk = after >= line.Length || !IsIdentChar(line[after]);
                    if (leftOk && rightOk) return true;
                    idx = after;
                }
            }
            return false;
        }

        /// <summary>
        /// Devuelve <paramref name="line"/> sin el comentario inline (todo desde
        /// el primer <c>%</c> no contenido en string literal). Soporta tanto
        /// <c>"..."</c> como <c>'...'</c>. No es un parser MATLAB completo (no
        /// distingue transpose vs char-array) pero basta para la heurística.
        /// </summary>
        private static string StripInlineComment(string line)
        {
            bool inSingle = false, inDouble = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inDouble && c == '\'' && !inSingle)
                {
                    // Heurística: tratamos como string si NO sigue a un char ident/cierre
                    // (que indicaría transpose). Conservador: en caso de duda asumir string.
                    bool isTranspose = i > 0 && (IsIdentChar(line[i - 1]) || line[i - 1] == ')' || line[i - 1] == ']');
                    if (!isTranspose) inSingle = true;
                }
                else if (inSingle && c == '\'') inSingle = false;
                else if (!inSingle && c == '"' && !inDouble) inDouble = true;
                else if (inDouble && c == '"') inDouble = false;
                else if (!inSingle && !inDouble && c == '%') return line.Substring(0, i);
            }
            return line;
        }

        private static bool IsIdentChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_';

        /// <summary>
        /// Heurística: el archivo es un "function file" estilo MATLAB si la PRIMERA
        /// línea no-vacía no-comentario empieza con <c>function</c>.
        /// Extrae el nombre de la función en <paramref name="functionName"/>.
        /// </summary>
        public static bool IsFunctionFile(string content, out string functionName)
        {
            functionName = null;
            if (string.IsNullOrEmpty(content)) return false;
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.TrimStart().TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("%")) continue;
                // Primera línea ejecutable: debe empezar con "function"
                if (!line.StartsWith("function", StringComparison.Ordinal)) return false;
                if (line.Length > 8 && !(line[8] == ' ' || line[8] == '\t' || line[8] == '[')) return false;

                // Extraer nombre — patrón típico:
                //   function out = NAME(args)
                //   function [a, b] = NAME(args)
                //   function NAME(args)
                var rest = line[8..].TrimStart();
                int eqIdx = rest.IndexOf('=');
                if (eqIdx > 0)
                {
                    // Skip ==, etc.
                    if (eqIdx + 1 < rest.Length && rest[eqIdx + 1] == '=') { /* not assignment */ }
                    else
                    {
                        rest = rest[(eqIdx + 1)..].TrimStart();
                    }
                }
                int parenIdx = rest.IndexOf('(');
                if (parenIdx > 0)
                    functionName = rest[..parenIdx].Trim();
                else
                {
                    int wEnd = 0;
                    while (wEnd < rest.Length &&
                           (char.IsLetterOrDigit(rest[wEnd]) || rest[wEnd] == '_'))
                        wEnd++;
                    functionName = rest[..wEnd];
                }
                return !string.IsNullOrEmpty(functionName);
            }
            return false;
        }
    }
}
