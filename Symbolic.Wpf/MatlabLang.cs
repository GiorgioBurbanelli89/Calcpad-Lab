using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Snippets;

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

    /// <summary>Items del popup, filtrados por lo que ya se escribio.</summary>
    public static IEnumerable<ICompletionData> Items(string prefijo)
    {
        var lista = new List<ICompletionData>();

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

    public MatlabCompletionData(string text, string doc, Func<Snippet>? snippet = null, double priority = 0)
    {
        Text = text;
        Description = doc;
        _snippet = snippet;
        Priority = priority;
    }

    public ImageSource? Image => null;
    public string Text { get; }
    public object Content => _snippet is null ? Text : Text + "   ▸";   // ▸ marca los snippets
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
