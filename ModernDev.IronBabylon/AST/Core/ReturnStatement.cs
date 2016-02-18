namespace ModernDev.IronBabylon
{
    public class ReturnStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "ReturnStatement";
        public IExpression Argument { get; set; }
    }
}