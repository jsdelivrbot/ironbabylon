namespace ModernDev.IronBabylon
{
    public class TryStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "TryStatement";
        public BlockStatement Block { get; set; }
        public CatchClause Handler { get; set; }
        public BlockStatement Finalizer { get; set; }
    }
}