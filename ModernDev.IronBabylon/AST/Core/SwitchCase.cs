using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class SwitchCase : Node
    {
        public new string Type  { get; set; } = "SwitchCase";
        public IExpression Test { get; set; }
        public IList<IStatement> Consequent { get; set; }
    }
}