using System;
using System.Collections.Generic;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
    public class TokenType
    {
        #region Class constructors

        private TokenType(string label, TokenTypeConfig config = null)
        {
            Label = label;
            Keyword = config?.Keyword;
            BeforeExpr = config?.BeforeExpr ?? false;
            StartsExpr = config?.StartsExpr ?? false;
            RightAssociative = config?.RightAssociative ?? false;
            IsLoop = config?.IsLoop ?? false;
            IsAssign = config?.IsAssign ?? false;
            Prefix = config?.Prefix ?? false;
            Postfix = config?.Postfix ?? false;
            Binop = config?.Binop;
            UpdateContext = null;
        }

        #endregion

        #region Class properties

        private static readonly TokenTypeConfig BeforeExprConfig = new TokenTypeConfig { BeforeExpr = true };
        private static readonly TokenTypeConfig StartsExprConfig = new TokenTypeConfig { StartsExpr = true };

        #endregion

        #region Class fields

        private static Dictionary<string, TokenContext> TC => TokenContext.Types;

        public string Label { get; set; }

        public string Keyword { get; private set; }

        public bool BeforeExpr { get; private set; }

        public bool StartsExpr { get; private set; }

        public bool RightAssociative { get; private set; }

        public bool IsLoop { get; private set; }

        public bool IsAssign { get; private set; }

        public bool Prefix { get; private set; }

        public bool Postfix { get; set; }

        public int? Binop { get; private set; }

        public Action<Tokenizer, TokenType> UpdateContext { get; private set; }

        public static readonly Dictionary<string, TokenType> Types = new Dictionary<string, TokenType>
        {
            {"num", new TokenType("num", StartsExprConfig)},
            {"regexp", new TokenType("regexp", StartsExprConfig)},
            {"string", new TokenType("string", StartsExprConfig)},
            {"name", new TokenType("name", StartsExprConfig) {UpdateContext = NameUpdateContext}},
            {"eof", new TokenType("eof")},

            // Punctuation token types.
            {"bracketL", new TokenType("[", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})},
            {"bracketR", new TokenType("]")},
            {
                "braceL",
                new TokenType("{", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})
                {
                    UpdateContext = BraceLUpdateContext
                }
            },
            {"braceR", new TokenType("}") {UpdateContext = ParenRBraceRUpdateContext}},
            {
                "parenL",
                new TokenType("(", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})
                {
                    UpdateContext = ParenLUpdateContext
                }
            },
            {"parenR", new TokenType(")") {UpdateContext = ParenRBraceRUpdateContext}},
            {"comma", new TokenType(",", BeforeExprConfig)},
            {"semi", new TokenType(";", BeforeExprConfig)},
            {"colon", new TokenType(":", BeforeExprConfig)},
            {"doubleColon", new TokenType("::", BeforeExprConfig)},
            {"dot", new TokenType(".")},
            {"question", new TokenType("?", BeforeExprConfig)},
            {"arrow", new TokenType("=>", BeforeExprConfig)},
            {"template", new TokenType("template")},
            {"ellipsis", new TokenType("...", BeforeExprConfig)},
            {"backQuote", new TokenType("`", BeforeExprConfig) {UpdateContext = BackQuoteUpdateContext}},
            {
                "dollarBraceL",
                new TokenType("${", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})
                {
                    UpdateContext = DollarBraceLUpdateContext
                }
            },
            {"at", new TokenType("@")},

            // Operators. These carry several kinds of properties to help the
            // parser use them properly (the presence of these properties is
            // what categorizes them as operators).
            //
            // `binop`, when present, specifies that this operator is a binary
            // operator, and will refer to its precedence.
            //
            // `prefix` and `postfix` mark the operator as a prefix or postfix
            // unary operator.
            //
            // `isAssign` marks all of `=`, `+=`, `-=` etcetera, which act as
            // binary operators with a very low precedence, that should result
            // in AssignmentExpression nodes.
            {"eq", new TokenType("=", new TokenTypeConfig {BeforeExpr = true, IsAssign = true})},
            {"assign", new TokenType("_=", new TokenTypeConfig {BeforeExpr = true, IsAssign = true})},
            {
                "incDec",
                new TokenType("++/--", new TokenTypeConfig {Prefix = true, Postfix = true, StartsExpr = true})
                {
                    UpdateContext = IncDecUpdateContext
                }
            },
            {
                "prefix",
                new TokenType("prefix", new TokenTypeConfig {BeforeExpr = true, Prefix = true, StartsExpr = true})
            },
            {"logicalOR", BinopToken("||", 1)},
            {"logicalAND", BinopToken("&&", 2)},
            {"bitwiseOR", BinopToken("|", 3)},
            {"bitwiseXOR", BinopToken("^", 4)},
            {"bitwiseAND", BinopToken("&", 5)},
            {"equality", BinopToken("==/!=", 6)},
            {"relational", BinopToken("</>", 7)},
            {"bitShift", BinopToken("<</>>", 8)},
            {
                "plusMin",
                new TokenType("+/-",
                    new TokenTypeConfig {BeforeExpr = true, Binop = 9, Prefix = true, StartsExpr = true})
            },
            {"modulo", BinopToken("%", 10)},
            {"star", BinopToken("*", 10)},
            {"slash", BinopToken("/", 10)},
            {
                "exponent",
                new TokenType("**", new TokenTypeConfig {BeforeExpr = true, Binop = 11, RightAssociative = true})
            },
            {"jsxName", new TokenType("jsxName")},
            {"jsxText", new TokenType("jsxText", new TokenTypeConfig {BeforeExpr = true})},
            {"jsxTagStart", new TokenType("jsxTagStart") {UpdateContext = JSXTagStartUpdateContext}},
            {"jsxTagEnd", new TokenType("jsxTagEnd") {UpdateContext = JSXTagEndUpdateContext}}
        };

        public static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            { "break", Kw("break")},
            {"case", Kw("case", BeforeExprConfig)},
            {"catch", Kw("catch")},
            {"continue", Kw("continue")},
            {"debugger", Kw("debugger")},
            {"default", Kw("default", BeforeExprConfig)},
            {"do", Kw("do", new TokenTypeConfig {IsLoop = true, BeforeExpr = true})},
            {"else", Kw("else", BeforeExprConfig)},
            {"finally", Kw("finally")},
            {"for", Kw("for", new TokenTypeConfig {IsLoop = true})},
            {"function", Kw("function", StartsExprConfig)},
            {"if", Kw("if")},
            {"return", Kw("return", BeforeExprConfig)},
            {"switch", Kw("switch")},
            {"throw", Kw("throw", BeforeExprConfig)},
            {"try", Kw("try")},
            {"var", Kw("var")},
            {"let", Kw("let")},
            {"const", Kw("const")},
            {"while", Kw("while", new TokenTypeConfig {IsLoop = true})},
            {"with", Kw("with")},
            {"new", Kw("new", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})},
            {"this", Kw("this", StartsExprConfig)},
            {"super", Kw("super", StartsExprConfig)},
            {"class", Kw("class")},
            {"extends", Kw("extends", BeforeExprConfig)},
            {"export", Kw("export")},
            {"import", Kw("import")},
            {"yield", Kw("yield", new TokenTypeConfig {BeforeExpr = true, StartsExpr = true})},
            {"null", Kw("null", StartsExprConfig)},
            {"true", Kw("true", StartsExprConfig)},
            {"false", Kw("false", StartsExprConfig)},
            {"in", Kw("in", new TokenTypeConfig {BeforeExpr = true, Binop = 7})},
            {"instanceof", Kw("instanceof", new TokenTypeConfig {BeforeExpr = true, Binop = 7})},
            {"typeof", Kw("typeof", new TokenTypeConfig {BeforeExpr = true, Prefix = true, StartsExpr = true})},
            {"void", Kw("void", new TokenTypeConfig {BeforeExpr = true, Prefix = true, StartsExpr = true})},
            {"delete", Kw("delete", new TokenTypeConfig {BeforeExpr = true, Prefix = true, StartsExpr = true})}
        };

        #endregion

        #region Class methods

        private static TokenType BinopToken(string name, int prec) => new TokenType(name, new TokenTypeConfig
        {
            BeforeExpr = true,
            Binop = prec
        });

        private static TokenType Kw(string name, TokenTypeConfig options = null)
        {
            var cfg = options ?? new TokenTypeConfig();

            cfg.Keyword = name;

            var tokenType = new TokenType(name, cfg);

            if (name == "function")
            {
                tokenType.UpdateContext = FunctionUpdateContext;
            }

            Types.Add("_" + name, tokenType);

            return tokenType;
        }

        private static void ParenRBraceRUpdateContext(Tokenizer tok, TokenType prevType)
        {
            if (tok.State.Context.Count == 1)
            {
                tok.State.ExprAllowed = true;

                return;
            }

            var outt = tok.State.Context.Pop();

            if (outt == TC["b_stat"] && tok.CurrentContext == TC["f_expr"])
            {
                tok.State.Context.Pop();
                tok.State.ExprAllowed = false;
            } else if (outt == TC["b_tmpl"])
            {
                tok.State.ExprAllowed = true;
            }
            else
            {
                tok.State.ExprAllowed = !outt.IsExpr;
            }
        }

        private static void NameUpdateContext(Tokenizer tok, TokenType prevType)
        {
            tok.State.ExprAllowed = false;

            if (prevType == Types["_let"] || prevType == Types["_const"] || prevType == Types["_var"])
            {
                if (LineBreak.IsMatch(tok.Input.Slice(tok.State.End)))
                {
                    tok.State.ExprAllowed = true;
                }
            }
        }

        private static void BraceLUpdateContext(Tokenizer tok, TokenType prevType)
        {
            tok.State.Context.Add(TC[tok.BraceIsBlock(prevType) ? "b_stat" : "b_expr"]);
            tok.State.ExprAllowed = true;
        }

        private static void DollarBraceLUpdateContext(Tokenizer tok, TokenType prevType)
        {
            tok.State.Context.Add(TC["b_tmpl"]);
            tok.State.ExprAllowed = true;
        }

        private static void ParenLUpdateContext(Tokenizer tok, TokenType prevType)
        {
            var statementParens = prevType == Types["_if"] || prevType == Types["_for"] || prevType == Types["_with"] ||
                                  prevType == Types["_while"];

            tok.State.Context.Add(TC[statementParens ? "p_stat" : "p_expr"]);
            tok.State.ExprAllowed = true;
        }

        private static void IncDecUpdateContext(Tokenizer tok, TokenType prevType) { }

        private static void FunctionUpdateContext(Tokenizer tok, TokenType prevType)
        {
            if (tok.CurrentContext == TC["b_stat"])
            {
                tok.State.Context.Add((TC["f_expr"]));
            }

            tok.State.ExprAllowed = false;
        }

        private static void BackQuoteUpdateContext(Tokenizer tok, TokenType prevType)
        {
            if (tok.CurrentContext == TC["q_tmpl"])
            {
                tok.State.Context.Pop();
            }
            else
            {
                tok.State.Context.Add(TC["q_tmpl"]);
            }

            tok.State.ExprAllowed = false;
        }

        private static void JSXTagStartUpdateContext(Tokenizer tok, TokenType prevType)
        {
            tok.State.Context.Add(TC["j_expr"]);
            tok.State.Context.Add(TC["j_oTag"]);
            tok.State.ExprAllowed = false;
        }

        private static void JSXTagEndUpdateContext(Tokenizer tok, TokenType prevType)
        {
            var outt = tok.State.Context.Pop();

            if (outt == TC["j_oTag"] && prevType == Types["slash"] ||
                outt == TC["j_cTag"])
            {
                tok.State.Context.Pop();
                tok.State.ExprAllowed = tok.CurrentContext == TC["j_expr"];
            }
            else
            {
                tok.State.ExprAllowed = true;
            }
        }

        #endregion
    }
}