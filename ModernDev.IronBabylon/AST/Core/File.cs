using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class File : Node
    {
        public new string Type  { get; set; } = "File";
        public Program Program { get; set; }
        public IList<Token> Tokens { get; set; } 
        public IList<INode> Comments { get; set; }
    }
}