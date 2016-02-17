namespace ModernDev.IronBabylon
{
    public class WhileStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "WhileStatement";
        public IExpression Test { get; set; }
        public IStatement Body { get; set; }
    }
}