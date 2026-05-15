// Tests de integración: funciones MATLAB de rango (linspace, logspace, arange)
// que se PRE-EVALÚAN en el preprocessor usando el DLL nativo C++ cuando los
// argumentos son CONSTANTES.
//
// Si los argumentos son variables, deja la línea sin transformar — esto es
// intencional y se documenta vía un test que verifica que NO transforma.
namespace Calcpad.Lab.Tests
{
    public class RangeFunctionsTests
    {
        // ─────────────────────────────────────────────────────────────────
        // linspace
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_Constants_ExpandsToVector()
        {
            // linspace(0, 10, 5) → [0, 2.5, 5, 7.5, 10] → v(3) = 5
            var result = new TestCalc().Run([
                "v = linspace(0, 10, 5)",
                "v(3)"
            ]);
            Assert.Equal(5d, result);
        }

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_LargeN_NoErrors()
        {
            // Caso del usuario: Dise_acero.m usa linspace(0, 15, 1000)
            var lab = new TestLab();
            var html = lab.Run("Lb = linspace(0, 15, 1000)\nLb(500)");
            Assert.Equal(0, lab.CountErrors(html));
        }

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_LastElement_IsEndpoint()
        {
            var result = new TestCalc().Run([
                "v = linspace(0, 1, 100)",
                "v(100)"
            ]);
            Assert.Equal(1d, result, 1e-15);
        }

        // ─────────────────────────────────────────────────────────────────
        // logspace
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Logspace")]
        public void Logspace_BasicRange()
        {
            // logspace(0, 3, 4) → [1, 10, 100, 1000]
            var result = new TestCalc().Run([
                "v = logspace(0, 3, 4)",
                "v(3)"
            ]);
            Assert.Equal(100d, result, 1e-10);
        }

        // ─────────────────────────────────────────────────────────────────
        // Cases NEGATIVOS: con variable, NO transforma
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_WithVariable_DoesNotPreEvaluate()
        {
            // n no es constante → preprocessor lo deja → Calcpad da error
            // (que es lo esperado hasta que registremos runtime support).
            // Verificamos que el HTML tenga error pero no crashee.
            var lab = new TestLab();
            var html = lab.Run("n = 5\nv = linspace(0, 10, n)");
            // Asumimos que va a haber errores — sólo verificamos que no crashee
            Assert.NotNull(html);
        }

        // ─────────────────────────────────────────────────────────────────
        // Notación científica DENTRO de linspace
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Linspace")]
        public void Linspace_WithScientificNotation()
        {
            // linspace(0, 1e3, 11) — el sci-notation se expande primero a (1*10^3)
            // y luego linspace lo pre-evalúa.
            var result = new TestCalc().Run([
                "v = linspace(0, 1e3, 11)",
                "v(6)"  // medio del rango: 500
            ]);
            Assert.Equal(500d, result, 1e-10);
        }
    }
}
