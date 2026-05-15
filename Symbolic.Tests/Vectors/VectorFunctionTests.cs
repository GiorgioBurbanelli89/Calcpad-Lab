// Calcpad Lab — tests de FUNCIONES sobre vectores en sintaxis MATLAB.
// Espejo de Calcpad-Symbolic/Symbolic.Tests/Vectors/VectorFunctionTests.cs.
//
// Cubre: length/len, sum, prod, max, min, mean, sort (cuando aplique).
namespace Calcpad.Lab.Tests
{
    public class VectorFunctionTests
    {
        private const double Tol = 1e-14;

        #region length / numel  (alias → len en Calcpad)
        [Fact]
        [Trait("Category", "Length")]
        public void Length_Of_3Vector()
        {
            var result = new TestCalc().Run([
                "v = [10, 20, 30]",
                "length(v)"
            ]);
            Assert.Equal(3d, result);
        }

        [Fact]
        [Trait("Category", "Length")]
        public void Numel_AsAliasFor_Length()
        {
            var result = new TestCalc().Run([
                "v = [1, 2, 3, 4, 5]",
                "numel(v)"
            ]);
            Assert.Equal(5d, result);
        }
        #endregion

        #region sum / prod
        [Fact]
        [Trait("Category", "Sum")]
        public void Sum_Of_Vector()
        {
            var result = new TestCalc().Run([
                "v = [1, 2, 3, 4, 5]",
                "sum(v)"
            ]);
            Assert.Equal(15d, result);
        }

        [Fact]
        [Trait("Category", "Prod")]
        public void Prod_Of_Vector()
        {
            var result = new TestCalc().Run([
                "v = [1, 2, 3, 4]",
                "prod(v)"
            ]);
            Assert.Equal(24d, result);
        }
        #endregion

        #region max / min
        [Fact]
        [Trait("Category", "MinMax")]
        public void Max_Of_Vector()
        {
            var result = new TestCalc().Run([
                "v = [3, 7, 2, 9, 5]",
                "max(v)"
            ]);
            Assert.Equal(9d, result);
        }

        [Fact]
        [Trait("Category", "MinMax")]
        public void Min_Of_Vector()
        {
            var result = new TestCalc().Run([
                "v = [3, 7, 2, 9, 5]",
                "min(v)"
            ]);
            Assert.Equal(2d, result);
        }
        #endregion

        #region mean / average
        [Fact]
        [Trait("Category", "Mean")]
        public void Mean_Of_Vector()
        {
            var result = new TestCalc().Run([
                "v = [2, 4, 6, 8]",
                "mean(v)"
            ]);
            Assert.Equal(5d, result, Tol);
        }
        #endregion
    }
}
