// Calcpad Lab — tests de OPERADORES sobre matrices en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Matrices/MatrixOperatorTests.cs.
//
// MATLAB matrix:  [1 2; 3 4]   (filas separadas por ;)
// Calcpad matrix: [1; 2| 3; 4] (filas separadas por |)
// El preprocessor mapea: dentro de '[..]', ',' → ';' y ';' → '|'.
// Indexing 2D MATLAB:  M(i, j)  →  M.(i; j)
namespace Calcpad.Lab.Tests
{
    public class MatrixOperatorTests
    {
        private const double Tol = 1e-12;

        #region Literal & Indexing
        [Fact]
        [Trait("Category", "Literal")]
        public void Matrix_2x2_Element_1_1()
        {
            // MATLAB: M = [1 2; 3 4] ; M(1,1) = 1
            var result = new TestCalc().Run(["M = [1 2; 3 4]", "M(1, 1)"]);
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Literal")]
        public void Matrix_2x2_Element_2_2()
        {
            var result = new TestCalc().Run(["M = [1 2; 3 4]", "M(2, 2)"]);
            Assert.Equal(4d, result);
        }

        [Fact]
        [Trait("Category", "Literal")]
        public void Matrix_2x2_Comma_Separated()
        {
            // [1, 2; 3, 4] también es válido en MATLAB
            var result = new TestCalc().Run(["M = [1, 2; 3, 4]", "M(2, 1)"]);
            Assert.Equal(3d, result);
        }
        #endregion

        #region Element-wise add / sub
        [Fact]
        [Trait("Category", "AddSub")]
        public void Matrix_Add_Elementwise()
        {
            var result = new TestCalc().Run([
                "A = [1 2; 3 4]",
                "B = [5 6; 7 8]",
                "C = A + B",
                "C(2, 2)"  // 4+8 = 12
            ]);
            Assert.Equal(12d, result);
        }

        [Fact]
        [Trait("Category", "AddSub")]
        public void Matrix_Sub_Elementwise()
        {
            var result = new TestCalc().Run([
                "A = [10 20; 30 40]",
                "B = [1 2; 3 4]",
                "C = A - B",
                "C(2, 1)"  // 30-3 = 27
            ]);
            Assert.Equal(27d, result);
        }
        #endregion

        #region Scalar * Matrix
        [Fact]
        [Trait("Category", "ScalarOp")]
        public void Matrix_Scale_By_Two()
        {
            var result = new TestCalc().Run([
                "M = [1 2; 3 4]",
                "N = 2 * M",
                "N(2, 2)"  // 2*4 = 8
            ]);
            Assert.Equal(8d, result);
        }
        #endregion
    }
}
