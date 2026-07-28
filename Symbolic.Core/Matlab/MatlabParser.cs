// =============================================================================
// Calcpad Lab — MATLAB Parser (top-down recursive descent + Pratt precedence)
// =============================================================================
//   Convierte un List<MatlabToken> en un AST de statements.
//   Independiente del MathParser de Calcpad.
// =============================================================================
using System;
using System.Collections.Generic;

namespace Calcpad.Core.Matlab
{
    public sealed class MatlabParser
    {
        private readonly List<MatlabToken> _tokens;
        private int _pos;
        private readonly bool _octave;
        /// <summary>true = el archivo usa el estilo CLASICO de MATLAB, en el que las
        /// funciones no se cierran con `end` (CEINCI-LAB y casi todo el codigo antiguo).
        /// Se detecta al parsear la primera funcion y se recuerda, para no reintentar
        /// en cada una (seria cuadratico).</summary>
        private bool _classicFnStyle;
        /// <summary>true si al parsear se detectó el estilo CLASICO (funciones sin `end`).
        /// El folder loader lo consulta para cerrarlas antes de concatenar archivos.</summary>
        public bool ClassicFunctionStyle => _classicFnStyle;

        public MatlabParser(List<MatlabToken> tokens)
        {
            _tokens = tokens; _pos = 0;
            _octave = MatlabTokenizer.OctaveMode;
        }

        // ─── Statement-level ────────────────────────────────────────────────
        public List<MatlabNode> ParseAllStatements()
        {
            var stmts = new List<MatlabNode>();
            while (!IsEof())
            {
                SkipNewlines();
                if (IsEof()) break;
                var s = ParseStatement();
                if (s != null) stmts.Add(s);
            }
            return stmts;
        }

        /// <summary>
        /// Funciones MATLAB que aceptan "command form" sin paréntesis:
        /// <c>syms x y z</c>, <c>clear all</c>, <c>format long</c>, etc.
        /// El parser detecta `IDENT IDENT ...` sin operadores y los convierte en
        /// `IDENT('IDENT', 'IDENT', ...)`.
        /// </summary>
        private static readonly HashSet<string> _commandFormFuncs = new(StringComparer.Ordinal)
        {
            // global/persistent SON keywords, NO command-form — tienen handlers en el switch de ParseStatement
            "syms", "clear", "close", "hold", "format", "cd",
            "load", "save", "disp", "warning", "error", "echo", "pkg",
            "grid", "axis", "legend", "box",   // plotting on/off commands
            "lighting", "material", "shading", "colormap",   // 3D: lighting gouraud, material dull, shading interp
            "rotate3d", "colorbar", "camlight", "view"   // 3D: rotate3d on, colorbar off, camlight left, view(3)
        };

