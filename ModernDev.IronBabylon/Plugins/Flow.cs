using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        private object FlowParseTypeInitialiser(TokenType tok = null)
        {
            var oldInType = State.InType;

            State.InType = true;

            Expect(tok ?? TT["colon"]);

            var type = FlowParseType();

            State.InType = oldInType;

            return type;
        }

        private Node FlowParseDeclareClass(Node node)
        {
            Next();
            FlowParseInterfaceish(node, true);

            return FinishNode(node, "DeclareClass");
        }

        private Node FlowParseDeclareFunction(Node node)
        {
            Next();

            var id = node.Id = ParseIdentifier();
            var typeNode = StartNode();
            var typeContainer = StartNode();

            typeNode.TypeParameters = IsRelational("<") ? FlowParseTypeParameterDeclaration() : null;

            Expect(TT["parenL"]);

            var tmp = FlowParseFunctionTypeParams();

            typeNode.Params = tmp.Params;
            typeNode.Rest = tmp.Rest;

            Expect(TT["parenR"]);

            typeNode.ReturnType = FlowParseTypeInitialiser();
            typeContainer.TypeAnnotation = FinishNode(typeNode, "FunctionTypeAnnotation");
            id.TypeAnnotation = FinishNode(typeContainer, "TypeAnnotation");

            FinishNode(id, id.Type);
            Semicolon();

            return FinishNode(node, "DeclareFunction");
        }

        private Node FlowParseDeclare(Node node)
        {
            if (Match(TT["_class"]))
            {
                return FlowParseDeclareClass(node);
            }

            if (Match(TT["_function"]))
            {
                return FlowParseDeclareFunction(node);
            }

            if (Match(TT["_var"]))
            {
                return FlowParseDeclareVariable(node);
            }

            if (IsContextual("module"))
            {
                return FlowParseDeclareModule(node);
            }

            Unexpected();

            return null;
        }

        private Node FlowParseDeclareModule(Node node)
        {
            Next();

            node.Id = Match(TT["string"]) ? ParseExprAtom(ref _nullRef) : ParseIdentifier();

            var bodyNode = StartNode();
            node.Body = bodyNode;

            var body = new List<Node>();
            bodyNode.Body = body;

            Expect(TT["braceL"]);

            while (!Match(TT["braceR"]))
            {
                var node2 = StartNode();

                Next();

                body.Add(FlowParseDeclare(node2));
            }

            Expect(TT["braceR"]);

            FinishNode(bodyNode, "BlockStatement");

            return FinishNode(node, "DeclareModule");
        }

        private Node FlowParseDeclareVariable(Node node)
        {
            Next();

            node.Id = FlowParseTypeAnnotatableIdentifier();

            Semicolon();

            return FinishNode(node, "DeclareVariable");
        }

        private Node FlowParseTypeAnnotation()
        {
            var node = StartNode();

            node.TypeAnnotation = FlowParseTypeInitialiser();

            return FinishNode(node, "TypeAnnotation");
        }

        private Node FlowParseTypeAnnotatableIdentifier(bool requireTypeAnnotation = false,
            bool canBeOptionalParam = false)
        {
            var ident = ParseIdentifier();
            var isOptionalParam = false;

            if (canBeOptionalParam && Eat(TT["question"]))
            {
                Expect(TT["question"]);

                isOptionalParam = true;
            }

            if (requireTypeAnnotation || Match(TT["colon"]))
            {
                ident.TypeAnnotation = FlowParseTypeAnnotation();

                FinishNode(ident, ident.Type);
            }

            if (isOptionalParam)
            {
                ident.Optional = true;
                FinishNode(ident, ident.Type);
            }

            return ident;
        }

        private dynamic FlowParseFunctionTypeParams()
        {
            dynamic res = new ExpandoObject();
            res.Params = new List<object>();
            res.Rest = null;

            while (Match(TT["name"]))
            {
                res.Params.Add(FlowParseFunctionTypeParam());

                if (!Match(TT["parenR"]))
                {
                    Expect(TT["comma"]);
                }
            }

            if (Eat(TT["ellipsis"]))
            {
                res.Rest = FlowParseFunctionTypeParam();
            }

            return res;

            /*return new Dictionary<string, object>
            {
                {"params", prms},
                {"rest", rest}
            };*/
        }

        private Node FlowIdentToTypeAnnotation(int startPos, Position startLoc, Node node,
            Node id)
        {
            switch (id.Name as string)
            {
                case "any":
                    return FinishNode(node, "AnyTypeAnnotation");

                case "void":
                    return FinishNode(node, "VoidTypeAnnotation");

                case "bool":
                case "boolean":
                    return FinishNode(node, "BooleanTypeAnnotation");

                case "mixed":
                    return FinishNode(node, "MixedTypeAnnotation");

                case "number":
                    return FinishNode(node, "NumberTypeAnnotation");

                case "string":
                    return FinishNode(node, "StringTypeAnnotation");

                default:
                    return FlowParseGenericType(startPos, startLoc, id);
            }
        }

        private void FlowParseInterfaceish(Node node, bool allowStatic)
        {
            node.Id = ParseIdentifier();
            node.TypeParameters = IsRelational("<") ? FlowParseTypeParameterDeclaration() : null;
            node.Extends = new List<Node>();

            if (Eat(TT["_extends"]))
            {
                do
                {
                    node.Extends.Add(FlowParseInterfaceExtends());
                } while (Eat(TT["comma"]));
            }

            node.Body = FlowParseObjectType(allowStatic);
        }

        private Node FlowParseObjectType(bool allowStatic = false)
        {
            var nodeStart = StartNode();
            var isStatic = false;

            nodeStart.CallProperties = new List<Node>();
            nodeStart.Properties = new List<Node>();
            nodeStart.Indexers = new List<Node>();

            Expect(TT["braceL"]);

            while (!Match(TT["braceR"]))
            {
                var optional = false;
                var startPos = State.Start;
                var startLoc = State.StartLoc;
                var node = StartNode();

                if (allowStatic && IsContextual("static"))
                {
                    Next();

                    isStatic = true;
                }

                if (Match(TT["bracketL"]))
                {
                    nodeStart.Indexers.Add(FlowParseObjectTypeIndexer(node, isStatic));
                }
                else if (Match(TT["parenL"]) || IsRelational("<"))
                {
                    nodeStart.CallProperties.Add(FlowParseObjectTypeCallProperty(node, allowStatic));
                }
                else
                {
                    var propertyKey =
                        isStatic && Match(TT["colon"])
                            ? ParseIdentifier()
                            : FlowParseObjectPropertyKey();

                    if (IsRelational("<") || Match(TT["parenL"]))
                    {
                        nodeStart.Properties.Add(FlowParseObjectTypeMethod(startPos, startLoc, isStatic,
                            propertyKey));
                    }
                    else
                    {
                        if (Eat(TT["question"]))
                        {
                            optional = true;
                        }

                        node.Key = propertyKey;
                        node.Value = FlowParseTypeInitialiser();
                        node.Optional = optional;
                        node.Static = isStatic;

                        FlowObjectTypeSemicolon();
                        nodeStart.Properties.Add(FinishNode(node, "ObjectTypeProperty"));
                    }
                }
            }

            Expect(TT["braceR"]);

            return FinishNode(nodeStart, "ObjectTypeAnnotation");
        }

        private Node FlowParseInterfaceExtends()
        {
            var node = StartNode();

            node.Id = ParseIdentifier();
            node.TypeParameters = IsRelational("<") ? FlowParseTypeParameterInstantiation() : null;

            return FinishNode(node, "InterfaceExtends");
        }

        private Node FlowParseInterface(Node node)
        {
            FlowParseInterfaceish(node, false);

            return FinishNode(node, "InterfaceDeclaration");
        }

        private Node FlowParseTypeAlias(Node node)
        {
            node.Id = ParseIdentifier();
            node.TypeParameters = IsRelational("<") ? FlowParseTypeParameterDeclaration() : null;
            node.Right = FlowParseTypeInitialiser(TT["eq"]);

            Semicolon();

            return FinishNode(node, "TypeAlias");
        }

        private Node FlowParseTypeParameterDeclaration()
        {
            var node = StartNode();

            node.Params = new List<object>();

            ExpectRelational("<");

            while (!IsRelational(">"))
            {
                node.Params.Add(FlowParseExistentialTypeParam() ?? FlowParseTypeAnnotatableIdentifier());

                if (!IsRelational(">"))
                {
                    Expect(TT["comma"]);
                }
            }

            ExpectRelational(">");

            return FinishNode(node, "TypeParameterDeclaration");
        }

        private Node FlowParseExistentialTypeParam()
        {
            if (Match(TT["star"]))
            {
                var node = StartNode();

                Next();

                return FinishNode(node, "ExistentialTypeParam");
            }

            return null;
        }

        private Node FlowParseTypeParameterInstantiation()
        {
            var node = StartNode();
            var oldInType = State.InType;

            node.Params = new List<object>();
            State.InType = true;

            ExpectRelational("<");

            while (!IsRelational(">"))
            {
                node.Params.Add(FlowParseExistentialTypeParam() ?? FlowParseType());

                if (!IsRelational(">"))
                {
                    Expect(TT["comma"]);
                }
            }

            ExpectRelational(">");

            State.InType = oldInType;

            return FinishNode(node, "TypeParameterInstantiation");
        }

        private Node FlowParseObjectPropertyKey() => (Match(TT["num"]) || Match(TT["string"]))
            ? ParseExprAtom(ref _nullRef)
            : ParseIdentifier(true);

        private Node FlowParseObjectTypeIndexer(Node node, bool isStatic)
        {
            node.Static = isStatic;

            Expect(TT["bracketL"]);

            node.Id = FlowParseObjectPropertyKey();
            node.Key = FlowParseTypeInitialiser();

            Expect(TT["bracketR"]);

            node.Value = FlowParseTypeInitialiser();

            FlowObjectTypeSemicolon();

            return FinishNode(node, "ObjectTypeIndexer");
        }

        private Node FlowParseObjectTypeMethodish(Node node)
        {
            node.Params = new List<object>();
            node.Rest = null;
            node.TypeParameters = null;

            if (IsRelational("<"))
            {
                node.TypeParameters = FlowParseTypeParameterDeclaration();
            }

            Expect(TT["parenL"]);

            while (Match(TT["name"]))
            {
                node.Params.Add(FlowParseFunctionTypeParam());

                if (!Match(TT["parenR"]))
                {
                    Expect(TT["comma"]);
                }
            }

            if (Eat(TT["ellipsis"]))
            {
                node.Rest = FlowParseFunctionTypeParam();
            }

            Expect(TT["parenR"]);

            node.ReturnType = FlowParseTypeInitialiser();

            return FinishNode(node, "FunctionTypeAnnotation");
        }

        private Node FlowParseObjectTypeMethod(int startPos, Position startLoc, bool isStatic,
            Node key)
        {
            var node = StartNodeAt(startPos, startLoc);

            node.Value = FlowParseObjectTypeMethodish(StartNodeAt(startPos, startLoc));
            node.Static = isStatic;
            node.Key = key;
            node.Optional = false;

            FlowObjectTypeSemicolon();

            return FinishNode(node, "ObjectTypeProperty");
        }

        private Node FlowParseObjectTypeCallProperty(Node node, bool isStatic)
        {
            var valueNode = StartNode();

            node.Static = isStatic;
            node.Value = FlowParseObjectTypeMethodish(valueNode);


            FlowObjectTypeSemicolon();

            return FinishNode(node, "ObjectTypeCallProperty");
        }

        private Node FlowParseFunctionTypeParam()
        {
            var optional = false;
            var node = StartNode();

            node.Name = ParseIdentifier();

            if (Eat(TT["question"]))
            {
                optional = true;
            }

            node.Optional = optional;
            node.TypeAnnotation = FlowParseTypeInitialiser();

            return FinishNode(node, "FunctionTypeParam");
        }

        private void FlowObjectTypeSemicolon()
        {
            if (!Eat(TT["semi"]) && !Eat(TT["comma"]) && !Match(TT["braceR"]))
            {
                Unexpected();
            }
        }

        private Node FlowParseGenericType(int startPos, Position startLoc, Node id)
        {
            var node = StartNodeAt(startPos, startLoc);

            node.TypeParameters = null;
            node.Id = id;

            while (Eat(TT["dot"]))
            {
                var node2 = StartNodeAt(startPos, startLoc);

                node2.Qualification = node.Id;
                node2.Id = ParseIdentifier();
                node.Id = FinishNode(node2, "QualifiedTypeIdentifier");
            }

            if (IsRelational("<"))
            {
                node.TypeParameters = FlowParseTypeParameterInstantiation();
            }

            return FinishNode(node, "GenericTypeAnnotation");
        }

        private Node FlowParseTypeofType()
        {
            var node = StartNode();

            Expect(TT["_typeof"]);

            node.Argument = FlowParsePrimaryType() as Node;

            return FinishNode(node, "TypeofTypeAnnotation");
        }

        private Node FlowParseTupleType()
        {
            var node = StartNode();

            node.Types = new List<object>();

            Expect(TT["bracketL"]);

            while (State.Position < Input.Length && !Match(TT["bracketR"]))
            {
                node.Types.Add(FlowParseType());

                if (Match(TT["bracketR"]))
                {
                    break;
                }

                Expect(TT["comma"]);
            }

            Expect(TT["bracketR"]);

            return FinishNode(node, "TupleTypeAnnotation");
        }

        /// <summary>
        /// The parsing of types roughly parallels the parsing of expressions, and
        /// primary types are kind of like primary expressions...they're the
        /// primitives with which other types are constructed.
        /// </summary>
        private object FlowParsePrimaryType()
        {
            var startPos = State.Start;
            var startLoc = State.StartLoc;
            var node = StartNode();
            var isGroupedType = false;
            var ct = State.Type;

            if (ct == TT["name"])
            {
                return FlowIdentToTypeAnnotation(startPos, startLoc, node, ParseIdentifier());
            }

            if (ct == TT["braceL"])
            {
                return FlowParseObjectType();
            }

            if (ct == TT["bracketL"])
            {
                return FlowParseTupleType();
            }

            if (ct == TT["relational"] || ct == TT["parenL"])
            {
                dynamic tmp;

                if (ct == TT["relational"] && (string) State.Value == "<")
                {
                    node.TypeParameters = FlowParseTypeParameterDeclaration();

                    Expect(TT["parenL"]);

                    tmp = FlowParseFunctionTypeParams();
                    node.Params = tmp.Params;
                    node.Rest = tmp.Rest;

                    Expect(TT["parenR"]);
                    Expect(TT["arrow"]);

                    node.ReturnType = FlowParseType();

                    return FinishNode(node, "FunctionTypeAnnotation");
                }

                Next();

                if (!Match(TT["parenR"]) && !Match(TT["ellipsis"]))
                {
                    if (Match(TT["name"]))
                    {
                        var token = Lookahead().Type;

                        isGroupedType = token != TT["question"] && token != TT["colon"];
                    }
                    else
                    {
                        isGroupedType = true;
                    }
                }

                if (isGroupedType)
                {
                    var type = FlowParseType();

                    Expect(TT["parenR"]);

                    if (Eat(TT["arrow"]))
                    {
                        Raise(node.Start, "Unexpected token =>. It looks like " +
                                               "you are trying to write a function type, but you ended up " +
                                               "writing a grouped type followed by an =>, which is a syntax " +
                                               "error. Remember, function type parameters are named so function " +
                                               "types look like (name1: type1, name2: type2) => returnType. You " +
                                               "probably wrote (type1) => returnType");
                    }

                    return type;
                }

                tmp = FlowParseFunctionTypeParams();
                node.Params = tmp.Params;
                node.Rest = tmp.Rest;

                Expect(TT["parenR"]);
                Expect(TT["arrow"]);

                node.ReturnType = FlowParseType();
                node.TypeParameters = null;

                return FinishNode(node, "FunctionTypeAnnotation");
            }

            if (ct == TT["string"])
            {
                node.Value = State.Value;

                AddExtra(node, "rawValue", node.Value);
                AddExtra(node, "raw", Input.Slice(State.Start, State.End));

                Next();

                return FinishNode(node, "StringLiteralTypeAnnotation");
            }

            if (ct == TT["_true"] || ct == TT["_false"])
            {
                node.Value = Match(TT["_true"]);

                Next();

                return FinishNode(node, "BooleanLiteralTypeAnnotation");
            }

            if (ct == TT["num"])
            {
                node.Value = State.Value;

                AddExtra(node, "rawValue", node.Value);
                AddExtra(node, "raw", Input.Slice(State.Start, State.End));

                Next();

                return FinishNode(node, "NumericLiteralTypeAnnotation");
            }

            if (ct == TT["_null"])
            {
                node.Value = Match(TT["_null"]);

                Next();

                return FinishNode(node, "NullLiteralTypeAnnotation");
            }

            if (ct == TT["_this"])
            {
                node.Value = Match(TT["_this"]);

                Next();

                return FinishNode(node, "ThisTypeAnnotation");
            }

            Unexpected();

            return null;
        }

        private object FlowParsePostfixType()
        {
            var node = StartNode();
            var type = node.ElementType = FlowParsePrimaryType();

            if (Match(TT["bracketL"]))
            {
                Expect(TT["bracketL"]);
                Expect(TT["bracketR"]);

                return FinishNode(node, "ArrayTypeAnnotation");
            }

            return type;
        }

        private object FlowParsePrefixType()
        {
            var node = StartNode();

            if (Eat(TT["question"]))
            {
                node.TypeAnnotation = FlowParsePrefixType();

                return FinishNode(node, "NullableTypeAnnotation");
            }

            return FlowParsePostfixType();
        }

        private object FlowParseIntersectionType()
        {
            var node = StartNode();
            var type = FlowParsePrefixType();

            node.Types = new List<object> {type};

            while (Eat(TT["bitwiseAND"]))
            {
                node.Types.Add(FlowParsePrefixType());
            }

            return node.Types.Count == 1 ? type : FinishNode(node, "IntersectionTypeAnnotation");
        }

        private object FlowParseUnionType()
        {
            var node = StartNode();
            var type = FlowParseIntersectionType();

            node.Types = new List<object> {type};

            while (Eat(TT["bitwiseOR"]))
            {
                node.Types.Add(FlowParseIntersectionType());
            }

            return node.Types.Count == 1 ? type : FinishNode(node, "UnionTypeAnnotation");
        }

        private object FlowParseType()
        {
            var oldInType = State.InType;

            State.InType = true;

            var type = FlowParseUnionType();

            State.InType = oldInType;

            return type;
        }

        #region Plugin overrides

        private void ParseFunctionBody(Node node, bool allowExpression = false)
        {
            if (Match(TT["colon"]) && !allowExpression)
            {
                node.ReturnType = FlowParseTypeAnnotation();
            }

            ParseFunctionBodyRegular(node, allowExpression);
        }

        private Node ParseStatement(bool declaration, bool topLevel = false)
        {
            if (State.Strict && Match(TT["name"]) && (string) State.Value == "interface")
            {
                var node = StartNode();

                Next();

                return FlowParseInterface(node);
            }

            return ParseStatementRegular(declaration, topLevel);
        }

        /// <summary>
        /// declares, interfaces and type aliases
        /// </summary>
        private Node ParseExpressionStatement(Node node, Node expr)
        {
            if (expr.Type == "Identifier")
            {
                if ((string) expr.Name == "declare")
                {
                    if (Match(TT["_class"]) || Match(TT["name"]) || Match(TT["_function"]) || Match(TT["_var"]))
                    {
                        return FlowParseDeclare(node);
                    }
                }
                else if (Match(TT["name"]))
                {
                    if ((string) expr.Name == "interface")
                    {
                        return FlowParseInterface(node);
                    }

                    if ((string) expr.Name == "type")
                    {
                        return FlowParseTypeAlias(node);
                    }
                }
            }

            return ParseExpressionStatementRegular(node, expr);
        }

        private bool ShouldParseExportDeclaration => IsContextual("type") || ShouldParseExportDeclarationRegular;

        private Node ParseParenItem(Node node = null, int? startPos = null, Position startLoc = null,
            bool forceArrow = false)
        {
            var canBeArrow = State.PotentialArrowAt = startPos ?? 0;

            if (Match(TT["colon"]))
            {
                var typeCastNode = StartNodeAt(startPos ?? 0, startLoc);

                typeCastNode.Expression = node;
                typeCastNode.TypeAnnotation = FlowParseTypeAnnotation();

                if (forceArrow && !Match(TT["arrow"]))
                {
                    Unexpected();
                }

                if (canBeArrow.ToBool() && Eat(TT["arrow"]))
                {
                    var prms = node?.Type == "SequenceExpression" ? node.Expressions : new List<Node> {node};
                    var func = ParseArrowExpression(StartNodeAt(startPos ?? 0, startLoc), prms);

                    func.ReturnType = typeCastNode.TypeAnnotation;

                    return func;
                }
                return FinishNode(typeCastNode, "TypeCastExpression");
            }
            return node;
        }

        private Node ParseExport(Node node)
        {
            node = ParseExportRegular(node);

            if (node.Type == "ExportNamedDeclaration")
            {
                node.ExportKind = node.ExportKind ?? "value";
            }

            return node;
        }

        private Node ParseExportDeclaration(Node node)
        {
            if (IsContextual("type"))
            {
                node.ExportKind = "type";

                var declarationNode = StartNode();

                Next();

                if (Match(TT["braceL"]))
                {
                    node.Specifiers = ParseExportSpecifiers();


                    ParseExportFrom(node);

                    return null;
                }

                return FlowParseTypeAlias(declarationNode);
            }

            return ParseExportDeclarationRegular();
        }

        private void ParseClassId(Node node, bool isStatement, bool optionalId = false)
        {
            ParseClassIdRegular(node, isStatement, optionalId);

            if (IsRelational("<"))
            {
                node.TypeParameters = FlowParseTypeParameterDeclaration();
            }
        }

        /// <summary>
        /// don't consider `void` to be a keyword as then it'll use the void token type
        /// and set startExpr
        /// </summary>
        protected override bool IsKeyword(string name)
        {
            if (State.InType && name == "void")
            {
                return false;
            }

            return base.IsKeyword(name);
        }

        protected override TokenType ReadToken(int? code)
            => State.InType && (code == 62 || code == 60) ? FinishOp(TT["relational"], 1) : ReadTokenJSX(code);

        /*
        TODO:
        instance.extend("jsx_readToken", function (inner) {
    return function () {
      if (!this.state.inType) return inner.call(this);
    };
  });
    */

        private static Node TypeCastToParameter(Node node)
        {
            ((Node) node.Expression).TypeAnnotation = node.TypeAnnotation;

            return (Node) node.Expression;
        }

        private Node ToAssignable(Node node, bool isBinding = false)
            => node.Type == "TypeCastExpression" ? TypeCastToParameter(node) : ToAssignableRegular(node, isBinding);

        /// <summary>
        /// turn type casts that we found in function parameter head into type annotated params
        /// </summary>
        private List<Node> ToAssignableList(List<Node> exprList, bool isBinding)
        {
            for (var i = 0; i < exprList.Count; i++)
            {
                var expr = exprList[i];

                if (expr && expr.Type == "TypeCastExpression")
                {
                    exprList[i] = TypeCastToParameter(expr);
                }
            }

            return ToAssignableListRegular(exprList, isBinding);
        }

        /// <summary>
        /// // this is a list of nodes, from something like a call expression, we need to filter the
        /// type casts that we've found that are illegal in this context
        /// </summary>
        protected List<Node> ToReferencedList(List<Node> exprList)
        {
            foreach (
                var expr in
                    exprList.Where(expr => expr && expr._ExprListItem && expr.Type == "TypeCastExpression"))
            {
                Raise(expr.Start, "Unexpected type cast");
            }

            return exprList;
        }

        /// <summary>
        /// parse an item inside a expression list eg. `(NODE, NODE)` where NODE represents
        /// the position where this function is cal;ed
        /// </summary>
        private Node ParseExprListItem(bool allowEmpty, ref int? refShorthandDefaultPos)
        {
            var container = StartNode();
            var node = ParseExprListItemRegular(allowEmpty, ref refShorthandDefaultPos);

            if (Match(TT["colon"]))
            {
                container._ExprListItem = true;
                container.Expression = node;
                container.TypeAnnotation = FlowParseTypeAnnotation();

                return FinishNode(container, "TypeCastExpression");
            }

            return node;
        }

        private void CheckLVal(Node node, bool isBinding = false, Dictionary<string, bool> checkClashes = null)
        {
            if (node.Type != "TypeCastExpression")
            {
                CheckLValRegular(node, isBinding, checkClashes);
            }
        }

        /// <summary>
        /// parse class property type annotations
        /// </summary>
        private Node ParseClassProperty(Node node)
        {
            if (Match(TT["colon"]))
            {
                node.TypeAnnotation = FlowParseTypeAnnotation();
            }

            return ParseClassPropertyRegular(node);
        }

        /// <summary>
        /// determine whether or not we're currently in the position where a class property would appear
        /// </summary>
        private bool IsClassProperty => Match(TT["colon"]) || IsClassPropertyRegular;

        /// <summary>
        /// parse type parameters for class methods
        /// </summary>
        private void ParseClassMethod(Node classBody, Node method, bool isGenerator, bool isAsync)
        {
            if (IsRelational("<"))
            {
                method.TypeParameters = FlowParseTypeParameterDeclaration();
            }

            ParseClassMethodRegular(classBody, method, isGenerator, isAsync);
        }

        /// <summary>
        /// parse a the super class type parameters and implements
        /// </summary>
        private void ParseClassSuper(Node node)
        {
            ParseClassSuperRegular(node);

            if (node.SuperClass && IsRelational("<"))
            {
                node.SuperTypeParameters = FlowParseTypeParameterInstantiation();
            }

            if (IsContextual("implements"))
            {
                Next();

                var implements = node.Implements = new List<Node>();

                do
                {
                    var n = StartNode();

                    n.Id = ParseIdentifier();
                    node.TypeParameters = IsContextual("<") ? FlowParseTypeParameterInstantiation() : null;

                    implements.Add(FinishNode(n, "ClassImplements"));
                } while (Eat(TT["comma"]));
            }
        }

        /// <summary>
        /// parse type parameters for object method shorthand
        /// </summary>
        private void ParseObjPropValue(Node prop, int startPos, Position startLoc, bool isGenerator, bool isAsync, bool isPattern, ref int? refShorthandDefaultPos)
        {
            Node typeParameters = null;

            if (IsRelational("<"))
            {
                typeParameters = FlowParseTypeParameterDeclaration();

                if (!Match(TT["parenL"]))
                {
                    Unexpected();
                }
            }

            ParseObjPropValueRegular(prop, startPos, startLoc, isGenerator, isAsync, isPattern,
                ref refShorthandDefaultPos);

            if (typeParameters)
            {
                if (prop.Value != null)
                {
                    ((Node) prop.Value).TypeParameters = typeParameters;
                }
                else
                {
                    prop.TypeParameters = typeParameters;
                }
            }
        }

        protected Node ParseAssignableListItemTypes(Node param)
        {
            if (Eat(TT["question"]))
            {
                param.Optional = true;
            }

            if (Match(TT["colon"]))
            {
                param.TypeAnnotation = FlowParseTypeAnnotation();
            }

            FinishNode(param, param.Type);

            return param;
        }

        private void ParseImportSpecifiers(Node node)
        {
            node.ImportKind = "value";

            string kind = null;

            if (Match(TT["_typeof"]))
            {
                kind = "typeof";
            }
            else if (IsContextual("type"))
            {
                kind = "type";
            }

            if (kind != null)
            {
                var lh = Lookahead();

                if ((lh.Type == TT["name"] && (string) lh.Value != "from") || lh.Type == TT["braceL"] ||
                    lh.Type == TT["star"])
                {
                    Next();

                    node.ImportKind = kind;
                }
            }

            ParseImportSpecifiersRegular(node);
        }

        /// <summary>
        /// parse function type parameters
        /// </summary>
        private void ParseFunctionParams(Node node)
        {
            if (IsRelational("<"))
            {
                node.TypeParameters = FlowParseTypeParameterDeclaration();
            }

            ParseFunctionParamsRegular(node);
        }

        /// <summary>
        /// parse flow type annotations on variable declarator heads - let foo: string = bar
        /// </summary>
        private void ParseVarHead(Node decl)
        {
            ParseVarHeadRegular(decl);

            if (Match(TT["colon"]))
            {
                decl.Id.TypeAnnotation = FlowParseTypeAnnotation();

                FinishNode(decl.Id, decl.Id.Type);
            }
        }

        /// <summary>
        /// parse the return type of an async arrow function - let foo = (async (): number => {});
        /// </summary>
        private Node ParseAsyncArrowFromCallExpression(Node node, Node call)
        {
            if (Match(TT["colon"]))
            {
                node.ReturnType = FlowParseTypeAnnotation();
            }

            return ParseAsyncArrowFromCallExpressionRegular(node, call);
        }

        private bool ShouldParseAsyncArrow => Match(TT["colon"]) || ShouldParseAsyncArrowRegular;

        /// <summary>
        /// handle return types for arrow functions
        /// </summary>
        private Node ParseParenAndDistinguishExpression(int? startPos, Position startLoc, bool canBeArrow,
            bool isAsync = false)
        {
            startPos = startPos ?? State.Start;
            startLoc = startLoc ?? State.StartLoc;

            if (canBeArrow && Lookahead().Type == TT["parenR"])
            {
                Expect(TT["parenL"]);
                Expect(TT["parenR"]);

                var node = StartNodeAt((int) startPos, startLoc);

                if (Match(TT["colon"]))
                {
                    node.ReturnType = FlowParseTypeAnnotation();
                }

                Expect(TT["arrow"]);

                return ParseArrowExpression(node, new List<Node>(), isAsync);
            }
            else
            {
                var node = ParseParenAndDistinguishExpressionRegular(startPos, startLoc, canBeArrow, isAsync);

                if (Match(TT["colon"]))
                {
                    var state = State.Clone();

                    try
                    {
                        return ParseParenItem(node, startPos, startLoc, true);
                    }
                    catch (SyntaxErrorException)
                    {
                        State = state;

                        return node;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message, ex);
                    }
                }

                return node;
            }
        }

        #endregion
    }
}