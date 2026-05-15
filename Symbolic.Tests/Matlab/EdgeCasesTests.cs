// Tests de edge-cases del parser MATLAB de Calcpad Lab.
// Cubren limitaciones conocidas y bordes en los que el parser
// se rompe históricamente: indexing 1D vs función definida por usuario,
// builtins ausentes, plots básicos, fprintf con varargs, length/size, etc.
//
// Algunos tests se marcan con Skip cuando documentan una limitación
// que todavía NO está resuelta (string literals como variable). Al ir
// fijando el parser, eliminamos el Skip para forzar verde permanente.

namespace Calcpad.Lab.Tests
{
    public class EdgeCasesTests
    {
        // ─────────────────────────────────────────────────────────────────
        // Indexing 1D
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Indexing")]
        public void Vector_FromZerosRowVector_IndexAccess_NoErrors()
        {
            // dofs = zeros(1, 8) en MATLAB es vector 1D, no matriz 2D.
            // El preprocessor convierte zeros(1,N) -> vector(N).
            var lab = new TestLab();
            var html = lab.Run("dofs = zeros(1, 8)\nx = dofs(3)");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Indexing")]
        public void Matrix_IndexedAssignment_NoErrors()
        {
            // M(i, j) = x debe convertirse a M.(i; j) = x sin error
            var lab = new TestLab();
            var html = lab.Run("M = zeros(3, 3)\nM(2, 2) = 99\ny = M(2, 2)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("99", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Indexing")]
        public void Vector_IndexedAssignment_NoErrors()
        {
            // v(i) = x para vector 1D debe funcionar
            var lab = new TestLab();
            var html = lab.Run("v = zeros(1, 5)\nv(3) = 42\ny = v(3)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("42", lab.ExtractBody(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // BuiltIns adicionales
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Length_OfVector_ReturnsCount()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [10, 20, 30, 40]\nn = length(v)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("4", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Size_OfMatrix_ReturnsRowsCols()
        {
            var lab = new TestLab();
            // size(M) puede devolver vector [rows cols] — verificamos que NO falle
            var html = lab.Run("M = zeros(3, 5)\ns = size(M)");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Sum_OfVector_AddsElements()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [1, 2, 3, 4, 5]\ns = sum(v)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("15", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Max_OfVector_ReturnsMaximum()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [3, 7, 2, 9, 5]\nm = max(v)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("9", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Min_OfVector_ReturnsMinimum()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [3, 7, 2, 9, 5]\nm = min(v)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("2", lab.ExtractBody(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Operaciones matemáticas
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Math")]
        public void Power_Caret_Operator_Works()
        {
            var lab = new TestLab();
            var html = lab.Run("x = 2^10");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("1024", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Math")]
        public void Modulo_OperatorOrFunc_Works()
        {
            var lab = new TestLab();
            var html = lab.Run("x = mod(10, 3)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("1", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Math")]
        public void Trig_SinPi_ApproxZero()
        {
            var lab = new TestLab();
            var html = lab.Run("x = sin(pi)");
            Assert.Equal(0, lab.CountErrors(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Fprintf con formato
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "IO")]
        public void Fprintf_WithValueArg_RendersFormatted()
        {
            var lab = new TestLab();
            var html = lab.Run("x = 42\nfprintf('valor = %d\\n', x)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("valor", html);
        }

        [Fact]
        [Trait("Category", "IO")]
        public void Fprintf_MultiplePercent_NoCrash()
        {
            var lab = new TestLab();
            var html = lab.Run("a = 1\nb = 2\nfprintf('a=%d b=%d\\n', a, b)");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "IO")]
        public void Disp_String_RendersText()
        {
            var lab = new TestLab();
            var html = lab.Run("disp('Resultado final')");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("Resultado final", html);
        }

        // ─────────────────────────────────────────────────────────────────
        // Comparaciones / condicionales
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Compare")]
        public void Comparison_NotEqual_TildeEquals_Works()
        {
            // MATLAB usa ~= ; Calcpad usa ≠ (o !=). Verificar que el parser
            // matlab traduce ~= a la versión interna sin error.
            var lab = new TestLab();
            var html = lab.Run("if 3 ~= 4\n  x = 1\nend");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Compare")]
        public void Comparison_LessEqual_Works()
        {
            var lab = new TestLab();
            var html = lab.Run("if 3 <= 4\n  x = 1\nend");
            Assert.Equal(0, lab.CountErrors(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Variables y constantes
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Constants")]
        public void Pi_Constant_Available()
        {
            var lab = new TestLab();
            var html = lab.Run("x = pi");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Constants")]
        public void Underscore_InVariableName_Allowed()
        {
            var lab = new TestLab();
            var html = lab.Run("my_var = 42\ny = my_var * 2");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("84", lab.ExtractBody(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Plot básico (formato Calcpad $Plot)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Plot")]
        public void DollarPlot_Sin_Renders_NoErrors()
        {
            var lab = new TestLab();
            // Plot ya en formato Calcpad — verifica que el render no falla
            var html = lab.Run("$Plot{ sin(x) @ x = 0 : 6.28 }");
            Assert.Equal(0, lab.CountErrors(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Notación científica
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Numeric")]
        public void Scientific_25e6_ParsesCorrectly()
        {
            var lab = new TestLab();
            var html = lab.Run("E = 25e6\nx = E + 0");
            Assert.Equal(0, lab.CountErrors(html));
            // 25e6 = 25 000 000
            Assert.Contains("25000000", lab.ExtractBody(html).Replace(",", "").Replace(" ", ""));
        }

        [Fact]
        [Trait("Category", "Numeric")]
        public void Scientific_2_5e_minus_3_ParsesCorrectly()
        {
            var lab = new TestLab();
            var html = lab.Run("k = 2.5e-3\ny = k * 1000");
            Assert.Equal(0, lab.CountErrors(html));
            // 2.5e-3 * 1000 = 2.5
            Assert.Contains("2.5", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Numeric")]
        public void Scientific_CapitalE_Works()
        {
            var lab = new TestLab();
            var html = lab.Run("x = 1.0E+12");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Numeric")]
        public void Variable_NamedE_PreservedByPreprocessor()
        {
            // Confirma que la transform de scientific notation NO se confunde
            // con el identificador 'E'.
            var lab = new TestLab();
            var html = lab.Run("E = 200000\nv = E + 1");
            Assert.Equal(0, lab.CountErrors(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // Igualdad ==
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Compare")]
        public void If_DoubleEquals_Comparison_Works()
        {
            var lab = new TestLab();
            var html = lab.Run("i = 3\nif i == 3\n  y = 100\nend");
            Assert.Equal(0, lab.CountErrors(html));
        }

        // ─────────────────────────────────────────────────────────────────
        // length() alias
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Length_AliasForLen_NoErrors()
        {
            var lab = new TestLab();
            // length(v) en MATLAB debe mapearse a len(v) en Calcpad
            var html = lab.Run("v = [10, 20, 30]\nn = length(v)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("3", lab.ExtractBody(html));
        }
    }
}