        public MatlabNode ParseStatement()
        {
            // Octave: incremento/decremento en prefijo  ++x / --x  →  x = x ± 1
            if (_octave && (Peek().Kind == MatlabTokenKind.Increment || Peek().Kind == MatlabTokenKind.Decrement))
            {
                var opTok = Consume();
                var tgt = TryParseAssignmentLhs(bareTargetOk: true);
                if (tgt == null || tgt.Count != 1)
                    throw new MatlabParseException("Se esperaba una variable tras '++'/'--'", opTok.Line, opTok.Column);
                bool supprInc = ConsumeStatementTerminator();
                string opInc = opTok.Kind == MatlabTokenKind.Increment ? "+" : "-";
                var combinedInc = new BinaryOp { Op = opInc, Left = tgt[0], Right = new NumberLit { Value = 1 },
                                                 Line = opTok.Line, Column = opTok.Column };
                return new Assignment { Targets = tgt, Rhs = combinedInc, Suppressed = supprInc,
                                        Line = opTok.Line, Column = opTok.Column };
            }
            // Command-form: `syms x y z` → `syms('x', 'y', 'z')` antes de comentarios/keywords
            if (Peek().Kind == MatlabTokenKind.Identifier &&
                _commandFormFuncs.Contains(Peek().Text) &&
                IsCommandFormContext())
            {
                var fnTok = Consume();
                var args = new List<MatlabNode>();
                while (true)
                {
                    var pk = Peek().Kind;
                    // identificador pelado: `hold on`, `cd Documents`, `axis equal`
                    if (pk == MatlabTokenKind.Identifier &&
                        Peek().Text != "end" && Peek().Text != "elseif" && Peek().Text != "else"
                        && Peek().Text != "case" && Peek().Text != "otherwise" && Peek().Text != "catch")
                    {
                        var tok = Consume();
                        args.Add(new StringLit { Value = tok.Text, Quote = '\'', Line = tok.Line, Column = tok.Column });
                    }
                    // string entre comillas: `cd 'C:\ruta'`, `load 'file.mat'`, `disp 'hola'`
                    else if (pk == MatlabTokenKind.String || pk == MatlabTokenKind.StringDouble)
                    {
                        var tok = Consume();
                        args.Add(new StringLit { Value = tok.Text,
                            Quote = pk == MatlabTokenKind.StringDouble ? '"' : '\'',
                            Line = tok.Line, Column = tok.Column });
                    }
                    else break;
                }
                bool suppr = ConsumeStatementTerminator();
                var call = new CallOrIndex
                {
                    Target = new IdentRef { Name = fnTok.Text, Line = fnTok.Line, Column = fnTok.Column },
                    Args = args, Line = fnTok.Line, Column = fnTok.Column
                };
                return new ExprStmt { Expr = call, Suppressed = suppr, Line = fnTok.Line, Column = fnTok.Column };
            }
            // Comentarios/heading aislados como statements (visualización)
            if (Peek().Kind == MatlabTokenKind.Comment || Peek().Kind == MatlabTokenKind.SectionHeading)
            {
                var t = Consume();
                ConsumeStatementTerminator();
                return new CommentStmt
                {
                    Text = t.Text,
                    IsHeading = t.Kind == MatlabTokenKind.SectionHeading,
                    Line = t.Line, Column = t.Column
                };
            }
            // Keywords MATLAB: for/while/if/function/break/continue/return
            if (Peek().Kind == MatlabTokenKind.Identifier)
            {
                var name = Peek().Text;
                switch (name)
                {
                    case "for":      return ParseForLoop();
                    case "do":       if (_octave) return ParseDoUntil(); break;
                    case "while":    return ParseWhileLoop();
                    case "if":       return ParseIfBlock();
                    case "switch":   return ParseSwitchBlock();
                    case "try":      return ParseTryCatch();
                    case "function": return ParseFunctionDef();
                    case "classdef": return ParseClassDef();
                    case "global":   return ParseScopeDecl(true);
                    case "persistent": return ParseScopeDecl(false);
                    case "break":    { var t = Consume(); ConsumeStatementTerminator();
                                       return new BreakStmt { Line = t.Line, Column = t.Column }; }
                    case "continue": { var t = Consume(); ConsumeStatementTerminator();
                                       return new ContinueStmt { Line = t.Line, Column = t.Column }; }
                    case "return":   { var t = Consume(); ConsumeStatementTerminator();
                                       return new ReturnStmt { Line = t.Line, Column = t.Column }; }
                }
            }
            // Asignación: LHS = RHS  (LHS puede ser id, id(args), o [id, id, ...])
            int saved = _pos;
            var lhs = TryParseAssignmentLhs();
            if (lhs != null && Peek().Kind == MatlabTokenKind.Assign)
            {
                Consume(); // =
                var rhs = ParseExpression();
                bool suppr = ConsumeStatementTerminator();
                return new Assignment { Targets = lhs, Rhs = rhs, Suppressed = suppr,
                                        Line = rhs?.Line ?? 0, Column = rhs?.Column ?? 0 };
            }
            // Octave: asignación compuesta  a += b   y postfijo  a++ / a--  →  a = a ± 1
            if (_octave && lhs != null && lhs.Count == 1)
            {
                string cop = Peek().Kind switch
                {
                    MatlabTokenKind.PlusAssign  => "+",
                    MatlabTokenKind.MinusAssign => "-",
                    MatlabTokenKind.StarAssign  => "*",
                    MatlabTokenKind.SlashAssign => "/",
                    _ => null
                };
                if (cop != null)
                {
                    var opTok = Consume();
                    var crhs = ParseExpression();
                    bool csuppr = ConsumeStatementTerminator();
                    var combined = new BinaryOp { Op = cop, Left = lhs[0], Right = crhs,
                                                  Line = opTok.Line, Column = opTok.Column };
                    return new Assignment { Targets = lhs, Rhs = combined, Suppressed = csuppr,
                                            Line = opTok.Line, Column = opTok.Column };
                }
                if (Peek().Kind == MatlabTokenKind.Increment || Peek().Kind == MatlabTokenKind.Decrement)
                {
                    var opTok = Consume();
                    bool psuppr = ConsumeStatementTerminator();
                    string pop = opTok.Kind == MatlabTokenKind.Increment ? "+" : "-";
                    var combined = new BinaryOp { Op = pop, Left = lhs[0], Right = new NumberLit { Value = 1 },
                                                  Line = opTok.Line, Column = opTok.Column };
                    return new Assignment { Targets = lhs, Rhs = combined, Suppressed = psuppr,
                                            Line = opTok.Line, Column = opTok.Column };
                }
            }
            // No es asignación → expresión simple
            _pos = saved;
            var expr = ParseExpression();
            bool suppr2 = ConsumeStatementTerminator();
            return new ExprStmt { Expr = expr, Suppressed = suppr2,
                                  Line = expr?.Line ?? 0, Column = expr?.Column ?? 0 };
        }

