using System;

namespace ModernDev.IronBabylon
{
    public partial interface INode: ICloneable
    {
        string Type { get; set; }
        int Start { get; set; }
        int End { get; set; }
        SourceLocation Loc { get; set; }
        INode FinishNodeAt(Parser parser, string type, int end, Position loc);
        T GetNode<T>() where T : INode;
    }
}