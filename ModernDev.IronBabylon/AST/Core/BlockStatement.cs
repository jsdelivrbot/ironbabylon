using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class BlockStatement : Node, IStatement, IDirectives
    {
        public new string Type  { get; set; } = "BlockStatement";
        public IList<INode> Body { get; set; } // IStatement
        public IList<Directive> Directives { get; set; } 
    }
}