namespace ModernDev.IronBabylon
{
    public class WithStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "WithStatement";
        public IExpression Object { get; set; }
        public IStatement Body { get; set; }
    }
}