using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public interface IDirectives: IBlockNode
    {
        IList<Directive> Directives { get; set; }
    }
}