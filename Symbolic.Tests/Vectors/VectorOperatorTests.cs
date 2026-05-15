// Calcpad Lab — tests de OPERADORES sobre vectores en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Vectors/VectorOperatorTests.cs.
//
// MATLAB:  vectores `[1 2 3]` (espacio) o `[1, 2, 3]` (coma).
//          Indexing `v(i)` con paréntesis, 1-indexed.
// Calcpad: detrás de escena usa `[1;2;3]` y `v.i`.
namespace Calcpad.Lab.Tests
{
    public class VectorOperatorTests
    {
        private const double Tol = 1e-14;

        #region Literal & Indexing
        [Fact]
        [Trait("Category", "Literal")]
        public void Vector_Comma_Index_3()
        {
            // v = [10, 20, 30] ; v(3) = 30
            var result = new TestCalc().Run(["v = [10, 20, 30]", "v(3)"]);
            Assert.Equal(30d, result);
        }

        [Fact]
        [Trait("Category", "Literal")]
        public void Vector_SpaceSeparated_Index_2()
        {
            var result = new TestCalc().Run(["v = [10 20 30]", "v(2)"]);
            Assert.Equal(20d, result);
        }

        [Fact]
        [Trait("Category", "Literal")]
        public void Vector_Index_1stElement()
        {
            var result = new TestCalc().Run(["v = [4, 8, 12]", "v(1)"]);
            Assert.Equal(4d, result);
        }
        #endregion

        #region Element-wise add / sub
        [Fact]
        [Trait("Category", "AddSub")]
        public void Vector_Add_Elementwise()
        {
            // [1,2,3] + [4,5,6] = [5,7,9] → preguntamos por v(2)=7
            var result = new TestCalc().Run([
                "a = [1, 2, 3]",
                "b = [4, 5, 6]",
                "c = a + b",
                "c(2)"
            ]);
            Assert.Equal(7d, result);
        }

        [Fact]
        [Trait("Category", "AddSub")]
        public void Vector_Sub_Elementwise()
        {
            var result = new TestCalc().Run([
                "a = [10, 20, 30]",
                "b = [1, 2, 3]",
                "c = a - b",
                "c(3)"
            ]);
            Assert.Equal(27d, result);
        }
        #endregion

        #region Scalar * Vector
        [Fact]
        [Trait("Category", "ScalarOp")]
        public void Vector_Scale_By_Two()
        {
            var result = new TestCalc().Run([
                "v = [1, 2, 3]",
                "w = 2 * v",
                "w(3)"
            ]);
            Assert.Equal(6d, result);
        }
        #endregion

        #region Dot product
        [Fact]
        [Trait("Category", "Dot")]
        public void Vector_DotProduct_Via_Sum()
        {
            // dot product como sum(a .* b) — MATLAB: a.*b es element-wise,
            // pero en Calcpad a*b para vectores ya es elementwise → sum(a*b)
            var result = new TestCalc().Run([
                "a = [1, 2, 3]",
                "b = [4, 5, 6]",
                "sum(a * b)"  // 4+10+18 = 32
            ]);
            Assert.Equal(32d, result);
        }
        #endregion
    }
}
