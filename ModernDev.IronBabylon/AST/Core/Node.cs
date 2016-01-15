using System.Linq;

namespace ModernDev.IronBabylon
{
    public partial class Node : INode
    {
        public string Type { get; set; } = "";
        public SourceLocation Loc { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public INode FinishNodeAt(Parser parser, string type, int end, Position loc)
        {
            if (!string.IsNullOrEmpty(type))
            {
                Type = type;
            }

            End = end;
            Loc.End = loc;
            parser.ProcessComment(this);

            return this;
        }

        public object Clone()
        {
            var node = GetType().GetConstructors()[0].Invoke(null);

            foreach (var prop in GetType().GetProperties().Where(prop => prop.CanWrite))
            {
                prop.SetValue(node, prop.GetValue(this));
            }

            return node;
        }

        public static implicit operator bool(Node node)
        {
            return node != null;
        }

        public T GetNode<T>() where T : INode
        {
            var inst = (T) typeof (T).GetConstructors()[0].Invoke(null);

            foreach (var prop in GetType().GetProperties().Where(prop => prop.CanWrite))
            {
                prop.SetValue(inst, prop.GetValue(this));
            }

            return inst;
        }
    }

    #region ILiteral

    #endregion

    #region Statements

    #endregion

    // jsx

    // directives

    // babylon
}
