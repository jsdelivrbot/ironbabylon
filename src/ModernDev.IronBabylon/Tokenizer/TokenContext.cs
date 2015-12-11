using System;
using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class TokenContext
    {
        public TokenContext(string token, bool isExpr = false, bool preserveSpace = false,
            Func<Tokenizer, TokenType> over = null)
        {
            Token = token;
            IsExpr = isExpr;
            PreserveSpace = preserveSpace;
            Override = over;
        }

        public string Token { get; set; }
        public bool IsExpr { get; set; }
        public bool PreserveSpace { get; set; }
        public Func<Tokenizer, TokenType> Override { get; set; }

        public static Dictionary<string, TokenContext> Types = new Dictionary<string, TokenContext>
        {
            {"b_stat", new TokenContext("{")},
            {"b_expr", new TokenContext("{", true)},
            {"b_tmpl", new TokenContext("${", true)},
            {"p_stat", new TokenContext("(")},
            {"p_expr", new TokenContext("(", true)},
            {"q_tmpl", new TokenContext("`", false, true, t => t.ReadTmplToken())},
            {"f_expr", new TokenContext("function", true)},
            {"j_oTag", new TokenContext("<tag")},
            {"j_cTag", new TokenContext("</tag")},
            {"j_expr", new TokenContext("<tag>...</tag>", true, true)}
        };
    }
}