using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class ArrayExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "ArrayExpression";
        public IList<IExpression> Elements { get; set; } 
    }
}