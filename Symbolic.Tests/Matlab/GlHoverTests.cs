// Verifica el HOVER en la animación WebGL retenida (talud Demo04): el frame GL debe
// llevar el overlay 'glh' que recibe el mouse, el array VR de valores CRUDOS por nodo,
// la etiqueta del campo (= colorbar) y el handler mousemove. Sin esto no hay cursor.
using Calcpad.Core.Matlab;

namespace Calcpad.Lab.Tests
{
    public class GlHoverTests
    {
        // Corre un .m en modo STREAMING (como el WPF) y captura el 1er frame GL emitido.
        private static string CaptureGlInit(string script)
        {
            System.Environment.SetEnvironmentVariable("HK_GL_HEADLESS", "1"); // fuerza la ruta WebGL en headless
            MatlabPlots.ResetGlAnim();
            var pipe = new MatlabPipeline { StreamingMode = true };
            string glInit = null;
            pipe.StatementCompleted += (line, html) =>
            {
                if (html != null && html.Contains("@@LABFRAME@@") && glInit == null &&
                    html.Contains("id=\"glc\""))          // el frame de INICIALIZACIÓN (no los GLDATA)
                    glInit = html;
            };
            try { pipe.Run(script); }
            finally { System.Environment.SetEnvironmentVariable("HK_GL_HEADLESS", null); }
            var dump = System.Environment.GetEnvironmentVariable("HK_DUMP_GLHTML");
            if (!string.IsNullOrEmpty(dump) && glInit != null)
                System.IO.File.WriteAllText(dump, glInit);   // verificacion funcional externa (Node)
            return glInit ?? "";
        }

        private const string TALUD =
            "V=[0 0; 4 0; 4 3; 0 3];\n" +
            "F=[1 2 3; 1 3 4];\n" +
            "C=[0.4; 17.6; 9.0; 2.0];\n" +
            "ph=patch('Faces',F,'Vertices',V,'FaceVertexCData',C,'FaceColor','interp');\n" +
            "caxis([-0.4 17.6]);\n" +
            "cb=colorbar; cb.Label.String='d_x  [mm]';\n" +
            "drawnow;\n";

        [Fact]
        [Trait("Category", "GlHover")]
        public void GlAnim_EmitsHoverOverlayCanvas()
        {
            var html = CaptureGlInit(TALUD);
            Assert.Contains("id=\"glc\"", html);          // canvas GPU
            Assert.Contains("id=\"glh\"", html);          // overlay que recibe el mouse (hover)
            Assert.Contains("id=\"gltip\"", html);        // tooltip
            Assert.Contains("pointer-events:auto", html); // el overlay SÍ recibe eventos
        }

        [Fact]
        [Trait("Category", "GlHover")]
        public void GlAnim_CarriesRawValuesAndMousemove()
        {
            var html = CaptureGlInit(TALUD);
            Assert.Contains("VR=", html);                 // valores CRUDOS por nodo (magnitud real)
            Assert.Contains("mousemove", html);           // handler de hover
            Assert.Contains("17.6", html);                // el valor real del nodo aparece en VR
        }

        [Fact]
        [Trait("Category", "GlHover")]
        public void GlAnim_HoverLabelMatchesColorbar()
        {
            var html = CaptureGlInit(TALUD);
            // El tooltip usa la MISMA etiqueta que la barra de color.
            Assert.Contains("HLBL=\"d_x", html);
        }

        // Los 4 campos de GEO5 (d_x, d_z, d_res, E_d,pl): cada uno con su etiqueta, su escala
        // (caxis) y su valor-pico real. El hover debe llevar SIEMPRE la etiqueta correcta y el
        // valor CRUDO del pico (no el normalizado). La ruta es la misma; cambia CData+label+caxis.
        [Theory]
        [Trait("Category", "GlHover")]
        [InlineData("d_x  [mm]",     "-0.4", "17.6", "17.6")]   // d_x   escala GEO5 0..17.6
        [InlineData("d_z  [mm]",     "-2.6", "14.1", "14.1")]   // d_z   escala GEO5 -2.6..14.1
        [InlineData("d_res  [mm]",   "0",    "17.9", "17.9")]   // d_res escala GEO5 0..17.9
        [InlineData("E_d,pl  [%]",   "0",    "0.70", "0.55")]   // plastica escala GEO5 0..0.70
        public void GlAnim_HoverCarriesEachFieldLabelAndRawPeak(string label, string clo, string chi, string peak)
        {
            string script =
                "V=[0 0; 4 0; 4 3; 0 3];\n" +
                "F=[1 2 3; 1 3 4];\n" +
                "C=[" + clo + "; " + peak + "; " + peak + "; 0];\n" +   // el pico del campo es un nodo real
                "ph=patch('Faces',F,'Vertices',V,'FaceVertexCData',C,'FaceColor','interp');\n" +
                "caxis([" + clo + " " + chi + "]);\n" +
                "cb=colorbar; cb.Label.String='" + label + "';\n" +
                "drawnow;\n";
            var html = CaptureGlInit(script);
            Assert.Contains("id=\"glh\"", html);                       // hay overlay de hover
            Assert.Contains("mousemove", html);                        // hay handler
            Assert.Contains("HLBL=\"" + label.Substring(0, 3), html);  // etiqueta del campo (d_x/d_z/d_r/E_d)
            Assert.Contains(peak, html);                               // el valor-pico CRUDO viaja en VR
        }
    }
}
