namespace ModernDev.IronBabylon
{
    public class BreakStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "BreakStatement";
        public Identifier Label { get; set; }
    }
}