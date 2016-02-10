using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public partial class FunctionDeclaration : Node, IFunction, IDeclaration
    {
        public new string Type  { get; set; } = "FunctionDeclaration";
        public Identifier Id { get; set; }
        public IList<ILVal> Params { get; set; }
        public BlockStatement Body { get; set; }
    }
}