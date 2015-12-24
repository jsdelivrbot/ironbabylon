namespace ModernDev.IronBabylon
{
    public partial class Parser : Tokenizer
    {
        #region Class constructors

        public Parser(ParserOptions options, string input) : base(options ?? ParserOptions.Default, input)
        {
            Options = options ?? ParserOptions.Default;
            InModule = options?.SourceType == "module";
            Input = input;

            if (State.Position == 0 && Input[0] == '#' && input[1] == '!')
            {
                SkipLineComment(2);
            }
        }

        #endregion

        #region Class properties

        private ParserOptions Options { get; }

        private static int? _nullRef;

        #endregion

        #region Class methods

        public Node Parse()
        {
            var file = StartNode();
            var program = StartNode();

            NextToken();

            return ParseTopLevel(file, program);
        }

        private Node StartNode() => new Node(State.Start, State.StartLoc);

        private static Node StartNodeAt(int pos, Position loc) => new Node(pos, loc);

        /// <summary>
        /// Finish an AST node, adding `type` and `end` properties.
        /// </summary>
        private Node FinishNode(Node node, string type)
            => Node.FinishNodeAt(this, node, type, State.LastTokenEnd, State.LastTokenEndLoc);

        /// <summary>
        /// Finish node at given position
        /// </summary>
        private Node FinishNodeAt(Node node, string type, int pos, Position loc)
            => Node.FinishNodeAt(this, node, type, pos, loc);

        #endregion
    }
}