        // ─── Block parsers ──────────────────────────────────────────────────
        private ForLoop ParseForLoop()
        {
            var hdr = Consume(); // "for"
            if (Peek().Kind != MatlabTokenKind.Identifier)
                throw new MatlabParseException("Expected loop variable after 'for'", Peek().Line, Peek().Column);
            var loopVar = Consume().Text;
            if (Peek().Kind != MatlabTokenKind.Assign)
                throw new MatlabParseException("Expected '=' after for-variable", Peek().Line, Peek().Column);
            Consume(); // =
            var iter = ParseExpression();
            ConsumeStatementTerminator();
            var body = ParseBlockUntil("end");
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return new ForLoop { VarName = loopVar, Iter = iter, Body = body,
                                 Line = hdr.Line, Column = hdr.Column };
        }
        private WhileLoop ParseWhileLoop()
        {
            var hdr = Consume(); // "while"
            var cond = ParseExpression();
            ConsumeStatementTerminator();
            var body = ParseBlockUntil("end");
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return new WhileLoop { Cond = cond, Body = body,
                                   Line = hdr.Line, Column = hdr.Column };
        }
        // Octave:  do BODY until COND   →   while true; BODY; if COND, break; end; end
        // (el cuerpo se ejecuta al menos una vez). Reutiliza WhileLoop + IfBlock + Break.
        private WhileLoop ParseDoUntil()
        {
            var hdr = Consume(); // "do"
            ConsumeStatementTerminator();
            var body = ParseBlockUntil("until");
            ExpectKeyword("until");
            var cond = ParseExpression();
            ConsumeStatementTerminator();
            var loopBody = new List<MatlabNode>(body);
            var brk = new IfBlock { Line = hdr.Line, Column = hdr.Column };
            brk.Branches.Add((cond, new List<MatlabNode> { new BreakStmt { Line = hdr.Line, Column = hdr.Column } }));
            loopBody.Add(brk);
            return new WhileLoop
            {
                Cond = new NumberLit { Value = 1, Line = hdr.Line, Column = hdr.Column },
                Body = loopBody, Line = hdr.Line, Column = hdr.Column
            };
        }
        private IfBlock ParseIfBlock()
        {
            var hdr = Consume(); // "if"
            var block = new IfBlock { Line = hdr.Line, Column = hdr.Column };
            var firstCond = ParseExpression();
            ConsumeStatementTerminator();
            var firstBody = ParseBlockUntil("elseif", "else", "end");
            block.Branches.Add((firstCond, firstBody));
            while (PeekKeyword("elseif"))
            {
                Consume();
                var c = ParseExpression();
                ConsumeStatementTerminator();
                var b = ParseBlockUntil("elseif", "else", "end");
                block.Branches.Add((c, b));
            }
            if (PeekKeyword("else"))
            {
                Consume();
                ConsumeStatementTerminator();
                var b = ParseBlockUntil("end");
                block.Branches.Add((null, b));
            }
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return block;
        }
        private SwitchBlock ParseSwitchBlock()
        {
            var hdr = Consume(); // "switch"
            var block = new SwitchBlock { Line = hdr.Line, Column = hdr.Column };
            block.Discriminant = ParseExpression();
            ConsumeStatementTerminator();
            while (true)
            {
                SkipNewlines();
                if (PeekKeyword("case"))
                {
                    Consume();
                    var values = new List<MatlabNode>();
                    // case puede ser un valor o `case {v1, v2, ...}` (lista)
                    if (Peek().Kind == MatlabTokenKind.LBrace)
                    {
                        Consume();
                        while (Peek().Kind != MatlabTokenKind.RBrace)
                        {
                            values.Add(ParseExpression());
                            if (Peek().Kind == MatlabTokenKind.Comma) Consume();
                        }
                        Consume();
                    }
                    else values.Add(ParseExpression());
                    ConsumeStatementTerminator();
                    var body = ParseBlockUntil("case", "otherwise", "end");
                    block.Cases.Add((values, body));
                }
                else if (PeekKeyword("otherwise"))
                {
                    Consume();
                    ConsumeStatementTerminator();
                    var body = ParseBlockUntil("end");
                    block.Cases.Add((null, body));
                }
                else break;
            }
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return block;
        }
        private TryCatch ParseTryCatch()
        {
            var hdr = Consume(); // "try"
            var tc = new TryCatch { Line = hdr.Line, Column = hdr.Column };
            ConsumeStatementTerminator();
            tc.TryBody = ParseBlockUntil("catch", "end");
            if (PeekKeyword("catch"))
            {
                Consume();
                if (Peek().Kind == MatlabTokenKind.Identifier && Peek().Text != "end")
                    tc.CatchVarName = Consume().Text;
                ConsumeStatementTerminator();
                tc.CatchBody = ParseBlockUntil("end");
            }
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return tc;
        }

        private FunctionDef ParseFunctionDef()
        {
            var hdr = Consume(); // "function"
            var def = new FunctionDef { Line = hdr.Line, Column = hdr.Column };
            // Three signatures:
            //   function NAME(args)
            //   function OUT = NAME(args)
            //   function [O1, O2] = NAME(args)
            if (Peek().Kind == MatlabTokenKind.LBracket)
            {
                Consume();
                while (Peek().Kind != MatlabTokenKind.RBracket)
                {
                    if (Peek().Kind != MatlabTokenKind.Identifier)
                        throw new MatlabParseException("Expected output name", Peek().Line, Peek().Column);
                    def.OutputNames.Add(Consume().Text);
                    if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                }
                Consume(); // ]
                if (Peek().Kind != MatlabTokenKind.Assign)
                    throw new MatlabParseException("Expected '=' after output list", Peek().Line, Peek().Column);
                Consume();
            }
            else if (Peek().Kind == MatlabTokenKind.Identifier &&
                     _tokens[_pos + 1].Kind == MatlabTokenKind.Assign)
            {
                def.OutputNames.Add(Consume().Text);
                Consume(); // =
            }
            if (Peek().Kind != MatlabTokenKind.Identifier)
                throw new MatlabParseException("Expected function name", Peek().Line, Peek().Column);
            def.Name = Consume().Text;
            if (Peek().Kind == MatlabTokenKind.LParen)
            {
                Consume();
                while (Peek().Kind != MatlabTokenKind.RParen)
                {
                    if (Peek().Kind == MatlabTokenKind.Identifier)
                        def.ParamNames.Add(Consume().Text);
                    else if (Peek().Kind == MatlabTokenKind.Not)   // '~' = parametro ignorado (MATLAB)
                        { Consume(); def.ParamNames.Add("~" + def.ParamNames.Count); }
                    else
                        throw new MatlabParseException("Expected parameter name", Peek().Line, Peek().Column);
                    if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                }
                Consume(); // )
            }
            ConsumeStatementTerminator();

            // MATLAB acepta DOS estilos de archivo de funciones, y hay que soportar
            // los dos porque conviven:
            //   moderno : cada `function` se cierra con `end` (permite anidadas)
            //   clásico : el `function` NO se cierra; el cuerpo llega hasta el
            //             siguiente `function` o el fin del archivo (así está escrito
            //             casi todo CEINCI-LAB)
            // Se intenta primero el moderno, que es el único que permite funciones
            // anidadas. Si no aparece el `end`, se rebobina y se reparsea al estilo
            // clásico. Probar en este orden es lo que evita romper las anidadas.
            // El estilo se decide UNA sola vez por archivo y se recuerda: reintentar en
            // cada función haría el parseo cuadrático (y con librerías grandes, eterno).
            if (!_classicFnStyle)
            {
                var save = _pos;
                try
                {
                    def.Body = ParseBlockUntil("end");
                    ExpectKeyword("end");
                    ConsumeStatementTerminator();
                    return def;
                }
                catch (MatlabParseException)
                {
                    _pos = save;                   // rebobinar: el archivo es estilo clásico
                    _classicFnStyle = true;
                }
            }
            def.Body = ParseBlockUntil("end", "function");
            if (!IsEof() && Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == "end")
            {
                Consume();
                ConsumeStatementTerminator();
            }
            return def;
        }

