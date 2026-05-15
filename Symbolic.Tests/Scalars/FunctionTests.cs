// Calcpad Lab — tests de FUNCIONES escalares en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Scalars/FunctionTests.cs.
//
// MATLAB usa `,` para separar args; el preprocessor lo traduce a `;` para Calcpad.
namespace Calcpad.Lab.Tests
{
    public class FunctionTests
    {
        private const double Tol = 1e-14;
        private readonly double _sqrt2 = Math.Sqrt(2);
        private const double E = Math.E;

        #region Trig (radians)
        [Fact]
        [Trait("Category", "Trig")]
        public void Sin_Pi_Returns_Zero()
        {
            var result = new TestCalc().Run("sin(pi)");
            Assert.Equal(0d, result, 1e-10);
        }

        [Fact]
        [Trait("Category", "Trig")]
        public void Cos_Zero_Returns_One()
        {
            var result = new TestCalc().Run("cos(0)");
            Assert.Equal(1d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Trig")]
        public void Tan_PiOver4_Returns_One()
        {
            var result = new TestCalc().Run("tan(pi/4)");
            Assert.Equal(1d, result, Tol);
        }
        #endregion

        #region Inverse Trig
        [Fact]
        [Trait("Category", "InverseTrig")]
        public void Asin_One()
        {
            var result = new TestCalc().Run("asin(1)");
            Assert.Equal(Math.PI / 2d, result, Tol);
        }

        [Fact]
        [Trait("Category", "InverseTrig")]
        public void Acos_Zero()
        {
            var result = new TestCalc().Run("acos(0)");
            Assert.Equal(Math.PI / 2d, result, Tol);
        }

        [Fact]
        [Trait("Category", "InverseTrig")]
        public void Atan2_OneOne()
        {
            // MATLAB: atan2(1, 1) → preprocessor → atan2(1; 1)
            var result = new TestCalc().Run("atan2(1, 1)");
            Assert.Equal(Math.PI / 4d, result, Tol);
        }
        #endregion

        #region Exp / Log
        [Fact]
        [Trait("Category", "Exp")]
        public void Exp_Zero_Returns_One()
        {
            var result = new TestCalc().Run("exp(0)");
            Assert.Equal(1d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Exp")]
        public void Exp_One_Returns_E()
        {
            var result = new TestCalc().Run("exp(1)");
            Assert.Equal(E, result, Tol);
        }

        [Fact]
        [Trait("Category", "Log")]
        public void Ln_E_Returns_One()
        {
            var result = new TestCalc().Run("ln(exp(1))");
            Assert.Equal(1d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Log")]
        public void Log10_1000_Returns_3()
        {
            var result = new TestCalc().Run("log10(1000)");
            Assert.Equal(3d, result, Tol);
        }
        #endregion

        #region Roots
        [Fact]
        [Trait("Category", "Roots")]
        public void Sqrt_4_Returns_2()
        {
            var result = new TestCalc().Run("sqrt(4)");
            Assert.Equal(2d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Roots")]
        public void Sqrt_2_Returns_Sqrt2()
        {
            var result = new TestCalc().Run("sqrt(2)");
            Assert.Equal(_sqrt2, result, Tol);
        }
        #endregion

        #region Abs / Sign / Round
        [Fact]
        [Trait("Category", "AbsSign")]
        public void Abs_Negative_Returns_Positive()
        {
            var result = new TestCalc().Run("abs(-7)");
            Assert.Equal(7d, result);
        }

        [Fact]
        [Trait("Category", "AbsSign")]
        public void Sign_Negative_Returns_MinusOne()
        {
            var result = new TestCalc().Run("sign(-3)");
            Assert.Equal(-1d, result);
        }

        [Fact]
        [Trait("Category", "Round")]
        public void Floor_2_9()
        {
            var result = new TestCalc().Run("floor(2.9)");
            Assert.Equal(2d, result);
        }

        [Fact]
        [Trait("Category", "Round")]
        public void Ceil_2_1()
        {
            var result = new TestCalc().Run("ceil(2.1)");
            Assert.Equal(3d, result);
        }

        [Fact]
        [Trait("Category", "Round")]
        public void Round_2_5()
        {
            var result = new TestCalc().Run("round(2.5)");
            Assert.Equal(3d, result);
        }
        #endregion

        #region Min / Max
        [Fact]
        [Trait("Category", "MinMax")]
        public void Min_Two_Args()
        {
            // MATLAB min(3, 7) → preprocessor → min(3; 7)
            var result = new TestCalc().Run("min(3, 7)");
            Assert.Equal(3d, result);
        }

        [Fact]
        [Trait("Category", "MinMax")]
        public void Max_Two_Args()
        {
            var result = new TestCalc().Run("max(3, 7)");
            Assert.Equal(7d, result);
        }
        #endregion
    }
}
