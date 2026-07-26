#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Calcpad.Core.Fortran;

/// <summary>Builds the AST for a practical Fortran subset (free form, F90 style).</summary>
public sealed class Parser
{
    private static readonly string[] TypeWords = { "integer", "real", "double", "logical", "character" };

    private readonly List<Token> _t;
    private int _p;

    public Parser(List<Token> tokens) => _t = tokens;

    private Token Cur => _t[_p];
    private Token Next => _t[Math.Min(_p + 1, _t.Count - 1)];
    private void Advance() => _p++;
    private bool AtEnd => Cur.Kind == TokKind.Eof;

    private void SkipEols() { while (Cur.Kind == TokKind.Eol) Advance(); }

    private void ExpectEol()
    {
        if (Cur.Kind is TokKind.Eol or TokKind.Eof) { if (Cur.Kind == TokKind.Eol) Advance(); return; }
        throw new FortranError($"sobra '{Cur}' al final de la instruccion", Cur.Line);
    }

    private void Expect(string op)
    {
        if (!Cur.Is(op)) throw new FortranError($"falta '{op}' (encontre '{Cur}')", Cur.Line);
        Advance();
    }

    public ProgramUnit ParseProgram()
    {
        SkipEols();
        var name = "main";
        if (Cur.IsWord("program"))
        {
            Advance();
            if (Cur.Kind == TokKind.Ident) { name = Cur.Text; Advance(); }
            ExpectEol();
        }

        var body = ParseBlock("end", "contains");
        // optional trailing: end / end program [name]
        if (Cur.IsWord("end"))
        {
            Advance();
            if (Cur.IsWord("program")) Advance();
            if (Cur.Kind == TokKind.Ident) Advance();
        }
        return new ProgramUnit(name, body);
    }

    /// <summary>Parses statements until one of the given block-closing words.</summary>
    private List<Stmt> ParseBlock(params string[] closers)
    {
        var list = new List<Stmt>();
        while (true)
        {
            SkipEols();
            if (AtEnd) break;
            if (Cur.Kind == TokKind.Ident && closers.Any(c => Cur.IsWord(c))) break;
            // 'else' / 'else if' also close an if-block
            if (Cur.IsWord("else")) break;
            list.Add(ParseStatement());
        }
        return list;
    }

    private Stmt ParseStatement()
    {
        var line = Cur.Line;

        if (Cur.Kind == TokKind.Ident)
        {
            if (IsTypeWord(Cur.Text)) return ParseDeclaration();
            if (Cur.IsWord("print")) return ParsePrint();
            if (Cur.IsWord("write")) return ParseWrite();
            if (Cur.IsWord("do")) return ParseDo();
            if (Cur.IsWord("if")) return ParseIf();
            if (Cur.IsWord("stop")) { Advance(); ExpectEol(); return new StopStmt(line); }
            if (Cur.IsWord("implicit")) { SkipToEol(); return new NoopStmt(line); }
        }
        return ParseAssignment();
    }

    private void SkipToEol()
    {
        while (Cur.Kind is not (TokKind.Eol or TokKind.Eof)) Advance();
        ExpectEol();
    }

    private static bool IsTypeWord(string w) =>
        TypeWords.Any(t => string.Equals(t, w, StringComparison.OrdinalIgnoreCase));

    private Stmt ParseDeclaration()
    {
        var line = Cur.Line;
        var type = Cur.Text.ToLowerInvariant();
        Advance();
        if (type == "double" && Cur.IsWord("precision")) { Advance(); type = "real"; }

        // optional kind: real(8) / integer(kind=4)
        if (Cur.Is("(")) SkipBalancedParens();

        var isParameter = false;
        while (Cur.Is(","))                       // attributes: , parameter , dimension(5)
        {
            Advance();
            if (Cur.IsWord("parameter")) { isParameter = true; Advance(); }
            else if (Cur.IsWord("dimension")) { Advance(); if (Cur.Is("(")) SkipBalancedParens(); }
            else if (Cur.Kind == TokKind.Ident) Advance();
            else break;
        }

        if (Cur.Is("::")) Advance();

        var vars = new List<DeclaredVar>();
        while (true)
        {
            if (Cur.Kind != TokKind.Ident)
                throw new FortranError("falta el nombre de la variable", Cur.Line);
            var name = Cur.Text; Advance();

            Expr? size = null;
            if (Cur.Is("(")) { Advance(); size = ParseExpr(); Expect(")"); }

            Expr? init = null;
            if (Cur.Is("=")) { Advance(); init = ParseExpr(); }

            vars.Add(new DeclaredVar(name, size, init));
            if (Cur.Is(",")) { Advance(); continue; }
            break;
        }
        ExpectEol();
        return new DeclStmt(type, vars, isParameter, line);
    }

