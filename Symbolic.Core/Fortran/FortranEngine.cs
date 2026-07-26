#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Calcpad.Core.Fortran;

/// <summary>Result of running a program: what it printed, plus the first error if any.</summary>
public sealed record FortranResult(string Output, string? Error, int ErrorLine)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Entry point of the embedded Fortran engine: source in, output out.
/// No external compiler, no temp files, no processes — everything runs in C#.
/// </summary>
public static class FortranEngine
{
    public static FortranResult Run(string source)
    {
        var interpreter = new Interpreter();
        try
        {
            var tokens = new Lexer(source).Tokenize();
            var program = new Parser(tokens).ParseProgram();
            var output = interpreter.Run(program);
            return new FortranResult(output, null, 0);
        }
        catch (FortranError ex)
        {
            // Keep whatever the program printed before failing — that context helps.
            return new FortranResult(interpreter.Output, ex.Message, ex.Line);
        }
        catch (Exception ex)
        {
            return new FortranResult(interpreter.Output, "error interno del motor: " + ex.Message, 0);
        }
    }
}
