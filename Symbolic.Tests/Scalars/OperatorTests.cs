// Calcpad Lab — tests de OPERADORES escalares en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Scalars/OperatorTests.cs pero
// la expresión pasa por MatlabPreprocessor primero (% comments, scientific
// notation, ==/~=/<=/>=, etc.).
namespace Calcpad.Lab.Tests
{
    public class OperatorTests
    {
        private const double Tol = 1e-15;

        #region Power
        [Fact]
        [Trait("Category", "Power")]
        public void Power_2_3()
        {
            var result = new TestCalc().Run("2^3");
            Assert.Equal(8d, result);
        }

        [Fact]
        [Trait("Category", "Power")]
        public void Power_3_NegTwo()
        {
            var result = new TestCalc().Run("3^(-2)");
            Assert.Equal(1d / 9d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Power")]
        public void Power_HighExponent()
        {
            var result = new TestCalc().Run("2^10");
            Assert.Equal(1024d, result);
        }
        #endregion

        #region Modulo
        [Fact]
        [Trait("Category", "Modulo")]
        public void Mod_10_3()
        {
            var result = new TestCalc().Run("mod(10; 3)");
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Modulo")]
        public void Mod_7_4_FromMatlab()
        {
            // MATLAB: mod(7, 4)  — comma → ; vía preprocessor
            var result = new TestCalc().Run("mod(7, 4)");
            Assert.Equal(3d, result);
        }
        #endregion

        #region Division
        [Fact]
        [Trait("Category", "Division")]
        public void Division_Integer()
        {
            var result = new TestCalc().Run("10 / 4");
            Assert.Equal(2.5d, result);
        }

        [Fact]
        [Trait("Category", "Division")]
        public void Division_Negative()
        {
            var result = new TestCalc().Run("-15 / 3");
            Assert.Equal(-5d, result);
        }
        #endregion

        #region Multiplication
        [Fact]
        [Trait("Category", "Multiplication")]
        public void Mult_PositiveInt()
        {
            var result = new TestCalc().Run("7 * 6");
            Assert.Equal(42d, result);
        }

        [Fact]
        [Trait("Category", "Multiplication")]
        public void Mult_Decimal()
        {
            var result = new TestCalc().Run("2.5 * 4");
            Assert.Equal(10d, result, Tol);
        }
        #endregion

        #region Add / Sub
        [Fact]
        [Trait("Category", "AddSub")]
        public void Add_Mixed()
        {
            var result = new TestCalc().Run("1 + 2 + 3 + 4 + 5");
            Assert.Equal(15d, result);
        }

        [Fact]
        [Trait("Category", "AddSub")]
        public void Subtract_Mixed()
        {
            var result = new TestCalc().Run("100 - 30 - 20 - 10");
            Assert.Equal(40d, result);
        }
        #endregion

        #region Scientific notation (MATLAB syntax)
        [Fact]
        [Trait("Category", "Scientific")]
        public void Scientific_25e6_Equals_25M()
        {
            // 25e6 → preprocessor → (25*10^6)
            var result = new TestCalc().Run("25e6");
            Assert.Equal(25_000_000d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Scientific")]
        public void Scientific_2_5e_minus_3()
        {
            var result = new TestCalc().Run("2.5e-3");
            Assert.Equal(0.0025d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Scientific")]
        public void Scientific_1e0_Equals_1()
        {
            var result = new TestCalc().Run("1e0");
            Assert.Equal(1d, result, Tol);
        }

        [Fact]
        [Trait("Category", "Scientific")]
        public void Scientific_CapitalE_Plus()
        {
            var result = new TestCalc().Run("1.5E+2");
            Assert.Equal(150d, result, Tol);
        }
        #endregion

        #region Comparison operators (MATLAB → Calcpad)
        [Fact]
        [Trait("Category", "Compare")]
        public void DoubleEquals_TrueIsOne()
        {
            // MATLAB == → preprocessor → ≡ → Calcpad
            var result = new TestCalc().Run("3 == 3");
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Compare")]
        public void TildeEquals_TrueIsOne()
        {
            // ~= → ≠
            var result = new TestCalc().Run("3 ~= 4");
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Compare")]
        public void LessEqual_TrueIsOne()
        {
            // <= → ≤
            var result = new TestCalc().Run("3 <= 4");
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Compare")]
        public void GreaterEqual_TrueIsOne()
        {
            var result = new TestCalc().Run("5 >= 5");
            Assert.Equal(1d, result);
        }
        #endregion
    }
}