        /// <summary>
        /// Parsea <c>classdef Name [&lt; Parent] ... [properties ... end] ... [methods ... end] ... end</c>.
        /// MVP: soporta múltiples bloques properties/methods, herencia simple opcional.
        /// </summary>
        private ClassDef ParseClassDef()
        {
            var hdr = Consume(); // "classdef"
            var def = new ClassDef { Line = hdr.Line, Column = hdr.Column };
            // (atributos entre paréntesis — ignorados por MVP: classdef (Sealed) Name ...)
            if (Peek().Kind == MatlabTokenKind.LParen)
            {
                int depth = 0;
                while (!IsEof())
                {
                    var t = Consume();
                    if (t.Kind == MatlabTokenKind.LParen) depth++;
                    else if (t.Kind == MatlabTokenKind.RParen) { depth--; if (depth == 0) break; }
                }
            }
            if (Peek().Kind != MatlabTokenKind.Identifier)
                throw new MatlabParseException("Expected class name after 'classdef'", Peek().Line, Peek().Column);
            def.Name = Consume().Text;
            // Herencia opcional: < ParentName
            if (Peek().Kind == MatlabTokenKind.Less)
            {
                Consume();
                if (Peek().Kind != MatlabTokenKind.Identifier)
                    throw new MatlabParseException("Expected parent class name after '<'", Peek().Line, Peek().Column);
                def.ParentName = Consume().Text;
            }
            ConsumeStatementTerminator();
            // Body: secuencia de bloques 'properties ... end' o 'methods ... end'.
            while (!IsEof())
            {
                SkipNewlines();
                if (IsEof()) break;
                if (Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == "end") break;
                if (Peek().Kind != MatlabTokenKind.Identifier)
                    throw new MatlabParseException($"Unexpected token in classdef body: {Peek().Kind}", Peek().Line, Peek().Column);
                var section = Peek().Text;
                if (section == "properties") { Consume(); ParsePropertiesBlock(def); }
                else if (section == "methods") { Consume(); ParseMethodsBlock(def); }
                else if (section == "events" || section == "enumeration")
                {
                    // Skip estos bloques por ahora — consumir hasta el 'end' correspondiente
                    Consume();
                    SkipBlockToEnd();
                }
                else
                    throw new MatlabParseException($"Unknown classdef section: '{section}'", Peek().Line, Peek().Column);
            }
            ExpectKeyword("end");
            ConsumeStatementTerminator();
            return def;
        }
        private void ParsePropertiesBlock(ClassDef def)
        {
            // (atributos opcionales entre paréntesis)
            if (Peek().Kind == MatlabTokenKind.LParen)
            {
                int depth = 0;
                while (!IsEof())
                {
                    var t = Consume();
                    if (t.Kind == MatlabTokenKind.LParen) depth++;
                    else if (t.Kind == MatlabTokenKind.RParen) { depth--; if (depth == 0) break; }
                }
            }
            ConsumeStatementTerminator();
            while (!IsEof())
            {
                SkipNewlines();
                if (IsEof()) break;
                if (Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == "end") { Consume(); ConsumeStatementTerminator(); return; }
                if (Peek().Kind != MatlabTokenKind.Identifier)
                    throw new MatlabParseException($"Expected property name, got {Peek().Kind}", Peek().Line, Peek().Column);
                var propName = Consume().Text;
                MatlabNode defaultExpr = null;
                if (Peek().Kind == MatlabTokenKind.Assign)
                {
                    Consume();
                    defaultExpr = ParseExpression();
                }
                ConsumeStatementTerminator();
                def.Properties.Add(new PropertyDef { Name = propName, DefaultExpr = defaultExpr });
            }
        }
        private void ParseMethodsBlock(ClassDef def)
        {
            // (atributos opcionales entre paréntesis) — detectar Static
            bool isStatic = false;
            if (Peek().Kind == MatlabTokenKind.LParen)
            {
                int depth = 0;
                while (!IsEof())
                {
                    var t = Consume();
                    if (t.Kind == MatlabTokenKind.LParen) depth++;
                    else if (t.Kind == MatlabTokenKind.RParen) { depth--; if (depth == 0) break; }
                    else if (t.Kind == MatlabTokenKind.Identifier && t.Text == "Static") isStatic = true;
                }
            }
            ConsumeStatementTerminator();
            while (!IsEof())
            {
                SkipNewlines();
                if (IsEof()) break;
                if (Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == "end") { Consume(); ConsumeStatementTerminator(); return; }
                if (Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == "function")
                {
                    var m = ParseFunctionDef();
                    if (isStatic) def.StaticMethods.Add(m);
                    else def.Methods.Add(m);
                    continue;
                }
                throw new MatlabParseException($"Unexpected token in methods block: '{Peek().Text}'", Peek().Line, Peek().Column);
            }
        }
        private void SkipBlockToEnd()
        {
            int depth = 1;
            while (!IsEof() && depth > 0)
            {
                if (Peek().Kind == MatlabTokenKind.Identifier)
                {
                    var t = Peek().Text;
                    if (t == "for" || t == "while" || t == "if" || t == "switch" || t == "try"
                        || t == "function" || t == "properties" || t == "methods" || t == "events"
                        || t == "enumeration" || t == "classdef") { Consume(); depth++; continue; }
                    if (t == "end") { Consume(); depth--; continue; }
                }
                Consume();
            }
        }

