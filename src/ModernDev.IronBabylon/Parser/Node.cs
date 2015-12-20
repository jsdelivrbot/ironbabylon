using System;
using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class Node : ICloneable
    {
        public Node(int pos, Position loc)
        {
            Type = "";
            Start = pos;
            End = 0;
            Loc = new SourceLocation(loc);
            Extra = null;
        }

        public Node() { }

        public string Type { get; set; }
        public Node Program { get; set; }
        public int Start { get; private set; }
        public int End { get; set; }
        public SourceLocation Loc { get; set; }
        public IDictionary<string, object> Extra { get; set; }
        public object Body { get; set; }
        public List<Node> TrailingComments { get; set; } = new List<Node>();
        public List<Node> LeadingComments { get; set; } = new List<Node>();
        public List<Node> InnerComments { get; set; } = new List<Node>();
        public object Value { get; set; }
        public string SourceType { get; set; }
        public List<Node> Comments { get; set; }
        public List<object> Tokens { get; set; }
        public List<Node> Decorators { get; set; }
        public object Expression { get; set; } // TODO: bool or Node
        public Node Label { get; set; }
        public object Name { get; set; }
        public string Kind { get; set; }
        public int? StatementStart { get; set; }
        public Node Test { get; set; }
        public List<Node> Declarations { get; set; }
        public Node Init { get; set; }
        public object Consequent { get; set; } // TODO: Node or Node[]
        public Node Altername { get; set; }
        public Node Argument { get; set; }
        public Node Discriminant { get; set; }
        public List<Node> Cases { get; set; }
        public Node Block { get; set; }
        public Node Handler { get; set; }
        public Node Param { get; set; }
        public List<Node> GuardedHandlers { get; set; }
        public Node Finalizer { get; set; }
        public Node Object { get; set; }
        public List<Node> Directives { get; set; }
        public Node Update { get; set; }
        public Node Left { get; set; }
        public object Right { get; set; }
        public Node Id { get; set; }
        public bool Async { get; set; }
        public bool Generator { get; set; }
        public object Params { get; set; }
        public Node SuperClass { get; set; }
        public bool Static { get; set; }
        public bool Computed { get; set; }
        public object Key { get; set; }
        public List<Node> Specifiers { get; set; }
        public Node Source { get; set; }
        public Node Declaration { get; set; }
        public Node Local { get; set; }
        public Node Exported { get; set; }
        public Node Imported { get; set; }
        public List<Node> Properties { get; set; }
        public List<Node> Elements { get; set; }
        public string Operator { get; set; }
        public List<Node> Expressions { get; set; }
        public bool Method { get; set; }
        public bool Delegate { get; set; }
        public bool Prefix { get; set; }
        public Node Callee { get; set; }
        public Node Property { get; set; }
        public List<Node> Arguments { get; set; }
        public Node Tag { get; set; }
        public Node Quasi { get; set; }
        public Node Meta { get; set; }
        public bool Tail { get; set; }
        public List<Node> Quasis { get; set; }
        public string Pattern { get; set; }
        public string Flags { get; set; }
        public bool Shorthand { get; set; }
        public bool All { get; set; }
        public Node TypeParameters { get; set; }
        public Node Rest { get; set; }
        public object TypeAnnotation { get; set; }
        public object ReturnType { get; set; } // TokenType or Node ?
        public List<Node> Extends { get; set; }
        public bool Optional { get; set; }
        public List<Node> CallProperties { get; set; }
        public List<Node> Indexers { get; set; }
        public Node Qualification { get; set; }
        public List<TokenType> Types { get; set; }
        public object ElementType { get; set; }

        #region Flow-related properties
        public string ExportKind { get; set; }

        // ReSharper disable once InconsistentNaming
        public bool _ExprListItem { get; set; }

        public Node SuperTypeParameters { get; set; }
        public List<Node> Implements { get; set; }
        public string ImportKind { get; set; }
        #endregion

        #region JSX-related properties
        public Node Namespace { get; set; }
        public List<Node> Attributes { get; set; }
        public bool SelfClosing { get; set; }
        public Node OpeningElement { get; set; }
        public Node ClosingElement { get; set; }
        public List<Node> Children { get; set; }

        #endregion

        public static implicit operator bool(Node node)
        {
            return node != null;
        }

        public static Node FinishNodeAt(Parser @this, Node n, string type, int end, Position loc)
        {
            n.Type = type;
            n.End = end;
            n.Loc.End = loc;
            @this.ProcessComment(n);

            return n;
        }

        public object Clone()
        {
            var node = new Node();

            foreach (var prop in typeof (Node).GetProperties())
            {
                prop.SetValue(node, prop.GetValue(this));
            }

            return node;
        }
    }
}