namespace ModernDev.IronBabylon
{
    public class ThrowStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "ThrowStatement";
        public IExpression Argument { get; set; }
    }
}