    private void SkipBalancedParens()
    {
        var depth = 0;
        do
        {
            if (Cur.Is("(")) depth++;
            else if (Cur.Is(")")) depth--;
            Advance();
        } while (depth > 0 && !AtEnd);
    }

    private Stmt ParsePrint()
    {
        var line = Cur.Line;
        Advance();                                 // print
        if (Cur.Is("*")) Advance();                // list-directed format
        else if (Cur.Kind == TokKind.Str) Advance();
        if (Cur.Is(",")) Advance();
        return new PrintStmt(ParsePrintItems(), line);
    }

    private Stmt ParseWrite()
    {
        var line = Cur.Line;
        Advance();                                 // write
        if (Cur.Is("(")) SkipBalancedParens();     // write(*,*)
        if (Cur.Is(",")) Advance();
        return new PrintStmt(ParsePrintItems(), line);
    }

    private List<Expr> ParsePrintItems()
    {
        var items = new List<Expr>();
        if (Cur.Kind is TokKind.Eol or TokKind.Eof) { ExpectEol(); return items; }
        while (true)
        {
            items.Add(ParseExpr());
            if (Cur.Is(",")) { Advance(); continue; }
            break;
        }
        ExpectEol();
        return items;
    }

    private Stmt ParseDo()
    {
        var line = Cur.Line;
        Advance();                                 // do

        if (Cur.IsWord("while"))
        {
            Advance();
            Expect("(");
            var cond = ParseExpr();
            Expect(")");
            ExpectEol();
            var whileBody = ParseBlock("end", "enddo");
            CloseDo();
            return new DoWhileStmt(cond, whileBody, line);
        }

        if (Cur.Kind != TokKind.Ident) throw new FortranError("falta la variable del bucle 'do'", Cur.Line);
        var v = Cur.Text; Advance();
        Expect("=");
        var from = ParseExpr();
        Expect(",");
        var to = ParseExpr();
        Expr? step = null;
        if (Cur.Is(",")) { Advance(); step = ParseExpr(); }
        ExpectEol();

        var body = ParseBlock("end", "enddo");
        CloseDo();
        return new DoStmt(v, from, to, step, body, line);
    }

    private void CloseDo()
    {
        if (Cur.IsWord("enddo")) { Advance(); ExpectEol(); return; }
        if (Cur.IsWord("end"))
        {
            Advance();
            if (Cur.IsWord("do")) Advance();
            ExpectEol();
            return;
        }
        throw new FortranError("falta 'end do'", Cur.Line);
    }

    private Stmt ParseIf()
    {
        var line = Cur.Line;
        Advance();                                 // if
        Expect("(");
        var cond = ParseExpr();
        Expect(")");

        // single-line form:  if (c) x = 1
        if (!Cur.IsWord("then"))
        {
            var single = ParseStatement();
            return new IfStmt(new List<IfBranch> { new(cond, new List<Stmt> { single }) }, null, line);
        }

        Advance();                                 // then
        ExpectEol();

        var branches = new List<IfBranch>();
        var body = ParseBlock("end", "endif");
        branches.Add(new IfBranch(cond, body));

        List<Stmt>? elseBody = null;
        while (Cur.IsWord("else"))
        {
            Advance();
            if (Cur.IsWord("if"))                  // else if (...) then
            {
                Advance();
                Expect("(");
                var c2 = ParseExpr();
                Expect(")");
                if (Cur.IsWord("then")) Advance();
                ExpectEol();
                branches.Add(new IfBranch(c2, ParseBlock("end", "endif")));
                continue;
            }
            ExpectEol();                           // plain else
            elseBody = ParseBlock("end", "endif");
            break;
        }

        if (Cur.IsWord("endif")) { Advance(); ExpectEol(); }
        else if (Cur.IsWord("end")) { Advance(); if (Cur.IsWord("if")) Advance(); ExpectEol(); }
        else throw new FortranError("falta 'end if'", Cur.Line);

        return new IfStmt(branches, elseBody, line);
    }

