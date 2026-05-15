// Calcpad Lab — Test helper que corre un script .m completo via ExpressionParser
// con IsMatlabSyntax=true y MatlabPreprocessor.Process aplicado.
// Devuelve el HTML output + variables del scope para que los tests puedan
// hacer Assert.Equal sobre valores numéricos finales.

using System.Text.RegularExpressions;

namespace Calcpad.Lab.Tests
{
    internal class TestLab
    {
        private readonly ExpressionParser _parser;

        public TestLab(Settings? settings = null)
        {
            // Default: radianes (Math.Degrees=1) para emular MATLAB nativo.
            // El usuario puede overridir pasando settings con Degrees=0 (grados).
            settings ??= new Settings();
            if (settings.Math.Degrees == 0) settings.Math.Degrees = 1;
            _parser = new ExpressionParser
            {
                Settings = settings,
                IsMatlabSyntax = true,
            };
        }

        /// <summary>
        /// Corre un script MATLAB completo. Retorna el HTML output.
        /// </summary>
        public string Run(string matlabScript)
        {
            // Replicar pipeline del CLI/WPF: preprocess → parser.Parse
            var preprocessed = MatlabPreprocessor.Process(matlabScript);
            _parser.Parse(preprocessed);
            return _parser.HtmlResult ?? "";
        }

        /// <summary>
        /// Cuenta cuántos errores generó el script (tags class="err" en el HTML).
        /// </summary>
        public int CountErrors(string html)
            => Regex.Matches(html, "class=\"err").Count;

        /// <summary>
        /// Extrae el body útil del HTML (sin <script>, <style>, head).
        /// </summary>
        public string ExtractBody(string html)
        {
            var endScript = html.LastIndexOf("</script>");
            if (endScript < 0) return html;
            var bodyStart = endScript + "</script>".Length;
            var bodyEnd = html.IndexOf("</body>", bodyStart);
            if (bodyEnd < 0) bodyEnd = html.Length;
            return html.Substring(bodyStart, bodyEnd - bodyStart);
        }

        /// <summary>
        /// Helper combinado: corre + cuenta errores en una sola llamada.
        /// </summary>
        public int RunAndCountErrors(string matlabScript)
            => CountErrors(Run(matlabScript));
    }
}
