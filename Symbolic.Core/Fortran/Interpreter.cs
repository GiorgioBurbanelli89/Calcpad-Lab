#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core.Fortran;

/// <summary>Executes the AST. Pure C# — no external Fortran compiler is involved.</summary>
public sealed class Interpreter
{
    private const long MaxSteps = 60_000_000;
    private const int MaxOutputChars = 2_000_000;

    private enum VKind { Int, Real, Logical, Text }

    private readonly record struct Val(double Num, string? Text, VKind Kind)
    {
        public static Val Int(double v) => new(v, null, VKind.Int);
        public static Val Real(double v) => new(v, null, VKind.Real);
        public static Val Logical(bool b) => new(b ? 1 : 0, null, VKind.Logical);
        public static Val Str(string s) => new(0, s, VKind.Text);
        public bool IsText => Kind == VKind.Text;
        public bool Truth => Num != 0;
    }

    private sealed class Variable
    {
        public string Type = "real";
        public double Scalar;
        public string Text = "";
        public double[]? Array;
        public bool IsInt => Type is "integer" or "logical";
    }

    private sealed class StopSignal : Exception { }

    private readonly Dictionary<string, Variable> _vars = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _out = new();
    private long _steps;

    /// <summary>What the program printed so far (also available after an error).</summary>
    public string Output => _out.ToString();

    public string Run(ProgramUnit program)
    {
        try { ExecBlock(program.Body); }
        catch (StopSignal) { /* 'stop' ends the program normally */ }
        return _out.ToString();
    }

    // ---------- statements ----------

    private void ExecBlock(List<Stmt> body)
    {
        foreach (var s in body) Exec(s);
    }

    private void Exec(Stmt s)
    {
        if (++_steps > MaxSteps)
            throw new FortranError("el programa hizo demasiadas operaciones (posible bucle infinito)", s.Line);

        switch (s)
        {
            case NoopStmt: return;
            case StopStmt: throw new StopSignal();
            case DeclStmt d: ExecDecl(d); return;
            case AssignStmt a: ExecAssign(a); return;
            case PrintStmt p: ExecPrint(p); return;
            case IfStmt i: ExecIf(i); return;
            case DoStmt d: ExecDo(d); return;
            case DoWhileStmt w: ExecDoWhile(w); return;
            default: throw new FortranError("instruccion no soportada", s.Line);
        }
    }

    private void ExecDecl(DeclStmt d)
    {
        foreach (var v in d.Vars)
        {
            var variable = new Variable { Type = d.Type };
            if (v.Size is not null)
            {
                var n = (int)Eval(v.Size).Num;
                if (n <= 0) throw new FortranError($"tamano de arreglo no valido en '{v.Name}'", d.Line);
                variable.Array = new double[n];
            }
            if (v.Init is not null)
            {
                var init = Eval(v.Init);
                if (init.IsText) { variable.Text = init.Text!; variable.Type = "character"; }
                else variable.Scalar = Coerce(init.Num, variable.IsInt);
            }
            _vars[v.Name] = variable;
        }
    }

    private void ExecAssign(AssignStmt a)
    {
        var value = Eval(a.Value);

        if (!_vars.TryGetValue(a.Name, out var variable))
        {
            // Fortran's implicit typing: i..n are integers, everything else real.
            var c = char.ToLowerInvariant(a.Name[0]);
            variable = new Variable { Type = c is >= 'i' and <= 'n' ? "integer" : "real" };
            if (value.IsText) variable.Type = "character";
            _vars[a.Name] = variable;
        }

        if (a.Index is { Count: > 0 })
        {
            if (variable.Array is null)
                throw new FortranError($"'{a.Name}' no es un arreglo", a.Line);
            var idx = (int)Eval(a.Index[0]).Num;
            if (idx < 1 || idx > variable.Array.Length)
                throw new FortranError($"indice {idx} fuera de rango en '{a.Name}' (1..{variable.Array.Length})", a.Line);
            variable.Array[idx - 1] = Coerce(value.Num, variable.IsInt);
            return;
        }

        if (value.IsText) { variable.Text = value.Text!; variable.Type = "character"; }
        else variable.Scalar = Coerce(value.Num, variable.IsInt);
    }

    private static double Coerce(double v, bool toInt) => toInt ? Math.Truncate(v) : v;

    private void ExecPrint(PrintStmt p)
    {
        var parts = new List<string>();
        foreach (var item in p.Items) parts.Add(Format(Eval(item)));
        var line = string.Join(" ", parts);
        if (_out.Length + line.Length > MaxOutputChars)
            throw new FortranError("el programa genero demasiada salida", p.Line);
        _out.Append(line).Append('\n');
    }

    private void ExecIf(IfStmt s)
    {
        foreach (var b in s.Branches)
            if (Eval(b.Cond).Truth) { ExecBlock(b.Body); return; }
        if (s.Else is not null) ExecBlock(s.Else);
    }

