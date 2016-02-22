using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class CallExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "CallExpression";
        public IExpression Callee { get; set; }
        public IList<IExpression> Arguments { get; set; }
    }
}