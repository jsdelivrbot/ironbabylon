using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class ArrayPattern : Node, IPattern
    {
        public new string Type  { get; set; } = "ArrayPattern";
        public IList<IExpression> Elements { get; set; }
    }
}