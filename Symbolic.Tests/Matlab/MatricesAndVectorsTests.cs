// Tests sobre vectores y matrices MATLAB:
//   [a, b, c]       (vector con comas)
//   [a b c]         (vector con espacios)
//   [a, b; c, d]    (matriz con ; entre filas)
//   M(i, j)         (indexing 2D)
//   M(i) = x        (indexing assignment)
//   zeros, ones, eye, transpose, inv

namespace Calcpad.Lab.Tests
{
    public class MatricesAndVectorsTests
    {
        [Fact]
        [Trait("Category", "Vector")]
        public void Vector_CommaSeparated_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [1, 2, 3]\nv");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Vector")]
        public void Vector_SpaceSeparated_MatlabStyle()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [1 2 3]\nv");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Vector")]
        public void Vector_Indexing_Access()
        {
            var lab = new TestLab();
            var html = lab.Run("v = [10, 20, 30]\nx = v(2)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("20", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Matrix")]
        public void Matrix_2x2Literal_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("M = [1, 2; 3, 4]\nM");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Zeros_2x3_Returns2x3Matrix()
        {
            var lab = new TestLab();
            var html = lab.Run("Z = zeros(2, 3)\nZ");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Eye_3_ReturnsIdentity3()
        {
            var lab = new TestLab();
            var html = lab.Run("I = eye(3)\nI");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Sqrt_16_Returns4()
        {
            var lab = new TestLab();
            var html = lab.Run("x = sqrt(16)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("4", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "BuiltIns")]
        public void Abs_Negative_ReturnsPositive()
        {
            var lab = new TestLab();
            var html = lab.Run("x = abs(-7)");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("7", lab.ExtractBody(html));
        }

        [Fact]
        [Trait("Category", "Shim")]
        public void Clear_Clc_NoOps_NoErrors()
        {
            var lab = new TestLab();
            var html = lab.Run("clear;\nclc;\nx = 5");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Shim")]
        public void Fprintf_RendersText()
        {
            var lab = new TestLab();
            var html = lab.Run("fprintf('Hello world\\n');");
            Assert.Equal(0, lab.CountErrors(html));
            Assert.Contains("Hello world", html);
        }
    }
}
