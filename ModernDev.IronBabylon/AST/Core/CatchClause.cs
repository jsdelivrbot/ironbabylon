namespace ModernDev.IronBabylon
{
    public class CatchClause : Node, IStatement
    {
        public new string Type  { get; set; } = "CatchClause";
        public IPattern Param { get; set; }
        public BlockStatement Body { get; set; }
    }
}