        /// <summary>
        /// Parsea statements hasta encontrar uno de los <paramref name="terminators"/>
        /// como keyword bare (identifier match), o EOF. NO consume el terminador.
        /// </summary>
        private List<MatlabNode> ParseBlockUntil(params string[] terminators)
        {
            var stmts = new List<MatlabNode>();
            while (!IsEof())
            {
                SkipNewlines();
                if (IsEof()) break;
                if (Peek().Kind == MatlabTokenKind.Identifier &&
                    Array.IndexOf(terminators, Peek().Text) >= 0)
                    break;
                stmts.Add(ParseStatement());
            }
            return stmts;
        }
        private AnonFunction ParseAnonFunction()
        {
            var at = Consume(); // @
            var fn = new AnonFunction { Line = at.Line, Column = at.Column };
            if (Peek().Kind == MatlabTokenKind.LParen)
            {
                Consume();
                while (Peek().Kind != MatlabTokenKind.RParen)
                {
                    if (Peek().Kind == MatlabTokenKind.Identifier)
                        fn.ParamNames.Add(Consume().Text);
                    else if (Peek().Kind == MatlabTokenKind.Not)   // '~' = parametro ignorado (MATLAB)
                        { Consume(); fn.ParamNames.Add("~" + fn.ParamNames.Count); }
                    else
                        throw new MatlabParseException("Expected parameter name in @(…)", Peek().Line, Peek().Column);
                    if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                }
                Consume(); // )
                fn.Body = ParseExpression();
            }
            else if (Peek().Kind == MatlabTokenKind.Identifier)
            {
                // @name → function handle a un function name. Lo modelamos como
                // anonymous wrapper: @(arg1, arg2, ...) → name(arg1, arg2, ...).
                // Sin embargo no conocemos arity → guardamos el nombre y resolvemos en runtime.
                var nameTok = Consume();
                fn.ParamNames.Add("__handle__");
                fn.Body = new IdentRef { Name = nameTok.Text, Line = nameTok.Line, Column = nameTok.Column };
            }
            else
                throw new MatlabParseException("Expected '(' or name after '@'", Peek().Line, Peek().Column);
            return fn;
        }

        private MatlabNode ParseScopeDecl(bool isGlobal)
        {
            var hdr = Consume(); // "global" o "persistent"
            var names = new List<string>();
            while (Peek().Kind == MatlabTokenKind.Identifier)
            {
                names.Add(Consume().Text);
                if (Peek().Kind == MatlabTokenKind.Comma) Consume();
            }
            ConsumeStatementTerminator();
            if (isGlobal)
                return new GlobalDecl { Names = names, Line = hdr.Line, Column = hdr.Column };
            return new PersistentDecl { Names = names, Line = hdr.Line, Column = hdr.Column };
        }

        private bool PeekKeyword(string kw)
            => Peek().Kind == MatlabTokenKind.Identifier && Peek().Text == kw;

        /// <summary>
        /// True si el siguiente token después de un IDENT command-form es OTRO IDENT
        /// (no `=`, `(`, operador, etc). Ej: `syms x` (sí) vs `syms = 5` (no).
        /// </summary>
        private bool IsCommandFormContext()
        {
            // Look-ahead al siguiente token después del identifier actual
            if (_pos + 1 >= _tokens.Count) return false;
            var next = _tokens[_pos + 1].Kind;
            // Forma comando: `hold on` (identificador) o `cd 'C:\ruta'` / `load 'f.mat'` (string).
            return next == MatlabTokenKind.Identifier
                || next == MatlabTokenKind.String
                || next == MatlabTokenKind.StringDouble;
        }
        private void ExpectKeyword(string kw)
        {
            if (!PeekKeyword(kw))
                throw new MatlabParseException($"Expected '{kw}'", Peek().Line, Peek().Column);
            Consume();
        }

