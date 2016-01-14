using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public interface IBlockNode : INode
    {
        IList<INode> Body { get; set; }
    }
}