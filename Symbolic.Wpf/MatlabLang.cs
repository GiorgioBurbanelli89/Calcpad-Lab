using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Snippets;
using Calcpad.Core.Matlab;

namespace Calcpad.Wpf;

/// <summary>
/// "Language pack" MATLAB para el autocompletado: las 577 funciones REALES del motor
/// (<see cref="MatlabBuiltins"/>, extraidas de Symbolic.Core), las palabras clave, y
/// snippets de bloque que se insertan YA CERRADOS con sus huecos.
/// Mismo patron que FortranLang.cs en Hekatan Fortran: cambiar de lenguaje = escribir
/// otro de estos.
/// </summary>
internal static class MatlabLang
{
    /// <summary>Firma + una linea de ayuda para las funciones que mas se escriben.
    /// El resto sale del motor con su nombre a secas — no me invento lo que hacen.</summary>
    private static readonly Dictionary<string, string> Ayuda = new(StringComparer.Ordinal)
    {
        ["zeros"] = "zeros(m, n) — matriz de ceros",
        ["ones"] = "ones(m, n) — matriz de unos",
        ["eye"] = "eye(n) — matriz identidad",
        ["linspace"] = "linspace(a, b, n) — n valores repartidos entre a y b",
        ["size"] = "size(A) — [filas columnas]",
        ["length"] = "length(v) — la dimension mas larga",
        ["numel"] = "numel(A) — cuantos elementos hay",
        ["sum"] = "sum(v) / sum(A, dim) — suma",
        ["inv"] = "inv(A) — inversa (si A es simetrica, Cholesky)",
        ["det"] = "det(A) — determinante",
        ["eig"] = "eig(A) — valores propios",
        ["sparse"] = "sparse(i, j, v, m, n) — matriz dispersa",
        ["plot"] = "plot(x, y) — grafica 2D",
        ["surf"] = "surf(X, Y, Z) — superficie 3D",
        ["fprintf"] = "fprintf('%s\\n', x) — imprime con formato",
        ["syms"] = "syms x y — declara variables simbolicas",
        ["diff"] = "diff(f, x) — derivada simbolica",
        ["int"] = "int(f, x) — integral simbolica",
        ["solve"] = "solve(ec, x) — resuelve la ecuacion",
        ["tic"] = "tic — arranca el cronometro",
        ["toc"] = "toc — segundos desde el ultimo tic",
    };

    private static readonly (string Trigger, string Doc, Func<Snippet> Build)[] Snippets =
    {
        ("for", "Bucle for … end (bloque completo)", For),
        ("if", "Condicion if … end (bloque completo)", If),
        ("ifelse", "if … else … end", IfElse),
        ("while", "Bucle while … end", While),
        ("switch", "switch … case … otherwise … end", Switch),
        ("try", "try … catch … end", Try),
        ("function", "function [salida] = nombre(entradas) … end", Function),
        ("seccion", "Seccion %% (se pliega en el editor; NO sale en el worksheet)", Seccion),
        ("titulo", "%\" titulo — SI sale en el worksheet", Titulo),
        ("texto", "%' texto — SI sale en el worksheet", Texto),
        ("tic", "Bloque tic/toc (mide el tiempo)", TicToc),
    };

    /// <summary>Items del popup, filtrados por lo que ya se escribio.
    ///
    /// <paramref name="declarados"/> son las variables y funciones que el usuario escribio EN
    /// SU ARCHIVO, leidas por el motor (<see cref="Calcpad.Core.Matlab.MatlabSymbols"/>). Van
    /// PRIMERO: al escribir, lo que mas se busca es lo propio, no la funcion 400 del motor.
    /// <paramref name="linea"/> es la del cursor: solo se ofrece lo declarado MAS ARRIBA.</summary>
    public static IEnumerable<ICompletionData> Items(
        string prefijo, IReadOnlyList<Simbolo> declarados = null, int linea = int.MaxValue, bool oscuro = true)
    {
        var lista = new List<ICompletionData>();

        if (declarados is not null)
            AgregarDelUsuario(lista, declarados, linea, oscuro);

        foreach (var (trigger, doc, build) in Snippets)
            lista.Add(new MatlabCompletionData(trigger, doc, snippet: build, priority: 3));

        foreach (var k in MatlabBuiltins.Keywords)
            lista.Add(new MatlabCompletionData(k, "palabra clave", priority: 2));

        foreach (var c in MatlabBuiltins.Constants)
            lista.Add(new MatlabCompletionData(c, "constante", priority: 1));

        foreach (var f in MatlabBuiltins.All)
            lista.Add(new MatlabCompletionData(f, Ayuda.TryGetValue(f, out var d) ? d : "funcion del motor"));

        if (string.IsNullOrEmpty(prefijo)) return lista;
        return lista.Where(d => d.Text.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // ---------- lo que el usuario declaro en SU archivo ----------

    /// <summary>Lo declarado por el usuario, sin repetir y solo lo que ya existe en esta linea.
    /// Las funciones se ofrecen con su firma (<c>momento(q, L)</c>) para no tener que subir a
    /// mirar los argumentos. Las de MAS ABAJO no se ofrecen: todavia no existen ahi.</summary>
    private static void AgregarDelUsuario(List<ICompletionData> lista, IReadOnlyList<Simbolo> simbolos, int linea, bool oscuro)
    {
        var cVar  = Pincel(oscuro ? "#98C379" : "#3F7A2E");   // variable
        var cFun  = Pincel(oscuro ? "#E5E5E5" : "#1A1A1A");   // funcion (negrita)
        var cPar  = Pincel(oscuro ? "#D19A66" : "#8A5A00");   // argumento de function

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in simbolos)
        {
            // Una funcion se puede llamar desde arriba (MATLAB las lee todas); una variable no
            // existe hasta que se asigna.
            if (s.Tipo != SimboloTipo.Funcion && s.Linea >= linea) continue;
            if (!vistos.Add(s.Nombre)) continue;

            var (doc, color, negrita, texto) = s.Tipo switch
            {
                SimboloTipo.Funcion   => ($"funcion de tu archivo — {s.Firma}", cFun, true, s.Nombre),
                SimboloTipo.Parametro => ("argumento de tu function", cPar, false, s.Nombre),
                _                     => ("variable de tu archivo", cVar, false, s.Nombre),
            };
            lista.Add(new MatlabCompletionData(texto, $"{doc} (linea {s.Linea})",
                                               priority: 5, color: color, negrita: negrita));
        }
    }

    private static Brush Pincel(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    // ---------- fabricas de snippets ----------

    private static SnippetReplaceableTextElement Hueco(string t) => new() { Text = t };
    private static SnippetTextElement Txt(string t) => new() { Text = t };

    private static Snippet For()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("for "));
        s.Elements.Add(Hueco("i"));
        s.Elements.Add(Txt(" = 1:"));
        s.Elements.Add(Hueco("n"));
        s.Elements.Add(Txt("\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\nend"));
        return s;
    }

