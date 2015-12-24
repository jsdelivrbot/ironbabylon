using System.Collections.Generic;
using System.Text.RegularExpressions;
using static ModernDev.IronBabylon.Util;
using static System.Convert;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        private readonly Regex _hexNumber = new Regex(@"^[\da-fA-F]+$");
        private readonly Regex _decimalNumber = new Regex(@"^\d+$");

        private static Dictionary<string, TokenContext> TC => TokenContext.Types;

        private TokenType JSXReadToken()
        {
            var outt = "";
            var chunkStart = State.Position;

            for (;;)
            {
                if (State.Position >= Input.Length)
                {
                    Raise(State.Start, "Unterminated JSX contents");
                }

                var ch = Input.CharCodeAt(State.Position);

                switch (ch)
                {
                    case 60:
                    case 123:
                        if (State.Position == State.Start)
                        {
                            if (ch == 60 && State.ExprAllowed)
                            {
                                ++State.Position;

                                return FinishToken(TT["jsxTagStart"]);
                            }

                            return GetTokenFromCode(ch);
                        }

                        outt += Input.Slice(chunkStart, State.Position);

                        return FinishToken(TT["jsxText"], outt);

                    case 38:
                        outt += Input.Slice(chunkStart, State.Position);
                        outt += JSXReadEntity();
                        chunkStart = State.Position;

                        break;

                    default:
                        if (IsNewLine(ch))
                        {
                            outt += Input.Slice(chunkStart, State.Position);
                            outt += JSXReadNewLine(true);
                            chunkStart = State.Position;
                        }
                        else
                        {
                            ++State.Position;
                        }

                        break;
                }
            }
        }

        private string JSXReadNewLine(bool normalizeCRLF)
        {
            var ch = Input.CharCodeAt(State.Position);
            var outt = "";

            ++State.Position;

            if (ch == 13 && Input.CharCodeAt(State.Position) == 10)
            {
                ++State.Position;
                outt += normalizeCRLF ? "\n" : "\r\n";
            }
            else
            {
                outt = ch.ToString();
            }

            ++State.CurLine;
            State.LineStart = State.Position;

            return outt;
        }

        private TokenType JSXReadString(int quote)
        {
            var outt = "";
            var chunkStart = ++State.Position;

            for (;;)
            {
                if (State.Position >= Input.Length)
                {
                    Raise(State.Start, "Unterminated string constant");
                }

                var ch = Input.CharCodeAt(State.Position);

                if (ch == quote)
                {
                    break;
                }

                if (ch == 38)
                {
                    outt += Input.Slice(chunkStart, State.Position);
                    outt += JSXReadEntity();
                    chunkStart = State.Position;
                } else if (IsNewLine(ch))
                {
                    outt += Input.Slice(chunkStart, State.Position);
                    outt += JSXReadNewLine(false);
                    chunkStart = State.Position;
                }
                else
                {
                    ++State.Position;
                }
            }

            outt += Input.Slice(chunkStart, State.Position++);

            return FinishToken(TT["string"], outt);
        }

        private string JSXReadEntity()
        {
            var str = "";
            var count = 0;
            string entity = null;
            var startPos = ++State.Position;

            while (State.Position < Input.Length && count++ < 10)
            {
                var ch = (char)Input.CharCodeAt(State.Position++);

                if (ch == ';')
                {
                    if (str[0] == '#')
                    {
                        if (str[1] == 'x')
                        {
                            str = str.Substr(2);

                            if (_hexNumber.IsMatch(str))
                            {
                                entity = ToInt32(str, 16).ToString();
                            }
                        }
                        else
                        {
                            str = str.Substring(1);

                            if (_decimalNumber.IsMatch(str))
                            {
                                entity = ToInt32(str, 10).ToString();
                            }
                        }
                    }
                    else
                    {
                        entity = XHTML.Entities.ContainsKey(str) ? XHTML.Entities[str].ToString() : string.Empty;
                    }

                    break;
                }

                str += ch;
            }

            if (string.IsNullOrEmpty(entity))
            {
                State.Position = startPos;

                return "&";
            }

            return entity;
        }

        /// <summary>
        /// Read a JSX identifier (valid tag or attribute name).
        ///
        /// Optimized version since JSX identifiers can"t contain
        /// escape characters and so can be read as single slice.
        /// Also assumes that first character was already checked
        /// by isIdentifierStart in readToken.
        /// </summary>
        private TokenType JSXReadWord()
        {
            int ch;
            var start = State.Position;

            do
            {
                ch = Input.CharCodeAt(++State.Position);
            } while (IsIdentifierChar(ch) || ch == 45);

            return FinishToken(TT["jsxName"], Input.Slice(start, State.Position));
        }

        /// <summary>
        /// Transforms JSX element name to string.
        /// </summary>
        private static string GetQualifiedJSXName(Node obj)
        {
            switch (obj.Type)
            {
                case "JSXIdentifier":
                    return obj.Name as string;
                case "JSXNamespacedName":
                    return (string) obj.Namespace.Name + ':' + ((Node) obj.Name).Name;
                case "JSXMemberExpression":
                    return GetQualifiedJSXName(obj.Object) + '.' + GetQualifiedJSXName(obj.Property);
            }

            return null;
        }

        /// <summary>
        /// Parse next token as JSX identifier
        /// </summary>
        private Node JSXParseIdentifier()
        {
            var node = StartNode();

            if (Match(TT["jsxName"]))
            {
                node.Name = State.Value;
            } else if (!string.IsNullOrEmpty(State.Type.Keyword))
            {
                node.Name = State.Type.Keyword;
            }
            else
            {
                Unexpected();
            }

            Next();

            return FinishNode(node, "JSXIdentifier");
        }

        /// <summary>
        /// Parse namespaced identifier.
        /// </summary>
        private Node JSXParseNamespacedName()
        {
            var start = State.Start;
            var startLoc = State.StartLoc;
            var name = JSXParseIdentifier();

            if (!Eat(TT["colon"]))
            {
                return name;
            }

            var node = StartNodeAt(start, startLoc);

            node.Namespace = name;
            node.Name = JSXParseIdentifier();

            return FinishNode(node, "JSXNamespacedName");
        }

        /// <summary>
        /// Parses element name in any form - namespaced, member or single identifier.
        /// </summary>
        private Node JSXParseElementName()
        {
            var start = State.Start;
            var startLoc = State.StartLoc;
            var node = JSXParseNamespacedName();

            while (Eat(TT["dot"]))
            {
                var newNode = StartNodeAt(start, startLoc);

                newNode.Object = node;
                newNode.Property = JSXParseIdentifier();
                node = FinishNode(newNode, "JSXMemberExpression");
            }

            return node;
        }

        /// <summary>
        /// Parses any type of JSX attribute value.
        /// </summary>
        private Node JSXParseAttributeValue()
        {
            Node node;

            if (State.Type == TT["braceL"])
            {
                node = JSXParseExpressionContainer();

                if (((Node) node.Expression).Type == "JSXEmptyExpression")
                {
                    Raise(node.Start, "JSX attributes must only be assigned a non-empty expression");
                }
                else
                {
                    return node;
                }
            }

            if (State.Type == TT["jsxTagStart"] || State.Type == TT["string"])
            {
                node = ParseExprAtom(ref _nullRef);
                node.Extra = null;

                return node;
            }

            Raise(State.Start, "JSX value should be either an expression or a quoted JSX text");

            return null;
        }

        /// <summary>
        /// JSXEmptyExpression is unique type since it doesn't actually parse anything,
        /// and so it should start at the end of last read token (left brace) and finish
        /// at the beginning of the next one (right brace).
        /// </summary>
        private Node JSXParseEmptyExpression()
            => FinishNodeAt(
                StartNodeAt(State.LastTokenEnd, State.LastTokenEndLoc),
                "JSXEmptyExpression",
                State.Start,
                State.StartLoc
                );

        /// <summary>
        /// Parses JSX expression enclosed into curly brackets.
        /// </summary>
        private Node JSXParseExpressionContainer()
        {
            var node = StartNode();

            Next();

            node.Expression = Match(TT["braceR"]) ? JSXParseEmptyExpression() : ParseExpression(false, ref _nullRef);

            Expect(TT["braceR"]);

            return FinishNode(node, "JSXExpressionContainer");
        }

        /// <summary>
        /// Parses following JSX attribute name-value pair.
        /// </summary>
        private Node JSXParseAttribute()
        {
            var node = StartNode();

            if (Eat(TT["braceL"]))
            {
                Expect(TT["ellipsis"]);

                node.Argument = ParseMaybeAssign(false, ref _nullRef);

                Expect(TT["braceR"]);

                return FinishNode(node, "JSXSpreadAttribute");
            }

            node.Name = JSXParseNamespacedName();
            node.Value = Eat(TT["eq"]) ? JSXParseAttributeValue() : null;

            return FinishNode(node, "JSXAttribute");
        }

        /// <summary>
        /// Parses JSX opening tag starting.
        /// </summary>
        private Node JSXParseOpeningElementAt(int startPos, Position startLoc)
        {
            var node = StartNodeAt(startPos, startLoc);

            node.Attributes = new List<Node>();
            node.Name = JSXParseElementName();

            while (!Match(TT["slash"]) && !Match(TT["jsxTagEnd"]))
            {
                node.Attributes.Add(JSXParseAttribute());
            }

            node.SelfClosing = Eat(TT["slash"]);

            Expect(TT["jsxTagEnd"]);

            return FinishNode(node, "JSXOpeningElement");
        }

        /// <summary>
        /// Parses JSX closing tag starting.
        /// </summary>
        private Node JSXParseClosingElementAt(int startPos, Position startLoc)
        {
            var node = StartNodeAt(startPos, startLoc);

            node.Name = JSXParseElementName();

            Expect(TT["jsxTagEnd"]);

            return FinishNode(node, "JSXClosingElement");
        }

        private Node JSXParseElementAt(int startPos, Position startLoc)
        {
            var node = StartNodeAt(startPos, startLoc);
            var children = new List<Node>();
            var openningElement = JSXParseOpeningElementAt(startPos, startLoc);
            Node closingElement = null;

            if (!openningElement.SelfClosing)
            {
                for (;;)
                {
                    if (State.Type == TT["jsxTagStart"])
                    {
                        startPos = State.Start;
                        startLoc = State.StartLoc;

                        Next();

                        if (Eat(TT["slash"]))
                        {
                            closingElement = JSXParseClosingElementAt(startPos, startLoc);

                            break;
                        }

                        children.Add(JSXParseElementAt(startPos, startLoc));
                    } else if (State.Type == TT["jsxText"])
                    {
                        children.Add(ParseExprAtom(ref _nullRef));
                    } else if (State.Type == TT["braceL"])
                    {
                        children.Add(JSXParseExpressionContainer());
                    }
                    else
                    {
                        Unexpected();
                    }
                }

                if (GetQualifiedJSXName(closingElement?.Name as Node) != GetQualifiedJSXName(openningElement.Name as Node))
                {
                    Raise(closingElement?.Start ?? 0,
                        "Expected corresponding JSX closing tag for <" + GetQualifiedJSXName(openningElement.Name as Node) +
                        ">");
                }
            }
            
            node.OpeningElement = openningElement;
            node.ClosingElement = closingElement;
            node.Children = children;

            if (Match(TT["relational"]) && (string) State.Value == "<")
            {
                Raise(State.Start, "Adjacent JSX elements must be wrapped in an enclosing tag");
            }

            return FinishNode(node, "JSXElement");
        }

        private Node JSXParseElement()
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;

            Next();

            return JSXParseElementAt(startPos, startLoc);
        }

        #region Plugin overrides

        private Node ParseExprAtom(ref int? refShorthandDefaultPos)
        {
            if (Match(TT["jsxText"]))
            {
                var node = ParseLiteral(State.Value, "JSXText");

                node.Extra = null;

                return node;
            }

            if (Match(TT["jsxTagStart"]))
            {
                return JSXParseElement();
            }

            return ParseExprAtomRegular(ref refShorthandDefaultPos);
        }

        private TokenType ReadTokenJSX(int? code)
        {
            var context = CurrentContext;

            if (context == TC["j_expr"])
            {
                return JSXReadToken();
            }

            if (context == TC["j_oTag"] || context == TC["j_cTag"])
            {
                if (IsIdentifierStart(code))
                {
                    return JSXReadWord();
                }

                if (code == 62)
                {
                    ++State.Position;

                    return FinishToken(TT["jsxTagEnd"]);
                }

                if ((code == 34 || code == 39) && context == TC["j_oTag"])
                {
                    return JSXReadString((int) code);
                }
            }

            if (code == 60 && State.ExprAllowed)
            {
                ++State.Position;

                return FinishToken(TT["jsxTagStart"]);
            }

            return base.ReadToken(code);
        }

        protected override void UpdateContext(TokenType prevType)
        {
            if (Match(TT["braceL"]))
            {
                var curContext = CurrentContext;

                if (curContext == TC["j_oTag"])
                {
                    State.Context.Add(TC["b_expr"]);
                } else if (curContext == TC["j_expr"])
                {
                    State.Context.Add(TC["b_tmpl"]);
                }
                else
                {
                    base.UpdateContext(prevType);
                }

                State.ExprAllowed = true;
            } else if (Match(TT["slash"]) && prevType == TT["jsxTagStart"])
            {
                State.Context.RemoveRange(State.Context.Count - 2, 2);
                State.Context.Add(TC["j_cTag"]);
                State.ExprAllowed = false;
            }
            else
            {
                base.UpdateContext(prevType);
            }
        }

        #endregion
    }
}
