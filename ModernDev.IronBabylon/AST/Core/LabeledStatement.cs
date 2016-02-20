namespace ModernDev.IronBabylon
{
    public class LabeledStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "LabeledStatement";
        public Identifier Label { get; set; }
        public IStatement Body { get; set; }
    }
}