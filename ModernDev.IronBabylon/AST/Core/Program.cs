using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class Program : Node, IDirectives
    {
        public new string Type { get; set; } = "Program";

        // ES6
        public string SourceType { get; set; } // "script" | "module"
        public IList<INode> Body { get; set; }
        
        public IList<Directive> Directives { get; set; }
    }
}