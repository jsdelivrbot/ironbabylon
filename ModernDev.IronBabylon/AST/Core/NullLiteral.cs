namespace ModernDev.IronBabylon
{
    public class NullLiteral : Node, ILiteral
    {
        public new string Type { get; set; } = "NullLiteral";
    }
}