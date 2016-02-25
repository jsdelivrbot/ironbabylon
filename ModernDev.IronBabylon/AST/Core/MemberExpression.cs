namespace ModernDev.IronBabylon
{
    public class MemberExpression : Node, IExpression, IPattern
    {
        public new string Type  { get; set; } = "MemberExpression";
        public IExpression Object { get; set; }
        public IExpression Property { get; set; }
        public bool Computed { get; set; }
    }
}