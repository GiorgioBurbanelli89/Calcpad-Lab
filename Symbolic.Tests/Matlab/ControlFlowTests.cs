// Tests de control de flujo MATLAB: for/end, if/else/end, while/end, break/continue.

namespace Calcpad.Lab.Tests
{
    public class ControlFlowTests
    {
        [Fact]
        [Trait("Category", "If")]
        public void If_TrueCondition_BodyExecutes()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = 5\n" +
                "if x > 0\n" +
                "  y = 10\n" +
                "end\n" +
                "y");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("10", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "If")]
        public void If_FalseCondition_BodySkipped()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = -5\n" +
                "y = 99\n" +
                "if x > 0\n" +
                "  y = 10\n" +
                "end\n" +
                "y");
            Assert.Equal(0, lab.CountErrors(html));
            // y debe seguir siendo 99
            Assert.Contains("99", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "If")]
        public void IfElse_PicksElseBranch()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = -5\n" +
                "if x > 0\n" +
                "  y = 10\n" +
                "else\n" +
                "  y = 20\n" +
                "end\n" +
                "y");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("20", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "If")]
        public void IfElseIf_PicksMiddleBranch()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "x = 5\n" +
                "if x > 10\n" +
                "  y = 1\n" +
                "elseif x > 0\n" +
                "  y = 2\n" +
                "else\n" +
                "  y = 3\n" +
                "end\n" +
                "y");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("2", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "For")]
        public void For_Loop_SumOfSquares()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "s = 0\n" +
                "for i = 1:10\n" +
                "  s = s + i^2\n" +
                "end\n" +
                "s");
            Assert.Equal(0, lab.CountErrors(html));
            // Sum of squares 1..10 = 385
            Assert.Contains("385", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "While")]
        public void While_Loop_BasicCounter()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "i = 0\n" +
                "while i < 5\n" +
                "  i = i + 1\n" +
                "end\n" +
                "i");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("5", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Nested")]
        public void Nested_For_And_If()
        {
            var lab = new TestLab();
            var html = lab.Run(
                "s = 0\n" +
                "for i = 1:5\n" +
                "  if i > 2\n" +
                "    s = s + i\n" +
                "  end\n" +
                "end\n" +
                "s");
            Assert.Equal(0, lab.CountErrors(html));
            // 3 + 4 + 5 = 12
            Assert.Contains("12", lab.ExtractBody(html));
        }
    }
}
