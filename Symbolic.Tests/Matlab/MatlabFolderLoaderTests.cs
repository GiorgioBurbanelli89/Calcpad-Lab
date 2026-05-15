// Tests para MatlabFolderLoader — el mecanismo "MATLAB path" que carga
// automáticamente las function-files de la misma carpeta.
using Calcpad.Core;

namespace Calcpad.Lab.Tests
{
    public class MatlabFolderLoaderTests
    {
        // ─────────────────────────────────────────────────────────────────
        // IsFunctionFile heuristic
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void IsFunctionFile_BasicSignature()
        {
            var content = "function out = myFn(x, y)\n  out = x + y;\nend\n";
            var isFn = MatlabFolderLoader.IsFunctionFile(content, out var name);
            Assert.True(isFn);
            Assert.Equal("myFn", name);
        }

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void IsFunctionFile_SkipsLeadingComments()
        {
            var content = "% Author: foo\n% Description: bar\nfunction y = sq(x)\n  y = x*x;\nend\n";
            var isFn = MatlabFolderLoader.IsFunctionFile(content, out var name);
            Assert.True(isFn);
            Assert.Equal("sq", name);
        }

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void IsFunctionFile_ScriptFile_ReturnsFalse()
        {
            var content = "x = 5;\ny = x * 2;\n";
            var isFn = MatlabFolderLoader.IsFunctionFile(content, out var name);
            Assert.False(isFn);
            Assert.Null(name);
        }

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void IsFunctionFile_NoOutputArg()
        {
            // function fn(x)
            //   ...
            // end
            var content = "function plotIt(x)\n  disp(x);\nend\n";
            var isFn = MatlabFolderLoader.IsFunctionFile(content, out var name);
            Assert.True(isFn);
            Assert.Equal("plotIt", name);
        }

        // ─────────────────────────────────────────────────────────────────
        // LoadFromFolder integración
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void LoadFromFolder_IncludesHelperFunction()
        {
            // Crear carpeta temporal con main.m y helper.m
            var tmp = Path.Combine(Path.GetTempPath(), "calcpad-lab-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "helper.m"),
                    "function y = helper(x)\n" +
                    "  y = x * 10;\n" +
                    "end\n");
                File.WriteAllText(Path.Combine(tmp, "main.m"),
                    "r = helper(5)");

                var mainScript = File.ReadAllText(Path.Combine(tmp, "main.m"));
                var combined = MatlabFolderLoader.LoadFromFolder(mainScript, tmp, "main.m");

                // El combined debe contener AMBOS: helper definition + main call
                Assert.Contains("function y = helper(x)", combined);
                Assert.Contains("r = helper(5)", combined);
                // Y el orden debe ser: helper primero, main al final
                Assert.True(combined.IndexOf("function y = helper(x)") <
                            combined.IndexOf("r = helper(5)"));
            }
            finally
            {
                try { Directory.Delete(tmp, recursive: true); } catch { }
            }
        }

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void LoadFromFolder_SkipsNonFunctionFiles()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "calcpad-lab-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmp);
            try
            {
                // unScript.m es un script puro — NO debe incluirse
                File.WriteAllText(Path.Combine(tmp, "unScript.m"), "x = 1;\ny = 2;");
                // unaFuncion.m sí es función
                File.WriteAllText(Path.Combine(tmp, "unaFuncion.m"),
                    "function v = unaFuncion(z)\n  v = z + 1;\nend\n");

                var combined = MatlabFolderLoader.LoadFromFolder("", tmp, "main.m");
                Assert.Contains("function v = unaFuncion", combined);
                Assert.DoesNotContain("x = 1", combined);
            }
            finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
        }

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void LoadFromFolder_DoesNotIncludeSelf()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "calcpad-lab-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "main.m"),
                    "function y = main(x)\n  y = x;\nend\n");

                var combined = MatlabFolderLoader.LoadFromFolder("r = main(5)", tmp, "main.m");
                // main.m mismo NO debe aparecer dos veces
                int count = 0;
                int idx = 0;
                while ((idx = combined.IndexOf("function y = main(x)", idx, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    idx += 1;
                }
                Assert.Equal(0, count); // No incluido (es el archivo principal)
            }
            finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
        }

        // ─────────────────────────────────────────────────────────────────
        // Integración E2E: TestLab + folder
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "FolderLoader")]
        public void E2E_ScriptCallsFunctionInOtherFile_NoErrors()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "calcpad-lab-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "mySquare.m"),
                    "function y = mySquare(x)\n" +
                    "  y = x * x;\n" +
                    "end\n");
                var mainPath = Path.Combine(tmp, "main.m");
                File.WriteAllText(mainPath, "r = mySquare(7)");

                var mainScript = File.ReadAllText(mainPath);
                var combined = MatlabFolderLoader.Load(mainScript, mainPath);
                var lab = new TestLab();
                var html = lab.Run(combined);
                Assert.Equal(0, lab.CountErrors(html));
                Assert.Contains("49", lab.ExtractBody(html));
            }
            finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
        }
    }
}
