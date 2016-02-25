using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class SequenceExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "SequenceExpression";
        public IList<IExpression> Expressions { get; set; }
    }
}