        /// <summary>
        /// Intenta parsear el LHS de una asignación. Devuelve la lista de targets
        /// si encuentra un patrón `target = ...` (o `[t1, t2] = ...`), o null si no.
        /// </summary>
        private List<MatlabNode> TryParseAssignmentLhs(bool bareTargetOk = false)
        {
            // Caso 1: [t1, t2, ...] = ...   (multi-output)
            if (Peek().Kind == MatlabTokenKind.LBracket)
            {
                int saved = _pos;
                Consume(); // [
                var targets = new List<MatlabNode>();
                while (true)
                {
                    // `~` = salida ignorada de MATLAB:  [~, ~, x] = f(...)
                    if (Peek().Kind == MatlabTokenKind.Not)
                    {
                        Consume();
                        targets.Add(new IdentRef { Name = "~" });
                    }
                    else if (Peek().Kind == MatlabTokenKind.Identifier)
                    {
                        MatlabNode tnode = new IdentRef { Name = Consume().Text };
                        // Targets indexados/campos en multi-output (MATLAB 2017a):
                        //   [~, CG(:,1)]=f()   [Bc{e}, dJ(e)]=f()   [s.a, b]=f()
                        bool chainOk = true;
                        while (chainOk)
                        {
                            var pk = Peek().Kind;
                            if (pk == MatlabTokenKind.LParen || pk == MatlabTokenKind.LBrace)
                            {
                                bool brace = pk == MatlabTokenKind.LBrace;
                                var close = brace ? MatlabTokenKind.RBrace : MatlabTokenKind.RParen;
                                Consume(); // ( o {
                                var iargs = new List<MatlabNode>();
                                bool iok = true;
                                if (Peek().Kind != close)
                                {
                                    while (true)
                                    {
                                        try { iargs.Add(ParseExpressionOrColon()); }
                                        catch { iok = false; break; }
                                        if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                                        break;
                                    }
                                }
                                if (!iok || Peek().Kind != close) { _pos = saved; return null; }
                                Consume(); // ) o }
                                tnode = brace ? (MatlabNode)new CellIndex { Target = tnode, Args = iargs }
                                              : new CallOrIndex { Target = tnode, Args = iargs };
                            }
                            else if (pk == MatlabTokenKind.Dot &&
                                     _pos + 1 < _tokens.Count &&
                                     _tokens[_pos + 1].Kind == MatlabTokenKind.Identifier)
                            {
                                Consume(); // .
                                tnode = new FieldAccess { Target = tnode, FieldName = Consume().Text };
                            }
                            else chainOk = false;
                        }
                        targets.Add(tnode);
                    }
                    else { _pos = saved; return null; }
                    if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                    if (Peek().Kind == MatlabTokenKind.RBracket) { Consume(); break; }
                    _pos = saved; return null;
                }
                if (Peek().Kind != MatlabTokenKind.Assign) { _pos = saved; return null; }
                return targets;
            }
            // Caso 2: ident = ... | ident(args) = ... | ident.field[.field...] = ...
            if (Peek().Kind == MatlabTokenKind.Identifier)
            {
                int saved = _pos;
                var name = Consume().Text;
                MatlabNode target = new IdentRef { Name = name };
                // Chain de field-access: s.a.b.c
                while (Peek().Kind == MatlabTokenKind.Dot &&
                       _pos + 1 < _tokens.Count &&
                       _tokens[_pos + 1].Kind == MatlabTokenKind.Identifier)
                {
                    Consume(); // .
                    var fld = Consume();
                    target = new FieldAccess { Target = target, FieldName = fld.Text };
                }
                // optional indexing: name(args) — usa ParseExpressionOrColon para
                // soportar A(:, 2) = ..., A(end, :) = ..., etc.
                if (Peek().Kind == MatlabTokenKind.LParen)
                {
                    Consume(); // (
                    var args = new List<MatlabNode>();
                    bool ok = true;
                    if (Peek().Kind != MatlabTokenKind.RParen)
                    {
                        while (true)
                        {
                            try { args.Add(ParseExpressionOrColon()); }
                            catch { ok = false; break; }
                            if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                            break;
                        }
                    }
                    if (!ok || Peek().Kind != MatlabTokenKind.RParen) { _pos = saved; return null; }
                    Consume(); // )
                    target = new CallOrIndex { Target = target, Args = args };
                }
                // optional curly indexing: name{args} — para cells, soporta end+N autoextend
                else if (Peek().Kind == MatlabTokenKind.LBrace)
                {
                    Consume(); // {
                    var args = new List<MatlabNode>();
                    bool ok = true;
                    if (Peek().Kind != MatlabTokenKind.RBrace)
                    {
                        while (true)
                        {
                            try { args.Add(ParseExpressionOrColon()); }
                            catch { ok = false; break; }
                            if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                            break;
                        }
                    }
                    if (!ok || Peek().Kind != MatlabTokenKind.RBrace) { _pos = saved; return null; }
                    Consume(); // }
                    target = new CellIndex { Target = target, Args = args };
                }
                // Field-access ENCADENADO tras el indexado: res(1).n = ..., a{2}.x = ...
                // (asignación a campo de un elemento de struct-array / cell de structs).
                while (Peek().Kind == MatlabTokenKind.Dot &&
                       _pos + 1 < _tokens.Count &&
                       _tokens[_pos + 1].Kind == MatlabTokenKind.Identifier)
                {
                    Consume(); // .
                    var fld = Consume();
                    target = new FieldAccess { Target = target, FieldName = fld.Text };
                }
                // En modo Octave, un target simple también es válido si va seguido de una
                // asignación compuesta (+= …) o de ++/-- (postfijo). Lo resuelve ParseStatement.
                // bareTargetOk: el llamador (p.ej. prefijo `++x`) ya consumió el operador,
                // así que un target "pelado" seguido de terminador es válido.
                if (!bareTargetOk &&
                    Peek().Kind != MatlabTokenKind.Assign &&
                    !(_octave && IsCompoundOrIncrement(Peek().Kind)))
                { _pos = saved; return null; }
                return new List<MatlabNode> { target };
            }
            return null;
        }

        private static bool IsCompoundOrIncrement(MatlabTokenKind k)
            => k == MatlabTokenKind.PlusAssign  || k == MatlabTokenKind.MinusAssign
            || k == MatlabTokenKind.StarAssign  || k == MatlabTokenKind.SlashAssign
            || k == MatlabTokenKind.Increment   || k == MatlabTokenKind.Decrement;

        private bool ConsumeStatementTerminator()
        {
            bool suppressed = false;
            // ';' fuerza suppression; ',' o '\n' o EOF terminan el statement.
            // Comentarios trailing (a partir de aquí ya estoy en la cola del statement)
            // se descartan silenciosamente — el usuario ya ve la fórmula y un comentario
            // de fin de línea no debería emitir una línea separada.
            while (true)
            {
                var k = Peek().Kind;
                if (k == MatlabTokenKind.Semicolon) { suppressed = true; Consume(); continue; }
                if (k == MatlabTokenKind.Comma || k == MatlabTokenKind.Newline) { Consume(); continue; }
                // NO consumir Comment tokens aqui — cada comment debe ser su propio
                // CommentStmt para que aparezca como texto narrativo en el output HTML.
                break;
            }
            return suppressed;
        }

        // ─── Expression-level — Pratt parser por precedencia ────────────────
        // Precedencias (de menor a mayor, similar a MATLAB):
        //   1  || && |  &           (lógicos)
        //   2  < <= > >= == ~=     (comparación)
        //   3  :                    (range — bajo en MATLAB)
        //   4  + -                  (binarios)
        //   5  * / \ .* ./ .\       (multiplicativos)
        //   6  ^ .^                 (potencia, asociatividad derecha)
        //   7  unary - + ~          (prefix)
        //   8  '  .'                (postfix transpose)
        //   9  ( indexing/call ) y . field-access (postfix-like)
        public MatlabNode ParseExpression() => ParseLogicalOr();

