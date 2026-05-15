// Tests para rangos MATLAB con step `for i = START:STEP:END`.
//
// Calcpad #for nativo solo soporta start:end (step=1 implícito).
// El preprocessor transforma `for i = 1:2:7` a un while-loop equivalente.
namespace Calcpad.Lab.Tests
{
    public class StepRangeTests
    {
        [Fact]
        [Trait("Category", "StepRange")]
        public void For_1_to_7_Step2_SumIs16()
        {
            // 1 + 3 + 5 + 7 = 16
            var lab = new TestLab();
            var html = lab.Run(
                "s = 0\n" +
                "for m = 1:2:7\n" +
                "  s = s + m\n" +
                "end\n" +
                "result = s");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("16", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "StepRange")]
        public void For_1_to_10_Step3_SumIs22()
        {
            // 1 + 4 + 7 + 10 = 22
            var lab = new TestLab();
            var html = lab.Run(
                "s = 0\n" +
                "for k = 1:3:10\n" +
                "  s = s + k\n" +
                "end\n" +
                "result = s");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("22", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "StepRange")]
        public void For_NegativeStep_10_to_1_SumIs55()
        {
            // 10+9+8+7+6+5+4+3+2+1 = 55
            var lab = new TestLab();
            var html = lab.Run(
                "s = 0\n" +
                "for i = 10:-1:1\n" +
                "  s = s + i\n" +
                "end\n" +
                "result = s");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("55", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "StepRange")]
        public void NestedStepFor_FourierLike()
        {
            // Suma 4·4 = 16 iteraciones de m,n ∈ {1, 3, 5, 7}
            var lab = new TestLab();
            var html = lab.Run(
                "count = 0\n" +
                "for m = 1:2:7\n" +
                "  for n = 1:2:7\n" +
                "    count = count + 1\n" +
                "  end\n" +
                "end\n" +
                "total = count");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("16", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "StepRange")]
        public void StepRange_PreservedThroughPreprocessor()
        {
            // Verificar la transformación del preprocessor a un for con contador.
            //   for i = 1:2:7
            //   →  for k_step_i = 1 : 4
            //        i = (1) + (k_step_i - 1) * (2)
            //        body
            //      #loop
            var src = "for i = 1:2:7\n  x = i\nend";
            var pre = Calcpad.Core.MatlabPreprocessor.Process(src);
            Assert.Contains("for k_step_i", pre);
            Assert.Contains("i = (1) + (k_step_i", pre);
        }
    }
}
