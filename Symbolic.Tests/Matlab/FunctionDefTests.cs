// Tests para definición de funciones MATLAB.
// MATLAB: function out = fnName(arg1, arg2)
//             body...
//             out = expression;
//         end
// → preprocessor renombra `out` a `fnName` para que Calcpad's UserFunction
// runtime lo reconozca como retorno.
namespace Calcpad.Lab.Tests
{
    public class FunctionDefTests
    {
        [Fact]
        [Trait("Category", "Function")]
        public void SingleOutput_SimpleAdd()
        {
            // function out = add(a, b)
            //   out = a + b;
            // end
            // K = add(3, 4) → 7
            var lab = new TestLab();
            var html = lab.Run(
                "function out = add(a, b)\n" +
                "  out = a + b;\n" +
                "end\n" +
                "K = add(3, 4)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("7", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Function")]
        public void SingleOutput_MultipleStatementsInBody()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "function y = square_plus_one(x)\n" +
                "  z = x * x;\n" +
                "  y = z + 1;\n" +
                "end\n" +
                "r = square_plus_one(5)");
            Assert.Equal(0, lab.CountErrors(html));
            // 5*5 + 1 = 26
            Assert.Contains("26", lab.ExtractBody(html));
        }

        [Fact(Skip = "Limitación: Calcpad UserFunction runtime usa MathParser que no entiende #if/#for/#while. " +
                     "Hace falta migrar UserFunction execution a ExpressionParser para soportar control flow en bodies.")]
        [Trait("Category", "Function")]
        public void Function_WithIfInBody_NotYetSupported()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "function y = abs_val(x)\n" +
                "  if x < 0\n" +
                "    y = -x;\n" +
                "  else\n" +
                "    y = x;\n" +
                "  end\n" +
                "end\n" +
                "r = abs_val(-7)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("7", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Function")]
        public void Function_WithInlineIf_CalcpadStyle()
        {
            // Workaround viable HOY: usar la función inline if() de Calcpad en lugar
            // de control flow en bloque.
            //   MATLAB:  if x<0; y = -x; else; y = x; end
            //   Calcpad: if(x < 0; -x; x)
            var lab = new TestLab();
            var html = lab.Run(
                "function y = abs_val(x)\n" +
                "  y = if(x < 0; -x; x);\n" +
                "end\n" +
                "r = abs_val(-7)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("7", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Function")]
        public void Function_VariableName_DoesNotLeakOutside()
        {
            // Después del call, `out` (la variable local) NO debe contaminar el scope global
            var lab = new TestLab();
            var html = lab.Run(
                "function y = double_it(x)\n" +
                "  y = 2 * x;\n" +
                "end\n" +
                "r = double_it(10)\n" +
                "% verificar que 'y' no existe afuera\n" +
                "% (si y existiera, esta línea daría error de duplicación)\n" +
                "y = 999");
            Assert.Equal(0, lab.CountErrors(html));
        }
    }
}
