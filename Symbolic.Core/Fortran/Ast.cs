#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Calcpad.Core.Fortran;

// ----- expressions -----

public abstract record Expr(int Line);

public sealed record NumExpr(double Value, bool IsInt, int Line) : Expr(Line);
public sealed record StrExpr(string Value, int Line) : Expr(Line);
public sealed record VarExpr(string Name, int Line) : Expr(Line);

/// <summary>Either an array element a(i) or an intrinsic call sqrt(x) — resolved at run time.</summary>
public sealed record CallExpr(string Name, List<Expr> Args, int Line) : Expr(Line);

public sealed record UnaryExpr(string Op, Expr Operand, int Line) : Expr(Line);
public sealed record BinaryExpr(string Op, Expr Left, Expr Right, int Line) : Expr(Line);

// ----- statements -----

public abstract record Stmt(int Line);

public sealed record DeclaredVar(string Name, Expr? Size, Expr? Init);

public sealed record DeclStmt(string Type, List<DeclaredVar> Vars, bool IsParameter, int Line) : Stmt(Line);

public sealed record AssignStmt(string Name, List<Expr>? Index, Expr Value, int Line) : Stmt(Line);

public sealed record PrintStmt(List<Expr> Items, int Line) : Stmt(Line);

public sealed record DoStmt(string Var, Expr From, Expr To, Expr? Step, List<Stmt> Body, int Line) : Stmt(Line);

public sealed record DoWhileStmt(Expr Cond, List<Stmt> Body, int Line) : Stmt(Line);

public sealed record IfBranch(Expr Cond, List<Stmt> Body);

public sealed record IfStmt(List<IfBranch> Branches, List<Stmt>? Else, int Line) : Stmt(Line);

public sealed record StopStmt(int Line) : Stmt(Line);

/// <summary>Accepted but ignored, e.g. 'implicit none'.</summary>
public sealed record NoopStmt(int Line) : Stmt(Line);

public sealed record ProgramUnit(string Name, List<Stmt> Body);