        private MatlabNode ParseLogicalOr()
        {
            var left = ParseLogicalAnd();
            while (Peek().Kind == MatlabTokenKind.OrShort || Peek().Kind == MatlabTokenKind.OrBit)
            {
                var op = Consume();
                var right = ParseLogicalAnd();
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private MatlabNode ParseLogicalAnd()
        {
            var left = ParseComparison();
            while (Peek().Kind == MatlabTokenKind.AndShort || Peek().Kind == MatlabTokenKind.AndBit)
            {
                var op = Consume();
                var right = ParseComparison();
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private MatlabNode ParseComparison()
        {
            var left = ParseRange();
            while (IsComparisonOp(Peek().Kind))
            {
                var op = Consume();
                var right = ParseRange();
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private static bool IsComparisonOp(MatlabTokenKind k) =>
            k == MatlabTokenKind.Equal || k == MatlabTokenKind.NotEqual ||
            k == MatlabTokenKind.Less || k == MatlabTokenKind.LessEq ||
            k == MatlabTokenKind.Greater || k == MatlabTokenKind.GreaterEq;

        private MatlabNode ParseRange()
        {
            var start = ParseAdditive();
            if (Peek().Kind != MatlabTokenKind.Colon) return start;
            Consume(); // :
            var middle = ParseAdditive();
            if (Peek().Kind == MatlabTokenKind.Colon)
            {
                Consume();
                var end = ParseAdditive();
                return new Range { Start = start, Step = middle, End = end,
                                   Line = start.Line, Column = start.Column };
            }
            return new Range { Start = start, Step = null, End = middle,
                               Line = start.Line, Column = start.Column };
        }
        private MatlabNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Peek().Kind == MatlabTokenKind.Plus || Peek().Kind == MatlabTokenKind.Minus)
            {
                var op = Consume();
                var right = ParseMultiplicative();
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private MatlabNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsMulOp(Peek().Kind))
            {
                var op = Consume();
                var right = ParseUnary();
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private static bool IsMulOp(MatlabTokenKind k) =>
            k == MatlabTokenKind.Star || k == MatlabTokenKind.Slash || k == MatlabTokenKind.Backslash ||
            k == MatlabTokenKind.DotStar || k == MatlabTokenKind.DotSlash || k == MatlabTokenKind.DotBackslash;

        // MATLAB precedencia: `^` (lv 2) > unary `-` (lv 3) > `*` (lv 4).
        // ParseUnary wraps ParsePower → `-x^2 = -(x^2)`.
        private MatlabNode ParseUnary()
        {
            if (Peek().Kind == MatlabTokenKind.Minus || Peek().Kind == MatlabTokenKind.Plus ||
                Peek().Kind == MatlabTokenKind.Not)
            {
                var op = Consume();
                var operand = ParsePower();
                return new UnaryOp { Op = op.Text, Operand = operand, IsPrefix = true,
                                     Line = op.Line, Column = op.Column };
            }
            return ParsePower();
        }
        private MatlabNode ParsePower()
        {
            var left = ParsePostfix();
            // Asociatividad DERECHA: a^b^c = a^(b^c)
            if (Peek().Kind == MatlabTokenKind.Caret || Peek().Kind == MatlabTokenKind.DotCaret)
            {
                var op = Consume();
                var right = ParsePower();  // recursión derecha (no unary aquí — ya hace MATLAB right side)
                left = new BinaryOp { Op = op.Text, Left = left, Right = right,
                                      Line = op.Line, Column = op.Column };
            }
            return left;
        }
        private MatlabNode ParsePostfix()
        {
            var node = ParsePrimary();
            while (true)
            {
                var k = Peek().Kind;
                if (k == MatlabTokenKind.Transpose)
                {
                    var op = Consume();
                    node = new UnaryOp { Op = "'", Operand = node, IsPrefix = false,
                                          Line = op.Line, Column = op.Column };
                    continue;
                }
                if (k == MatlabTokenKind.DotTranspose)
                {
                    var op = Consume();
                    node = new UnaryOp { Op = ".'", Operand = node, IsPrefix = false,
                                          Line = op.Line, Column = op.Column };
                    continue;
                }
                if (k == MatlabTokenKind.LParen)
                {
                    var op = Consume();
                    var args = new List<MatlabNode>();
                    if (Peek().Kind != MatlabTokenKind.RParen)
                    {
                        while (true)
                        {
                            args.Add(ParseExpressionOrColon());
                            if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                            break;
                        }
                    }
                    Expect(MatlabTokenKind.RParen, ")");
                    node = new CallOrIndex { Target = node, Args = args,
                                              Line = op.Line, Column = op.Column };
                    continue;
                }
                // Field access: <node>.<ident> — sólo si el siguiente es Identifier
                // (NO confundir con `.` de un número o `.*`/`.^`/`./`/`.\`/`.'`).
                if (k == MatlabTokenKind.Dot && _pos + 1 < _tokens.Count &&
                    _tokens[_pos + 1].Kind == MatlabTokenKind.Identifier)
                {
                    var op = Consume(); // .
                    var fld = Consume();
                    node = new FieldAccess { Target = node, FieldName = fld.Text,
                                              Line = op.Line, Column = op.Column };
                    continue;
                }
                // Cell-array indexing: <node>{i, j} — recupera el contenido (no wrap)
                if (k == MatlabTokenKind.LBrace)
                {
                    var op = Consume();
                    var args = new List<MatlabNode>();
                    if (Peek().Kind != MatlabTokenKind.RBrace)
                    {
                        while (true)
                        {
                            args.Add(ParseExpressionOrColon());
                            if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                            break;
                        }
                    }
                    Expect(MatlabTokenKind.RBrace, "}");
                    node = new CellIndex { Target = node, Args = args,
                                            Line = op.Line, Column = op.Column };
                    continue;
                }
                break;
            }
            return node;
        }
        /// <summary>Para indexing: <c>A(:, 3)</c>. Un `:` aislado es ColonAll.</summary>
        private MatlabNode ParseExpressionOrColon()
        {
            if (Peek().Kind == MatlabTokenKind.Colon)
            {
                // Check si es `:` solo (no parte de un rango)
                var look = _tokens[_pos + 1].Kind;
                if (look == MatlabTokenKind.Comma || look == MatlabTokenKind.RParen || look == MatlabTokenKind.RBracket)
                {
                    var t = Consume();
                    return new ColonAll { Line = t.Line, Column = t.Column };
                }
            }
            // Permitir `end` como pseudo-identifier dentro de indexing.
            // El evaluator lo resolverá contextualmente al tamaño de la dim actual.
            return ParseExpression();
        }
        private MatlabNode ParsePrimary()
        {
            var t = Peek();
            switch (t.Kind)
            {
                case MatlabTokenKind.Number:
                    Consume();
                    return new NumberLit { Value = t.NumberValue, OrigText = t.Text,
                                            Line = t.Line, Column = t.Column };
                case MatlabTokenKind.ImaginaryNumber:
                    Consume();
                    return new ImaginaryLit { Value = t.NumberValue, OrigText = t.Text,
                                              Line = t.Line, Column = t.Column };
                case MatlabTokenKind.String:
                    Consume();
                    return new StringLit { Value = t.Text, Quote = '\'',
                                            Line = t.Line, Column = t.Column };
                case MatlabTokenKind.StringDouble:
                    Consume();
                    return new StringLit { Value = t.Text, Quote = '"',
                                            Line = t.Line, Column = t.Column };
                case MatlabTokenKind.At:
                    return ParseAnonFunction();
                case MatlabTokenKind.Identifier:
                    Consume();
                    return new IdentRef { Name = t.Text, Line = t.Line, Column = t.Column };
                case MatlabTokenKind.LParen:
                    Consume();
                    var inner = ParseExpression();
                    Expect(MatlabTokenKind.RParen, ")");
                    return inner;
                case MatlabTokenKind.LBracket:
                    return ParseMatrixLit();
                case MatlabTokenKind.LBrace:
                    return ParseCellLit();
                case MatlabTokenKind.Minus:
                case MatlabTokenKind.Plus:
                case MatlabTokenKind.Not:
                    return ParseUnary();
                default:
                    throw new MatlabParseException($"Unexpected token '{t.Text}'", t.Line, t.Column);
            }
        }
        private MatrixLit ParseMatrixLit()
        {
            var t = Consume(); // [
            var lit = new MatrixLit { Line = t.Line, Column = t.Column };
            var currentRow = new List<MatlabNode>();
            while (Peek().Kind != MatlabTokenKind.RBracket && Peek().Kind != MatlabTokenKind.EndOfFile)
            {
                if (Peek().Kind == MatlabTokenKind.Semicolon || Peek().Kind == MatlabTokenKind.Newline)
                {
                    Consume();
                    if (currentRow.Count > 0)
                    {
                        lit.Rows.Add(currentRow);
                        currentRow = new List<MatlabNode>();
                    }
                    continue;
                }
                if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                // Comentario DENTRO de una matriz multilínea (MATLAB lo permite:
                // `[1 2;% Eje A` \n `3 4]`). Se ignora; el Newline que sigue separa fila.
                if (Peek().Kind == MatlabTokenKind.Comment || Peek().Kind == MatlabTokenKind.SectionHeading)
                { Consume(); continue; }
                currentRow.Add(ParseExpression());
            }
            if (currentRow.Count > 0) lit.Rows.Add(currentRow);
            Expect(MatlabTokenKind.RBracket, "]");
            return lit;
        }
        private CellLit ParseCellLit()
        {
            var t = Consume(); // {
            var lit = new CellLit { Line = t.Line, Column = t.Column };
            var currentRow = new List<MatlabNode>();
            while (Peek().Kind != MatlabTokenKind.RBrace && Peek().Kind != MatlabTokenKind.EndOfFile)
            {
                if (Peek().Kind == MatlabTokenKind.Semicolon || Peek().Kind == MatlabTokenKind.Newline)
                {
                    Consume();
                    if (currentRow.Count > 0)
                    {
                        lit.Rows.Add(currentRow);
                        currentRow = new List<MatlabNode>();
                    }
                    continue;
                }
                if (Peek().Kind == MatlabTokenKind.Comma) { Consume(); continue; }
                if (Peek().Kind == MatlabTokenKind.Comment || Peek().Kind == MatlabTokenKind.SectionHeading)
                { Consume(); continue; }   // comentario dentro de un cell multilínea
                currentRow.Add(ParseExpression());
            }
            if (currentRow.Count > 0) lit.Rows.Add(currentRow);
            Expect(MatlabTokenKind.RBrace, "}");
            return lit;
        }

        // ─── Token helpers ──────────────────────────────────────────────────
        private MatlabToken Peek() => _tokens[_pos];
        private MatlabToken Consume() => _tokens[_pos++];
        private bool IsEof() => Peek().Kind == MatlabTokenKind.EndOfFile;
        private void SkipNewlines()
        {
            while (Peek().Kind == MatlabTokenKind.Newline) Consume();
        }
        private void Expect(MatlabTokenKind kind, string label)
        {
            var t = Peek();
            if (t.Kind != kind)
                throw new MatlabParseException($"Expected '{label}', got '{t.Text}'", t.Line, t.Column);
            Consume();
        }
    }
}
