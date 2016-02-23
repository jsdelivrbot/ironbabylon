namespace ModernDev.IronBabylon
{
    public class EmptyStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "EmptyStatement";
    }
}