    private void ExecDo(DoStmt d)
    {
        var from = Eval(d.From).Num;
        var to = Eval(d.To).Num;
        var step = d.Step is null ? 1 : Eval(d.Step).Num;
        if (step == 0) throw new FortranError("el paso del bucle 'do' no puede ser 0", d.Line);

        if (!_vars.TryGetValue(d.Var, out var loopVar))
        {
            var c = char.ToLowerInvariant(d.Var[0]);
            loopVar = new Variable { Type = c is >= 'i' and <= 'n' ? "integer" : "real" };
            _vars[d.Var] = loopVar;
        }

        for (var i = from; step > 0 ? i <= to : i >= to; i += step)
        {
            loopVar.Scalar = Coerce(i, loopVar.IsInt);
            ExecBlock(d.Body);
            if (++_steps > MaxSteps)
                throw new FortranError("bucle demasiado largo (posible bucle infinito)", d.Line);
        }
    }

    private void ExecDoWhile(DoWhileStmt w)
    {
        while (Eval(w.Cond).Truth)
        {
            ExecBlock(w.Body);
            if (++_steps > MaxSteps)
                throw new FortranError("bucle 'do while' demasiado largo (posible bucle infinito)", w.Line);
        }
    }

    // ---------- expressions ----------

    private Val Eval(Expr e)
    {
        switch (e)
        {
            case NumExpr n: return n.IsInt ? Val.Int(n.Value) : Val.Real(n.Value);
            case StrExpr s: return Val.Str(s.Value);

            case VarExpr v:
            {
                if (!_vars.TryGetValue(v.Name, out var variable))
                    throw new FortranError($"variable '{v.Name}' sin valor", v.Line);
                if (variable.Type == "character") return Val.Str(variable.Text);
                if (variable.Array is not null)
                    throw new FortranError($"'{v.Name}' es un arreglo: falta el indice", v.Line);
                return variable.IsInt ? Val.Int(variable.Scalar) : Val.Real(variable.Scalar);
            }

            case UnaryExpr u:
            {
                var x = Eval(u.Operand);
                return u.Op switch
                {
                    "-" => x.Kind == VKind.Int ? Val.Int(-x.Num) : Val.Real(-x.Num),
                    "+" => x,
                    ".not." => Val.Logical(!x.Truth),
                    _ => throw new FortranError($"operador '{u.Op}' no soportado", u.Line)
                };
            }

            case BinaryExpr b: return EvalBinary(b);
            case CallExpr c: return EvalCall(c);
            default: throw new FortranError("expresion no soportada", e.Line);
        }
    }

    private Val EvalBinary(BinaryExpr b)
    {
        // string concatenation
        if (b.Op == "//")
        {
            var ls = Eval(b.Left); var rs = Eval(b.Right);
            return Val.Str(Format(ls) + Format(rs));
        }

        var l = Eval(b.Left);
        var r = Eval(b.Right);

        switch (b.Op)
        {
            case ".and.": return Val.Logical(l.Truth && r.Truth);
            case ".or.": return Val.Logical(l.Truth || r.Truth);
            case ".eqv.": return Val.Logical(l.Truth == r.Truth);
            case ".neqv.": return Val.Logical(l.Truth != r.Truth);
        }

        if (l.IsText || r.IsText)
        {
            var a = Format(l); var c = Format(r);
            return b.Op switch
            {
                "==" => Val.Logical(string.Equals(a, c, StringComparison.Ordinal)),
                "/=" => Val.Logical(!string.Equals(a, c, StringComparison.Ordinal)),
                _ => throw new FortranError($"no se puede usar '{b.Op}' con texto", b.Line)
            };
        }

        switch (b.Op)
        {
            case "==": return Val.Logical(l.Num == r.Num);
            case "/=": return Val.Logical(l.Num != r.Num);
            case "<": return Val.Logical(l.Num < r.Num);
            case "<=": return Val.Logical(l.Num <= r.Num);
            case ">": return Val.Logical(l.Num > r.Num);
            case ">=": return Val.Logical(l.Num >= r.Num);
        }

        var bothInt = l.Kind == VKind.Int && r.Kind == VKind.Int;

        switch (b.Op)
        {
            case "+": return Num(l.Num + r.Num, bothInt);
            case "-": return Num(l.Num - r.Num, bothInt);
            case "*": return Num(l.Num * r.Num, bothInt);
            case "/":
                if (r.Num == 0) throw new FortranError("division por cero", b.Line);
                // Fortran truncates when both operands are integers: 5/2 == 2
                return bothInt ? Val.Int(Math.Truncate(l.Num / r.Num)) : Val.Real(l.Num / r.Num);
            case "**":
                return Num(Math.Pow(l.Num, r.Num), bothInt);
            default:
                throw new FortranError($"operador '{b.Op}' no soportado", b.Line);
        }
    }

    private static Val Num(double v, bool isInt) => isInt ? Val.Int(v) : Val.Real(v);

