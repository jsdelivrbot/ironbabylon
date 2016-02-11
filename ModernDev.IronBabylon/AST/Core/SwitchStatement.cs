using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class SwitchStatement : Node, IStatement
    {
        public new string Type  { get; set; } = "SwitchStatement";
        public IExpression Discriminant { get; set; }
        public IList<SwitchCase> Cases { get; set; }
    }
}