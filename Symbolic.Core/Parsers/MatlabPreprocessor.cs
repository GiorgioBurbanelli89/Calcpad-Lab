using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Calcpad Lab — pre-procesa archivos .m (MATLAB scripts) antes de pasarlos
    /// al ExpressionParser. Maneja las convenciones de MATLAB que necesitan
    /// transformación a sintaxis Calcpad:
    ///
    /// <list type="bullet">
    ///   <item>%% section headers → '** bold heading (Calcpad heading)</item>
    ///   <item>multi-statement por línea (a=1; b=2; c=3;) → líneas separadas</item>
    ///   <item>respeta strings y brackets para no romper matrix literals [a,b; c,d]</item>
    /// </list>
    ///
    /// El resto (for/end, %, ;, identificadores, etc.) lo entiende el parser
    /// directamente porque tiene detección MATLAB-aware en GetMatlabBareKeyword
    /// y GetTokens.
    /// </summary>
    public static class MatlabPreprocessor
    {
        /// <summary>
        /// Pre-procesa el texto fuente .m para que el ExpressionParser de
        /// Calcpad Lab lo digiera correctamente. El input puede ser una linea
        /// o todo el archivo.
        /// </summary>
        public static string Process(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;
            // PASO -1: transformar signatures MATLAB de funciones:
            //   function out = fn(x, y)   →   function fn(x; y)
            //                                 (y dentro del cuerpo: out = expr → fn = expr)
            //   function [a, b] = fn(x)   →   función "multi-out" expandida
            //                                 (TODO: por ahora se queda como warning)
            src = TransformMatlabFunctionSignatures(src);

            // PASO -0.5: rangos `for VAR = START : STEP : END` (MATLAB step-range)
            //   for i = 1:2:7     →   bucle while equivalente con VAR += STEP
            //   for i = 10:-1:1   →   step negativo (count down)
            // Calcpad #for solo soporta start:end (step=1 implícito), por eso
            // convertimos a `#while` + auto-increment al final del cuerpo.
            src = TransformMatlabStepRangeFor(src);

            // Calcpad Lab MATLAB-only: el motor lee TODOS los keywords MATLAB
            // nativos directamente (for, while, if, elseif, else, function,
            // break, continue, end). `end` se resuelve contextualmente en el
            // motor con _matlabBlockStack — el preprocessor NO emite `#` para
            // ningún keyword. La sintaxis es 100% MATLAB de extremo a extremo.

            // PASO 0.5: pre-scan para encontrar todos los nombres de user-functions
            // (las definidas con `function NAME(...)`). Estos nombres se agregan
            // al set de "no transformar" para que TransformMatrixReads NO los
            // convierta en indexing cuando aparecen como call: `K = NAME(args)`.
            var userFunctionNames = ScanUserFunctionNames(src);

            // ── FASE PREVIA: line continuation MATLAB '...' ──────────────────
            // Si una línea termina (después de quitar whitespace y un comment %)
            // con '...', se concatena con la línea siguiente.
            // Ejemplo:
            //   fprintf('a=%d b=%d', ...
            //           a, b);
            // se vuelve una sola línea:
            //   fprintf('a=%d b=%d',         a, b);
            src = MergeMatlabContinuations(src);

            var sb = new StringBuilder(src.Length + 256);
            var lines = src.Split('\n');
            // Tracking: estamos dentro de un cuerpo `#def` ... `#end def`?
            // En tal caso, NO aplicar TransformIndexedAssignment ni TransformMatrixReads
            // a las líneas del body (rompen la signature y la línea-de-retorno).
            int insideDefDepth = 0;
            foreach (var rawLine in lines)
            {
                var line0 = rawLine.TrimEnd('\r');
                // 0-pre) Operadores MATLAB → Calcpad: <= → ≤, >= → ≥, ~= → ≠, == → ≡
                //        (preserva strings y comentarios)
                line0 = TransformComparisonOperators(line0);
                // 0-pre0a) Element-wise MATLAB ops: A.*B → hp(A;B), A.^2 → hp(A;A), A./B → element div
                line0 = TransformElementWiseOps(line0);
                // 0-pre0) Greek-letter aliases: nu → ν, alpha → α, theta → θ etc.
                //         MATLAB no permite Greek en identifiers — el usuario escribe Latin,
                //         transformamos para que el output renderice Greek (como Calcpad nativo).
                line0 = TransformGreekAliases(line0);
                // 0-pre2) Aliases de builtins: length() → len(), numel() → len()
                line0 = TransformBuiltinAliases(line0);
                // 0-pre3) Notación científica MATLAB: 25e6 → (25*10^6), 2.5e-3 → (2.5*10^-3)
                line0 = TransformScientificNotation(line0);
                // 0-pre4) linspace/logspace/arange con CONSTANTES → expandir literal
                //         (vía matlab_helpers.dll C++/Eigen — micro-secs para n=1000)
                line0 = TransformConstantRangeFns(line0);
                // 0) Shims MATLAB → Calcpad: clear/clc → comment, fprintf/disp → text
                var line0b = TransformShims(line0);
                // 0b) zeros(1, N) o zeros(N, 1) → vector(N) (1D verdadero en Calcpad)
                //     Esto se debe hacer ANTES de TransformDelimiters porque busca el patrón
                //     literal con coma.
                var line0b1 = TransformZerosOnesToVector(line0b);
                // 0b2) ones(...) y zeros(n,m) generales — usan mfill/matrix de Calcpad.
                var line0c = TransformOnesZerosGeneral(line0b1);
                // 0c) Plots MATLAB → directivas Calcpad ($Plot, $Map)
                //     plot(x,y) → $Plot{x.i|y.i @ i = 1 : len(x)}
                //     title(...), xlabel(...), grid on, figure → text/no-op
                var line0d = TransformPlots(line0c);
                // 0c2) Range standalone MATLAB: `var = a:b` o `var = a:s:b` (fuera de for/while)
                //      → `var = range(a; b; 1)` o `var = range(a; b; s)`
                line0d = TransformStandaloneRange(line0d);
                // 1) Delim MATLAB → Calcpad dentro de () y []
                var line1 = TransformDelimitersMatlabToCalcpad(line0d);

                // Tracking del body de funciones — necesario para SALTEAR
                // TransformIndexedAssignment / TransformMatrixReads y para strip
                // del `;` final dentro del body. Reconoce tanto bare `function`
                // (MATLAB nativo) como `#function` (legacy).
                var trimLine = line1.TrimStart();
                if (StartsWithKeyword(trimLine, "function") ||
                    trimLine.StartsWith("#function", StringComparison.Ordinal) ||
                    trimLine.StartsWith("#def", StringComparison.Ordinal))   // alias legacy
                {
                    insideDefDepth++;
                    sb.Append(line1).Append('\n');
                    continue;
                }
                if (trimLine.StartsWith("#end function", StringComparison.Ordinal) ||
                    trimLine.StartsWith("#end def", StringComparison.Ordinal))   // alias legacy
                {
                    if (insideDefDepth > 0) insideDefDepth--;
                    sb.Append(line1).Append('\n');
                    continue;
                }

                string line;
                if (insideDefDepth > 0)
                {
                    // Dentro del cuerpo: no aplicar matrix-reads ni indexed-assignment
                    // porque "fnName = expr" es la línea de retorno y los identificadores
                    // del cuerpo son variables locales (no matrices a indexar).
                    line = line1;
                    // Strip trailing ';' (MATLAB suppression — pero dentro del body
                    // se ejecuta como expresión y el ';' final causaría parse error).
                    var lt = line.TrimEnd();
                    if (lt.EndsWith(";", StringComparison.Ordinal))
                        line = lt[..^1];
                }
                else
                {
                    // 2) Asignación indexada MATLAB → Calcpad dot syntax
                    //    M(i, j) = x  →  M.(i; j) = x
                    //    M(i)    = x  →  M.i = x
                    var line2 = TransformIndexedAssignment(line1, userFunctionNames);
                    // 3) Lecturas indexadas inline: IDENT(args) → IDENT.(args)
                    //    cuando IDENT no es una función reservada de Calcpad
                    line = TransformMatrixReads(line2, userFunctionNames);
                }
                var trimmed = line.TrimStart();

                // %% Heading (MATLAB section header) → ## Heading (Markdown h2 en Calcpad)
                // El parser tiene detección Markdown que convierte '## Title' a '<h2>Title</h2>'.
                if (trimmed.StartsWith("%%"))
                {
                    var title = trimmed.Length > 2 ? trimmed[2..].Trim() : "";
                    sb.Append("## ");
                    sb.Append(title);
                    sb.Append('\n');
                    continue;
                }

                // Split por ';' fuera de strings/parens/brackets/braces
                var splits = SplitMatlabStatements(line);
                if (splits.Count == 1)
                {
                    sb.Append(splits[0]);
                    sb.Append('\n');
                }
                else
                {
                    foreach (var stmt in splits)
                    {
                        var t = stmt.Trim();
                        if (t.Length == 0) continue;
                        var ts = TransformShims(t);
                        sb.Append(ts);
                        sb.Append('\n');
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convierte <c>ones(args)</c> y casos generales de <c>zeros(args)</c> que NO
        /// son 1D a equivalentes Calcpad:
        ///
        ///   <c>ones(n, m)</c> → <c>mfill(n, m, 1)</c>
        ///   <c>ones(n)</c>    → <c>mfill(n, n, 1)</c>  (MATLAB: matriz cuadrada)
        ///   <c>zeros(n, m)</c> → <c>matrix(n, m)</c>   (Calcpad init en 0)
        ///   <c>zeros(n)</c>    → <c>matrix(n, n)</c>
        ///
        /// Esto se aplica DESPUÉS de TransformZerosOnesToVector (que ya capturó
        /// el caso 1D linspace-friendly: <c>zeros(1, N)</c> → <c>vector(N)</c>).
        /// </summary>
        internal static string TransformOnesZerosGeneral(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Fast-path
            if (!line.Contains("ones(", StringComparison.Ordinal) &&
                !line.Contains("zeros(", StringComparison.Ordinal)) return line;

            var sb = new StringBuilder(line.Length + 16);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                bool isOnes = i + 5 <= line.Length && line.AsSpan(i, 5).Equals("ones(", StringComparison.Ordinal);
                bool isZeros = !isOnes && i + 6 <= line.Length && line.AsSpan(i, 6).Equals("zeros(", StringComparison.Ordinal);
                if (!isOnes && !isZeros) { sb.Append(c); i++; continue; }

                // Left boundary
                bool leftOk = sb.Length == 0 || !(char.IsLetterOrDigit(sb[^1]) || sb[^1] == '_');
                if (!leftOk) { sb.Append(c); i++; continue; }

                int fnLen = isOnes ? 4 : 5; // "ones" o "zeros"
                int parenStart = i + fnLen;
                int parenEnd = FindMatchingParen(line, parenStart);
                if (parenEnd == -1) { sb.Append(c); i++; continue; }

                var argsRaw = line[(parenStart + 1)..parenEnd];
                var args = SplitTopLevelComma(argsRaw);
                if (args.Count == 0 || args.Count > 2) { sb.Append(c); i++; continue; }

                var a1 = args[0].Trim();
                var a2 = args.Count == 2 ? args[1].Trim() : a1; // square si 1 arg

                if (isOnes)
                {
                    // mfill(matrix(rows, cols), 1) — Calcpad: mfill toma una matriz
                    // existente y la llena con un valor.
                    sb.Append("mfill(matrix(");
                    sb.Append(a1);
                    sb.Append(", ");
                    sb.Append(a2);
                    sb.Append("), 1)");
                }
                else
                {
                    // matrix(rows, cols) — Calcpad inicializa en 0
                    sb.Append("matrix(");
                    sb.Append(a1);
                    sb.Append(", ");
                    sb.Append(a2);
                    sb.Append(')');
                }
                i = parenEnd + 1;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convierte zeros(1, N) o zeros(N, 1) en vector(N) — un 1D verdadero
        /// indexable con M.i (no matriz 1×N que necesita M.(1; i)).
        /// Idem ones(1, N) / ones(N, 1).
        /// Sólo reconoce el patrón LITERAL ya que se ejecuta ANTES del delim transform.
        /// </summary>
        internal static string TransformZerosOnesToVector(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Patrón:  zeros(1, N)  → vector(N)
            //          zeros(N, 1)  → vector(N)
            //          ones(1, N)   → vector(N)*0 + 1   (workaround: Calcpad no tiene ones)
            //          ones(N, 1)   → vector(N)*0 + 1
            // Regex simple porque buscamos texto literal "zeros(<expr>, <expr>)".
            // Para más robustez usamos un walker manual.
            var sb = new StringBuilder(line.Length);
            int i = 0;
            bool inSq = false, inDq = false;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }
                // Detectar "zeros(" o "ones("
                bool isZeros = i + 6 <= line.Length && line.AsSpan(i, 6).Equals("zeros(", StringComparison.Ordinal);
                bool isOnes  = !isZeros && i + 5 <= line.Length && line.AsSpan(i, 5).Equals("ones(", StringComparison.Ordinal);
                if (!isZeros && !isOnes) { sb.Append(c); i++; continue; }
                // Verificar que la palabra anterior NO sea letra/digit (para evitar matchear ej "myzeros(")
                if (sb.Length > 0)
                {
                    var prev = sb[^1];
                    if (char.IsLetterOrDigit(prev) || prev == '_') { sb.Append(c); i++; continue; }
                }
                int fnLen = isZeros ? 5 : 4;  // "zeros" o "ones"
                int parenStart = i + fnLen;
                int parenEnd = FindMatchingParen(line, parenStart);
                if (parenEnd == -1) { sb.Append(c); i++; continue; }
                var argsRaw = line[(parenStart + 1)..parenEnd];
                // Encontrar los argumentos separados por ',' (top-level)
                var args = SplitTopLevelComma(argsRaw);
                if (args.Count != 2) { sb.Append(c); i++; continue; }
                var a1 = args[0].Trim();
                var a2 = args[1].Trim();
                string N = null;
                if (a1 == "1") N = a2;
                else if (a2 == "1") N = a1;
                if (N == null) { sb.Append(c); i++; continue; }
                // Match! Reemplazar.
                if (isZeros)
                {
                    sb.Append("vector(");
                    sb.Append(N);
                    sb.Append(")");
                }
                else  // ones — workaround
                {
                    sb.Append("(vector(");
                    sb.Append(N);
                    sb.Append(") * 0 + 1)");
                }
                i = parenEnd + 1;
            }
            return sb.ToString();
        }

        private static int FindMatchingParen(string s, int openIdx)
        {
            if (openIdx < 0 || openIdx >= s.Length || s[openIdx] != '(') return -1;
            int depth = 0;
            bool inSq = false, inDq = false;
            for (int j = openIdx; j < s.Length; j++)
            {
                var c = s[j];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') depth++;
                else if (c == ')') { depth--; if (depth == 0) return j; }
            }
            return -1;
        }

        private static List<string> SplitTopLevelComma(string s)
        {
            var result = new List<string>();
            int paren = 0, brack = 0, brace = 0;
            bool inSq = false, inDq = false;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') brack++;
                else if (c == ']') brack--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == ',' && paren == 0 && brack == 0 && brace == 0)
                {
                    result.Add(s[start..i]);
                    start = i + 1;
                }
            }
            if (start <= s.Length) result.Add(s[start..]);
            return result;
        }

        /// <summary>
        /// Transforma rangos MATLAB con step a equivalente while-loop:
        ///
        ///   for i = 1:2:7        →   i = 1
        ///     body                    #while i <= 7
        ///   end                         body
        ///                               i = i + 2
        ///                             #loop
        ///
        ///   for i = 10:-1:1      →   i = 10
        ///     body                    #while i >= 1
        ///   end                         body
        ///                               i = i + -1
        ///                             #loop
        ///
        /// Calcpad <c>#for</c> nativo solo soporta <c>start:end</c> con step=1.
        /// Para soportar la sintaxis MATLAB completa transformamos a <c>#while</c>.
        ///
        /// Detecta el step parseando el RHS del `for`. Si el step es LITERAL
        /// positivo/negativo usa <c>≤</c> o <c>≥</c> directamente. Si el step
        /// es una expresión variable, emite check runtime con <c>if(step≥0,
        /// i≤end, i≥end)</c>.
        ///
        /// IMPORTANTE: necesita encontrar el `end` correspondiente para insertar
        /// el auto-increment ANTES de cerrar el while. Usa un stack de
        /// matching simple.
        /// </summary>
        internal static string TransformMatlabStepRangeFor(string src)
        {
            if (string.IsNullOrEmpty(src) || !src.Contains("for ", StringComparison.Ordinal))
                return src;

            var sb = new StringBuilder(src.Length + 256);
            var lines = src.Split('\n');

            // Stack: para cada bloque abierto, guardar (kind, info)
            //   kind = "for_step"   → necesita auto-increment + close
            //   kind = "if"         → no requiere acción especial
            //   kind = "for"        → rango simple, no requiere
            //   kind = "while"      → no requiere
            //   kind = "function"   → no requiere
            // Solo "for_step" exige insertar `VAR = VAR + STEP` antes del end.
            var stack = new Stack<(string Kind, string Var, string Step)>();

            for (int li = 0; li < lines.Length; li++)
            {
                var line = lines[li].TrimEnd('\r');
                int lead = 0;
                while (lead < line.Length && (line[lead] == ' ' || line[lead] == '\t')) lead++;
                var indent = line[..lead];
                var trimmed = lead < line.Length ? line[lead..] : "";

                // Detectar `for VAR = START : STEP : END`
                if (StartsWithKeyword(trimmed, "for"))
                {
                    var afterFor = trimmed[3..].TrimStart();
                    var stepMatch = TryParseForStepRange(afterFor);
                    if (stepMatch.HasValue)
                    {
                        var (varName, start, step, end) = stepMatch.Value;
                        // Transformar a #for con índice contador. Esto preserva
                        // el comportamiento "oculta iteraciones" de Calcpad #for.
                        //
                        //   for VAR = START : STEP : END        →
                        //     #for _stepidx_VAR = 1 : N
                        //       VAR = START + (_stepidx_VAR - 1) * STEP
                        //       body
                        //     #loop
                        //
                        // donde N = floor(|(END - START) / STEP|) + 1
                        string countExpr;
                        // Si START, STEP, END son literales numéricos, computamos N en C#.
                        // Si no, emitimos una expresión runtime.
                        if (TryEvalLiteral(start, out double sNum)
                            && TryEvalLiteral(step, out double tNum)
                            && TryEvalLiteral(end, out double eNum)
                            && Math.Abs(tNum) > 1e-12)
                        {
                            int n = (int)Math.Floor((eNum - sNum) / tNum) + 1;
                            if (n < 0) n = 0;
                            countExpr = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            // Expression-based count: floor((END - START) / STEP) + 1
                            countExpr = $"floor(({end} - ({start})) / ({step})) + 1";
                        }
                        // Use uniquename for the counter (prefix con letra
                        // para que sea variable válida — algunos parsers no
                        // aceptan identifiers que empiezan con `_`).
                        string ctr = "k_step_" + varName;
                        sb.Append(indent).Append("for ").Append(ctr)
                          .Append(" = 1 : ").Append(countExpr).Append('\n');
                        // Compute la variable real adentro
                        sb.Append(indent).Append("  ").Append(varName)
                          .Append(" = (").Append(start).Append(") + (").Append(ctr)
                          .Append(" - 1) * (").Append(step).Append(")\n");
                        stack.Push(("for_step", varName, step));
                        continue;
                    }
                    // for sin step → comportamiento default (push for normal)
                    stack.Push(("for", "", ""));
                    sb.Append(line).Append('\n');
                    continue;
                }

                if (StartsWithKeyword(trimmed, "while"))
                {
                    stack.Push(("while", "", ""));
                    sb.Append(line).Append('\n');
                    continue;
                }
                if (StartsWithKeyword(trimmed, "if"))
                {
                    stack.Push(("if", "", ""));
                    sb.Append(line).Append('\n');
                    continue;
                }
                if (StartsWithKeyword(trimmed, "function"))
                {
                    stack.Push(("function", "", ""));
                    sb.Append(line).Append('\n');
                    continue;
                }

                // `end` cierra el bloque más reciente — el contador #for ya
                // se incrementa solo, no necesitamos auto-incremento manual.
                if (trimmed == "end" || trimmed.StartsWith("end ") || trimmed.StartsWith("end;") || trimmed.StartsWith("end%"))
                {
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                    }
                    sb.Append(line).Append('\n');
                    continue;
                }

                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Helper: <c>line</c> empieza con <c>kw</c> seguido de espacio/tab/EOL.</summary>
        private static bool StartsWithKeyword(string line, string kw)
        {
            if (line.Length < kw.Length) return false;
            if (!line.StartsWith(kw, StringComparison.Ordinal)) return false;
            if (line.Length == kw.Length) return true;
            var c = line[kw.Length];
            return c == ' ' || c == '\t' || c == '%' || c == ';' || c == '(';
        }

        /// <summary>
        /// Parsea <c>VAR = START : STEP : END</c>. Retorna null si no es
        /// step-range (e.g. <c>VAR = START : END</c> con dos componentes).
        /// </summary>
        private static (string Var, string Start, string Step, string End)?
            TryParseForStepRange(string s)
        {
            int eqIdx = s.IndexOf('=');
            if (eqIdx <= 0) return null;
            // Cuidar de == o <= o >=
            if (eqIdx + 1 < s.Length && s[eqIdx + 1] == '=') return null;
            if (eqIdx > 0 && (s[eqIdx - 1] == '<' || s[eqIdx - 1] == '>' || s[eqIdx - 1] == '~' || s[eqIdx - 1] == '!')) return null;
            var varName = s[..eqIdx].Trim();
            if (string.IsNullOrEmpty(varName)) return null;
            // Verificar varName es identifier válido
            foreach (var ch in varName) if (!(char.IsLetterOrDigit(ch) || ch == '_')) return null;
            var rhs = s[(eqIdx + 1)..].Trim();
            // Strip trailing comment si hay
            int pct = -1;
            bool inSq = false, inDq = false;
            for (int i = 0; i < rhs.Length; i++)
            {
                var ch = rhs[i];
                if (ch == '\'' && !inDq) inSq = !inSq;
                else if (ch == '"' && !inSq) inDq = !inDq;
                else if (ch == '%' && !inSq && !inDq) { pct = i; break; }
            }
            if (pct >= 0) rhs = rhs[..pct].TrimEnd();
            // Split en `:` top-level (no dentro de paréntesis/brackets)
            var parts = SplitTopLevelColon(rhs);
            if (parts.Count != 3) return null;
            return (varName, parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
        }

        /// <summary>Split por <c>:</c> top-level (fuera de () [] {} y strings).</summary>
        private static List<string> SplitTopLevelColon(string s)
        {
            var result = new List<string>();
            int paren = 0, brack = 0, brace = 0;
            bool inSq = false, inDq = false;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') brack++;
                else if (c == ']') brack--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == ':' && paren == 0 && brack == 0 && brace == 0)
                {
                    result.Add(s[start..i]);
                    start = i + 1;
                }
            }
            result.Add(s[start..]);
            return result;
        }

        /// <summary>Intenta parsear <c>s</c> como literal numérico (incl. negativo).</summary>
        private static bool TryEvalLiteral(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return double.TryParse(s.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Pre-scan del script ya transformado por <c>TransformBareKeywordsToHash</c>
        /// para extraer todos los nombres de user-functions definidas con
        /// <c>#function NAME(...)</c>. Estos nombres NO deben tratarse como matrices
        /// (cuando se llaman: <c>K = NAME(args)</c>) por los pases posteriores.
        /// </summary>
        internal static HashSet<string> ScanUserFunctionNames(string src)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(src)) return set;
            if (!src.Contains("function", StringComparison.Ordinal)) return set;

            foreach (var rawLine in src.Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (!StartsWithKeyword(line, "function")) continue;
                var rest = line[8..].TrimStart(); // después de "function"
                if (rest.Length == 0) continue;
                int parenIdx = rest.IndexOf('(');
                string name;
                if (parenIdx > 0)
                    name = rest[..parenIdx].Trim();
                else
                {
                    // Sin paréntesis: tomar la primera palabra
                    int wEnd = 0;
                    while (wEnd < rest.Length && (char.IsLetterOrDigit(rest[wEnd]) || rest[wEnd] == '_'))
                        wEnd++;
                    name = rest[..wEnd];
                }
                if (!string.IsNullOrEmpty(name)) set.Add(name);
            }
            return set;
        }

        /// <summary>
        /// Transforma signatures MATLAB de funciones a formato Calcpad-compatible:
        ///
        ///   <c>function out = fn(x, y)</c>  →  <c>function fn(x, y)</c>
        ///   ... cuerpo ...                  →  body con <c>out</c> renombrado a <c>fn</c>
        ///   <c>end</c>                       →  (lo maneja TransformBareKeywordsToHash)
        ///
        /// El runtime de Calcpad-Lab (UserFunction) busca la línea
        /// <c>FuncName = expr</c> dentro del cuerpo para identificar el valor de
        /// retorno. MATLAB usa una variable <c>out</c> declarada en la signature,
        /// así que renombramos esa variable a <c>FuncName</c> en todo el cuerpo.
        ///
        /// Multi-output (<c>function [a, b] = fn(x)</c>) se deja como warning;
        /// Calcpad nativo no soporta multi-return.
        ///
        /// El método procesa LÍNEA POR LÍNEA y usa un stack para tracking de
        /// función actual (permite funciones anidadas — raro en MATLAB pero
        /// soportado).
        /// </summary>
        internal static string TransformMatlabFunctionSignatures(string src)
        {
            if (string.IsNullOrEmpty(src) || !src.Contains("function", StringComparison.Ordinal))
                return src;

            var sb = new StringBuilder(src.Length + 32);
            var lines = src.Split('\n');
            // Stack: (returnVar, functionName) — push on `function` open, pop on `end`
            var ctxStack = new Stack<(string returnVar, string fnName, int blockDepth)>();
            int currentDepth = 0;  // tracking de blocks for/if/while dentro de function

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                int lead = 0;
                while (lead < line.Length && (line[lead] == ' ' || line[lead] == '\t')) lead++;
                var trimmed = lead < line.Length ? line[lead..] : "";

                // ¿Es una signature MATLAB?
                if (trimmed.StartsWith("function", StringComparison.Ordinal) &&
                    (trimmed.Length == 8 ||
                     trimmed[8] == ' ' || trimmed[8] == '\t' || trimmed[8] == '['))
                {
                    var sig = ParseMatlabFunctionSignature(trimmed);
                    if (sig.HasValue)
                    {
                        var (retVar, fnName, paramsStr, isMultiOut) = sig.Value;
                        if (isMultiOut)
                        {
                            // No soportamos multi-output; emitimos un comment.
                            sb.Append(line[..lead]);
                            sb.Append("% [Calcpad Lab] multi-output function no soportado: ");
                            sb.Append(trimmed);
                            sb.Append('\n');
                            ctxStack.Push((retVar, fnName, currentDepth));
                            continue;
                        }
                        // Reescribir: "function fn(params)"
                        sb.Append(line[..lead]);
                        sb.Append("function ");
                        sb.Append(fnName);
                        sb.Append('(');
                        sb.Append(paramsStr);
                        sb.Append(')');
                        sb.Append('\n');
                        ctxStack.Push((retVar, fnName, currentDepth));
                        continue;
                    }
                    // No matcheó como signature MATLAB — emitir tal cual
                    sb.Append(line).Append('\n');
                    continue;
                }

                // Track block opens/closes dentro de function (for/if/while crean depth)
                if (ctxStack.Count > 0)
                {
                    if (LineStartsWithKeyword(trimmed, "for") ||
                        LineStartsWithKeyword(trimmed, "if") ||
                        LineStartsWithKeyword(trimmed, "while"))
                    {
                        currentDepth++;
                    }
                    else if (LineStartsWithKeyword(trimmed, "end"))
                    {
                        if (currentDepth > ctxStack.Peek().blockDepth)
                        {
                            currentDepth--;
                            // Es un end de bloque interno, no cierre de function
                            sb.Append(line).Append('\n');
                            continue;
                        }
                        // Es el end de la función — popear y emitir
                        ctxStack.Pop();
                        sb.Append(line).Append('\n');
                        continue;
                    }

                    // Estamos dentro del cuerpo de una función — renombrar
                    // la return variable.
                    var (retVar, fnName, _) = ctxStack.Peek();
                    var renamed = RenameVariableInLine(line, retVar, fnName);
                    sb.Append(renamed).Append('\n');
                    continue;
                }

                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parsea una signature MATLAB. Soporta:
        ///   <c>function out = fn(x, y)</c>               → single-out
        ///   <c>function [out1, out2] = fn(x, y)</c>      → multi-out (marca isMultiOut)
        ///   <c>function fn(x, y)</c>                      → no-out
        /// </summary>
        private static (string retVar, string fnName, string paramsStr, bool isMultiOut)?
            ParseMatlabFunctionSignature(string line)
        {
            // Skip "function "
            var rest = line[8..].TrimStart();
            if (rest.Length == 0) return null;

            string retVar = "";
            bool multiOut = false;

            // Detectar return var
            int eqIdx = FindTopLevelEquals(rest);
            if (eqIdx >= 0)
            {
                var lhs = rest[..eqIdx].Trim();
                if (lhs.StartsWith("[") && lhs.EndsWith("]"))
                {
                    // Multi-output [a, b] = ...
                    multiOut = true;
                    retVar = lhs[1..^1].Trim(); // contenido raw
                }
                else
                {
                    retVar = lhs;
                }
                rest = rest[(eqIdx + 1)..].TrimStart();
            }
            // else: no "= " → no return var (`function fn(x)`)

            // Parsear: fn(params)
            int parenIdx = rest.IndexOf('(');
            if (parenIdx < 0)
            {
                // Sin paréntesis: `function fn` (sin args)
                var fnName = rest.Trim();
                if (string.IsNullOrEmpty(fnName)) return null;
                return (retVar, fnName, "", multiOut);
            }
            var fnNamePart = rest[..parenIdx].Trim();
            if (string.IsNullOrEmpty(fnNamePart)) return null;
            int parenEndIdx = FindMatchingParen(rest, parenIdx);
            if (parenEndIdx < 0) parenEndIdx = rest.Length - 1;
            var paramsStr = (parenEndIdx > parenIdx + 1) ? rest[(parenIdx + 1)..parenEndIdx] : "";
            return (retVar, fnNamePart, paramsStr, multiOut);
        }

        /// <summary>Encuentra el primer <c>=</c> top-level que NO sea parte de == ~= &lt;= &gt;=.</summary>
        private static int FindTopLevelEquals(string s)
        {
            int paren = 0, brack = 0;
            bool inSq = false, inDq = false;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') brack++;
                else if (c == ']') brack--;
                else if (c == '=' && paren == 0 && brack == 0)
                {
                    // Skip ==, <=, >=, ~=, !=
                    if (i + 1 < s.Length && s[i + 1] == '=') { i++; continue; }
                    if (i > 0 && (s[i - 1] == '<' || s[i - 1] == '>' ||
                                  s[i - 1] == '~' || s[i - 1] == '!')) continue;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Chequea si <c>line</c> empieza con un keyword exacto (boundary check).</summary>
        private static bool LineStartsWithKeyword(string line, string kw)
        {
            if (line.Length < kw.Length) return false;
            if (!line.StartsWith(kw, StringComparison.Ordinal)) return false;
            if (line.Length == kw.Length) return true;
            var next = line[kw.Length];
            return next == ' ' || next == '\t' || next == ';' || next == '%' || next == '(' || next == '\r';
        }

        /// <summary>
        /// Reemplaza ocurrencias del identificador <c>oldName</c> por <c>newName</c>
        /// en una línea, respetando strings/comentarios y boundaries de identificador.
        /// </summary>
        private static string RenameVariableInLine(string line, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(oldName) ||
                oldName == newName || !line.Contains(oldName, StringComparison.Ordinal))
                return line;

            var sb = new StringBuilder(line.Length + 8);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                if (i + oldName.Length <= line.Length &&
                    line.AsSpan(i, oldName.Length).Equals(oldName, StringComparison.Ordinal))
                {
                    // Boundary izq
                    bool leftOk = i == 0 ||
                                  !(char.IsLetterOrDigit(line[i - 1]) || line[i - 1] == '_');
                    // Boundary der
                    int j = i + oldName.Length;
                    bool rightOk = j >= line.Length ||
                                   !(char.IsLetterOrDigit(line[j]) || line[j] == '_');
                    if (leftOk && rightOk)
                    {
                        sb.Append(newName);
                        i += oldName.Length;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Transforma solo <c>end</c> a su keyword contextual del motor:
        ///
        ///   end (cierra for/while) → #loop
        ///   end (cierra if)         → #end if
        ///   end (cierra function)   → #end function
        ///
        /// El resto de keywords MATLAB (<c>for</c>, <c>while</c>, <c>if</c>,
        /// <c>function</c>, <c>break</c>, <c>continue</c>, <c>elseif</c>,
        /// <c>else</c>) NO se modifican — el motor los detecta directamente
        /// vía <c>GetMatlabBareKeyword</c>. El usuario nunca ve los #.
        /// </summary>
        internal static string TransformMatlabEndToContextual(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;
            var sb = new StringBuilder(src.Length + 64);
            var lines = src.Split('\n');
            // Stack para tracking del tipo de bloque que cierra cada `end`.
            var blockStack = new Stack<string>();
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                int lead = 0;
                while (lead < line.Length && (line[lead] == ' ' || line[lead] == '\t')) lead++;
                if (lead >= line.Length)
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                var c0 = line[lead];
                // Si línea ya empieza con `#` o `%` no la tocamos
                if (c0 == '#' || c0 == '%')
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Extraer primera palabra
                int wEnd = lead;
                while (wEnd < line.Length && (char.IsLetter(line[wEnd]) || line[wEnd] == '_'))
                    wEnd++;
                if (wEnd == lead)
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Boundary: terminar con espacio/tab/EOL/`(`/`;`/`%`
                if (wEnd < line.Length)
                {
                    var b = line[wEnd];
                    if (b != ' ' && b != '\t' && b != '\r' && b != '(' && b != ';' && b != '%')
                    {
                        sb.Append(line).Append('\n');
                        continue;
                    }
                }
                var word = line[lead..wEnd];
                switch (word)
                {
                    case "for":      blockStack.Push("for"); break;
                    case "while":    blockStack.Push("while"); break;
                    case "if":
                        // No confundir con `if(...)` function call
                        if (wEnd < line.Length && line[wEnd] == '(') break;
                        blockStack.Push("if");
                        break;
                    case "function": blockStack.Push("function"); break;
                    case "end":
                        if (blockStack.Count == 0) break;
                        var kind = blockStack.Pop();
                        var indent = line[..lead];
                        var rest = wEnd < line.Length ? line[wEnd..] : "";
                        string repl = kind switch
                        {
                            "for" => "#loop",
                            "while" => "#loop",
                            "if" => "#end if",
                            "function" => "#end function",
                            _ => null
                        };
                        if (repl == null) break;
                        sb.Append(indent).Append(repl).Append(rest).Append('\n');
                        goto nextLine;
                }
                sb.Append(line).Append('\n');
                nextLine:;
            }
            return sb.ToString();
        }

        /// <summary>
        /// (DEPRECATED) Transforma TODOS los bare MATLAB keywords a `#`-prefixed.
        /// Mantiene un stack para resolver 'end' contextual.
        ///   for i = 1:n   →  #for i = 1:n
        ///   while cond    →  #while cond
        ///   if cond       →  #if cond
        ///   elseif cond   →  #else if cond
        ///   else          →  #else
        ///   function ...  →  #def ...
        ///   break/continue→  #break / #continue
        ///   end           →  #loop  (cierra for/while)
        ///                  #end if (cierra if)
        ///                  #end def(cierra function)
        /// </summary>
        internal static string TransformBareKeywordsToHash(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;
            var sb = new StringBuilder(src.Length + 64);
            var lines = src.Split('\n');
            var blockStack = new Stack<string>();   // "if" | "for" | "while" | "function"
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                int lead = 0;
                while (lead < line.Length && (line[lead] == ' ' || line[lead] == '\t')) lead++;
                if (lead >= line.Length)
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Si la línea ya empieza con '#' o '%', no tocar
                var c0 = line[lead];
                if (c0 == '#' || c0 == '%')
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Extraer primera palabra
                int wEnd = lead;
                while (wEnd < line.Length && (char.IsLetter(line[wEnd]) || line[wEnd] == '_'))
                    wEnd++;
                if (wEnd == lead)
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Boundary: keyword necesita terminar con whitespace, EOL, '(' o ';'
                if (wEnd < line.Length)
                {
                    var b = line[wEnd];
                    if (b != ' ' && b != '\t' && b != '\r' && b != '(' && b != ';' && b != '%')
                    {
                        sb.Append(line).Append('\n');
                        continue;
                    }
                }
                var word = line[lead..wEnd];
                var indent = line[..lead];
                var rest = line[wEnd..];
                string repl = null;
                switch (word)
                {
                    case "for":      blockStack.Push("for");      repl = "#for"; break;
                    case "while":    blockStack.Push("while");    repl = "#while"; break;
                    case "if":       blockStack.Push("if");       repl = "#if"; break;
                    case "elseif":   repl = "#else if"; break;
                    case "else":     repl = "#else"; break;
                    case "function": blockStack.Push("function"); repl = "#function"; break;
                    case "break":    repl = "#break"; break;
                    case "continue": repl = "#continue"; break;
                    case "end":
                        if (blockStack.Count == 0) { sb.Append(line).Append('\n'); continue; }
                        var kind = blockStack.Pop();
                        repl = kind switch
                        {
                            "for" => "#loop",
                            "while" => "#loop",
                            "if" => "#end if",
                            "function" => "#end function",
                            _ => null
                        };
                        if (repl == null) { sb.Append(line).Append('\n'); continue; }
                        break;
                }
                if (repl == null)
                {
                    sb.Append(line).Append('\n');
                    continue;
                }
                // Concatenar: indent + repl + rest
                sb.Append(indent);
                sb.Append(repl);
                sb.Append(rest);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Une líneas que terminan con '...' (MATLAB line continuation) con la
        /// siguiente. Respeta strings y comentarios — el '...' DENTRO de un
        /// string literal no cuenta. Si la línea tiene un comentario inline
        /// (% xxx), el '...' debe estar ANTES del '%' para considerarse continuación.
        /// </summary>
        internal static string MergeMatlabContinuations(string src)
        {
            if (string.IsNullOrEmpty(src) || !src.Contains("...")) return src;
            var sb = new StringBuilder(src.Length);
            var lines = src.Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i].TrimEnd('\r');
                // Buscar '...' fuera de strings/comments
                int dotsPos = FindMatlabContinuationDots(line);
                if (dotsPos >= 0 && i + 1 < lines.Length)
                {
                    // Línea continúa con la próxima. Concatenar sin '...' y sin newline.
                    sb.Append(line[..dotsPos].TrimEnd());
                    sb.Append(' ');  // separador entre las dos líneas
                    i++;
                    // Mantener acumulando hasta que una línea NO termine en '...'
                    while (i < lines.Length)
                    {
                        var nextLine = lines[i].TrimEnd('\r');
                        int nextDots = FindMatlabContinuationDots(nextLine);
                        if (nextDots >= 0 && i + 1 < lines.Length)
                        {
                            sb.Append(nextLine[..nextDots].TrimEnd());
                            sb.Append(' ');
                            i++;
                        }
                        else
                        {
                            sb.Append(nextLine);
                            sb.Append('\n');
                            i++;
                            break;
                        }
                    }
                }
                else
                {
                    sb.Append(line);
                    sb.Append('\n');
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encuentra la posición de '...' al final de la línea, fuera de string/comment.
        /// Retorna -1 si la línea no termina con '...' efectivo.
        /// </summary>
        private static int FindMatlabContinuationDots(string line)
        {
            if (string.IsNullOrEmpty(line)) return -1;
            bool inSq = false, inDq = false;
            // Encontrar el primer '%' fuera de strings → marca fin de código
            int codeEnd = line.Length;
            for (int j = 0; j < line.Length; j++)
            {
                var c = line[j];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '%') { codeEnd = j; break; }
            }
            // Trim trailing whitespace en la sección de código
            int e = codeEnd;
            while (e > 0 && (line[e - 1] == ' ' || line[e - 1] == '\t')) e--;
            // ¿Termina con '...'?
            if (e >= 3 && line[e - 1] == '.' && line[e - 2] == '.' && line[e - 3] == '.')
                return e - 3;
            return -1;
        }

        /// <summary>
        /// Transformaciones MATLAB plot/surf/contour → Calcpad $Plot / $Map.
        ///
        ///   plot(x, y)             → $Plot{x.i|y.i @ i = 1 : len(x)}
        ///   plot(x, y1, x, y2)     → $Plot{x.i|y1.i & x.i|y2.i @ i = 1 : len(x)}
        ///   plot(x, y, '-r')       → ignora el style string (Calcpad usa colores automáticos)
        ///   title('t')             → 'text bold heading
        ///   xlabel('x'), ylabel('y'), legend(...), grid on/off, figure, clf, hold → text/no-op
        ///
        /// Aplica SOLO a líneas que empiezan con plot(/title(/etc. (no expresiones generales).
        /// </summary>
        /// <summary>
        /// MATLAB range standalone (fuera de `for`/`while`):
        ///   <c>idx = 1:8</c>        → <c>idx = range(1; 8; 1)</c>
        ///   <c>idx = 1:2:10</c>     → <c>idx = range(1; 10; 2)</c>
        ///   <c>idx = a:n</c>        → <c>idx = range(a; n; 1)</c>
        ///
        /// NO toca:
        ///   - Líneas `for VAR = ...:...` o `while ...:...` (range legítimo)
        ///   - Líneas con `:` dentro de paréntesis (`M(:,1)` slicing — TODO separado)
        /// </summary>
        internal static string TransformStandaloneRange(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.Contains(':')) return line;
            var trimmed = line.TrimStart();
            // Saltar for/while: el `:` adentro es legítimo
            if (StartsWithKeyword(trimmed, "for") ||
                StartsWithKeyword(trimmed, "while")) return line;
            // Saltar comentarios completos
            if (trimmed.StartsWith('%')) return line;

            // Encontrar `=` (no `==`, `<=`, `>=`, `~=`)
            int eqIdx = -1;
            bool inSq = false, inDq = false;
            int parenDepth = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '%') break;  // inline comment
                if (c == '(' || c == '[' || c == '{') { parenDepth++; continue; }
                if (c == ')' || c == ']' || c == '}') { parenDepth--; continue; }
                if (parenDepth > 0) continue;
                if (c == '=')
                {
                    // Skip ==
                    if (i + 1 < line.Length && line[i + 1] == '=') { i++; continue; }
                    // Skip <=, >=, ~=
                    if (i > 0 && (line[i - 1] == '<' || line[i - 1] == '>' || line[i - 1] == '~' || line[i - 1] == '!')) continue;
                    eqIdx = i;
                    break;
                }
            }
            if (eqIdx < 0) return line;

            // RHS: desde después del `=` hasta `;` o final de línea o `%`
            int rhsStart = eqIdx + 1;
            int rhsEnd = line.Length;
            inSq = inDq = false;
            parenDepth = 0;
            for (int i = rhsStart; i < line.Length; i++)
            {
                char c = line[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(' || c == '[' || c == '{') { parenDepth++; continue; }
                if (c == ')' || c == ']' || c == '}') { parenDepth--; continue; }
                if (parenDepth > 0) continue;
                if (c == ';' || c == '%')
                {
                    rhsEnd = i;
                    break;
                }
            }
            var rhs = line.Substring(rhsStart, rhsEnd - rhsStart).Trim();
            if (rhs.Length == 0) return line;

            // ¿RHS es range `a:b` o `a:s:b`?  (sin `(` `[` adentro al top-level)
            var parts = SplitTopLevelColon(rhs);
            if (parts.Count < 2 || parts.Count > 3) return line;
            // Verificar que cada parte sea expresión escalar simple (no contiene `:` ni `;`)
            foreach (var p in parts)
            {
                var pt = p.Trim();
                if (pt.Length == 0) return line;
                if (pt.Contains(':') || pt.Contains(';')) return line;
            }
            // Construir range(start; end; step)
            string start = parts[0].Trim();
            string end_;
            string step;
            if (parts.Count == 2)
            {
                end_ = parts[1].Trim();
                step = "1";
            }
            else
            {
                step = parts[1].Trim();
                end_ = parts[2].Trim();
            }
            var lhs = line.Substring(0, rhsStart);  // incluye el `=`
            var tail = rhsEnd < line.Length ? line[rhsEnd..] : "";
            return $"{lhs} range({start}; {end_}; {step}){tail}";
        }

        /// <summary>
        /// Map Latin name → Greek char. MATLAB no admite Greek en identifiers;
        /// el usuario escribe `nu`, `alpha`, `theta` y este preprocessor reemplaza
        /// para que el motor lo guarde y renderice como Greek (igual que Calcpad nativo).
        ///
        /// Reglas:
        ///   - Match al inicio de identifier (precedido por non-word) o standalone.
        ///   - Suffix puede ser `_x`, `_1`, digit — pero NO letra (sino corta una palabra).
        ///       theta_x → θ_x   ✓
        ///       theta1  → θ1    ✓
        ///       thetapi → no transformar (es una palabra que contiene "theta")
        ///       numel   → no transformar (nu seguido de m)
        ///   - Strings y comentarios se respetan.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> _greekMap = new()
        {
            { "alpha", "α" }, { "beta", "β" }, { "gamma", "γ" }, { "delta", "δ" },
            { "epsilon", "ε" }, { "zeta", "ζ" }, { "eta", "η" }, { "theta", "θ" },
            { "iota", "ι" }, { "kappa", "κ" }, { "lambda", "λ" }, { "mu", "μ" },
            { "nu", "ν" }, { "xi", "ξ" }, { "omicron", "ο" }, { "pi", "π" },
            { "rho", "ρ" }, { "sigma", "σ" }, { "tau", "τ" }, { "upsilon", "υ" },
            { "phi", "φ" }, { "chi", "χ" }, { "psi", "ψ" }, { "omega", "ω" },
            { "Alpha", "Α" }, { "Beta", "Β" }, { "Gamma", "Γ" }, { "Delta", "Δ" },
            { "Epsilon", "Ε" }, { "Zeta", "Ζ" }, { "Eta", "Η" }, { "Theta", "Θ" },
            { "Iota", "Ι" }, { "Kappa", "Κ" }, { "Lambda", "Λ" }, { "Mu", "Μ" },
            { "Nu", "Ν" }, { "Xi", "Ξ" }, { "Omicron", "Ο" }, { "Pi", "Π" },
            { "Rho", "Ρ" }, { "Sigma", "Σ" }, { "Tau", "Τ" }, { "Upsilon", "Υ" },
            { "Phi", "Φ" }, { "Chi", "Χ" }, { "Psi", "Ψ" }, { "Omega", "Ω" }
        };

        // Regex pre-compilada: cada alias con lookbehind (no letra/digit/_) y lookahead (no letra ascii).
        // Lookahead admite digit/_ para subscripts: theta_x, nu1, etc.
        private static readonly System.Text.RegularExpressions.Regex _greekRegex =
            BuildGreekRegex();

        private static System.Text.RegularExpressions.Regex BuildGreekRegex()
        {
            // Ordenar por longitud descendente para que "omega" matchee antes que "o..." si hay overlap.
            var names = new System.Collections.Generic.List<string>(_greekMap.Keys);
            names.Sort((a, b) => b.Length.CompareTo(a.Length));
            var pattern = @"(?<![a-zA-Z0-9_])(" + string.Join("|", names) + @")(?![a-zA-Z])";
            return new System.Text.RegularExpressions.Regex(pattern,
                System.Text.RegularExpressions.RegexOptions.Compiled);
        }

        internal static string TransformGreekAliases(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Preservar strings y comentarios (mismo patrón que otros transforms)
            var sb = new StringBuilder(line.Length + 16);
            int i = 0;
            bool inSq = false, inDq = false;
            while (i < line.Length)
            {
                char c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%')
                {
                    // Comentario hasta fin de línea — preservar tal cual
                    sb.Append(line[i..]);
                    return sb.ToString();
                }
                // Scan hasta string/comentario o fin
                int chunkStart = i;
                while (i < line.Length)
                {
                    char d = line[i];
                    if (d == '\'' || d == '"' || d == '%') break;
                    i++;
                }
                var chunk = line[chunkStart..i];
                // Aplicar regex de aliases sobre el chunk
                var transformed = _greekRegex.Replace(chunk, m => _greekMap[m.Groups[1].Value]);
                sb.Append(transformed);
            }
            return sb.ToString();
        }

        internal static string TransformPlots(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            var trimmed = line.TrimStart();
            var indent = line[..(line.Length - trimmed.Length)];

            // figure, clf, hold on, hold off, grid on/off  → no-op
            if (IsPlotNoOp(trimmed)) return indent + "'(" + trimmed.TrimEnd(';').Trim() + ")";

            // title('t')  →  '**t (heading)
            if (trimmed.StartsWith("title("))
            {
                var inner = ExtractParens(trimmed, 5);
                if (inner != null)
                {
                    var s = StripFirstStringArg(inner.Trim());
                    return indent + "'** " + s;
                }
            }

            // xlabel('x') / ylabel('y') / zlabel / legend / colorbar / shading / view / axis
            //   → '<text>  (texto plano)
            // NOTA: `colormap()` se excluye aquí para que llegue al engine (native MATLAB
            // call) y pueda cambiar el colormap activo (e.g. SAP2000).
            string[] labelFns = { "xlabel(", "ylabel(", "zlabel(", "legend(", "colorbar(", "shading(", "view(", "axis(", "subplot(" };
            foreach (var fn in labelFns)
            {
                if (trimmed.StartsWith(fn))
                {
                    var inner = ExtractParens(trimmed, fn.Length - 1);
                    if (inner != null)
                    {
                        var name = fn[..^1];
                        var s = StripFirstStringArg(inner.Trim());
                        return indent + "'" + name + "(" + s + ")";
                    }
                }
            }

            // plot(x, y, ...) — el más importante
            if (trimmed.StartsWith("plot(") || trimmed.StartsWith("plot3("))
            {
                int offset = trimmed.StartsWith("plot3(") ? 5 : 4;
                var inner = ExtractParens(trimmed, offset);
                if (inner != null)
                {
                    var args = SplitTopLevelComma(inner);
                    // Filtrar strings de estilo (e.g. '-r', '--', 'b.', 'LineWidth', 2)
                    var dataArgs = new List<string>();
                    foreach (var arg in args)
                    {
                        var a = arg.Trim();
                        if (a.Length == 0) continue;
                        // String literal con quotes → estilo, omitir
                        if ((a[0] == '\'' && a[^1] == '\'') || (a[0] == '"' && a[^1] == '"'))
                            continue;
                        // 'LineWidth' o 'Color' name-value pairs: la siguiente arg numérica también se omite
                        dataArgs.Add(a);
                    }
                    if (dataArgs.Count >= 2)
                    {
                        // Pares (x, y) [, (x, y), ...]
                        var curves = new List<string>();
                        for (int p = 0; p + 1 < dataArgs.Count; p += 2)
                        {
                            curves.Add($"{dataArgs[p]}.i|{dataArgs[p + 1]}.i");
                        }
                        // Range: usar len() del primer x
                        return indent + "$Plot{" + string.Join(" & ", curves) +
                               " @ i = 1 : len(" + dataArgs[0] + ")}";
                    }
                }
            }

            return line;
        }

        private static bool IsPlotNoOp(string s)
        {
            // figure, clf, hold on/off, grid on/off, axis equal/tight/...,
            // text(x, y, str), annotation(...), drawnow, pause
            // Estos llamados se silencian para que el script corra sin error
            // cuando el rendering no aplica (CLI/no-graphics-context).
            if (s.StartsWith("figure"))
            {
                if (s.Length == 6 || s[6] == ' ' || s[6] == ';' || s[6] == '%' || s[6] == '(') return true;
            }
            if (s.StartsWith("clf"))
            {
                if (s.Length == 3 || s[3] == ' ' || s[3] == ';' || s[3] == '%' || s[3] == '(') return true;
            }
            if (s.StartsWith("hold"))
            {
                if (s.Length == 4 || s[4] == ' ' || s[4] == ';') return true;
            }
            if (s.StartsWith("grid"))
            {
                if (s.Length == 4 || s[4] == ' ' || s[4] == ';') return true;
            }
            if (s.StartsWith("axis"))
            {
                if (s.Length == 4 || s[4] == ' ' || s[4] == ';' || s[4] == '(') return true;
            }
            if (s.StartsWith("text("))   return true;
            if (s.StartsWith("annotation(")) return true;
            if (s.StartsWith("drawnow"))
            {
                if (s.Length == 7 || s[7] == ' ' || s[7] == ';' || s[7] == '(') return true;
            }
            if (s.StartsWith("pause"))
            {
                if (s.Length == 5 || s[5] == ' ' || s[5] == ';' || s[5] == '(') return true;
            }
            if (s.StartsWith("colorbar"))
            {
                if (s.Length == 8 || s[8] == ' ' || s[8] == ';' || s[8] == '(') return true;
            }
            return false;
        }

        private static string StripFirstStringArg(string args)
        {
            args = args.Trim();
            if (args.Length >= 2 && ((args[0] == '\'' && args.IndexOf('\'', 1) > 0) ||
                                     (args[0] == '"' && args.IndexOf('"', 1) > 0)))
            {
                int end = args.IndexOf(args[0], 1);
                if (end > 0) return args[1..end];
            }
            return args;
        }

        /// <summary>
        /// Shims para comandos/funciones MATLAB que no tienen equivalente Calcpad directo.
        ///   clear / clc                    → '% (comentario, no-op)
        ///   fprintf('fmt', args)           → ''formateado (text + valor de la primera arg)
        ///   disp(x)                        → x (bare expression para Calcpad muestre el valor)
        /// Si no matchea ningún shim, retorna la línea sin cambios.
        /// </summary>
        internal static string TransformShims(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            var trimmed = line.TrimStart();
            var indent = line[..(line.Length - trimmed.Length)];

            // clear / clc — no-ops en Calcpad Lab (preservan la línea pero no hacen nada visible)
            // Los convertimos en comentarios que el parser ignora.
            if (trimmed.StartsWith("clear") &&
                (trimmed.Length == 5 || trimmed[5] == ' ' || trimmed[5] == ';' || trimmed[5] == '%' || trimmed[5] == '\t'))
                return indent + "'(clear)";
            if (trimmed.StartsWith("clc") &&
                (trimmed.Length == 3 || trimmed[3] == ' ' || trimmed[3] == ';' || trimmed[3] == '%' || trimmed[3] == '\t'))
                return indent + "'(clc)";

            // disp(x) — convertir a bare expression. Si el contenido es un string literal,
            // emitir como comentario. Si es una variable o expresión, emitir tal cual
            // (Calcpad ya muestra expresiones automáticamente).
            if (trimmed.StartsWith("disp("))
            {
                var inner = ExtractParens(trimmed, 4);
                if (inner != null)
                {
                    var s = inner.Trim();
                    // String literal
                    if (s.Length >= 2 && ((s[0] == '\'' && s[^1] == '\'') || (s[0] == '"' && s[^1] == '"')))
                        return indent + "'" + s[1..^1];
                    return indent + s;
                }
            }

            // fprintf("fmt", args) / printf("fmt", args) — substitución real de format specifiers
            //   "%d"/"%i" → entero, "%f"/"%g"/"%e" → float, "%s" → string, "%x" → hex
            //   Se transforma a sintaxis Calcpad text-toggle:  'fmt-chunk' arg 'fmt-chunk' arg ...
            if (trimmed.StartsWith("fprintf(") || trimmed.StartsWith("printf("))
            {
                int openParen = trimmed.StartsWith("fprintf(") ? 7 : 6;
                var inner = ExtractParens(trimmed, openParen);
                if (inner != null)
                {
                    var result = ParseFprintfArgs(inner);
                    if (result != null)
                        return indent + result;
                }
                return indent + "'(fprintf)";
            }

            // sprintf("fmt", args) → assignar string formateado a variable.
            //   s = sprintf("fmt", a, b)  →  s = sprintf-with-substituted-vals
            // En Calcpad-Lab lo emitimos como text-toggle inline al RHS de un `=`.
            if (trimmed.Contains("sprintf("))
            {
                int eq = trimmed.IndexOf('=');
                int sp = trimmed.IndexOf("sprintf(", StringComparison.Ordinal);
                if (eq > 0 && sp > eq)
                {
                    var lhs = trimmed[..eq].Trim();
                    int openP = sp + 7;  // pos of '('
                    var inner = ExtractParens(trimmed, openP);
                    if (inner != null)
                    {
                        var sub = ParseFprintfArgs(inner);
                        if (sub != null)
                        {
                            // sub es algo como `'a = ' 5 ' b = ' 7`. Asignamos como text-comment.
                            return indent + "'" + lhs + " = " + sub.Substring(1);  // skip leading `'`
                        }
                    }
                }
            }

            return line;
        }

        /// <summary>
        /// Formatea un literal numérico según un format specifier de C/MATLAB
        /// (e.g. <c>%d</c>, <c>%.3f</c>, <c>%e</c>, <c>%g</c>).
        /// </summary>
        private static string FormatLiteralForSpec(double val, string fmt, int specStart, int specEnd, char spec)
        {
            var fs = fmt.Substring(specStart, specEnd - specStart);  // "" o ".3" o "10.2" etc
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                int dotIdx = fs.IndexOf('.');
                int precision = -1;
                if (dotIdx >= 0 && dotIdx + 1 < fs.Length &&
                    int.TryParse(fs[(dotIdx + 1)..], System.Globalization.NumberStyles.Integer,
                                 culture, out int p))
                    precision = p;
                switch (spec)
                {
                    case 'd': case 'i':
                        return ((long)val).ToString(culture);
                    case 'u':
                        return ((ulong)val).ToString(culture);
                    case 'f':
                        return val.ToString(precision < 0 ? "F6" : "F" + precision, culture);
                    case 'e':
                        return val.ToString(precision < 0 ? "E6" : "E" + precision, culture);
                    case 'g':
                        return val.ToString(precision < 0 ? "G6" : "G" + precision, culture);
                    case 'x':
                        return ((long)val).ToString("x", culture);
                    case 'o':
                        return System.Convert.ToString((long)val, 8);
                    case 'c':
                        return ((char)(int)val).ToString();
                    case 's':
                        return val.ToString(culture);
                    default:
                        return val.ToString(culture);
                }
            }
            catch { return val.ToString(culture); }
        }

        /// <summary>
        /// Parsea el contenido de fprintf/sprintf y produce sintaxis Calcpad de
        /// text-toggle: <c>'text-chunk' arg 'text-chunk' arg ...</c>.
        /// Soporta especificadores: %d, %i, %u, %f, %g, %e, %s, %x, %o, .Nf, %.Ng, etc.
        /// </summary>
        private static string ParseFprintfArgs(string inner)
        {
            inner = inner.Trim();
            if (inner.Length < 2) return null;
            // Primer arg = format string literal
            if (inner[0] != '\'' && inner[0] != '"') return null;
            char quote = inner[0];
            int fmtEnd = inner.IndexOf(quote, 1);
            if (fmtEnd < 0) return null;
            var fmt = inner[1..fmtEnd];
            // Procesar escape sequences
            fmt = fmt.Replace("\\n", "").Replace("\\t", " ").Replace("\\\\", "\\");

            // Args: lo que viene después de fmtEnd, después de la coma (skip whitespace)
            var rest = fmtEnd + 1 < inner.Length ? inner[(fmtEnd + 1)..].TrimStart() : "";
            if (rest.StartsWith(',')) rest = rest[1..].TrimStart();
            var argList = rest.Length > 0 ? SplitTopLevelComma(rest) : new List<string>();

            // Recorrer fmt buscando `%` specifiers
            var sb = new StringBuilder();
            sb.Append('\'');  // start text mode
            int argIdx = 0;
            int i = 0;
            while (i < fmt.Length)
            {
                char c = fmt[i];
                if (c == '%' && i + 1 < fmt.Length)
                {
                    int specStart = i + 1;
                    int specEnd = specStart;
                    // Skip flags, width, precision: [-+ #0]*[0-9]*[.[0-9]+]?
                    while (specEnd < fmt.Length &&
                           "-+ #0123456789.".IndexOf(fmt[specEnd]) >= 0)
                        specEnd++;
                    if (specEnd >= fmt.Length) { sb.Append(c); i++; continue; }
                    char spec = fmt[specEnd];
                    // Recognized specifiers: d i u f g e s x o c
                    if ("diufgesxoc".IndexOf(spec) >= 0)
                    {
                        if (argIdx < argList.Count)
                        {
                            var arg = argList[argIdx].Trim();
                            // Si el arg es literal numérico → emitir como texto plano
                            // (Calcpad no le agregará "= valor" redundante).
                            if (TryEvalLiteral(arg, out double litVal))
                            {
                                // Format según el specifier
                                string formatted = FormatLiteralForSpec(litVal, fmt, specStart, specEnd, spec);
                                sb.Append(formatted);
                            }
                            else
                            {
                                // Variable/expresión → cerrar text, emitir expr, reabrir text
                                sb.Append('\'').Append(' ').Append(arg).Append(' ').Append('\'');
                            }
                        }
                        else
                            sb.Append('?');  // arg faltante
                        argIdx++;
                        i = specEnd + 1;
                        continue;
                    }
                    // %% → literal %
                    if (spec == '%')
                    {
                        sb.Append('%');
                        i = specEnd + 1;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static string ExtractParens(string s, int openParenIdx)
        {
            if (openParenIdx < 0 || openParenIdx >= s.Length || s[openParenIdx] != '(') return null;
            int depth = 0;
            bool inSq = false, inDq = false;
            for (int j = openParenIdx; j < s.Length; j++)
            {
                var c = s[j];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') depth++;
                else if (c == ')') { depth--; if (depth == 0) return s[(openParenIdx + 1)..j]; }
            }
            return null;
        }

        /// <summary>
        /// Transforma asignación indexada MATLAB → Calcpad dot syntax.
        /// MATLAB:  M(i, j) = x       →  Calcpad:  M.i; j = x
        /// MATLAB:  vec(i) = x        →  Calcpad:  vec.i = x
        /// MATLAB:  M(i, j) en RHS    →  se deja como M(i; j) — Calcpad lo entiende si M es matriz
        ///
        /// Detecta el patrón LHS:  ^\s*IDENT\(.*\)\s*=\s*
        /// (asume que NO es una definición de función — en MATLAB las funcs van con 'function')
        /// </summary>
        internal static string TransformIndexedAssignment(string line, HashSet<string> userFuncs = null)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Buscar el patrón <ident>(<args>) =   al inicio de la línea (post-whitespace)
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            int idStart = i;
            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                i++;
            if (i == idStart || i >= line.Length || line[i] != '(') return line;
            var ident = line[idStart..i];
            // Reservados: no transformar palabras-keyword (for/if/etc) ni funciones built-in
            if (IsKeywordOrCalcpadFunc(ident)) return line;
            // Tampoco si es una user-function definida en el mismo script
            if (userFuncs != null && userFuncs.Contains(ident)) return line;
            // Encontrar ')' balanceado
            int parenStart = i;
            int depth = 0;
            int parenEnd = -1;
            bool inSq = false, inDq = false;
            for (int j = parenStart; j < line.Length; j++)
            {
                var c = line[j];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') depth++;
                else if (c == ')') { depth--; if (depth == 0) { parenEnd = j; break; } }
            }
            if (parenEnd == -1) return line;
            // Skip whitespace, debe seguir '='
            int afterParen = parenEnd + 1;
            while (afterParen < line.Length && (line[afterParen] == ' ' || line[afterParen] == '\t'))
                afterParen++;
            if (afterParen >= line.Length || line[afterParen] != '=') return line;
            // Caso especial: '==' no es asignación
            if (afterParen + 1 < line.Length && line[afterParen + 1] == '=') return line;
            // Construir según número de índices:
            //   1 índice simple (var/num)  → IDENT.idx = rhs
            //   1 índice expresión         → IDENT.(idx) = rhs   (parens para expresiones)
            //   2+ índices                  → IDENT.(idx1; idx2) = rhs
            var indent = line[..idStart];
            var args = line[(parenStart + 1)..parenEnd].Trim();
            var rhs = line[(afterParen + 1)..];
            bool isMultiDim = HasTopLevelSemicolon(args);
            bool isSimpleSingle = !isMultiDim && IsSimpleIdentOrNumber(args);
            var sb = new StringBuilder(line.Length);
            sb.Append(indent);
            sb.Append(ident);
            if (isSimpleSingle)
            {
                sb.Append('.');
                sb.Append(args);
            }
            else
            {
                sb.Append(".(");
                sb.Append(args);
                sb.Append(")");
            }
            sb.Append(" =");
            sb.Append(rhs);
            return sb.ToString();
        }

        /// <summary>
        /// Transforma lecturas indexadas inline: IDENT(args) → IDENT.(args) cuando
        /// IDENT no está en la lista de funciones reservadas Calcpad/MATLAB.
        /// Ej:  K = gp2(ig; 1)  →  K = gp2.(ig; 1)
        ///      x = sqrt(M(i; j))  →  x = sqrt(M.(i; j))   (sqrt reservado, M no)
        ///
        /// Para single-index: IDENT(i) → IDENT.i  (sin parens)
        /// Para multi-index:  IDENT(i; j) → IDENT.(i; j)  (con parens para preservar ';')
        ///
        /// Walks la línea por tokens detectando IDENT( y aplicando la regla.
        /// Respeta strings y nesting.
        /// </summary>
        internal static string TransformMatrixReads(string line, HashSet<string> userFuncs = null)
        {
            if (string.IsNullOrEmpty(line)) return line;
            var sb = new StringBuilder(line.Length + 16);
            int i = 0;
            bool inSq = false, inDq = false;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%')  // inline comment — copia resto tal cual
                {
                    sb.Append(line[i..]);
                    return sb.ToString();
                }
                // ¿Empieza un identifier seguido de '('?
                if (char.IsLetter(c) || c == '_')
                {
                    int idStart = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;
                    if (i < line.Length && line[i] == '(')
                    {
                        var ident = line[idStart..i];
                        // No transformar si es función reservada o keyword
                        if (IsKeywordOrCalcpadFunc(ident))
                        {
                            sb.Append(ident);
                            sb.Append('(');
                            i++;  // consume '('
                            continue;
                        }
                        // Tampoco si es una user-function definida en el script
                        if (userFuncs != null && userFuncs.Contains(ident))
                        {
                            sb.Append(ident);
                            sb.Append('(');
                            i++;
                            continue;
                        }
                        // Encontrar ')' balanceado
                        int parenStart = i;
                        int depth = 0;
                        int parenEnd = -1;
                        bool inSq2 = false, inDq2 = false;
                        for (int j = parenStart; j < line.Length; j++)
                        {
                            var cj = line[j];
                            if (inSq2) { if (cj == '\'') inSq2 = false; continue; }
                            if (inDq2) { if (cj == '"') inDq2 = false; continue; }
                            if (cj == '\'') { inSq2 = true; continue; }
                            if (cj == '"') { inDq2 = true; continue; }
                            if (cj == '(') depth++;
                            else if (cj == ')') { depth--; if (depth == 0) { parenEnd = j; break; } }
                        }
                        if (parenEnd == -1)
                        {
                            // no se cerró; emitir literal y continuar
                            sb.Append(ident);
                            sb.Append('(');
                            i = parenStart + 1;
                            continue;
                        }
                        var args = line[(parenStart + 1)..parenEnd];
                        // Transformar args recursivamente (puede haber otras lecturas indexadas adentro)
                        var transformedArgs = TransformMatrixReads(args, userFuncs);
                        // ¿Es multi-dim?
                        bool multi = HasTopLevelSemicolon(transformedArgs);
                        // ¿Esta es asignación? (espacio luego '=' no '==')
                        // Si lo es, lo deja para TransformIndexedAssignment — pero ese ya pasó.
                        // Por simplicidad acá hacemos la transformación a dot también
                        // (mismo resultado: IDENT.args  →  Calcpad lo lee bien si IDENT es matriz).
                        sb.Append(ident);
                        bool simpleSingle = !multi && IsSimpleIdentOrNumber(transformedArgs);
                        if (simpleSingle)
                        {
                            sb.Append('.');
                            sb.Append(transformedArgs.Trim());
                        }
                        else
                        {
                            sb.Append(".(");
                            sb.Append(transformedArgs);
                            sb.Append(")");
                        }
                        i = parenEnd + 1;
                        continue;
                    }
                    // No es IDENT( ... ) — sólo identifier — emitir tal cual
                    sb.Append(line[idStart..i]);
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>True si args es un identificador simple (letras/dígitos/_) o un literal numérico.</summary>
        private static bool IsSimpleIdentOrNumber(string args)
        {
            if (string.IsNullOrEmpty(args)) return false;
            args = args.Trim();
            foreach (var c in args)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                    return false;
            return true;
        }

        private static bool HasTopLevelSemicolon(string args)
        {
            int paren = 0, brack = 0, brace = 0;
            bool inSq = false, inDq = false;
            foreach (var c in args)
            {
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') brack++;
                else if (c == ']') brack--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == ';' && paren == 0 && brack == 0 && brace == 0) return true;
            }
            return false;
        }

        private static readonly HashSet<string> _calcpadReservedFuncs = new(StringComparer.OrdinalIgnoreCase)
        {
            // Calcpad scalar functions (no son indexación)
            "sin","cos","tan","asin","acos","atan","atan2","sinh","cosh","tanh",
            "csc","sec","cot","acsc","asec","acot",
            "log","ln","log10","log_2","log2","exp","sqrt","cbrt","abs","sign",
            "floor","ceil","ceiling","round","trunc","frac",
            "min","max","mod","rem","fact","fib","random","rand","randn","randi","randperm",
            // Aggregate / reductions sobre vectores y matrices (MATLAB / Calcpad)
            "sum","sumsq","srss","prod","product","mean","average","count","not","and","or","xor",
            "gcd","lcm",
            "norm","norm_e","norm_p","norm_1","norm_2","norm_i","cond","cond_2","cond_1","cond_i",
            "if","switch","take","line","spline","getr","setr","getc","setc","getv","setv",
            // Matrix
            "identity","matrix","inverse","transp","transpose","inv","det","rank","trace",
            "n_rows","n_cols","zeros","ones","eye","diagonal","row","col","column",
            "submatrix","matrow","matcol","mfill","fill","vector","len","length","size",
            "meshgrid","ndgrid","repmat","reshape","kron","cat","horzcat","vertcat",
            "lsolve","msolve","eigen","eigenvals","eigenvecs","cholesky","qr","svd","lu",
            "hp","hprod","hadamard","frobenius","kronecker",
            "vec2row","vec2col","vec2diag","diag2vec","extract_rows","extract_cols",
            // Sorting / search (MATLAB friendly)
            "sort","sort_asc","sort_desc","find","first","last","find_eq","find_ne","find_lt","find_gt","find_le","find_ge","reverse","unique",
            // Range / sequence
            "range","range_hp","linspace","logspace","arange",
            // MATLAB shims que aún no implementamos pero queremos respetar
            "fprintf","sprintf","printf","disp","display","clear","clc","format",
            // Plot / output
            "plot","plot3","surf","mesh","contour","contourf","quiver","scatter","bar",
            "title","xlabel","ylabel","zlabel","legend","colormap","colorbar","shading","view","axis","subplot","figure","clf","hold","grid",
            "text","annotation","line","rectangle","patch","fill","fill3",
            // MATLAB mesh / triangulation (interceptadas por ExpressionParser
            // como native functions → backend Triangle + Three.js)
            "delaunay","mesh2d","trimesh","triplot","trisurf","patch","tetramesh","triangulation",
            // MATLAB demo / surface generators (native en Calcpad Lab — ExecutePeaks)
            "peaks",
        };

        private static bool IsKeywordOrCalcpadFunc(string ident)
        {
            // Keywords MATLAB (bare)
            switch (ident)
            {
                case "for": case "while": case "if": case "elseif": case "else":
                case "end": case "function": case "break": case "continue":
                case "return": case "switch": case "case": case "otherwise":
                case "try": case "catch":
                    return true;
            }
            return _calcpadReservedFuncs.Contains(ident);
        }

        /// <summary>
        /// Transforma delimitadores MATLAB a Calcpad dentro de paréntesis y brackets,
        /// respetando strings y nesting. Reglas:
        ///
        /// <list type="bullet">
        ///   <item>Dentro de '[' ... ']':  ';' (sep filas) → '|', ',' (sep elem) → ';'</item>
        ///   <item>Dentro de '(' ... ')':  ',' (sep args)   → ';'</item>
        ///   <item>Top-level ',' y ';' se mantienen (top-level ';' lo maneja
        ///         SplitMatlabStatements luego)</item>
        /// </list>
        /// </summary>
        internal static string TransformDelimitersMatlabToCalcpad(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Pila de contextos: 'P' (paren), 'B' (bracket 2D), 'V' (bracket 1D vector), 'C' (curly).
            // Antes de pushear 'B', escaneamos el contenido top-level del bracket para
            // decidir si es 1D (sin separador de columna `,` ni espacio-entre-valores)
            // o 2D. En 1D, MATLAB `;` (entre elementos) → Calcpad `;` (vector). En 2D,
            // MATLAB `;` (row sep) → Calcpad `|`, y MATLAB `,` (col sep) → Calcpad `;`.
            var stack = new Stack<char>();
            var sb = new StringBuilder(line.Length + 8);
            bool inSq = false, inDq = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                // Strings: preservar tal cual
                if (inSq)
                {
                    sb.Append(c);
                    if (c == '\'') inSq = false;
                    continue;
                }
                if (inDq)
                {
                    sb.Append(c);
                    if (c == '"') inDq = false;
                    continue;
                }
                if (c == '\'') { sb.Append(c); inSq = true; continue; }
                if (c == '"') { sb.Append(c); inDq = true; continue; }
                // Comentario inline %: dejar resto tal cual
                if (c == '%' && stack.Count == 0)
                {
                    sb.Append(line[i..]);
                    return sb.ToString();
                }
                // Nesting
                if (c == '(') { stack.Push('P'); sb.Append(c); continue; }
                if (c == '[')
                {
                    // Decidir si este bracket es 1D (vector) o 2D (matriz).
                    // 1D si NO hay `,` ni espacio-separador en el top-level del bracket.
                    bool is1D = IsOneDimBracket(line, i);
                    stack.Push(is1D ? 'V' : 'B');
                    sb.Append(c);
                    continue;
                }
                if (c == '{') { stack.Push('C'); sb.Append(c); continue; }
                if (c == ')' || c == ']' || c == '}')
                {
                    if (stack.Count > 0) stack.Pop();
                    sb.Append(c);
                    continue;
                }
                // Transformación delimitadores
                if (stack.Count > 0)
                {
                    var ctx = stack.Peek();
                    if (ctx == 'V')   // 1D vector: `;` stays as `;`, `,` no debería aparecer
                    {
                        if (c == ',') { sb.Append(';'); continue; }
                        // espacio entre valores → `;` (caso `[1 2 3]`)
                        if (c == ' ')
                        {
                            char prev = sb.Length > 0 ? sb[^1] : '\0';
                            int look = i + 1;
                            while (look < line.Length && line[look] == ' ') look++;
                            if (look < line.Length)
                            {
                                char next = line[look];
                                bool prevIsValue = char.IsLetterOrDigit(prev) || prev == '_' || prev == ']' || prev == ')' || prev == '.';
                                bool nextIsValue = char.IsLetterOrDigit(next) || next == '_' || next == '(' || next == '[' || next == '.';
                                if (!nextIsValue && (next == '-' || next == '+'))
                                    nextIsValue = true;
                                if (prevIsValue && nextIsValue)
                                {
                                    sb.Append(';');
                                    i = look - 1;
                                    continue;
                                }
                            }
                        }
                    }
                    else if (ctx == 'B')   // 2D matriz
                    {
                        if (c == ';') { sb.Append('|'); continue; }
                        if (c == ',') { sb.Append(';'); continue; }
                        if (c == ' ')
                        {
                            char prev = sb.Length > 0 ? sb[^1] : '\0';
                            int look = i + 1;
                            while (look < line.Length && line[look] == ' ') look++;
                            if (look < line.Length)
                            {
                                char next = line[look];
                                bool prevIsValue = char.IsLetterOrDigit(prev) || prev == '_' || prev == ']' || prev == ')' || prev == '.';
                                bool nextIsValue = char.IsLetterOrDigit(next) || next == '_' || next == '(' || next == '[' || next == '.';
                                if (!nextIsValue && (next == '-' || next == '+'))
                                    nextIsValue = true;
                                if (prevIsValue && nextIsValue)
                                {
                                    sb.Append(';');
                                    i = look - 1;
                                    continue;
                                }
                            }
                        }
                    }
                    else if (ctx == 'P')   // dentro de (..)
                    {
                        if (c == ',') { sb.Append(';'); continue; }
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Determina si el bracket que empieza en <paramref name="openIdx"/> es un
        /// vector 1D de SCALARES (todos los elementos son números/expresiones-numéricas
        /// simples). En este caso `[a; b; c]` → Calcpad vector 1D (`.i` indexable).
        ///
        /// Devuelve <c>false</c> (= 2D matrix concat) cuando:
        ///   - Hay coma <c>,</c> (sintaxis 2D mixta)
        ///   - Algún elemento es un identifier solo (probable matriz/vector pre-existente)
        ///   - Algún elemento contiene un function call <c>foo(args)</c>
        ///   - Algún elemento contiene `[` (sub-vector nested)
        /// Tratamos casos puramente literales/scalares como 1D para preservar el
        /// pattern común <c>v = [1; 2; 3]; v(i)</c>.
        /// </summary>
        private static bool IsOneDimBracket(string line, int openIdx)
        {
            int depth = 0;
            bool inSq = false, inDq = false;
            int elemStart = openIdx + 1;
            bool hasIdent = false;
            for (int j = openIdx; j < line.Length; j++)
            {
                char c = line[j];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(' || c == '[' || c == '{')
                {
                    // Function call or nested bracket dentro del top-level → 2D (matrices)
                    if (depth == 1) return false;
                    depth++;
                    continue;
                }
                if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                    if (depth == 0 && c == ']')
                    {
                        // Cierre del bracket. Si vimos identifier(s), es 2D.
                        return !hasIdent;
                    }
                    continue;
                }
                if (depth == 1)  // top-level dentro del bracket
                {
                    if (c == ',') return false;
                    // Detectar identifier (letra seguida de letras/digits/_)
                    if (char.IsLetter(c) || c == '_')
                    {
                        // Lookahead: es identifier completo? Si después hay `(` es function-call
                        int k = j + 1;
                        while (k < line.Length && (char.IsLetterOrDigit(line[k]) || line[k] == '_'))
                            k++;
                        // Reservados aceptables que NO indican matrix-concat:
                        // ninguno en este nivel — tratamos cualquier identifier como
                        // potencial matrix → 2D stack.
                        hasIdent = true;
                    }
                    if (c == ' ')
                    {
                        // Space-as-separator entre valores: 2D row vector
                        char prev = j > 0 ? line[j - 1] : '\0';
                        int look = j + 1;
                        while (look < line.Length && line[look] == ' ') look++;
                        if (look < line.Length)
                        {
                            char next = line[look];
                            bool prevIsValue = char.IsLetterOrDigit(prev) || prev == '_' || prev == ']' || prev == ')' || prev == '.';
                            bool nextIsValue = char.IsLetterOrDigit(next) || next == '_' || next == '(' || next == '[' || next == '.';
                            if (!nextIsValue && (next == '-' || next == '+'))
                                nextIsValue = true;
                            if (prevIsValue && nextIsValue) return false;
                        }
                    }
                }
            }
            return !hasIdent;
        }

        /// <summary>
        /// Traduce operadores de comparación / lógicos MATLAB a sus equivalentes Calcpad:
        ///   <c>&lt;=</c> → <c>≤</c>
        ///   <c>&gt;=</c> → <c>≥</c>
        ///   <c>~=</c>   → <c>≠</c>
        ///   <c>==</c>   → <c>≡</c>   (Calcpad usa ≡ como igualdad binaria)
        ///
        /// Respeta strings (simples y dobles) y comentarios (% ...) para no romper texto.
        /// IMPORTANTE: <c>~</c> SOLO se traduce a <c>≠</c> cuando va seguido inmediatamente de <c>=</c>.
        /// </summary>
        /// <summary>
        /// MATLAB element-wise operators:
        ///   A.*B  →  hprod(A; B)     (Hadamard product — element-wise multiply)
        ///   A.^2  →  hprod(A; A)     (element-wise square — caso común)
        ///   A.^N  →  matrix_pow_e(A, N)   (general case — TODO)
        ///   A./B  →  hp(A; mfill(1./B))   (element-wise divide — TODO simplificado)
        /// Respeta strings y comentarios.
        /// </summary>
        internal static string TransformElementWiseOps(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            if (line.IndexOf(".*", StringComparison.Ordinal) < 0 &&
                line.IndexOf(".^", StringComparison.Ordinal) < 0 &&
                line.IndexOf("./", StringComparison.Ordinal) < 0) return line;
            var sb = new StringBuilder(line.Length + 16);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                // Detectar `OPERAND .{*,/,^} OPERAND` pattern
                // .* o .^ o ./
                if (c == '.' && i + 1 < line.Length &&
                    (line[i + 1] == '*' || line[i + 1] == '^' || line[i + 1] == '/'))
                {
                    char op = line[i + 1];
                    // Extract left operand (lookback through current sb)
                    int operandStartInSb;
                    string left = ExtractTrailingOperand(sb, out operandStartInSb);
                    // Extract right operand (lookahead)
                    int rEnd = i + 2;
                    string right = ExtractLeadingOperand(line, ref rEnd);
                    if (left != null && right != null)
                    {
                        // Recursar dentro de operandos balanceados — si el operando
                        // contiene `.*`, `.^` o `./` internos (típicamente paréntesis
                        // como `exp(-X.^2 - Y.^2)`), hay que transformarlos también.
                        // Sin esto, los element-wise ops anidados quedan sin
                        // traducir y el MathParser falla.
                        if (left.IndexOfAny(['.', '*', '^', '/']) >= 0 &&
                            (left.Contains(".*", StringComparison.Ordinal) ||
                             left.Contains(".^", StringComparison.Ordinal) ||
                             left.Contains("./", StringComparison.Ordinal)))
                            left = TransformElementWiseOps(left);
                        if (right.IndexOfAny(['.', '*', '^', '/']) >= 0 &&
                            (right.Contains(".*", StringComparison.Ordinal) ||
                             right.Contains(".^", StringComparison.Ordinal) ||
                             right.Contains("./", StringComparison.Ordinal)))
                            right = TransformElementWiseOps(right);
                        // Truncate sb to just before the left operand (incluye trailing ws)
                        sb.Length = operandStartInSb;
                        if (op == '*')
                            sb.Append("hprod(").Append(left).Append("; ").Append(right).Append(")");
                        else if (op == '^' && right == "2")
                            sb.Append("hprod(").Append(left).Append("; ").Append(left).Append(")");
                        else if (op == '^')
                        {
                            // General element-wise power — fallback (Calcpad lo aplica element-wise para matrix^scalar)
                            sb.Append(left).Append("^(").Append(right).Append(")");
                        }
                        else // '/'
                        {
                            // Element-wise divide: A./B
                            sb.Append(left).Append("/(").Append(right).Append(")");
                        }
                        i = rEnd;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extrae el operando inmediatamente trailing en sb (identifier, número, o
        /// paréntesis balanceado). Devuelve la string y el offset donde empieza
        /// (incluyendo trailing whitespace después del operando, para que el caller
        /// pueda truncar sb hasta antes del operando completo).
        /// </summary>
        private static string ExtractTrailingOperand(StringBuilder sb, out int operandStart)
        {
            operandStart = sb.Length;
            int n = sb.Length;
            if (n == 0) return null;
            // Skip trailing whitespace
            while (n > 0 && (sb[n - 1] == ' ' || sb[n - 1] == '\t')) n--;
            if (n == 0) return null;
            int end = n;
            char last = sb[n - 1];
            if (last == ')')
            {
                int depth = 1;
                int k = n - 2;
                while (k >= 0 && depth > 0)
                {
                    if (sb[k] == ')') depth++;
                    else if (sb[k] == '(') depth--;
                    k--;
                }
                if (depth > 0) return null;
                int start = k + 1;
                while (start > 0 && (char.IsLetterOrDigit(sb[start - 1]) || sb[start - 1] == '_'))
                    start--;
                operandStart = start;
                return sb.ToString(start, end - start);
            }
            int s = n - 1;
            while (s > 0 && (char.IsLetterOrDigit(sb[s - 1]) || sb[s - 1] == '_' || sb[s - 1] == '.'))
                s--;
            if (s == n) return null;
            operandStart = s;
            return sb.ToString(s, end - s);
        }

        /// <summary>Extrae el operando leading desde line[start..]; avanza start al fin del operando.</summary>
        private static string ExtractLeadingOperand(string line, ref int start)
        {
            // Skip whitespace
            while (start < line.Length && (line[start] == ' ' || line[start] == '\t')) start++;
            if (start >= line.Length) return null;
            int begin = start;
            char first = line[start];
            // Optional unary -
            if (first == '-' || first == '+')
            {
                start++;
                if (start >= line.Length) return null;
            }
            if (line[start] == '(')
            {
                // Balanced parens
                int depth = 1;
                int k = start + 1;
                while (k < line.Length && depth > 0)
                {
                    if (line[k] == '(') depth++;
                    else if (line[k] == ')') depth--;
                    k++;
                }
                if (depth > 0) return null;
                start = k;
                return line[begin..start];
            }
            // Identifier or number, possibly followed by (...) for function call
            while (start < line.Length && (char.IsLetterOrDigit(line[start]) || line[start] == '_' || line[start] == '.'))
                start++;
            if (start < line.Length && line[start] == '(')
            {
                int depth = 1;
                int k = start + 1;
                while (k < line.Length && depth > 0)
                {
                    if (line[k] == '(') depth++;
                    else if (line[k] == ')') depth--;
                    k++;
                }
                if (depth > 0) return null;
                start = k;
            }
            if (start == begin) return null;
            return line[begin..start];
        }

        internal static string TransformComparisonOperators(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Fast-path: nada que traducir
            if (line.IndexOf('<') < 0 && line.IndexOf('>') < 0 && line.IndexOf('~') < 0 &&
                line.IndexOf("==", StringComparison.Ordinal) < 0 &&
                line.IndexOf("||", StringComparison.Ordinal) < 0 &&
                line.IndexOf("&&", StringComparison.Ordinal) < 0) return line;
            var sb = new StringBuilder(line.Length);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }
                // ==  →  ≡   (igualdad binaria; sólo si no es === o =>)
                if (c == '=' && i + 1 < line.Length && line[i + 1] == '=' &&
                    (i + 2 >= line.Length || line[i + 2] != '='))
                {
                    sb.Append('≡'); i += 2; continue;
                }
                // <=  →  ≤   (pero NO == ni <=>)
                if (c == '<' && i + 1 < line.Length && line[i + 1] == '=')
                {
                    sb.Append('≤'); i += 2; continue;
                }
                // >=  →  ≥
                if (c == '>' && i + 1 < line.Length && line[i + 1] == '=')
                {
                    sb.Append('≥'); i += 2; continue;
                }
                // ~=  →  ≠   (sólo ~=, no '~' aislado — eso sería 'not')
                if (c == '~' && i + 1 < line.Length && line[i + 1] == '=')
                {
                    sb.Append('≠'); i += 2; continue;
                }
                // ||  →  ∨   (lógico OR — short-circuit en MATLAB)
                if (c == '|' && i + 1 < line.Length && line[i + 1] == '|')
                {
                    sb.Append('∨'); i += 2; continue;
                }
                // &&  →  ∧   (lógico AND — short-circuit en MATLAB)
                if (c == '&' && i + 1 < line.Length && line[i + 1] == '&')
                {
                    sb.Append('∧'); i += 2; continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convierte notación científica MATLAB <c>25e6</c>, <c>2.5e-3</c>, <c>1.0E+12</c>
        /// a la forma explícita Calcpad <c>(25*10^6)</c>, <c>(2.5*10^-3)</c>, <c>(1.0*10^12)</c>.
        /// Calcpad NO soporta scientific-notation con 'e' (lee 'e' como unidad).
        ///
        /// Patrón: <c>\d[\d.]*[eE][+-]?\d+</c> precedido por algo que NO sea letra/_/dígito
        /// (para no romper identificadores como <c>r1e6</c> o variables).
        /// Respeta strings y comentarios.
        /// </summary>
        internal static string TransformScientificNotation(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Fast-path: no hay e/E entre dígitos
            if (line.IndexOf('e') < 0 && line.IndexOf('E') < 0) return line;
            var sb = new StringBuilder(line.Length + 8);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                // Inicio de número: dígito, NO precedido por letra/dígito/_
                if (char.IsDigit(c))
                {
                    char prev = sb.Length > 0 ? sb[^1] : '\0';
                    if (char.IsLetter(prev) || prev == '_')
                    {
                        // forma parte de un identificador como x1, var2 — emitir tal cual
                        sb.Append(c); i++; continue;
                    }
                    // Consumir parte mantisa: dígitos[.dígitos]?
                    int start = i;
                    while (i < line.Length && char.IsDigit(line[i])) i++;
                    bool hasDot = false;
                    if (i < line.Length && line[i] == '.' &&
                        i + 1 < line.Length && char.IsDigit(line[i + 1]))
                    {
                        hasDot = true;
                        i++;
                        while (i < line.Length && char.IsDigit(line[i])) i++;
                    }
                    // ¿Hay exponente?
                    if (i < line.Length && (line[i] == 'e' || line[i] == 'E'))
                    {
                        int eIdx = i;
                        int j = i + 1;
                        bool hasSign = false;
                        if (j < line.Length && (line[j] == '+' || line[j] == '-')) { hasSign = true; j++; }
                        int expStart = j;
                        while (j < line.Length && char.IsDigit(line[j])) j++;
                        if (j > expStart)
                        {
                            // Match real: emitir (mantisa*10^exp)
                            var mantissa = line[start..eIdx];
                            var sign = hasSign ? line[i + 1].ToString() : "";
                            var exp = line[expStart..j];
                            sb.Append('(');
                            sb.Append(mantissa);
                            sb.Append("*10^");
                            if (sign == "-") sb.Append('-');
                            sb.Append(exp);
                            sb.Append(')');
                            i = j;
                            // suprimir warning
                            _ = hasDot;
                            continue;
                        }
                    }
                    // Sin exponente: emitir tal cual
                    sb.Append(line.AsSpan(start, i - start));
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Pre-evalúa funciones MATLAB de generación de rangos cuando sus argumentos
        /// son todos CONSTANTES NUMÉRICAS, usando el DLL nativo <c>matlab_helpers.dll</c>:
        ///
        ///   <c>linspace(0, 10, 5)</c>      → <c>[0, 2.5, 5, 7.5, 10]</c>
        ///   <c>logspace(0, 3, 4)</c>       → <c>[1, 10, 100, 1000]</c>
        ///   <c>arange(1, 5, 1)</c>         → <c>[1, 2, 3, 4, 5]</c>
        ///
        /// Si CUALQUIER argumento no es literal numérico, deja la línea sin cambios
        /// (el parser dará "Invalid function" igual que antes, pero al menos no
        /// rompemos casos válidos).
        ///
        /// El DLL puede no estar cargado; en ese caso degrada graceful retornando
        /// la línea original.
        /// </summary>
        internal static string TransformConstantRangeFns(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Fast-path: ninguna de las funciones interesantes aparece
            if (!line.Contains("linspace(", StringComparison.Ordinal)
             && !line.Contains("logspace(", StringComparison.Ordinal)
             && !line.Contains("arange(", StringComparison.Ordinal))
                return line;

            // Verificar DLL disponible (cache simple)
            if (!MatlabHelpersInterop.IsAvailable()) return line;

            var sb = new StringBuilder(line.Length + 32);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                // Probar matches: cada función con su firma
                string fnName = null;
                int fnLen = 0;
                if (TryStartsWith(line, i, "linspace(", out _)) { fnName = "linspace"; fnLen = 8; }
                else if (TryStartsWith(line, i, "logspace(", out _)) { fnName = "logspace"; fnLen = 8; }
                else if (TryStartsWith(line, i, "arange(", out _)) { fnName = "arange"; fnLen = 6; }

                if (fnName == null) { sb.Append(c); i++; continue; }

                // Boundary izquierda: char anterior no debe ser letra/dígito/_
                bool leftBoundary = (sb.Length == 0) || !(char.IsLetterOrDigit(sb[^1]) || sb[^1] == '_');
                if (!leftBoundary) { sb.Append(c); i++; continue; }

                int parenStart = i + fnLen;
                int parenEnd = FindMatchingParen(line, parenStart);
                if (parenEnd == -1) { sb.Append(c); i++; continue; }

                var argsRaw = line[(parenStart + 1)..parenEnd];
                var args = SplitTopLevelComma(argsRaw);
                if (fnName == "arange" && args.Count != 3) { sb.Append(c); i++; continue; }
                if (fnName != "arange" && args.Count != 3) { sb.Append(c); i++; continue; }

                // Todos los args deben ser literales numéricos
                if (!TryParseConstant(args[0], out double a)
                 || !TryParseConstant(args[1], out double b)
                 || !TryParseConstant(args[2], out double cArg))
                {
                    sb.Append(c); i++; continue;
                }

                double[] values;
                try
                {
                    switch (fnName)
                    {
                        case "linspace":
                            values = MatlabHelpersInterop.Linspace(a, b, (int)cArg);
                            break;
                        case "logspace":
                            values = MatlabHelpersInterop.Logspace(a, b, (int)cArg);
                            break;
                        case "arange":
                            values = MatlabHelpersInterop.Arange(a, b, cArg);
                            break;
                        default:
                            sb.Append(c); i++; continue;
                    }
                }
                catch
                {
                    // P/Invoke falló — degrade graceful
                    sb.Append(c); i++; continue;
                }

                // Emitir vector literal [v1, v2, ..., vN]  (MATLAB syntax;
                // el TransformDelimiters lo convertirá a [v1;v2;...] Calcpad)
                if (values.Length == 0)
                {
                    sb.Append("vector(0)");  // Calcpad no tiene [] vacío
                }
                else
                {
                    sb.Append('[');
                    for (int k = 0; k < values.Length; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append(values[k].ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    sb.Append(']');
                }
                i = parenEnd + 1;
            }
            return sb.ToString();
        }

        /// <summary>Helper: <c>s</c> empieza en <c>idx</c> con la palabra clave <c>kw</c>.</summary>
        private static bool TryStartsWith(string s, int idx, string kw, out int _)
        {
            _ = 0;
            if (idx + kw.Length > s.Length) return false;
            return s.AsSpan(idx, kw.Length).Equals(kw, StringComparison.Ordinal);
        }

        /// <summary>
        /// Intenta parsear un argumento como literal numérico (acepta '-', '+', '.',
        /// dígitos y notación científica YA EXPANDIDA — recordá que TransformScientific
        /// la convierte a <c>(25*10^6)</c>, así que aquí también aceptamos expresiones
        /// puramente numéricas simples; usamos <c>double.TryParse</c> con InvariantCulture).
        /// </summary>
        private static bool TryParseConstant(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            // Caso A: literal puro "0", "1.5", "-3"
            if (double.TryParse(t, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;
            // Caso B: (mantisa*10^exp) — generado por TransformScientificNotation
            //         Resolver: extraer mantisa y exp, calcular value.
            if (t.StartsWith("(") && t.EndsWith(")"))
            {
                var inner = t[1..^1];
                var idx = inner.IndexOf("*10^", StringComparison.Ordinal);
                if (idx > 0)
                {
                    var mant = inner[..idx];
                    var expS = inner[(idx + 4)..];
                    if (double.TryParse(mant, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out double m)
                     && int.TryParse(expS, System.Globalization.NumberStyles.Integer,
                                     System.Globalization.CultureInfo.InvariantCulture, out int e))
                    {
                        value = m * Math.Pow(10, e);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Traduce identificadores MATLAB → Calcpad cuando hay un mapeo 1:1 directo.
        /// Por ahora:
        ///   <c>length(</c> → <c>len(</c>     (Calcpad usa len; len() acepta vector o matrix)
        ///   <c>numel(</c>  → <c>len(</c>     (similar — número de elementos)
        ///
        /// Respeta strings/comentarios. Sólo reemplaza cuando es palabra completa
        /// (no captura "mylength" o "lengthen" — usa boundary check).
        /// </summary>
        internal static string TransformBuiltinAliases(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            // Lista de aliases MATLAB → Calcpad (orden: more-specific first).
            //
            // OJO con los conflictos semánticos:
            //   MATLAB:  log = ln (natural log)        Calcpad: log = log10
            //   MATLAB:  log10 = base 10               Calcpad: log = log10
            //   MATLAB:  log2  = base 2                Calcpad: log_2
            //   MATLAB:  ceil  = redondear arriba      Calcpad: ceiling
            //   MATLAB:  prod  = producto              Calcpad: product
            //   MATLAB:  mean  = media aritmética      Calcpad: mean = ¿geom?, average = arit
            //
            // La transformación MAPEA semántica MATLAB → la función Calcpad correcta.
            var aliases = new (string from, string to)[]
            {
                ("length(", "len("),
                ("numel(",  "len("),
                ("log10(",  "log("),     // base 10 — MATLAB log10 = Calcpad log
                ("log2(",   "log_2("),   // base 2
                ("log(",    "ln("),      // natural — MATLAB log = Calcpad ln
                ("ceil(",   "ceiling("),
                ("prod(",   "product("),
                ("mean(",   "average("), // arithmetic mean
            };
            // Quick check
            bool any = false;
            foreach (var (f, _) in aliases) if (line.Contains(f, StringComparison.Ordinal)) { any = true; break; }
            if (!any) return line;

            var sb = new StringBuilder(line.Length);
            bool inSq = false, inDq = false;
            int i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (inSq) { sb.Append(c); if (c == '\'') inSq = false; i++; continue; }
                if (inDq) { sb.Append(c); if (c == '"') inDq = false; i++; continue; }
                if (c == '\'') { sb.Append(c); inSq = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '%') { sb.Append(line[i..]); return sb.ToString(); }

                // Probar matches: identifier boundary requerido a la izquierda
                bool matched = false;
                foreach (var (from, to) in aliases)
                {
                    if (i + from.Length > line.Length) continue;
                    if (line.AsSpan(i, from.Length).Equals(from, StringComparison.Ordinal))
                    {
                        // Verificar que el char anterior NO sea letra/dígito/_ (sino es lengthen, etc.)
                        bool leftBoundary = (sb.Length == 0) || !(char.IsLetterOrDigit(sb[^1]) || sb[^1] == '_');
                        if (leftBoundary)
                        {
                            sb.Append(to);
                            i += from.Length;
                            matched = true;
                            break;
                        }
                    }
                }
                if (matched) continue;
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static List<string> SplitMatlabStatements(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                result.Add(line);
                return result;
            }
            int paren = 0, brack = 0, brace = 0;
            bool inSq = false, inDq = false;
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inSq) { if (c == '\'') inSq = false; continue; }
                if (inDq) { if (c == '"') inDq = false; continue; }
                if (c == '\'') { inSq = true; continue; }
                if (c == '"') { inDq = true; continue; }
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') brack++;
                else if (c == ']') brack--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == '%' && paren == 0 && brack == 0 && brace == 0)
                {
                    // Comentario inline encontrado. Si ANTES hubo `;` (statement-split)
                    // y entre el `;` y el `%` solo hay whitespace, el comentario pertenece
                    // al statement SUPRIMIDO previo — no emitirlo como statement nuevo.
                    bool onlyWsBeforeComment = true;
                    for (int k = start; k < i; k++)
                    {
                        if (line[k] != ' ' && line[k] != '\t') { onlyWsBeforeComment = false; break; }
                    }
                    if (result.Count > 0 && onlyWsBeforeComment)
                    {
                        // Append comment al último statement; ya está suprimido por `;`
                        result[^1] = result[^1] + line[start..];
                        start = line.Length;  // todo consumido
                    }
                    break;
                }
                else if (c == ';' && paren == 0 && brack == 0 && brace == 0)
                {
                    // Statement separator MATLAB. Incluir el ';' para preservar suppression.
                    result.Add(line[start..(i + 1)]);
                    start = i + 1;
                }
            }
            if (start < line.Length)
                result.Add(line[start..]);
            else if (result.Count == 0)
                result.Add(line);
            return result;
        }
    }
}
