using System.Collections.Generic;
using System.Linq;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        /// <summary>
        /// Convert existing expression atom to assignable pattern if possible.
        /// </summary>
        private Node ToAssignableRegular(Node node, bool isBinding = false)
        {
            if (node)
            {
                switch (node.Type)
                {
                    case "Identifier":
                    case "ObjectPattern":
                    case "ArrayPattern":
                    case "AssignmentPattern":
                        break;

                    case "ObjectExpression":
                        node.Type = "ObjectPattern";

                        foreach (var prop in node.Properties)
                        {
                            if (prop.Type == "ObjectMethod")
                            {
                                if (prop.Kind == "get" || prop.Kind == "set")
                                {
                                    Raise(prop.Key.As<Node>().Start,
                                        "Object pattern can't contain getter or setter");
                                }
                                else
                                {
                                    Raise(prop.Key.As<Node>().Start, "Object pattern can't contain methods");
                                }
                            }
                            else
                            {
                                ToAssignable(prop, isBinding);
                            }
                        }

                        break;

                    case "ObjectProperty":
                        ToAssignable(node.Value as Node, isBinding);

                        break;

                    case "SpreadProperty":
                        node.Type = "RestProperty";

                        break;

                    case "ArrayExpression":
                        node.Type = "ArrayPattern";
                        ToAssignableList(node.Elements, isBinding);

                        break;

                    case "AssignmentExpression":
                        if (node.Operator == "=")
                        {
                            node.Type = "AssignmentPattern";
                            node.Operator = null;
                        }
                        else
                        {
                            Raise(node.Left.End, "Only '=' operator can be used for specifying default value.");
                        }

                        break;

                    case "MemberExpression":
                        if (!isBinding)
                        {
                            break;
                        }

                        Raise(node.Start, "Assigning to rvalue");

                        break;

                    default:
                        Raise(node.Start, "Assigning to rvalue");

                        break;
                }
            }

            return node;
        }

        /// <summary>
        /// Convert list of expression atoms to binding list.
        /// </summary>
        private List<Node> ToAssignableListRegular(List<Node> exprList, bool isBinding)
        {
            var end = exprList.Count;

            if (end > 0)
            {
                var last = exprList[end - 1];

                switch (last?.Type)
                {
                    case "RestElement":
                        --end;

                        break;
                    case "SpreadElement":
                        last.Type = "RestElement";

                        var arg = last.Argument;

                        ToAssignable(arg, isBinding);

                        if (arg.Type != "Identifier" && arg.Type != "MemberExpression" && arg.Type != "ArrayPattern")
                        {
                            Unexpected(arg.Start);
                        }

                        --end;

                        break;
                }
            }

            for (var i = 0; i < end; i++)
            {
                var elt = exprList[i];

                if (elt)
                {
                    ToAssignable(elt, isBinding);
                }
            }

            return exprList;
        }

        /// <summary>
        /// Parses spread element.
        /// </summary>
        private Node ParseSpread(ref int? refShorthandDefaultPos)
        {
            var node = StartNode();

            Next();

            node.Argument = ParseMaybeAssign(false, ref refShorthandDefaultPos);

            return FinishNode(node, "SpreadElement");
        }

        private Node ParseRest()
        {
            var node = StartNode();

            Next();
            node.Argument = ParseBindingIdentifier();

            return FinishNode(node, "RestElement");
        }

        private bool ShouldAllowYieldIdentifier
            => Match(TT["_yield"]) && !State.Strict && !State.InGenerator;

        private Node ParseBindingIdentifier() => ParseIdentifier(ShouldAllowYieldIdentifier);

        /// <summary>
        /// Parses lvalue (assignable) atom.
        /// </summary>
        private Node ParseBindingAtom()
        {
            if (State.Type == TT["_yield"] || State.Type == TT["name"])
            {
                if (State.Type == TT["_yield"] && (State.Strict || State.InGenerator))
                {
                    Unexpected();
                }

                return ParseIdentifier(true);
            }

            if (State.Type == TT["bracketL"])
            {
                var node = StartNode();

                Next();

                node.Elements = ParseBindingList(TT["bracketR"], true, true);

                return FinishNode(node, "ArrayPattern");
            }

            if (State.Type == TT["braceL"])
            {
                return ParseObj(true, ref _nullRef);
            }

            Unexpected();
            return null;
        }

        private List<Node> ParseBindingList(TokenType close, bool allowEmpty, bool allowTrailingComma)
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
                }

                if (allowEmpty && Match(TT["comma"]))
                {
                    elts.Add(null);
                }
                else if (allowTrailingComma && Eat(close))
                {
                    break;
                }
                else if (Match(TT["ellipsis"]))
                {
                    elts.Add(ParseAssignableListItemTypes(ParseRest()));
                    Expect(close);

                    break;
                }
                else
                {
                    var left = ParseMaybeDefault();

                    ParseAssignableListItemTypes(left);
                    elts.Add(ParseMaybeDefault(null, null, left));
                }
            }

            return elts;
        }

        /// <summary>
        /// Parses assignment pattern around given atom if possible.
        /// </summary>
        private Node ParseMaybeDefault(int? startPos = null, Position startLoc = null, Node left = null)
        {
            startLoc = startLoc ?? State.StartLoc;
            startPos = startPos ?? State.Start;
            left = left ?? ParseBindingAtom();

            if (!Eat(TT["eq"]))
            {
                return left;
            }

            var node = StartNodeAt((int) startPos, startLoc);

            node.Left = left;
            node.Right = ParseMaybeAssign(false, ref _nullRef);

            return FinishNode(node, "AssignmentPattern");
        }

        private void CheckLValRegular(Node expr, bool isBinding = false, Dictionary<string, bool> checkClashes = null)
        {
            switch (expr.Type)
            {
                case "Identifier":
                    if (State.Strict &&
                        (ReservedWords["strictBind"]((string) expr.Name) || ReservedWords["strict"]((string) expr.Name)))
                    {
                        Raise(expr.Start, (isBinding ? "Binding " : "Assigning to ") + expr.Name + " in strict mode");
                    }

                    if (checkClashes != null)
                    {
                        var key = $"_{expr.Name}";

                        if (checkClashes.ContainsKey(key) && checkClashes[key])
                        {
                            Raise(expr.Start, "Argument name clash in strict mode");
                        }
                        else
                        {
                            checkClashes[key] = true;
                        }
                    }

                    break;

                case "MemberExpression":
                    if (isBinding)
                    {
                        Raise(expr.Start, "Binding member expression");
                    }

                    break;

                case "ObjectPattern":
                    foreach (var propNode in expr.Properties)
                    {
                        object prop = propNode;

                        if (propNode.Type == "ObjectProperty")
                        {
                            prop = propNode.Value;
                        }

                        CheckLVal(((Node) prop), isBinding, checkClashes);
                    }

                    break;

                case "ArrayPattern":
                    foreach (var elem in expr.Elements.Where(el => el))
                    {
                        CheckLVal(elem, isBinding, checkClashes);
                    }

                    break;

                case "AssignmentPattern":
                    CheckLVal(expr.Left, isBinding, checkClashes);

                    break;

                case "RestProperty":
                case "RestElement":
                    CheckLVal(expr.Argument, isBinding, checkClashes);

                    break;

                default:
                    Raise(expr.Start, (isBinding ? "Binding" : "Assigning to") + " rvalue");

                    break;
            }
        }
    }
}
