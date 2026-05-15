// Calcpad Lab — tests de FUNCIONES sobre matrices en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Matrices/MatrixFunctionTests.cs.
//
// MATLAB: zeros(m, n), ones(m, n), eye(n), transpose(M), inv(M), det(M)
//
// IMPORTANTE:
//   - zeros(1, N) y zeros(N, 1) ya se mapean a vector(N) (1D real)
//     vía MatlabPreprocessor.TransformZerosOnesToVector.
//   - eye(n) está mapeado a identity(n) en MatrixCalculator (Calcpad-Lab).
namespace Calcpad.Lab.Tests
{
    public class MatrixFunctionTests
    {
        private const double Tol = 1e-12;

        #region zeros / ones
        [Fact]
        [Trait("Category", "Zeros")]
        public void Zeros_2x3_Element_1_1_Is_Zero()
        {
            var result = new TestCalc().Run([
                "M = zeros(2, 3)",
                "M(1, 1)"
            ]);
            Assert.Equal(0d, result);
        }

        [Fact]
        [Trait("Category", "Zeros")]
        public void Zeros_3x3_Element_3_3_Is_Zero()
        {
            var result = new TestCalc().Run([
                "M = zeros(3, 3)",
                "M(3, 3)"
            ]);
            Assert.Equal(0d, result);
        }
        #endregion

        #region eye
        [Fact]
        [Trait("Category", "Eye")]
        public void Eye_3_Diagonal_Is_One()
        {
            var result = new TestCalc().Run([
                "I = eye(3)",
                "I(2, 2)"
            ]);
            Assert.Equal(1d, result);
        }

        [Fact]
        [Trait("Category", "Eye")]
        public void Eye_3_OffDiagonal_Is_Zero()
        {
            var result = new TestCalc().Run([
                "I = eye(3)",
                "I(1, 2)"
            ]);
            Assert.Equal(0d, result);
        }
        #endregion

        #region transpose
        [Fact]
        [Trait("Category", "Transpose")]
        public void Transpose_2x3_Swaps_Dimensions()
        {
            // M = [1 2 3; 4 5 6]   (2×3)
            // transpose(M) = [1 4; 2 5; 3 6]  (3×2)
            // transpose(M)(3, 2) = 6
            var result = new TestCalc().Run([
                "M = [1 2 3; 4 5 6]",
                "T = transpose(M)",
                "T(3, 2)"
            ]);
            Assert.Equal(6d, result);
        }
        #endregion

        #region ones (mapped to mfill)
        [Fact]
        [Trait("Category", "Ones")]
        public void Ones_2x3_AllEqualOne()
        {
            var result = new TestCalc().Run([
                "M = ones(2, 3)",
                "M(1, 1) + M(2, 3)"
            ]);
            Assert.Equal(2d, result);
        }

        [Fact]
        [Trait("Category", "Ones")]
        public void Ones_SingleArg_Square()
        {
            // MATLAB: ones(3) = 3×3 matriz de unos
            var result = new TestCalc().Run([
                "M = ones(3)",
                "M(2, 2)"
            ]);
            Assert.Equal(1d, result);
        }
        #endregion

        #region Indexed assignment (M(i, j) = x)
        [Fact]
        [Trait("Category", "IndexedAssign")]
        public void Matrix_Indexed_Assignment_Updates()
        {
            // M(i, j) = x  →  preprocessor → M.(i; j) = x  →  Calcpad escribe
            var result = new TestCalc().Run([
                "M = zeros(3, 3)",
                "M(2, 2) = 99",
                "M(2, 2)"
            ]);
            Assert.Equal(99d, result);
        }
        #endregion
    }
}
