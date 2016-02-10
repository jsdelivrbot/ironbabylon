namespace ModernDev.IronBabylon
{
    public class FunctionExpression : FunctionDeclaration, IExpression
    {
        public new string Type  { get; set; } = "FunctionExpression";
    }
}