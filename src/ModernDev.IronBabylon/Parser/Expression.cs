using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        /// <summary>
        /// Check if property name clashes with already added.
        /// Object/class getters and setters are not allowed to clash —
        /// either with each other or with an init property — and in
        /// strict mode, init properties are also not allowed to be repeated.
        /// </summary>
        private void CheckPropClash(Node prop, IDictionary<string, bool> propHash)
        {
            if (prop.Computed)
            {
                return;
            }

            var key = prop.Key as Node;
            string name;

            switch (key?.Type)
            {
                case "Identifier":
                    name = key.Name as string;

                    break;

                case "StringLiteral":
                case "NumericLiteral":
                    name = key.Value as string;

                    break;

                default:
                    return;
            }

            if (name == "__proto__" && prop.Kind == "init")
            {
                if (propHash.ContainsKey("proto") && propHash["proto"])
                {
                    Raise(key.Start, "Redefinition of __proto__ property");
                }

                propHash["proto"] = true;
            }
        }

        /// <summary>
        /// Parse a full expression. The optional arguments are used to
        /// forbid the `in` operator (in for loops initalization expressions)
        /// and provide reference for storing '=' operator inside shorthand
        /// property assignment in contexts where both object expression
        /// and object pattern might appear (so it's possible to raise
        /// delayed syntax error at correct position).
        /// </summary>
        private Node ParseExpression(bool noIn, ref int? refShorthandDefaultPos)
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var expr = ParseMaybeAssign(noIn, ref refShorthandDefaultPos);

            if (Match(TT["comma"]))
            {
                var node = StartNodeAt(startPos, startLoc);

                node.Expressions = new List<Node> {expr};

                while (Eat(TT["comma"]))
                {
                    node.Expressions.Add(ParseMaybeAssign(noIn, ref refShorthandDefaultPos));
                }

                ToReferencedList(node.Expressions);

                return FinishNode(node, "SequenceExpression");
            }

            return expr;
        }

        /// <summary>
        /// Parse an assignment expression. This includes applications of operators like `+=`.
        /// </summary>
        private Node ParseMaybeAssign(bool noIn, ref int? refShorthandDefaultPos,
            Func<Node, int?, Position, bool, Node> afterLeftParse = null)
        {
            if (Match(TT["_yield"]) && State.InGenerator)
            {
                return ParseYield();
            }

            bool failOnShorthandAssign;

            if (refShorthandDefaultPos.ToBool())
            {
                failOnShorthandAssign = false;
            }
            else
            {
                refShorthandDefaultPos = 0;
                failOnShorthandAssign = true;
            }

            var startPos = State.Start;
            var startLoc = State.StartLoc;

            if (Match(TT["parenL"]) || Match(TT["name"]))
            {
                State.PotentialArrowAt = State.Start;
            }

            var left = ParseMaybeConditional(noIn, ref refShorthandDefaultPos);

            if (afterLeftParse != null)
            {
                left = afterLeftParse(left, startPos, startLoc, false);
            }

            if (State.Type.IsAssign)
            {
                var node = StartNodeAt(startPos, startLoc);

                node.Operator = State.Value as string;
                node.Left = Match(TT["eq"]) ? ToAssignable(left) : left;
                refShorthandDefaultPos = 0;

                CheckLVal(left);

                if (left.Extra != null && left.Extra.ContainsKey("parenthesized") && (bool) left.Extra["parenthesized"])
                {
                    var errorMsg = "";

                    switch (left.Type)
                    {
                        case "ObjectPattern":
                            errorMsg = "`({a}) = 0` use `({a} = 0)`";
                            break;
                        case "ArrayPattern":
                            errorMsg = "`([a]) = 0` use `([a] = 0)`";
                            break;
                    }

                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        Raise(left.Start,
                            $"You're trying to assign to a parenthesized expression, eg. instead of {errorMsg}");
                    }
                }

                Next();
                
                node.Right = ParseMaybeAssign(noIn, ref _nullRef);

                return FinishNode(node, "AssignmentExpression");
            }

            if (failOnShorthandAssign && refShorthandDefaultPos.ToBool())
            {
                Unexpected(refShorthandDefaultPos);
            }

            return left;
        }

        /// <summary>
        /// Parse a ternary conditional (`?:`) operator.
        /// </summary>
        private Node ParseMaybeConditional(bool noIn, ref int? refShorthandDefaultPos)
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var expr = ParseExprOps(noIn, ref refShorthandDefaultPos);

            if (refShorthandDefaultPos.ToBool())
            {
                return expr;
            }

            if (Eat(TT["question"]))
            {
                var node = StartNodeAt(startPos, startLoc);

                node.Test = expr;
                node.Consequent = ParseMaybeAssign(false, ref _nullRef);

                Expect(TT["colon"]);

                node.Altername = ParseMaybeAssign(noIn, ref _nullRef);

                return FinishNode(node, "ConditionalExpression");
            }

            return expr;
        }

        /// <summary>
        /// Start the precedence parser.
        /// </summary>
        private Node ParseExprOps(bool noIn, ref int? refShorthandDefaultPos)
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var expr = ParseMaybeUnary(ref refShorthandDefaultPos);

            return refShorthandDefaultPos.ToBool() ? expr : ParseExprOp(expr, startPos, startLoc, -1, noIn);
        }

        /// <summary>
        /// Parse binary operators with the operator precedence parsing
        /// algorithm. `left` is the left-hand side of the operator.
        /// `minPrec` provides context that allows the function to stop and
        /// defer further parser to one of its callers when it encounters an
        /// operator that has a lower precedence than the set it is parsing.
        /// </summary>
        private Node ParseExprOp(Node left, int leftStartPos, Position leftStartLoc, int minPrec, bool noIn)
        {
            var prec = State.Type.Binop;

            if (prec != null && (!noIn || !Match(TT["_in"])))
            {
                if (prec > minPrec)
                {
                    var node = StartNodeAt(leftStartPos, leftStartLoc);

                    node.Left = left;
                    node.Operator = State.Value as string;

                    if (node.Operator == "**" && left.Type == "UnaryExpression" && left.Extra != null &&
                        (!left.Extra.ContainsKey("parenthesizedArgument") ||
                        (bool) left.Extra["parenthesizedArgument"] == false))
                    {
                        Raise(left.Argument.Start,
                            "Illegal expression. Wrap left hand side or entire exponentiation in parentheses.");
                    }

                    var op = State.Type;

                    Next();

                    var startPos = State.Start;
                    var startLoc = State.StartLoc;

                    node.Right = ParseExprOp(ParseMaybeUnary(ref _nullRef), startPos, startLoc,
                        op.RightAssociative ? (int) prec - 1 : (int) prec, noIn);

                    FinishNode(node,
                        op == TT["logicalOR"] || op == TT["logicalAND"] ? "LogicalExpression" : "BinaryExpression");

                    return ParseExprOp(node, leftStartPos, leftStartLoc, minPrec, noIn);
                }
            }

            return left;
        }

        /// <summary>
        /// Parse unary operators, both prefix and postfix.
        /// </summary>
        private Node ParseMaybeUnary(ref int? refShorthandDefaultPos)
        {
            if (State.Type.Prefix)
            {
                var node = StartNode();
                var update = Match(TT["incDec"]);

                node.Operator = State.Value as string;
                node.Prefix = true;

                Next();

                var argType = State.Type;

                AddExtra(node, "parenthesizedArgument", argType == TT["parenL"]);

                node.Argument = ParseMaybeUnary(ref _nullRef);

                if (refShorthandDefaultPos.ToBool())
                {
                    Unexpected(refShorthandDefaultPos);
                }

                if (update)
                {
                    CheckLVal(node.Argument);
                }
                else if (State.Strict && node.Operator == "delete" && node.Argument.Type == "Identifier")
                {
                    Raise(node.Start, "Deleting local variable in strict mode");
                }

                return FinishNode(node, update ? "UpdateExpression" : "UnaryExpression");
            }

            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var expr = ParseExprSubscripts(ref refShorthandDefaultPos);

            if (refShorthandDefaultPos.ToBool())
            {
                return expr;
            }

            while (State.Type.Postfix && !CanInsertSemicolon)
            {
                var node = StartNodeAt(startPos, startLoc);

                node.Operator = State.Value as string;
                node.Prefix = false;
                node.Argument = expr;

                CheckLVal(expr);
                Next();

                expr = FinishNode(node, "UpdateExpression");
            }

            return expr;
        }

        /// <summary>
        /// // Parse call, dot, and `[]`-subscript expressions.
        /// </summary>
        private Node ParseExprSubscripts(ref int? refShorthandDefaultPos)
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var paa = State.PotentialArrowAt;
            var expr = ParseExprAtom(ref refShorthandDefaultPos);

            if (expr.Type == "ArrowFunctionExpression" && expr.Start == paa)
            {
                return expr;
            }

            if (refShorthandDefaultPos.ToBool())
            {
                return expr;
            }

            return ParseSubscripts(expr, startPos, startLoc);
        }

        private Node ParseSubscripts(Node b, int startPos, Position startLoc, bool noCalls = false)
        {
            for (;;)
            {
                if (!noCalls && Eat(TT["doubleColon"]))
                {
                    var node = StartNodeAt(startPos, startLoc);

                    node.Object = b;
                    node.Callee = ParseNoCallExpr();

                    return ParseSubscripts(FinishNode(node, "BindExpression"), startPos, startLoc, noCalls);
                } else if (Eat(TT["dot"]))
                {
                    var node = StartNodeAt(startPos, startLoc);

                    node.Object = b;
                    node.Property = ParseIdentifier(true);
                    node.Computed = false;
                    b = FinishNode(node, "MemberExpression");
                }
                else if (Eat(TT["bracketL"]))
                {
                    var node = StartNodeAt(startPos, startLoc);

                    node.Object = b;
                    node.Property = ParseExpression(false, ref _nullRef);
                    node.Computed = true;

                    Expect(TT["bracketR"]);

                    return FinishNode(node, "MemberExpression");
                }
                else if (!noCalls && Match(TT["parenL"]))
                {
                    var possibleAsync = State.PotentialArrowAt == b.Start && b.Type == "Identifier" &&
                                        (string) b.Name == "async" &&
                                        !CanInsertSemicolon;

                    Next();

                    var node = StartNodeAt(startPos, startLoc);

                    node.Callee = b;
                    node.Arguments = ParseCallExpressionArguments(TT["parenR"], true, possibleAsync);
                    b = FinishNode(node, "CallExpression");

                    if (possibleAsync && ShouldParseAsyncArrow)
                    {
                        return ParseAsyncArrowFromCallExpression(StartNodeAt(startPos, startLoc), node);
                    }

                    ToReferencedList(node.Arguments);
                }
                else if (Match(TT["backQuote"]))
                {
                    var node = StartNodeAt(startPos, startLoc);

                    node.Tag = b;
                    node.Quasi = ParseTemplate();
                    b = FinishNode(node, "TaggedTemplateExpression");
                }
                else
                {
                    return b;
                }
            }
        }

        private List<Node> ParseCallExpressionArguments(TokenType close, bool allowtrailingComma, bool possibleAsyncArrow)
        {
            var innerParentStart = 0;
            var elts = new List<Node>();
            var first = true;

            while (!Eat(close))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Expect(TT["comma"]);

                    if (allowtrailingComma && Eat(close))
                    {
                        break;
                    }
                }

                if (Match(TT["parenL"]) && !innerParentStart.ToBool())
                {
                    innerParentStart = State.Start;
                }

                elts.Add(ParseExprListItem(false, ref _nullRef));
            }

            if (possibleAsyncArrow && innerParentStart.ToBool() && ShouldParseAsyncArrow)
            {
                Unexpected();
            }

            return elts;
        }

        private bool ShouldParseAsyncArrow => Match(TT["arrow"]);

        private Node ParseAsyncArrowFromCallExpression(Node node, Node call)
        {
            Expect(TT["arrow"]);

            return ParseArrowExpression(node, call.Arguments, true);
        }

        /// <summary>
        /// Parse a no-call expression (like argument of `new` or `::` operators).
        /// </summary>
        private Node ParseNoCallExpr()
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;

            return ParseSubscripts(ParseExprAtom(ref _nullRef), startPos, startLoc, true);
        }

        /// <summary>
        /// Parse an atomic expression — either a single token that is an
        /// expression, an expression started by a keyword like `function` or
        /// `new`, or an expression wrapped in punctuation like `()`, `[]`, or `{}`.
        /// </summary>
        private Node ParseExprAtom(ref int? refShorthandDefaultPos)
        {
            Node node;
            var canBeArrow = State.PotentialArrowAt == State.Start;

            if (State.Type == TT["_super"])
            {
                if (!State.InMethod.ToBool() && !Options.AllowSuperOutsideMethod)
                {
                    Raise(State.Start, "'super' outside of function or class");
                }

                node = StartNode();

                Next();

                if (!Match(TT["parenL"]) && !Match(TT["bracketL"]) && !Match(TT["dot"]))
                {
                    Unexpected();
                }

                if (Match(TT["parenL"]) && State.InMethod is string && (string) State.InMethod != "constructor" &&
                    !Options.AllowSuperOutsideMethod)
                {
                    Raise(node.Start, "super() outside of class constructor");
                }

                return FinishNode(node, "Super");
            }
            else if (State.Type == TT["_this"])
            {
                node = StartNode();

                Next();

                return FinishNode(node, "ThisExpression");
            }
            else if (State.Type == TT["_yield"] || State.Type == TT["name"])
            {
                if (State.Type == TT["_yield"] && State.InGenerator)
                {
                    Unexpected();
                }

                node = StartNode();
                
                var id = ParseIdentifier((State.Value as string == "await" && State.InAsync) || ShouldAllowYieldIdentifier);

                if (id.Name as string == "await")
                {
                    if (State.InAsync || InModule)
                    {
                        return ParseAwait(node);
                    }
                }
                else if (id.Name as string == "async" && Match(TT["_function"]) && !CanInsertSemicolon)
                {
                    Next();

                    return ParseFunction(node, false, false, true);
                }
                else if (canBeArrow && id.Name as string == "async" && Match(TT["name"]))
                {
                    var prms = new List<Node> {ParseIdentifier()};

                    Expect(TT["arrow"]);

                    return ParseArrowExpression(node, prms, true);
                }

                if (canBeArrow && !CanInsertSemicolon && Eat(TT["arrow"]))
                {
                    return ParseArrowExpression(node, new List<Node> {id});
                }

                return id;
            }
            else if (State.Type == TT["_do"])
            {
                node = StartNode();

                Next();

                var oldInFunc = State.InFunction;
                var oldLabels = State.Labels.ToList();

                State.Labels = new List<Node>();
                State.InFunction = false;
                node.Body = ParseBlock(/*false, true*/); // TODO: additional arg?
                State.InFunction = oldInFunc;
                State.Labels = oldLabels;

                return FinishNode(node, "DoExpression");
            }
            else if (State.Type == TT["regexp"])
            {
                var value = State.Value as Node;

                node = ParseLiteral(value, "RegExpLiteral");
                node.Pattern = value?.Pattern;
                node.Flags = value?.Flags;

                return node;
            }
            else if (State.Type == TT["num"])
            {
                return ParseLiteral(State.Value, "NumericLiteral");
            }
            else if (State.Type == TT["string"])
            {
                return ParseLiteral(State.Value, "StringLiteral");
            }
            else if (State.Type == TT["_null"])
            {
                node = StartNode();

                Next();

                return FinishNode(node, "NullLiteral");
            }
            else if (State.Type == TT["_true"] || State.Type == TT["_false"])
            {
                node = StartNode();
                node.Value = Match(TT["_true"]);

                Next();

                return FinishNode(node, "BooleanLiteral");
            }
            else if (State.Type == TT["parenL"])
            {
                return ParseParenAndDistinguishExpression(null, null, canBeArrow);
            }
            else if (State.Type == TT["bracketL"])
            {
                node = StartNode();

                Next();

                node.Elements = ParseExprList(TT["bracketR"], true, true, ref refShorthandDefaultPos);

                ToReferencedList(node.Elements);

                return FinishNode(node, "ArrayExpression");
            }
            else if (State.Type == TT["braceL"])
            {
                return ParseObj(false, ref refShorthandDefaultPos);
            }
            else if (State.Type == TT["_function"])
            {
                return ParseFunctionExpression();
            }
            else if (State.Type == TT["at"] || State.Type == TT["_class"])
            {
                if (State.Type == TT["at"])
                {
                    ParseDecorators();
                }

                node = StartNode();

                TakeDecorators(node);

                return ParseClass(node, false);
            }
            else if (State.Type == TT["_new"])
            {
                return ParseNew();
            }
            else if (State.Type == TT["backQuote"])
            {
                return ParseTemplate();
            }
            else if (State.Type == TT["doubleColon"])
            {
                node = StartNode();

                Next();

                node.Object = null;

                var callee = node.Callee = ParseNoCallExpr();

                if (callee.Type == "MemberExpression")
                {
                    return FinishNode(node, "BindExpression");
                }

                Raise(callee.Start, "Binding should be performed on object property.");
            }
            else
            {
                Unexpected();
            }

            return null;
        }

        private Node ParseFunctionExpression()
        {
            var node = StartNode();
            var meta = ParseIdentifier(true);

            if (State.InGenerator && Eat(TT["dot"]))
            {
                return ParseMetaProperty(node, meta, "sent");
            }


            return ParseFunction(node, false);
        }

        private Node ParseMetaProperty(Node node, Node meta, string propertyName)
        {
            node.Meta = meta;
            node.Property = ParseIdentifier(true);

            if ((string) node.Property.Name != propertyName)
            {
                Raise(node.Property.Start, $"The only valid meta property for new is {meta.Name}.{propertyName}");
            }

            return FinishNode(node, "MetaProperty");
        }

        private Node ParseLiteral(object value, string type)
        {
            var node = StartNode();

            AddExtra(node, "rawValue", value);
            AddExtra(node, "raw", Input.Slice(State.Start, State.End));

            node.Value = value;

            Next();

            return FinishNode(node, type);
        }

        private Node ParseParenExpression()
        {
            Expect(TT["parenL"]);

            var val = ParseExpression(false, ref _nullRef);

            Expect(TT["parenR"]);

            return val;
        }

        private Node ParseParenAndDistinguishExpression(int? startPos, Position startLoc, bool canBeArrow,
            bool isAsync = false)
        {
            startPos = startPos ?? State.Start;
            startLoc = startLoc ?? State.StartLoc;

            Node val;

            Next();

            var innerStartPos = State.Start;
            var innerStartLoc = State.StartLoc;
            var exprList = new List<Node>();
            var optionalCommaStart = 0;
            int? refShorthandDefaultPos = 0;
            var spreadStart = 0;
            var first = true;

            while (!Match(TT["parenR"]))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Expect(TT["comma"]);

                    if (Match(TT["parenR"]))
                    {
                        optionalCommaStart = State.Start;

                        break;
                    }
                }

                if (Match(TT["ellipsis"]))
                {
                    var spreadNodeStartPos = State.Start;
                    var spreadNodeStartLoc = State.StartLoc;
                    spreadStart = State.Start;

                    exprList.Add(ParseParenItem(ParseRest(), spreadNodeStartPos, spreadNodeStartLoc));

                    break;
                }

                exprList.Add(ParseMaybeAssign(false, ref refShorthandDefaultPos, ParseParenItem));
            }

            var innerEndPos = State.Start;
            var innerEndLoc = State.StartLoc;

            Expect(TT["parenR"]);

            if (canBeArrow && !CanInsertSemicolon && Eat(TT["arrow"]))
            {
                foreach (var p in exprList.Where(p => p.Extra != null && p.Extra.ContainsKey("parenthesized") && (bool) p.Extra["parenthesized"]))
                {
                    Debug.Assert(p.Extra != null, "p.Extra != null"); // TODO:
                    Unexpected((int) p.Extra?["parenStart"]);
                }

                return ParseArrowExpression(StartNodeAt((int) startPos, startLoc), exprList, isAsync);
            }

            if (!exprList.Any())
            {
                if (isAsync)
                {
                    return null;
                }

                Unexpected();
            }

            if (optionalCommaStart.ToBool())
            {
                Unexpected(optionalCommaStart);
            }

            if (spreadStart.ToBool())
            {
                Unexpected(spreadStart);
            }

            if (refShorthandDefaultPos.ToBool())
            {
                Unexpected(refShorthandDefaultPos);
            }

            if (exprList.Count > 1)
            {
                val = StartNodeAt(innerStartPos, innerStartLoc);

                val.Expressions = exprList;

                ToReferencedList(val.Expressions);
                FinishNodeAt(val, "SequenceExpression", innerEndPos, innerEndLoc);
            }
            else
            {
                val = exprList.First();
            }

            AddExtra(val, "parenthesized", true);
            AddExtra(val, "parenStart", startPos);

            return val;
        }

        private Node ParseParenItem(Node node, int? startPos = null, Position startLoc = null, bool forceArrow = false)
            => node;

        /// <summary>
        /// New's precedence is slightly tricky. It must allow its argument
        /// to be a `[]` or dot subscript expression, but not a call — at
        /// least, not without wrapping it in parentheses. Thus, it uses the
        /// </summary>
        private Node ParseNew()
        {
            var node = StartNode();
            var meta = ParseIdentifier(true);

            if (Eat(TT["dot"]))
            {
                return ParseMetaProperty(node, meta, "target");
            }

            node.Callee = ParseNoCallExpr();

            if (Eat(TT["parenL"]))
            {
                node.Arguments = ParseExprList(TT["parenR"], true, false, ref _nullRef);

                ToReferencedList(node.Arguments);
            }
            else
            {
                node.Arguments = new List<Node>();
            }

            return FinishNode(node, "NewExpression");
        }

        /// <summary>
        /// Parse template expression.
        /// </summary>
        private Node ParseTemplateElement()
        {
            var elem = StartNode();
            var rgx = new Regex("\r\n?");

            elem.Value = new Dictionary<string, string>
            {
                {"raw", rgx.Replace(Input.Slice(State.Start, State.End), "\n")},
                {"cooked", State.Value as string}
            };

            Next();

            elem.Tail = Match(TT["backQuote"]);

            return FinishNode(elem, "TemplateElement");
        }

        private Node ParseTemplate()
        {
            var node = StartNode();

            Next();

            node.Expressions = new List<Node>();

            var curElt = ParseTemplateElement();

            node.Quasis = new List<Node> {curElt};

            while (!curElt.Tail)
            {
                Expect(TT["dollarBraceL"]);
                node.Expressions.Add(ParseExpression(false, ref _nullRef));
                Expect(TT["braceR"]);
                node.Quasis.Add(curElt = ParseTemplateElement());
            }

            Next();

            return FinishNode(node, "TemplateLiteral");
        }

        /// <summary>
        /// Parse an object literal or binding pattern.
        /// </summary>
        private Node ParseObj(bool isPattern, ref int? refShorthandDefaultPos)
        {
            var decorators = new List<Node>();
            var propHash = new Dictionary<string, bool>();
            var first = true;
            var node = StartNode();

            node.Properties = new List<Node>();

            Next();

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

                while (Match(TT["at"]))
                {
                    decorators.Add(ParseDecorator());
                }

                var prop = StartNode();
                var isGenerator = false;
                var isAsync = false;
                var startPos = 0;
                Position startLoc = null;

                if (decorators.Any())
                {
                    prop.Decorators = decorators.ToList();
                    decorators = new List<Node>();
                }

                if (Match(TT["ellipsis"]))
                {
                    prop = ParseSpread(ref _nullRef);
                    prop.Type = isPattern ? "RestProperty" : "SpreadProperty";
                    node.Properties.Add(prop);

                    continue;
                }

                prop.Method = false;
                prop.Shorthand = false;

                if (isPattern || refShorthandDefaultPos.HasValue)
                {
                    startPos = State.Start;
                    startLoc = State.StartLoc;
                }

                if (!isPattern)
                {
                    isGenerator = Eat(TT["star"]);
                }

                if (!isPattern && IsContextual("async"))
                {
                    if (isGenerator)
                    {
                        Unexpected();
                    }

                    var asyncId = ParseIdentifier();

                    if (Match(TT["colon"]) || Match(TT["parenL"]) || Match(TT["braceR"]))
                    {
                        prop.Key = asyncId;
                    }
                    else
                    {
                        isAsync = true;
                        isGenerator = Eat(TT["star"]);

                        ParsePropertyName(prop);
                    }
                }
                else
                {
                    ParsePropertyName(prop);
                }

                ParseObjPropValue(prop, startPos, startLoc, isGenerator, isAsync, isPattern, ref refShorthandDefaultPos);
                CheckPropClash(prop, propHash);

                if (prop.Shorthand)
                {
                    AddExtra(prop, "shorthand", true);
                }

                node.Properties.Add(prop);
            }

            if (decorators.Any())
            {
                Raise(State.Start, "You have trailing decorators with no property");
            }

            return FinishNode(node, isPattern ? "ObjectPattern" : "ObjectExpression");
        }

        private Node ParseObjPropValue(Node prop, int startPos, Position startLoc, bool isGenerator, bool isAsync,
            bool isPattern, ref int? refShorthandDefaultPos)
        {
            if (isGenerator || isAsync || Match(TT["parenL"]))
            {
                if (isPattern)
                {
                    Unexpected();
                }

                prop.Kind = "method";
                prop.Method = true;

                ParseMethod(prop, isGenerator, isAsync);

                return FinishNode(prop, "ObjectMethod");
            }

            if (Eat(TT["colon"]))
            {
                prop.Value = isPattern
                    ? ParseMaybeDefault(State.Start, State.StartLoc)
                    : ParseMaybeAssign(false, ref refShorthandDefaultPos);

                return FinishNode(prop, "ObjectProperty");
            }

            if (!prop.Computed && prop.Key.As<Node>().Type == "Identifier" &&
                (prop.Key.As<Node>().Name as string == "get" || prop.Key.As<Node>().Name as string == "set") &&
                !Match(TT["comma"]) && !Match(TT["braceR"]))
            {
                if (isGenerator || isAsync || isPattern)
                {
                    Unexpected();
                }

                prop.Kind = prop.Key.As<Node>().Name as string;

                ParsePropertyName(prop);
                ParseMethod(prop);

                var paramCount = prop.Kind == "get" ? 0 : 1;

                if ((prop.Params as IList)?.Count != paramCount)
                {
                    Raise(prop.Start,
                        prop.Kind == "get" ? "getter should have no params" : "setter should have exactly one param");
                }

                return FinishNode(prop, "ObjectMethod");
            }

            if (!prop.Computed && prop.Key.As<Node>().Type == "Identifier")
            {
                if (isPattern)
                {
                    var illegalBinding = IsKeyword(prop.Key.As<Node>().Name as string);

                    if (!illegalBinding && State.Strict)
                    {
                        illegalBinding = ReservedWords["strictBind"](prop.Key.As<Node>().Name as string) ||
                                         ReservedWords["strict"](prop.Key.As<Node>().Name as string);
                    }

                    if (illegalBinding)
                    {
                        Raise(prop.Key.As<Node>().Start, "Binding " + prop.Key.As<Node>().Name);
                    }

                    prop.Value = ParseMaybeDefault(startPos, startLoc, (Node) prop.Key.As<Node>().Clone());
                }
                else if (Match(TT["eq"]) && refShorthandDefaultPos != null)
                {
                    if (!refShorthandDefaultPos.ToBool())
                    {
                        refShorthandDefaultPos = State.Start;
                    }

                    prop.Value = ParseMaybeDefault(startPos, startLoc, (Node) prop.Key.As<Node>().Clone());
                }
                else
                {
                    prop.Value = prop.Key.As<Node>().Clone();
                }

                prop.Shorthand = true;

                return FinishNode(prop, "ObjectProperty");
            }

            Unexpected();
            return null;
        }

        private Node ParsePropertyName(Node prop)
        {
            if (Eat(TT["bracketL"]))
            {
                prop.Computed = true;
                prop.Key = ParseMaybeAssign(false, ref _nullRef);

                Expect(TT["bracketR"]);

                return (Node) prop.Key;
            }

            prop.Computed = false;
            prop.Key = Match(TT["num"]) || Match(TT["string"]) ? ParseExprAtom(ref _nullRef) : ParseIdentifier(true);

            return (Node) prop.Key;
        }

        /// <summary>
        /// Initialize empty function node.
        /// </summary>
        private static void InitFunction(Node node, bool isAsync)
        {
            node.Id = null;
            node.Generator = false;
            node.Expression = null;
            node.Async = isAsync;
        }

        /// <summary>
        /// Parse object or class method.
        /// </summary>
        private Node ParseMethod(Node node, bool isGenerator = false, bool isAsync = false)
        {
            var oldInMethod = State.InMethod;

            if (node.Kind.ToBool())
            {
                State.InMethod = node.Kind;
            }
            else
            {
                State.InMethod = true;
            }

            InitFunction(node, isAsync);
            Expect(TT["parenL"]);

            node.Params = ParseBindingList(TT["parenR"], false, true);
            node.Generator = isGenerator;

            ParseFunctionBody(node);

            State.InMethod = oldInMethod;

            return node;
        }

        /// <summary>
        /// Parse arrow function expression with given parameters.
        /// </summary>
        private Node ParseArrowExpression(Node node, List<Node> prms, bool isAsync = false)
        {
            InitFunction(node, isAsync);

            node.Params = ToAssignableList(prms, true);

            ParseFunctionBody(node, true);

            return FinishNode(node, "ArrowFunctionExpression");
        }

        /// <summary>
        /// Parse function body and check parameters.
        /// </summary>
        private void ParseFunctionBody(Node node, bool allowExpression = false)
        {
            var isExpression = allowExpression && !Match(TT["braceL"]);
            var oldInAsync = State.InAsync;

            State.InAsync = node.Async;

            if (isExpression)
            {
                node.Body = ParseMaybeAssign(false, ref _nullRef);
                node.Expression = true;
            }
            else
            {
                var oldInFunction = State.InFunction;
                var oldInGen = State.InGenerator;
                var oldLabels = State.Labels.ToList();

                State.InFunction = true;
                State.InGenerator = node.Generator;
                State.Labels = new List<Node>();
                node.Body = ParseBlock(true);
                node.Expression = false;
                State.InFunction = oldInFunction;
                State.InGenerator = oldInGen;
                State.Labels = oldLabels;
            }

            State.InAsync = oldInAsync;

            var checkLVal = State.Strict;
            var checkLValStrict = false;
            var isStrict = false;

            if (allowExpression)
            {
                checkLVal = true;
            }

            if (!isExpression && ((Node) node.Body).Directives.Any())
            {
                if (
                    ((Node) node.Body).Directives.Any(
                        directive => (string) ((Node) directive.Value).Value == "use strict"))
                {
                    isStrict = true;
                    checkLVal = true;
                    checkLValStrict = true;
                }
            }

            if (isStrict && node.Id != null && node.Id.Type == "Identifier" && node.Id.Name as string == "yield")
            {
                Raise(node.Id.Start, "Binding yield in strict mode");
            }

            if (checkLVal)
            {
                var nameHash = new Dictionary<string, bool>();
                var oldStrict = State.Strict;

                if (checkLValStrict)
                {
                    State.Strict = true;
                }

                if (node.Id != null)
                {
                    CheckLVal(node.Id, true);
                }

                foreach (var param in (List<Node>) node.Params)
                {
                    CheckLVal(param, true, nameHash);
                }

                State.Strict = oldStrict;
            }
        }

        /// <summary>
        /// Parses a comma-separated list of expressions, and returns them as
        /// an array. `close` is the token type that ends the list, and
        /// `allowEmpty` can be turned on to allow subsequent commas with
        /// nothing in between them to be parsed as `null` (which is needed
        /// for array literals).
        /// </summary>
        private List<Node> ParseExprList(TokenType close, bool allowTrailingComma, bool allowEmpty,
            ref int? refShorthandDefaultPos)
        {
            var elts = new List<Node>();
            var first = true;

            while (!Eat(close))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Expect(TT["comma"]);

                    if (allowTrailingComma && Eat(close))
                    {
                        break;
                    }
                }

                elts.Add(ParseExprListItem(allowEmpty, ref refShorthandDefaultPos));
            }

            return elts;
        }

        private Node ParseExprListItem(bool allowEmpty, ref int? refShorthandDefaultPos)
        {
            Node elt;

            if (allowEmpty && Match(TT["comma"]))
            {
                elt = null;
            }
            else if (Match(TT["ellipsis"]))
            {
                elt = ParseSpread(ref refShorthandDefaultPos);
            }
            else
            {
                elt = ParseMaybeAssign(false, ref refShorthandDefaultPos);
            }

            return elt;
        }

        /// <summary>
        /// Parse the next token as an identifier. If `liberal` is true (used
        /// when parsing properties), it will also convert keywords into
        /// identifiers.
        /// </summary>
        private Node ParseIdentifier(bool liberal = false)
        {
            var node = StartNode();

            if (Match(TokenType.Types["name"]))
            {
                if (!liberal && State.Strict && ReservedWords["strict"]((string) State.Value))
                {
                    Raise(State.Start, $"The keyword '{State.Value}' is reserved");
                }

                node.Name = (string) State.Value;
            }
            else if (liberal && !string.IsNullOrEmpty(State.Type.Keyword))
            {
                node.Name = State.Type.Keyword;
            }
            else
            {
                Unexpected();
            }

            if (!liberal && node.Name as string == "await" && State.InAsync)
            {
                Raise(node.Start, "invalid use of await inside of an async function");
            }

            Next();

            return FinishNode(node, "Identifier");
        }

        /// <summary>
        /// Parses await expression inside async function.
        /// </summary>
        private Node ParseAwait(Node node)
        {
            if (!State.InAsync)
            {
                Unexpected();
            }

            if (IsLineTerminator)
            {
                Unexpected();
            }

            node.All = Eat(TT["star"]);
            node.Argument = ParseMaybeUnary(ref _nullRef);

            return FinishNode(node, "AwaitExpression");
        }

        /// <summary>
        /// Parses yield expression inside generator.
        /// </summary>
        private Node ParseYield()
        {
            var node = StartNode();

            Next();

            if (Match(TT["semi"]) || CanInsertSemicolon || (!Match(TT["star"]) && !State.Type.StartsExpr))
            {
                node.Delegate = false;
                node.Argument = null;
            }
            else
            {
                node.Delegate = Eat(TT["star"]);
                node.Argument = ParseMaybeAssign(false, ref _nullRef);
            }

            return FinishNode(node, "YieldExpression");
        }
    }
}
