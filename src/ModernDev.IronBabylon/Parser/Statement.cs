using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        private static Node LoopLabel => new Node {Kind = "loop"};
        private static Node Switchlabel => new Node {Kind = "switch"};

        /// <summary>
        /// Parse a program. Initializes the parser, reads any number of
        /// statements, and wraps them in a Program node.  Optionally takes a
        /// `program` argument.  If present, the statements will be appended
        /// to its body instead of creating a new node.
        /// </summary>
        private Node ParseTopLevel(Node file, Node program)
        {
            program.SourceType = Options.SourceType;

            ParseBlockBody(program, true, true, TT["eof"]);

            file.Program = FinishNode(program, "Program");
            file.Comments = State.Comments;
            file.Tokens = State.Tokens;

            return FinishNode(file, "File");
        }

        private Node StmtToDirective(Node stmt)
        {
            var expr = stmt.Expression as Node;
            Debug.Assert(expr != null, "expr not null");
            var directiveLiteral = StartNodeAt(expr.Start, expr.Loc.Start);
            var directive = StartNodeAt(stmt.Start, stmt.Loc.Start);
            var raw = Input.Slice(expr.Start, expr.End);
            var val = directive.Value = raw.Slice(1, -1);

            AddExtra(directiveLiteral, "raw", raw);
            AddExtra(directiveLiteral, "rawValue", val);

            directive.Value = FinishNodeAt(directiveLiteral, "DirectiveLiteral", expr.End, expr.Loc.End);

            return FinishNodeAt(directive, "Directive", stmt.End, stmt.Loc.End);
        }

        /// <summary>
        ///  Parse a single statement.
        ///
        /// If expecting a statement and finding a slash operator, parse a
        /// regular expression literal. This is to handle cases like
        /// `if (foo) /blah/.exec(foo)`, where looking at the previous token
        /// does not help.
        /// </summary>
        private Node ParseStatement(bool declaration, bool topLevel = false)
        {
            if (Match(TT["at"]))
            {
                ParseDecorators(true);
            }

            var startType = State.Type;
            var node = StartNode();

            if (startType == TT["_break"] || startType == TT["_continue"])
            {
                return ParseBreakContinueStatement(node, startType.Keyword);
            }
            if (startType == TT["_debugger"])
            {
                return ParseDebuggerStatement(node);
            }
            if (startType == TT["_do"])
            {
                return ParseDoStatement(node);
            }
            if (startType == TT["_for"])
            {
                return ParseForStatement(node);
            }
            if (startType == TT["_function"])
            {
                if (!declaration)
                {
                    Unexpected();
                }

                return ParseFunctionStatement(node);
            }
            if (startType == TT["_class"])
            {
                if (!declaration)
                {
                    Unexpected();
                }

                TakeDecorators(node);

                return ParseClass(node, true);
            }
            if (startType == TT["_if"])
            {
                return ParseIfStatement(node);
            }
            if (startType == TT["_return"])
            {
                return ParseReturnStatement(node);
            }
            if (startType == TT["_switch"])
            {
                return ParseSwitchStatement(node);
            }
            if (startType == TT["_throw"])
            {
                return ParseThrowStatement(node);
            }
            if (startType == TT["_try"])
            {
                return ParseTryStatement(node);
            }
            if (startType == TT["_let"] || startType == TT["_const"] || startType == TT["_var"])
            {
                if ((startType == TT["_let"] || startType == TT["_const"]) && !declaration)
                {
                    Unexpected();
                }

                return ParseVarStatement(node, startType);
            }
            if (startType == TT["_while"])
            {
                return ParseWhileStatement(node);
            }
            if (startType == TT["_with"])
            {
                return ParseWithStatement(node);
            }
            if (startType == TT["braceL"])
            {
                return ParseBlock();
            }
            if (startType == TT["semi"])
            {
                return ParseEmptyStatement(node);
            }
            if (startType == TT["_export"] || startType == TT["_import"])
            {
                if (!Options.AllowImportExportEverywhere)
                {
                    if (!topLevel)
                    {
                        Raise(State.Start, "'import' and 'export' may only appear at the top level");
                    }

                    if (!InModule)
                    {
                        Raise(State.Start, "'import' and 'export' may appear only with 'sourceType: module'");
                    }
                }

                return startType == TT["_import"] ? ParseImport(node) : ParseExport(node);
            }
            if (startType == TT["name"])
            {
                if ((string) State.Value == "async")
                {
                    var state = State.Clone();

                    Next();

                    if (Match(TT["_function"]) && !CanInsertSemicolon)
                    {
                        Expect(TT["_function"]);

                        return ParseFunction(node, true, false, true);
                    }

                    State = state;
                }
            }

            var maybeName = State.Value;
            var expr = ParseExpression(false, ref _nullRef);

            if (startType == TT["name"] && expr.Type == "Identifier" && Eat(TT["colon"]))
            {
                return ParseLabelStatement(node, maybeName, expr);
            }

            return ParseExpressionStatement(node, expr);
        }

        private void TakeDecorators(Node node)
        {
            if (State.Decorators.Any())
            {
                node.Decorators = State.Decorators.ToList();

                State.Decorators = new List<Node>();
            }
        }

        private void ParseDecorators(bool allowExport = false)
        {
            while (Match(TT["at"]))
            {
                State.Decorators.Add(ParseDecorator());
            }

            if (allowExport && Match(TT["_export"]))
            {
                return;
            }

            if (!Match(TT["_class"]))
            {
                Raise(State.Start, "Leading decorators must be attached to a class declaration");
            }
        }

        private Node ParseDecorator()
        {
            var node = StartNode();

            Next();

            node.Expression = ParseMaybeAssign(false, ref _nullRef);

            return FinishNode(node, "Decorator");
        }

        private Node ParseBreakContinueStatement(Node node, string keyword)
        {
            var isBreak = keyword == "break";

            Next();

            if (IsLineTerminator)
            {
                node.Label = null;
            }
            else if (!Match(TT["name"]))
            {
                Unexpected();
            }
            else
            {
                node.Label = ParseIdentifier();

                Semicolon();
            }

            int i;

            for (i = 0; i < State.Labels.Count; ++i)
            {
                var lab = State.Labels[i];

                if (node.Label == null || lab.Name as string == node.Label.Name as string)
                {
                    if (!string.IsNullOrEmpty(lab.Kind) && (isBreak || lab.Kind == "loop"))
                    {
                        break;
                    }

                    if (node.Label && isBreak)
                    {
                        break;
                    }
                }
            }

            if (i == State.Labels.Count)
            {
                Raise(node.Start, "Unsyntactic " + keyword);
            }

            return FinishNode(node, isBreak ? "BreakStatement" : "ContinueStatement");
        }

        private Node ParseDebuggerStatement(Node node)
        {
            Next();
            Semicolon();

            return FinishNode(node, "DebuggerStatement");
        }

        private Node ParseDoStatement(Node node)
        {
            Next();

            State.Labels.Add(LoopLabel);

            node.Body = ParseStatement(false);

            State.Labels.Pop();
            Expect(TT["_while"]);

            node.Test = ParseParenExpression();

            Eat(TT["semi"]);

            return FinishNode(node, "DoWhileStatement");
        }

        /// <summary>
        /// Disambiguating between a `for` and a `for`/`in` or `for`/`of`
        /// loop is non-trivial. Basically, we have to parse the init `var`
        /// statement or expression, disallowing the `in` operator (see
        /// the second parameter to `parseExpression`), and then check
        /// whether the next token is `in` or `of`. When there is no init
        /// part (semicolon immediately after the opening parenthesis), it
        /// is a regular `for` loop.
        /// </summary>
        private Node ParseForStatement(Node node)
        {
            Node init;

            Next();
            State.Labels.Add(LoopLabel);
            Expect(TT["parenL"]);

            if (Match(TT["semi"]))
            {
                return ParseFor(node, null);
            }

            if (Match(TT["_var"]) || Match(TT["_let"]) || Match(TT["_const"]))
            {
                init = StartNode();
                var varKind = State.Type;

                Next();

                ParseVar(init, true, varKind);
                FinishNode(init, "VariableDeclaration");
                
                if (Match(TT["_in"]) || IsContextual("of"))
                {
                    if (init.Declarations.Count == 1 && !init.Declarations[0].Init)
                    {
                        return ParseForIn(node, init);
                    }
                }

                return ParseFor(node, init);
            }

            int? refShorthandDefaultPos = 0;
            init = ParseExpression(true, ref refShorthandDefaultPos);

            if (Match(TT["_in"]) || IsContextual("of"))
            {
                ToAssignable(init);
                CheckLVal(init);

                return ParseForIn(node, init);
            }

            if (refShorthandDefaultPos.ToBool())
            {
                Unexpected(refShorthandDefaultPos);
            }

            return ParseFor(node, init);
        }

        private Node ParseFunctionStatement(Node node)
        {
            Next();

            return ParseFunction(node, true);
        }

        private Node ParseIfStatement(Node node)
        {
            Next();

            node.Test = ParseParenExpression();
            node.Consequent = ParseStatement(false);
            node.Altername = Eat(TT["_else"]) ? ParseStatement(false) : null;

            return FinishNode(node, "IfStatement");
        }

        private Node ParseReturnStatement(Node node)
        {
            if (!State.InFunction && !Options.AllowReturnOutsideFunction)
            {
                Raise(State.Start, "'return' outside of function");
            }

            Next();

            if (IsLineTerminator)
            {
                node.Argument = null;
            }
            else
            {
                node.Argument = ParseExpression(false, ref _nullRef);

                Semicolon();
            }

            return FinishNode(node, "ReturnStatement");
        }

        private Node ParseSwitchStatement(Node node)
        {
            Next();

            node.Discriminant = ParseParenExpression();
            node.Cases = new List<Node>();

            Expect(TT["braceL"]);
            State.Labels.Add(Switchlabel);

            Node cur = null;

            for (var sawDefault = false; !Match(TT["braceR"]);)
            {
                if (Match(TT["_case"]) || Match(TT["_default"]))
                {
                    var isCase = Match(TT["_case"]);

                    if (cur)
                    {
                        FinishNode(cur, "SwitchCase");
                    }

                    node.Cases.Add(cur = StartNode());
                    cur.Consequent = new List<Node>();

                    Next();

                    if (isCase)
                    {
                        cur.Test = ParseExpression(false, ref _nullRef);
                    }
                    else
                    {
                        if (sawDefault)
                        {
                            Raise(State.LastTokenStart, "Multiple default clauses");
                        }

                        sawDefault = true;
                        cur.Test = null;
                    }

                    Expect(TT["colon"]);
                }
                else
                {
                    if (cur)
                    {
                        ((List<Node>) cur.Consequent).Add(ParseStatement(true));
                    }
                    else
                    {
                        Unexpected();
                    }
                }
            }

            if (cur)
            {
                FinishNode(cur, "SwitchCase");
            }

            Next();
            State.Labels.Pop();

            return FinishNode(node, "SwitchStatement");
        }

        private Node ParseThrowStatement(Node node)
        {
            Next();

            if (LineBreak.IsMatch(Input.Slice(State.LastTokenEnd, State.Start)))
            {
                Raise(State.LastTokenEnd, "Illegal newline after throw");
            }

            node.Argument = ParseExpression(false, ref _nullRef);

            Semicolon();

            return FinishNode(node, "ThrowStatement");
        }

        private Node ParseTryStatement(Node node)
        {
            Next();

            node.Block = ParseBlock();
            node.Handler = null;

            if (Match(TT["_catch"]))
            {
                var clause = StartNode();

                Next();
                Expect(TT["parenL"]);

                clause.Param = ParseBindingAtom();

                CheckLVal(clause.Param, true, new Dictionary<string, bool>());
                Expect(TT["parenR"]);

                clause.Body = ParseBlock();
                node.Handler = FinishNode(clause, "CatchClause");
            }

            node.GuardedHandlers = new List<Node>();
            node.Finalizer = Eat(TT["_finally"]) ? ParseBlock() : null;

            if (node.Handler == null && node.Finalizer == null)
            {
                Raise(node.Start, "Missing catch or finally clause");
            }

            return FinishNode(node, "TryStatement");
        }

        private Node ParseVarStatement(Node node, TokenType kind)
        {
            Next();
            ParseVar(node, false, kind);
            Semicolon();

            return FinishNode(node, "VariableDeclaration");
        }

        private Node ParseWhileStatement(Node node)
        {
            Next();

            node.Test = ParseParenExpression();

            State.Labels.Add(LoopLabel);

            node.Body = ParseStatement(false);

            State.Labels.Pop();

            return FinishNode(node, "WhileStatement");
        }

        private Node ParseWithStatement(Node node)
        {
            if (State.Strict)
            {
                Raise(State.Start, "'with' in strict mode");
            }

            Next();

            node.Object = ParseParenExpression();
            node.Body = ParseStatement(false);

            return FinishNode(node, "WithStatement");
        }

        private Node ParseEmptyStatement(Node node)
        {
            Next();

            return FinishNode(node, "EmptyStatement");
        }

        private Node ParseLabelStatement(Node node, object /* TODO: string*/ maybeName, Node expr)
        {
            State.Labels.Where(label => label.Name as string == (string) maybeName)
                .ToList()
                .ForEach(label => Raise(expr.Start, $"Label '{maybeName}' is already declared"));

            var kind = State.Type.IsLoop ? "loop" : Match(TT["_switch"]) ? "switch" : null;

            for (var i = State.Labels.Count - 1; i >= 0; i--)
            {
                var label = State.Labels[i];

                if (label.StatementStart == node.Start)
                {
                    label.StatementStart = State.Start;
                    label.Kind = kind;
                }
                else
                {
                    break;
                }
            }

            State.Labels.Add(new Node
            {
                Name = (string) maybeName,
                Kind = kind,
                StatementStart = State.Start
            });

            node.Body = ParseStatement(true);

            State.Labels.Pop();

            node.Label = expr;

            return FinishNode(node, "LabeledStatement");
        }

        private Node ParseExpressionStatement(Node node, Node expr)
        {
            node.Expression = expr;

            Semicolon();

            return FinishNode(node, "ExpressionStatement");
        }

        /// <summary>
        /// Parse a semicolon-enclosed block of statements, handling `"use
        /// strict"` declarations when `allowStrict` is true (used for
        /// function bodies).
        /// </summary>
        /// <returns></returns>
        private Node ParseBlock(bool allowDirectives = false)
        {
            var node = StartNode();

            Expect(TT["braceL"]);
            ParseBlockBody(node, allowDirectives, false, TT["braceR"]);

            return FinishNode(node, "BlockStatement");
        }

        private void ParseBlockBody(Node node, bool allowDirectives, bool topLevel, TokenType end)
        {
            node.Body = new List<Node>();
            node.Directives = new List<Node>();

            var parsedNonDirective = false;
            var oldStrict = false;
            int? octalPosition = 0;

            while (!Eat(end))
            {
                if (!parsedNonDirective && State.ContainsOctal && !octalPosition.ToBool())
                {
                    octalPosition = State.OctalPosition;
                }

                var stmt = ParseStatement(true, topLevel);

                if (allowDirectives && !parsedNonDirective && stmt.Type == "ExpressionStatement" &&
                    ((Node) stmt.Expression).Type == "StringLiteral")
                {
                    var directive = StmtToDirective(stmt);

                    node.Directives.Add(directive);

                    if ((string) ((Node) directive.Value).Value == "use strict")
                    {
                        oldStrict = State.Strict;

                        SetStrict(true);

                        if (octalPosition.ToBool())
                        {
                            Raise(octalPosition ?? 0, "Octal literal in strict mode");
                        }
                    }

                    continue;
                }

                parsedNonDirective = true;
                ((List<Node>) node.Body).Add(stmt);
            }

            if (!oldStrict)
            {
                SetStrict(false);
            }
        }

        /// <summary>
        /// Parse a regular `for` loop. The disambiguation code in
        /// `parseStatement` will already have parsed the init statement or expression.
        /// </summary>
        private Node ParseFor(Node node, Node init)
        {
            node.Init = init;

            Expect(TT["semi"]);

            node.Test = Match(TT["semi"]) ? null : ParseExpression(false, ref _nullRef);

            Expect(TT["semi"]);

            node.Update = Match(TT["parenR"]) ? null : ParseExpression(false, ref _nullRef);

            Expect(TT["parenR"]);

            node.Body = ParseStatement(false);

            State.Labels.Pop();

            return FinishNode(node, "ForStatement");
        }

        /// <summary>
        /// Parse a `for`/`in` and `for`/`of` loop, which are almost same from parser's perspective.
        /// </summary>
        private Node ParseForIn(Node node, Node init)
        {
            var type = Match(TT["_in"]) ? "ForInStatement" : "ForOfStatement";

            Next();

            node.Left = init;
            node.Right = ParseExpression(false, ref _nullRef);

            Expect(TT["parenR"]);

            node.Body = ParseStatement(false);

            State.Labels.Pop();

            return FinishNode(node, type);
        }

        private Node ParseVar(Node node, bool isFor, TokenType kind)
        {
            node.Declarations = new List<Node>();
            node.Kind = kind.Keyword;

            for (;;)
            {
                var decl = StartNode();

                ParseVarHead(decl);

                if (Eat(TT["eq"]))
                {
                    decl.Init = ParseMaybeAssign(isFor, ref _nullRef);
                }
                else if (kind == TT["_const"] && !(Match(TT["_in"]) || IsContextual("of")))
                {
                    Unexpected();
                }
                else if (decl.Id.Type != "Identifier" && !(isFor && (Match(TT["_in"]) || IsContextual("of"))))
                {
                    Raise(State.LastTokenEnd, "Complex binding patterns require an initialization value");
                }
                else
                {
                    decl.Init = null;
                }

                node.Declarations.Add(FinishNode(decl, "VariableDeclarator"));

                if (!Eat(TT["comma"]))
                {
                    break;
                }
            }

            return node;
        }

        private void ParseVarHead(Node decl)
        {
            decl.Id = ParseBindingAtom();

            CheckLVal(decl.Id, true);
        }

        /// <summary>
        /// Parse a function declaration or literal (depending on the `isStatement` parameter).
        /// </summary>
        private Node ParseFunction(Node node, bool isStatement, bool allowExpressionBody = false, bool isAsync = false,
            bool optionalId = false)
        {
            var oldMethod = State.InMethod;

            State.InMethod = false;

            InitFunction(node, isAsync);

            if (Match(TT["star"]))
            {
                node.Generator = true;
                Next();
            }

            if (isStatement && !optionalId && !Match(TT["name"]) && !Match(TT["_yield"]))
            {
                Unexpected();
            }

            if (Match(TT["name"]) || Match(TT["_yield"]))
            {
                node.Id = ParseBindingIdentifier();
            }

            ParseFunctionParams(node);
            ParseFunctionBody(node, allowExpressionBody);

            State.InMethod = oldMethod;

            return FinishNode(node, isStatement ? "FunctionDeclaration" : "FunctionExpression");
        }

        private void ParseFunctionParams(Node node)
        {
            Expect(TT["parenL"]);

            node.Params = ParseBindingList(TT["parenR"], false, true);
        }

        /// <summary>
        /// Parse a class declaration or literal (depending on the `isStatement` parameter).
        /// </summary>
        private Node ParseClass(Node node, bool isStatement, bool optionalId = false)
        {
            Next();
            ParseClassId(node, isStatement, optionalId);
            ParseClassSuper(node);
            ParseClassBody(node);

            return FinishNode(node, isStatement ? "ClassDeclaration" : "ClassExpression");
        }

        private bool IsClassProperty => Match(TT["eq"]) || IsLineTerminator;

        private void ParseClassBody(Node node)
        {
            var oldStrict = State.Strict;

            State.Strict = true;

            var hadConstructorCall = false;
            var hadConstructor = false;
            var decorators = new List<Node>();
            var classBody = StartNode();

            classBody.Body = new List<Node>();

            Expect(TT["braceL"]);

            while (!Eat(TT["braceR"]))
            {
                if (Eat(TT["semi"]))
                {
                    continue;
                }

                if (Match(TT["at"]))
                {
                    decorators.Add(ParseDecorator());

                    continue;
                }

                var method = StartNode();

                if (decorators.Any())
                {
                    method.Decorators = decorators.ToList();
                    decorators = new List<Node>();
                }

                var isConstructorCall = false;
                var isMaybeStatic = Match(TT["name"]) && (string) State.Value == "static";
                var isGenerator = Eat(TT["star"]);
                var isGetSet = false;
                var isAsync = false;

                ParsePropertyName(method);

                method.Static = isMaybeStatic && !Match(TT["parenL"]);

                if (method.Static)
                {
                    if (isGenerator)
                    {
                        Unexpected();
                    }

                    isGenerator = Eat(TT["star"]);

                    ParsePropertyName(method);
                }

                if (!isGenerator && method.Key.As<Node>().Type == "Identifier" && !method.Computed)
                {
                    if (IsClassProperty)
                    {
                        ((List<Node>) classBody.Body).Add(ParseClassProperty(method));

                        continue;
                    }

                    if (method.Key.As<Node>().Name as string == "call" &&
                        Match(TT["name"]) &&
                        (string) State.Value == "constructor")
                    {
                        isConstructorCall = true;

                        ParsePropertyName(method);
                    }
                }

                var isAsyncMethod = !Match(TT["parenL"]) && !method.Computed &&
                                    method.Key.As<Node>().Type == "Identifier" &&
                                    method.Key.As<Node>().Name as string == "async";

                if (isAsyncMethod)
                {
                    if (Eat(TT["star"]))
                    {
                        isGenerator = true;
                    }

                    isAsync = true;

                    ParsePropertyName(method);
                }

                method.Kind = "method";

                if (!method.Computed)
                {
                    var key = method.Key as Node;

                    if (!isAsync && !isGenerator && key.Type == "Identifier" && !Match(TT["parenL"]) &&
                        (key.Name as string == "get" || key.Name as string == "set"))
                    {
                        isGetSet = true;
                        method.Kind = key.Name as string;

                        key = ParsePropertyName(method);
                    }

                    var isConstructor = !isConstructorCall && !method.Static &&
                                        ((key.Type == "Identifier" && key.Name as string == "constructor") ||
                                         (key.Type == "StringLiteral" && (string) key.Value == "constructor"));

                    if (isConstructor)
                    {
                        if (hadConstructor)
                        {
                            Raise(key.Start, "Duplicate constructor in the same class");
                        }

                        if (isGetSet)
                        {
                            Raise(key.Start, "Constructor can't have get/set modifier");
                        }

                        if (isGenerator)
                        {
                            Raise(key.Start, "Constructor can't be a generator");
                        }

                        if (isAsync)
                        {
                            Raise(key.Start, "Constructor can't be an async function");
                        }

                        method.Kind = "constructor";
                        hadConstructor = true;
                    }

                    var isStaticPrototype = method.Static &&
                                            ((key.Type == "Identifier" && key.Name as string == "prototype") ||
                                             (key.Type == "StringLiteral" && (string) key.Value == "prototype"));

                    if (isStaticPrototype)
                    {
                        Raise(key.Start, "Classes may not have static property named prototype");
                    }
                }

                if (isConstructorCall)
                {
                    if (hadConstructorCall)
                    {
                        Raise(method.Start, "Duplicate constructor call in the same class");
                    }

                    method.Kind = "constructorCall";
                    hadConstructorCall = true;
                }

                if ((method.Kind == "constructor" || method.Kind == "constructorCall") && method.Decorators != null)
                {
                    Raise(method.Start, "You can't attach decorators to a class constructor");
                }

                ParseClassMethod(classBody, method, isGenerator, isAsync);

                if (isGetSet)
                {
                    var paramCount = method.Kind == "get" ? 0 : 1;

                    if (method.Params.As<List<Node>>().Count != paramCount)
                    {
                        Raise(method.Start,
                            method.Kind == "get"
                                ? "getter should have no params"
                                : "setter should have exactly one param");
                    }
                }
            }

            if (decorators.Any())
            {
                Raise(State.Start, "You have trailing decorators with no method");
            }

            node.Body = FinishNode(classBody, "ClassBody");

            State.Strict = oldStrict;
        }

        private Node ParseClassProperty(Node node)
        {
            if (Match(TT["eq"]))
            {
                Next();

                node.Value = ParseMaybeAssign(false, ref _nullRef);
            }
            else
            {
                node.Value = null;
            }

            Semicolon();

            return FinishNode(node, "ClassProperty");
        }

        private void ParseClassMethod(Node classBody, Node method, bool isGenerator, bool isAsync)
        {
            ParseMethod(method, isGenerator, isAsync);
            ((List<Node>) classBody.Body).Add(FinishNode(method, "ClassMethod"));
        }

        private void ParseClassId(Node node, bool isStatement, bool optionalId = false)
        {
            if (Match(TT["name"]))
            {
                node.Id = ParseIdentifier();
            }
            else
            {
                if (optionalId || !isStatement)
                {
                    node.Id = null;
                }
                else
                {
                    Unexpected();
                }
            }
        }

        private void ParseClassSuper(Node node)
        {
            node.SuperClass = Eat(TT["_extends"]) ? ParseExprSubscripts(ref _nullRef) : null;
        }

        /// <summary>
        /// Parses module export declaration.
        /// </summary>
        private Node ParseExport(Node node)
        {
            Next();

            if (Match(TT["star"]))
            {
                var specifier = StartNode();

                Next();

                if (EatContextual("as"))
                {
                    specifier.Exported = ParseIdentifier();
                    node.Specifiers = new List<Node> {FinishNode(specifier, "ExportNamespaceSpecifier")};

                    ParseExportSpecifiersMaybe(node);
                    ParseExportFrom(node, true);
                }
                else
                {
                    ParseExportFrom(node, true);

                    return FinishNode(node, "ExportAllDeclaration");
                }
            }
            else if (IsExportDefaultSpecifier)
            {
                var specifier = StartNode();

                specifier.Exported = ParseIdentifier(true);
                node.Specifiers = new List<Node> {FinishNode(specifier, "ExportDefaultSpecifier")};

                if (Match(TT["comma"]) && Lookahead().Type == TT["star"])
                {
                    Expect(TT["comma"]);

                    specifier = StartNode();

                    Expect(TT["star"]);
                    ExpectContextual("as");

                    specifier.Exported = ParseIdentifier();

                    node.Specifiers.Add(FinishNode(specifier, "ExportNamespaceSpecifier"));
                }
                else
                {
                    ParseExportSpecifiersMaybe(node);
                }

                ParseExportFrom(node, true);
            }
            else if (Eat(TT["_default"]))
            {
                var expr = StartNode();
                var needsSemi = false;

                if (Eat(TT["_function"]))
                {
                    expr = ParseFunction(expr, true, false, false, true);
                }
                else if (Match(TT["_class"]))
                {
                    expr = ParseClass(expr, true, true);
                }
                else
                {
                    needsSemi = true;
                    expr = ParseMaybeAssign(false, ref _nullRef);
                }

                node.Declaration = expr;

                if (needsSemi)
                {
                    Semicolon();
                }

                CheckExport(node);

                return FinishNode(node, "ExportDefaultDeclaration");
            }
            else if (!string.IsNullOrEmpty(State.Type.Keyword) || ShouldParseExportDeclaration)
            {
                node.Specifiers = new List<Node>();
                node.Source = null;
                node.Declaration = ParseExportDeclaration(node);
            }
            else
            {
                node.Declaration = null;
                node.Specifiers = ParseExportSpecifiers();

                ParseExportFrom(node);
            }

            CheckExport(node);

            return FinishNode(node, "ExportNamedDeclaration");
        }

        private Node ParseExportDeclaration(Node node) => ParseStatement(true);

        private bool IsExportDefaultSpecifier
        {
            get
            {
                if (Match(TT["name"]))
                {
                    return (string) State.Value != "type" && (string) State.Value != "async";
                }

                if (!Match(TT["_default"]))
                {
                    return false;
                }

                var lookahead = Lookahead();

                return lookahead.Type == TT["comma"] ||
                       (lookahead.Type == TT["name"] && (string) lookahead.Value == "from");
            }
        }

        private void ParseExportSpecifiersMaybe(Node node)
        {
            if (Eat(TT["comma"]))
            {
                if (node.Specifiers == null)
                {
                    node.Specifiers = new List<Node>();
                }

                node.Specifiers.AddRange(ParseExportSpecifiers());
            }
        }

        private void ParseExportFrom(Node node, bool expect = false)
        {
            if (EatContextual("from"))
            {
                if (Match(TT["string"]))
                {
                    node.Source = ParseExprAtom(ref _nullRef);
                }
                else
                {
                    Unexpected();
                }

                CheckExport(node);
            }
            else
            {
                if (expect)
                {
                    Unexpected();
                }
                else
                {
                    node.Source = null;
                }
            }

            Semicolon();
        }

        private bool ShouldParseExportDeclaration => IsContextual("async");

        private void CheckExport(Node node)
        {
            if (State.Decorators.Any())
            {
                var isClass = node.Declaration &&
                              (node.Declaration.Type == "ClassDeclaration" || node.Declaration.Type == "ClassExpression");

                if (!node.Declaration || !isClass)
                {
                    Raise(node.Start, "You can only use decorators on an export when exporting a class");
                }

                TakeDecorators(node.Declaration);
            }
        }

        /// <summary>
        /// Parses a comma-separated list of module exports.
        /// </summary>
        private List<Node> ParseExportSpecifiers()
        {
            var nodes = new List<Node>();
            var first = true;
            var needsFrom = false;

            Expect(TT["braceL"]);

            while (!Eat(TT["braceR"]))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Expect(TT["comma"]);

                    if (Eat(TT["braceR"]))
                    {
                        break;
                    }
                }

                var isDefault = Match(TT["_default"]);

                if (isDefault && !needsFrom)
                {
                    needsFrom = true;
                }

                var node = StartNode();

                node.Local = ParseIdentifier(isDefault);
                node.Exported = EatContextual("as") ? ParseIdentifier(true) : (Node) node.Local.Clone();
                nodes.Add(FinishNode(node, "ExportSpecifier"));
            }

            if (needsFrom && !IsContextual("from"))
            {
                Unexpected();
            }

            return nodes;
        }

        /// <summary>
        /// Parses import declaration.
        /// </summary>
        private Node ParseImport(Node node)
        {
            Next();

            node.Specifiers = new List<Node>();

            if (Match(TT["string"]))
            {
                node.Source = ParseExprAtom(ref _nullRef);
            }
            else
            {
                ParseImportSpecifiers(node);
                ExpectContextual("from");

                if (Match(TT["string"]))
                {
                    node.Source = ParseExprAtom(ref _nullRef);
                }
                else
                {
                    Unexpected();
                }
            }

            Semicolon();

            return FinishNode(node, "ImportDeclaration");
        }

        /// <summary>
        /// Parses a comma-separated list of module imports.
        /// </summary>
        private void ParseImportSpecifiers(Node node)
        {
            var first = true;
            
            if (Match(TT["name"]))
            {
                var startPos = State.Start;
                var startLoc = State.StartLoc;

                node.Specifiers.Add(ParseImportSpecifierDefault(ParseIdentifier(), startPos, startLoc));

                if (!Eat(TT["comma"]))
                {
                    return;
                }
            }

            if (Match(TT["star"]))
            {
                var specifier = StartNode();

                Next();

                ExpectContextual("as");

                specifier.Local = ParseIdentifier();

                CheckLVal(specifier.Local, true);

                node.Specifiers.Add(FinishNode(specifier, "ImportNamespaceSpecifier"));

                return;
            }

            Expect(TT["braceL"]);

            while (!Eat(TT["braceR"]))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Expect(TT["comma"]);

                    if (Eat(TT["braceR"]))
                    {
                        break;
                    }
                }

                var specifier = StartNode();

                specifier.Imported = ParseIdentifier(true);
                specifier.Local = EatContextual("as") ? ParseIdentifier() : (Node) specifier.Imported.Clone();

                CheckLVal(specifier.Local, true);
                node.Specifiers.Add(FinishNode(specifier, "ImportSpecifier"));
            }
        }

        private Node ParseImportSpecifierDefault(Node id, int startPos, Position startLoc)
        {
            var node = StartNodeAt(startPos, startLoc);

            node.Local = id;
            CheckLVal(node.Local, true);

            return FinishNode(node, "ImportDefaultSpecifier");
        }
    }
}
