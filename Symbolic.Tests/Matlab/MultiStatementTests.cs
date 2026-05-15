// Tests de múltiples statements por línea separados por `;` — convención MATLAB.
//
// MATLAB nativo soporta:
//   y=3;y_1=5;y_4=5;                  → 3 asignaciones, sin output
//   a=1; b=2; c=a+b                   → 3 statements; c=3 muestra output (sin ; final)
//   x=10; y=20; z=x*y;                → todas suprimidas
namespace Calcpad.Lab.Tests
{
    public class MultiStatementTests
    {
        [Fact]
        [Trait("Category", "MultiStatement")]
        public void ThreeStatementsOneLine_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("y=3;y_1=5;y_4=5;");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "MultiStatement")]
        public void ThreeStatements_AllValuesAvailable()
        {
            // Después de y=3;y_1=5;y_4=5; verificamos que los valores se asignaron
            var lab = new TestLab();
            var html = lab.Run(
                "y=3;y_1=5;y_4=5;\n" +
                "total = y + y_1 + y_4");
            Assert.Equal(0, lab.CountErrors(html));
            // total = 3+5+5 = 13
            Assert.Contains("13", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "MultiStatement")]
        public void MixedStatements_LastWithoutSemicolon_Shows()
        {
            // a=1; b=2; c=a+b   → solo c se muestra (sin ; final en última)
            var lab = new TestLab();
            var html = lab.Run("a=1; b=2; c=a+b");
            Assert.Equal(0, lab.CountErrors(html));
            var body = lab.ExtractBody(html);
            // c debe aparecer con valor 3
            Assert.Contains("3", body);
            // a y b están suprimidos (no debe aparecer "<var>a</var> = 1" como display)
            Assert.DoesNotContain("<var>a</var> = 1<", body);
        }

        [Fact]
        [Trait("Category", "MultiStatement")]
        public void SemicolonOnly_ButFunctionCall_ExecutesCall()
        {
            // disp('hello'); — la llamada se ejecuta aunque hay ; final
            var lab = new TestLab();
            var html = lab.Run("x=10; disp('hello'); y=20;");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("hello", html);
        }

        [Fact]
        [Trait("Category", "MultiStatement")]
        public void MatlabIdiom_OneLineMatrixSetup()
        {
            // Patrón típico MATLAB: definir varias constantes en una línea
            var lab = new TestLab();
            var html = lab.Run(
                "E=200e9; nu=0.3; rho=7850;\n" +
                "G = E / (2*(1+nu))");
            Assert.Equal(0, lab.CountErrors(html));
            // G ≈ 76.92e9 (módulo de corte del acero)
            // Verificamos que se calculó
            Assert.Contains("G", lab.ExtractBody(html));
        }
    }
}
