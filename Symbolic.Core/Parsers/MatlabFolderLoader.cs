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
        // Builtins de VISUALIZACION nativos de Lab que NO deben ser sombreados por un .m
        // hermano. Justificacion: `hoverdata.m` existe como la version MATLAB (datacursormode)
        // para que el MISMO script corra en MATLAB 2017a; en Calcpad Lab gana el builtin (canvas).
        private static readonly HashSet<string> ReservedLabBuiltins = new(StringComparer.OrdinalIgnoreCase) { "hoverdata" };

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

            // Las funciones DEFINIDAS en el archivo principal (top-level + locales) son
            // AUTORITATIVAS: se anexa el main al final, y cualquier function-file hermana
            // que redefina alguno de esos nombres se SALTEA para evitar colisión de firmas.
            // (Bug real: `placa_base_3d_lab.m` define drawBox(9 args) y la hermana
            //  `placa_base_3d.m` define drawBox(10 args, con ax) → chocaban y rompían el
            //  render 3D. Como en MATLAB, las funciones del propio archivo tienen prioridad.)
            foreach (var fn in ExtractAllFunctionNames(mainScript))
                includedFunctions.Add(fn);

            foreach (var path in mFiles)
            {
                var name = Path.GetFileName(path);
                if (string.Equals(name, mainFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                string content;
                try { content = File.ReadAllText(path); }
                catch { continue; }

                if (!IsFunctionFile(content, out var fnName)) continue;
                if (ReservedLabBuiltins.Contains(fnName)) continue;   // hoverdata.m: version MATLAB; en Lab gana el builtin
                // Robustez: si una function-file usa sintaxis MATLAB aún NO soportada
                // por el parser (multi-output indexado, etc.), NO debe romper TODA la
                // carpeta. La validamos por separado y la salteamos si no parsea —
                // queda no-disponible, pero el resto de la carpeta renderiza igual.
                if (!ParsesOk(content)) continue;
                // Eager-load: incluir TODAS las function-files del path (como MATLAB
                // real, que tiene todas las funciones del current directory + path
                // disponibles). Esto resuelve cadenas de dispatch transitivas donde
                // el main llama f1() y f1() llama internamente f2() en otro archivo.
                // PERO saltear la hermana si CUALQUIERA de sus funciones (top o local)
                // ya está definida por el main o por otra hermana ya incluida → evita
                // colisión de helpers homónimos con firmas distintas.
                var sibFns = ExtractAllFunctionNames(content);
                bool collides = false;
                foreach (var fn in sibFns)
                    if (includedFunctions.Contains(fn)) { collides = true; break; }
                if (collides) continue;
                foreach (var fn in sibFns) includedFunctions.Add(fn);

                // Auto-include silencioso: las function-files se REGISTRAN (sus
                // funciones quedan disponibles) pero sus comentarios de doc a nivel
                // de módulo NO deben renderizarse como worksheet. Por eso se quitan
                // las líneas-comentario completas antes de anexar (sino el header
                // `%` de p.ej. calcpad_lab_lib.m se volcaba al output).
                // Cerrar las funciones de estilo clasico ANTES de concatenar: si no,
                // la ultima se traga el codigo del archivo que venga despues.
                sb.AppendLine(CloseClassicFunctions(StripFullLineComments(content).TrimEnd()));
                sb.AppendLine();
            }

            // Anexar el script principal al final (las funciones quedan disponibles).
            sb.Append(mainScript);
            return sb.ToString();
        }

        /// <summary>Quita las líneas que son ENTERAMENTE un comentario (`%...` o `%%...`)
        /// de una function-file auto-incluida, para que su documentación no aparezca en
        /// el output. El código se conserva intacto (los comentarios inline tras código
        /// quedan, pero igual no se renderizan porque están dentro de funciones que sólo
        /// se registran). Quitar líneas-comentario completas es siempre seguro en MATLAB.</summary>
        private static string StripFullLineComments(string content)
        {
            if (string.IsNullOrEmpty(content)) return content ?? string.Empty;
            var sb = new StringBuilder(content.Length);
            foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
            {
                var t = line.TrimStart();
                if (t.StartsWith("%", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
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
        /// <summary>True si la function-file parsea sin error con el parser MATLAB de Lab.
        /// Red de seguridad: una function-file con sintaxis aún no soportada se saltea en
        /// vez de romper el parseo de TODA la carpeta auto-incluida.</summary>
        private static bool ParsesOk(string content)
        {
            try
            {
                var toks = Calcpad.Core.Matlab.MatlabTokenizer.Tokenize(content);
                new Calcpad.Core.Matlab.MatlabParser(toks).ParseAllStatements();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Cierra con <c>end</c> las funciones escritas al estilo CLASICO de
        /// MATLAB (las que no se cierran, como casi todo CEINCI-LAB).
        /// Hace falta porque aqui los archivos se CONCATENAN: sin el <c>end</c>, el
        /// cuerpo de la ultima funcion se traga el script principal que va despues
        /// y no se ejecuta nada. En un archivo suelto MATLAB no tiene este problema.
        /// Los archivos ya cerrados con <c>end</c> se devuelven intactos.</summary>
        private static string CloseClassicFunctions(string content)
        {
            List<int> fnLines = new();
            try
            {
                var toks = Calcpad.Core.Matlab.MatlabTokenizer.Tokenize(content);
                var parser = new Calcpad.Core.Matlab.MatlabParser(toks);
                var stmts = parser.ParseAllStatements();
                if (!parser.ClassicFunctionStyle) return content;      // ya cierra: no tocar
                foreach (var s in stmts)
                    if (s is Calcpad.Core.Matlab.FunctionDef fd) fnLines.Add(fd.Line);
            }
            catch { return content; }
            if (fnLines.Count == 0) return content;
            fnLines.Sort();

            var lines = new List<string>(content.Replace("\r\n", "\n").Split('\n'));
            lines.Add("end");                                          // cierra la ultima
            // De atras hacia adelante para no desplazar los indices que faltan.
            for (int i = fnLines.Count - 1; i >= 1; i--)
            {
                var idx = fnLines[i] - 1;                              // FunctionDef.Line es 1-based
                if (idx >= 0 && idx <= lines.Count) lines.Insert(idx, "end");
            }
            return string.Join("\n", lines);
        }

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

        /// <summary>Extrae el nombre de UNA línea `function ... NAME(args)` (ya TrimStart,
        /// empezando con "function"). Devuelve "" si no puede.</summary>
        private static string ParseFunctionNameFromLine(string line)
        {
            if (line.Length <= 8) return "";
            var rest = line[8..].TrimStart();
            int eqIdx = rest.IndexOf('=');
            if (eqIdx > 0 && !(eqIdx + 1 < rest.Length && rest[eqIdx + 1] == '='))
                rest = rest[(eqIdx + 1)..].TrimStart();
            int parenIdx = rest.IndexOf('(');
            if (parenIdx > 0) return rest[..parenIdx].Trim();
            int w = 0;
            while (w < rest.Length && (char.IsLetterOrDigit(rest[w]) || rest[w] == '_')) w++;
            return rest[..w];
        }

        /// <summary>Todos los nombres de función DEFINIDOS en el contenido (top-level + locales).
        /// Usado para que las funciones del archivo principal tengan prioridad sobre las hermanas.</summary>
        private static HashSet<string> ExtractAllFunctionNames(string content)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(content)) return names;
            foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (!line.StartsWith("function", StringComparison.Ordinal)) continue;
                if (line.Length > 8 && !(line[8] == ' ' || line[8] == '\t' || line[8] == '[')) continue;
                var nm = ParseFunctionNameFromLine(line);
                if (!string.IsNullOrEmpty(nm)) names.Add(nm);
            }
            return names;
        }

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
