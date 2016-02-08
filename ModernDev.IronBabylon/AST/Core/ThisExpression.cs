namespace ModernDev.IronBabylon
{
    public class ThisExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "ThisExpression";
    }
}