    private static Snippet If()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("if "));
        s.Elements.Add(Hueco("cond"));
        s.Elements.Add(Txt("\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\nend"));
        return s;
    }

    private static Snippet IfElse()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("if "));
        s.Elements.Add(Hueco("cond"));
        s.Elements.Add(Txt("\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\nelse\n    \nend"));
        return s;
    }

    private static Snippet While()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("while "));
        s.Elements.Add(Hueco("cond"));
        s.Elements.Add(Txt("\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\nend"));
        return s;
    }

    private static Snippet Switch()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("switch "));
        s.Elements.Add(Hueco("x"));
        s.Elements.Add(Txt("\n    case "));
        s.Elements.Add(Hueco("1"));
        s.Elements.Add(Txt("\n        "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\n    otherwise\n        \nend"));
        return s;
    }

    private static Snippet Try()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("try\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\ncatch "));
        s.Elements.Add(Hueco("err"));
        s.Elements.Add(Txt("\n    \nend"));
        return s;
    }

    private static Snippet Function()
    {
        var nombre = Hueco("nombre");
        var s = new Snippet();
        s.Elements.Add(Txt("function "));
        s.Elements.Add(Hueco("y"));
        s.Elements.Add(Txt(" = "));
        s.Elements.Add(nombre);
        s.Elements.Add(Txt("("));
        s.Elements.Add(Hueco("x"));
        s.Elements.Add(Txt(")\n    "));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\nend"));
        return s;
    }

    private static Snippet Seccion()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("%% "));
        s.Elements.Add(Hueco("Titulo de la seccion"));
        s.Elements.Add(Txt("\n"));
        s.Elements.Add(new SnippetCaretElement());
        return s;
    }

    private static Snippet Titulo()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("%\" "));
        s.Elements.Add(Hueco("Titulo"));
        s.Elements.Add(Txt("\n"));
        s.Elements.Add(new SnippetCaretElement());
        return s;
    }

    private static Snippet Texto()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("%' "));
        s.Elements.Add(Hueco("texto que sale en el reporte"));
        s.Elements.Add(Txt("\n"));
        s.Elements.Add(new SnippetCaretElement());
        return s;
    }

    private static Snippet TicToc()
    {
        var s = new Snippet();
        s.Elements.Add(Txt("tic\n"));
        s.Elements.Add(new SnippetCaretElement());
        s.Elements.Add(Txt("\ndt = toc;"));
        return s;
    }
}

/// <summary>Un item del popup. Con snippet: borra lo escrito e inserta el bloque
/// con sus huecos; sin snippet: inserta la palabra.</summary>
internal sealed class MatlabCompletionData : ICompletionData
{
    private readonly Func<Snippet>? _snippet;
    private readonly Brush? _color;
    private readonly bool _negrita;

    public MatlabCompletionData(string text, string doc, Func<Snippet>? snippet = null,
                                double priority = 0, Brush? color = null, bool negrita = false)
    {
        Text = text;
        Description = doc;
        _snippet = snippet;
        Priority = priority;
        _color = color;
        _negrita = negrita;
    }

    public ImageSource? Image => null;
    public string Text { get; }

    /// <summary>Lo que se VE en la lista. Con color/negrita cuando es algo declarado por el
    /// usuario (mismo codigo de colores que el editor clasico); "▸" marca los snippets.</summary>
    public object Content
    {
        get
        {
            var etiqueta = _snippet is null ? Text : Text + "   ▸";
            if (_color is null && !_negrita) return etiqueta;
            return new System.Windows.Controls.TextBlock
            {
                Text = etiqueta,
                Foreground = _color,
                FontWeight = _negrita ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
            };
        }
    }

    public object Description { get; }
    public double Priority { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs e)
    {
        if (_snippet is not null)
        {
            textArea.Document.Remove(completionSegment.Offset, completionSegment.Length);
            _snippet().Insert(textArea);
        }
        else
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
