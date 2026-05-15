// =============================================================================
// Calcpad Lab — MATLAB Abstract Syntax Tree (AST nodes)
// =============================================================================
//   AST de un statement MATLAB. Evaluado por MatlabEvaluator.
//   Render HTML por MatlabHtmlWriter. Ningún nodo referencia tipos de Calcpad.
// =============================================================================
using System.Collections.Generic;

namespace Calcpad.Core.Matlab
{
    public abstract class MatlabNode
    {
        public int Line;
        public int Column;
    }

    // ─── Expresiones ────────────────────────────────────────────────────────
    public sealed class NumberLit : MatlabNode { public double Value; public string OrigText; }
    public sealed class ImaginaryLit : MatlabNode { public double Value; public string OrigText; }
    public sealed class StringLit : MatlabNode { public string Value; public char Quote; /* ' o " */ }
    public sealed class IdentRef : MatlabNode { public string Name; }
    public sealed class UnaryOp : MatlabNode
    {
        public string Op;           // "-", "+", "~", "!"   y postfix "'", ".'"
        public MatlabNode Operand;
        public bool IsPrefix;
    }
    public sealed class BinaryOp : MatlabNode
    {
        public string Op;           // "+", "-", "*", "/", "\\", "^", ".*", "./", ".\\", ".^"
                                    // "==", "~=", "<", ">", "<=", ">=", "&&", "||", "&", "|"
        public MatlabNode Left;
        public MatlabNode Right;
    }
    public sealed class CallOrIndex : MatlabNode
    {
        // En MATLAB la sintaxis A(i, j) cubre tanto function call como indexing.
        // El evaluator decide en runtime mirando el binding de `Target`.
        public MatlabNode Target;       // típicamente IdentRef
        public List<MatlabNode> Args;
    }
    public sealed class Range : MatlabNode
    {
        public MatlabNode Start;
        public MatlabNode Step;          // null si no especificado
        public MatlabNode End;
    }
    public sealed class MatrixLit : MatlabNode
    {
        // [a, b, c; d, e, f]  ⇒  filas separadas por ';' (o newlines), cols por ',' o espacio
        public List<List<MatlabNode>> Rows = new();
    }
    public sealed class CellLit : MatlabNode
    {
        public List<List<MatlabNode>> Rows = new();
    }
    /// <summary>Acceso a campo de struct: <c>obj.field</c>. Target puede ser variable, llamada, indexado, etc.</summary>
    public sealed class FieldAccess : MatlabNode
    {
        public MatlabNode Target;
        public string FieldName;
    }
    /// <summary>Indexing de cell array con llaves: <c>c{i}</c>.</summary>
    public sealed class CellIndex : MatlabNode
    {
        public MatlabNode Target;
        public List<MatlabNode> Args = new();
    }
    public sealed class ColonAll : MatlabNode { /* el `:` aislado significa "todos" en indexing */ }
    public sealed class EndKeyword : MatlabNode { /* `end` dentro de indexing = última posición */ }

    // ─── Statements ─────────────────────────────────────────────────────────
    public sealed class Assignment : MatlabNode
    {
        public List<MatlabNode> Targets = new();  // 1 elemento normal, >1 si `[a, b] = func()`
        public MatlabNode Rhs;
        public bool Suppressed;  // termina con ';' → no mostrar valor
    }
    public sealed class ExprStmt : MatlabNode
    {
        public MatlabNode Expr;
        public bool Suppressed;
    }
    /// <summary>Comentario <c>% ...</c> que se preserva como anotación visual.</summary>
    public sealed class CommentStmt : MatlabNode
    {
        public string Text;
        public bool IsHeading;   // %%
    }

    // ─── Control flow ───────────────────────────────────────────────────────
    public sealed class ForLoop : MatlabNode
    {
        public string VarName;          // for VarName = Iter
        public MatlabNode Iter;          // típicamente un Range o vector
        public List<MatlabNode> Body = new();
    }
    public sealed class WhileLoop : MatlabNode
    {
        public MatlabNode Cond;
        public List<MatlabNode> Body = new();
    }
    public sealed class IfBlock : MatlabNode
    {
        // Branches: pares (cond, body). El "else" final tiene cond = null.
        public List<(MatlabNode Cond, List<MatlabNode> Body)> Branches = new();
    }
    public sealed class SwitchBlock : MatlabNode
    {
        public MatlabNode Discriminant;
        // Cada case: (lista de valores a comparar, body). otherwise: values = null.
        public List<(List<MatlabNode> Values, List<MatlabNode> Body)> Cases = new();
    }
    public sealed class TryCatch : MatlabNode
    {
        public List<MatlabNode> TryBody = new();
        public string CatchVarName;  // opcional: catch err → guarda mensaje
        public List<MatlabNode> CatchBody = new();
    }
    public sealed class BreakStmt : MatlabNode { }
    public sealed class ContinueStmt : MatlabNode { }
    public sealed class ReturnStmt : MatlabNode { }
    /// <summary>Declaración <c>global var1 var2 ...</c> dentro de una función.</summary>
    public sealed class GlobalDecl : MatlabNode { public List<string> Names = new(); }
    /// <summary>Declaración <c>persistent var1 var2 ...</c> dentro de una función.</summary>
    public sealed class PersistentDecl : MatlabNode { public List<string> Names = new(); }

    /// <summary>
    /// Función anónima MATLAB: <c>@(x, y) x.^2 + y</c>. Captura el scope donde
    /// se define (closure simple, sólo lectura).
    /// </summary>
    public sealed class AnonFunction : MatlabNode
    {
        public List<string> ParamNames = new();
        public MatlabNode Body;
    }

    // ─── Function definitions ───────────────────────────────────────────────
    public sealed class FunctionDef : MatlabNode
    {
        public string Name;
        public List<string> OutputNames = new();   // función out = name(args)  → ["out"]
                                                    //          [a, b] = name(args) → ["a", "b"]
                                                    //          name(args)         → [] (procedure)
        public List<string> ParamNames = new();
        public List<MatlabNode> Body = new();
    }

    // ─── Class definitions ──────────────────────────────────────────────────
    /// <summary>Propiedad de clase: <c>name = defaultExpr</c> (defaultExpr puede ser null).</summary>
    public sealed class PropertyDef
    {
        public string Name;
        public MatlabNode DefaultExpr;   // expresión opcional para el valor inicial
    }
    /// <summary>
    /// Definición de clase: <c>classdef Name [&lt; Parent] ... end</c>.
    /// MVP: una sola sección properties + una sola sección methods.
    /// </summary>
    public sealed class ClassDef : MatlabNode
    {
        public string Name;
        public string ParentName;   // opcional (herencia simple)
        public List<PropertyDef> Properties = new();
        public List<FunctionDef> Methods = new();
        public List<FunctionDef> StaticMethods = new();  // methods (Static) … end
    }
}