    private Val EvalCall(CallExpr c)
    {
        // array element takes priority over an intrinsic with the same name
        if (_vars.TryGetValue(c.Name, out var variable) && variable.Array is not null)
        {
            if (c.Args.Count != 1)
                throw new FortranError($"'{c.Name}' necesita un indice", c.Line);
            var idx = (int)Eval(c.Args[0]).Num;
            if (idx < 1 || idx > variable.Array.Length)
                throw new FortranError($"indice {idx} fuera de rango en '{c.Name}' (1..{variable.Array.Length})", c.Line);
            var value = variable.Array[idx - 1];
            return variable.IsInt ? Val.Int(value) : Val.Real(value);
        }

        var name = c.Name.ToLowerInvariant();

        // whole-array intrinsics
        if (name is "sum" or "size" or "maxval" or "minval" or "product" && c.Args.Count == 1 &&
            c.Args[0] is VarExpr av && _vars.TryGetValue(av.Name, out var arrVar) && arrVar.Array is not null)
        {
            var a = arrVar.Array;
            return name switch
            {
                "sum" => Num(a.Sum(), arrVar.IsInt),
                "product" => Num(a.Aggregate(1.0, (x, y) => x * y), arrVar.IsInt),
                "size" => Val.Int(a.Length),
                "maxval" => Num(a.Max(), arrVar.IsInt),
                "minval" => Num(a.Min(), arrVar.IsInt),
                _ => throw new FortranError($"'{name}' no soportado", c.Line)
            };
        }

        var args = c.Args.Select(Eval).ToList();
        double A(int i) => args[i].Num;

        void Need(int n)
        {
            if (args.Count != n)
                throw new FortranError($"'{name}' espera {n} argumento(s), recibio {args.Count}", c.Line);
        }

        switch (name)
        {
            case "sqrt": Need(1); if (A(0) < 0) throw new FortranError("sqrt de un numero negativo", c.Line); return Val.Real(Math.Sqrt(A(0)));
            case "abs": Need(1); return Num(Math.Abs(A(0)), args[0].Kind == VKind.Int);
            case "sin": Need(1); return Val.Real(Math.Sin(A(0)));
            case "cos": Need(1); return Val.Real(Math.Cos(A(0)));
            case "tan": Need(1); return Val.Real(Math.Tan(A(0)));
            case "asin": Need(1); return Val.Real(Math.Asin(A(0)));
            case "acos": Need(1); return Val.Real(Math.Acos(A(0)));
            case "atan": Need(1); return Val.Real(Math.Atan(A(0)));
            case "atan2": Need(2); return Val.Real(Math.Atan2(A(0), A(1)));
            case "exp": Need(1); return Val.Real(Math.Exp(A(0)));
            case "log": Need(1); if (A(0) <= 0) throw new FortranError("log de un numero no positivo", c.Line); return Val.Real(Math.Log(A(0)));
            case "log10": Need(1); if (A(0) <= 0) throw new FortranError("log10 de un numero no positivo", c.Line); return Val.Real(Math.Log10(A(0)));
            case "int": Need(1); return Val.Int(Math.Truncate(A(0)));
            case "nint": Need(1); return Val.Int(Math.Round(A(0), MidpointRounding.AwayFromZero));
            case "floor": Need(1); return Val.Int(Math.Floor(A(0)));
            case "ceiling": Need(1); return Val.Int(Math.Ceiling(A(0)));
            case "real": case "dble": case "dfloat": Need(1); return Val.Real(A(0));
            case "mod": Need(2); if (A(1) == 0) throw new FortranError("mod con divisor cero", c.Line);
                // Fortran MOD keeps the sign of the first argument.
                return Num(A(0) - Math.Truncate(A(0) / A(1)) * A(1),
                           args[0].Kind == VKind.Int && args[1].Kind == VKind.Int);
            case "modulo": Need(2); if (A(1) == 0) throw new FortranError("modulo con divisor cero", c.Line);
                return Num(A(0) - Math.Floor(A(0) / A(1)) * A(1), args[0].Kind == VKind.Int && args[1].Kind == VKind.Int);
            case "max": if (args.Count < 2) throw new FortranError("'max' espera 2 o mas argumentos", c.Line);
                return Num(args.Max(v => v.Num), args.All(v => v.Kind == VKind.Int));
            case "min": if (args.Count < 2) throw new FortranError("'min' espera 2 o mas argumentos", c.Line);
                return Num(args.Min(v => v.Num), args.All(v => v.Kind == VKind.Int));
            default:
                throw new FortranError($"no conozco '{c.Name}' (¿variable sin declarar o funcion no soportada?)", c.Line);
        }
    }

    // ---------- output formatting (list-directed, Fortran-like) ----------

    private static string Format(Val v) => v.Kind switch
    {
        VKind.Text => v.Text!,
        VKind.Logical => v.Num != 0 ? "T" : "F",
        VKind.Int => ((long)Math.Round(v.Num)).ToString(CultureInfo.InvariantCulture),
        _ => FormatReal(v.Num)
    };

    private static string FormatReal(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsInfinity(v)) return v > 0 ? "Infinity" : "-Infinity";
        var mag = Math.Abs(v);
        if (v != 0 && (mag >= 1e7 || mag < 1e-4))
            return v.ToString("0.000000E+00", CultureInfo.InvariantCulture);
        return v.ToString("0.0#####", CultureInfo.InvariantCulture);
    }
}
