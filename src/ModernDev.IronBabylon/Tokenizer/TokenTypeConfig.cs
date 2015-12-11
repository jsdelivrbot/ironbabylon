namespace ModernDev.IronBabylon
{
    public class TokenTypeConfig
    {
        public string Keyword { get; set; }
        public bool BeforeExpr { get; set; }
        public bool StartsExpr { get; set; }
        public bool RightAssociative { get; set; }
        public bool IsLoop { get; set; }
        public bool IsAssign { get; set; }
        public bool Prefix { get; set; }
        public bool Postfix { get; set; }
        public int? Binop { get; set; }
    }
}