    private Stmt ParseAssignment()
    {
        var line = Cur.Line;
        if (Cur.Kind != TokKind.Ident)
            throw new FortranError($"no entiendo la instruccion que empieza con '{Cur}'", line);

        var name = Cur.Text; Advance();

        List<Expr>? index = null;
        if (Cur.Is("("))
        {
            Advance();
            index = new List<Expr>();
            while (!Cur.Is(")"))
            {
                index.Add(ParseExpr());
                if (Cur.Is(",")) { Advance(); continue; }
                break;
            }
            Expect(")");
        }

        Expect("=");
        var value = ParseExpr();
        ExpectEol();
        return new AssignStmt(name, index, value, line);
    }

    // ----- expressions (lowest precedence first) -----

    public Expr ParseExpr() => ParseOr();

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Cur.Is(".or.") || Cur.Is(".eqv.") || Cur.Is(".neqv."))
        {
            var op = Cur.Text; var line = Cur.Line; Advance();
            left = new BinaryExpr(op, left, ParseAnd(), line);
        }
        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParseNot();
        while (Cur.Is(".and."))
        {
            var line = Cur.Line; Advance();
            left = new BinaryExpr(".and.", left, ParseNot(), line);
        }
        return left;
    }

    private Expr ParseNot()
    {
        if (Cur.Is(".not."))
        {
            var line = Cur.Line; Advance();
            return new UnaryExpr(".not.", ParseNot(), line);
        }
        return ParseComparison();
    }

    private Expr ParseComparison()
    {
        var left = ParseConcat();
        while (true)
        {
            var op = Cur.Kind == TokKind.Op ? Cur.Text : "";
            var normalized = op switch
            {
                "==" or ".eq." => "==",
                "/=" or ".ne." => "/=",
                "<" or ".lt." => "<",
                "<=" or ".le." => "<=",
                ">" or ".gt." => ">",
                ">=" or ".ge." => ">=",
                _ => null
            };
            if (normalized is null) return left;
            var line = Cur.Line; Advance();
            left = new BinaryExpr(normalized, left, ParseConcat(), line);
        }
    }

    private Expr ParseConcat()
    {
        var left = ParseAdditive();
        while (Cur.Is("//"))
        {
            var line = Cur.Line; Advance();
            left = new BinaryExpr("//", left, ParseAdditive(), line);
        }
        return left;
    }

    private Expr ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Cur.Is("+") || Cur.Is("-"))
        {
            var op = Cur.Text; var line = Cur.Line; Advance();
            left = new BinaryExpr(op, left, ParseMultiplicative(), line);
        }
        return left;
    }

    private Expr ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Cur.Is("*") || Cur.Is("/"))
        {
            var op = Cur.Text; var line = Cur.Line; Advance();
            left = new BinaryExpr(op, left, ParseUnary(), line);
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Cur.Is("-") || Cur.Is("+"))
        {
            var op = Cur.Text; var line = Cur.Line; Advance();
            return new UnaryExpr(op, ParseUnary(), line);
        }
        return ParsePower();
    }

    /// <summary>Power binds tighter than unary minus and is right-associative.</summary>
    private Expr ParsePower()
    {
        var b = ParsePrimary();
        if (Cur.Is("**"))
        {
            var line = Cur.Line; Advance();
            return new BinaryExpr("**", b, ParseUnary(), line);
        }
        return b;
    }

    private Expr ParsePrimary()
    {
        var line = Cur.Line;

        if (Cur.Kind == TokKind.Number)
        {
            var v = Cur.Num;
            // An integer literal has no decimal point and no exponent — this matters
            // because Fortran does integer division (5/2 == 2).
            var isInt = Cur.Text.IndexOf('.') < 0 && Cur.Text.IndexOf('e') < 0 && Cur.Text.IndexOf('E') < 0;
            Advance();
            return new NumExpr(v, isInt, line);
        }
        if (Cur.Kind == TokKind.Str) { var s = Cur.Text; Advance(); return new StrExpr(s, line); }
        if (Cur.Is(".true.")) { Advance(); return new NumExpr(1, true, line); }
        if (Cur.Is(".false.")) { Advance(); return new NumExpr(0, true, line); }

        if (Cur.Is("("))
        {
            Advance();
            var inner = ParseExpr();
            Expect(")");
            return inner;
        }

        if (Cur.Kind == TokKind.Ident)
        {
            var name = Cur.Text; Advance();
            if (Cur.Is("("))
            {
                Advance();
                var args = new List<Expr>();
                while (!Cur.Is(")"))
                {
                    args.Add(ParseExpr());
                    if (Cur.Is(",")) { Advance(); continue; }
                    break;
                }
                Expect(")");
                return new CallExpr(name, args, line);
            }
            return new VarExpr(name, line);
        }

        throw new FortranError($"no esperaba '{Cur}' aqui", line);
    }
}
