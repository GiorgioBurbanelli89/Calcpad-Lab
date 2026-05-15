// Tests del parser MATLAB nativo de Calcpad Lab.
// Cubren sintaxis básica: comentarios, suppression, multi-statement, headings, line continuation.

namespace Calcpad.Lab.Tests
{
    public class BasicSyntaxTests
    {
        [Fact]
        [Trait("Category", "Comments")]
        public void Percent_LineComment_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("% Esto es un comentario\nx = 5");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("Esto es un comentario", html);
        }

        [Fact]
        [Trait("Category", "Comments")]
        public void Percent_InlineComment_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("x = 5 % comentario inline");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("comentario inline", html);
        }

        [Fact]
        [Trait("Category", "Comments")]
        public void DoublePercent_Section_RendersAsH2()
        {
            var lab = new TestLab();
            var html = lab.Run("%% Section Heading\nx = 1");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("<h2", html);
            Assert.Contains("Section Heading", html);
        }

        [Fact]
        [Trait("Category", "Suppression")]
        public void Semicolon_SuppressesOutput()
        {
            var lab = new TestLab();
            // x = 5; suprime; sin ; muestra
            var html = lab.Run("x = 5;\ny = 10\nz = 15;");
            Assert.Equal(0, lab.CountErrors(html));
            var body = lab.ExtractBody(html);
            // y debe aparecer; x y z NO
            Assert.Contains("y", body);
            // Quick check: NO debe haber "<var>x</var> = 5"
            Assert.DoesNotContain("<var>x</var> = 5", body);
            Assert.DoesNotContain("<var>z</var> = 15", body);
        }

        [Fact]
        [Trait("Category", "MultiStatement")]
        public void MultipleStatements_PerLine_Split()
        {
            var lab = new TestLab();
            var html = lab.Run("a = 1; b = 2; c = a + b");
            Assert.Equal(0, lab.CountErrors(html));
            // c se evalúa y es visible
            var body = lab.ExtractBody(html);
            Assert.Contains("c", body);
        }

        [Fact]
        [Trait("Category", "LineContinuation")]
        public void Dots_LineContinuation_JoinsLines()
        {
            var lab = new TestLab();
            var html = lab.Run("x = 1 + ...\n    2 + ...\n    3");
            Assert.Equal(0, lab.CountErrors(html));
            // Resultado x = 6
            var body = lab.ExtractBody(html);
            Assert.Contains("6", body);
        }
    }
}
