using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public partial interface IFunction : INode
    {
        Identifier Id { get; set; }
        IList<ILVal> Params { get; set; }
        BlockStatement Body { get; set; } 
    }
}