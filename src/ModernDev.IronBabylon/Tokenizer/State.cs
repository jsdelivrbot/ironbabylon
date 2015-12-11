using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public class State
    {
        public State(ParserOptions options, string input)
        {
            Strict = options.StrictMode != false && options.SourceType == "module";
            Input = input;
            PotentialArrowAt = -1;
            InMethod = InFunction = InGenerator = InAsync = false;
            Labels = new List<Node>();
            Decorators = new List<Node>();
            Tokens = new List<object>();
            Comments = new List<Node>();
            TrailingComments = new List<Node>();
            LeadingComments = new List<Node>();
            CommentStack = new List<Node>();
            Position = LineStart = 0;
            CurLine = 1;
            Type = TokenType.Types["eof"];
            Value = null;
            Start = End = Position;
            StartLoc = EndLoc = CurrentPosition;
            LastTokenEndLoc = LastTokenStartLoc = null;
            LastTokenStart = LastTokenEnd = Position;
            Context = new List<TokenContext> {TokenContext.Types["b_stat"]};
            ExprAllowed = true;
            ContainsEsc = ContainsOctal = false;
            OctalPosition = null;

            _parserOptions = options;
        }

        private readonly ParserOptions _parserOptions;

        public bool Strict { get; set; }
        public string Input { get; set; }

        /// <summary>
        /// Used to signify the start of a potential arrow function
        /// </summary>
        public int PotentialArrowAt { get; set; }
        public bool InAsync { get; set; }
        public bool InGenerator { get; set; }
        public bool InMethod { get; set; }

        /// <summary>
        /// Labels in scope.
        /// </summary>
        public List<Node> Labels { get; set; }

        /// <summary>
        /// Leading decorators.
        /// </summary>
        public List<Node> Decorators { get; set; }

        /// <summary>
        /// Token store.
        /// </summary>
        public List<object> Tokens { get; set; }

        /// <summary>
        /// Comment store.
        /// </summary>
        public List<Node> Comments { get; set; }
        public List<Node> TrailingComments { get; set; }
        public List<Node> LeadingComments { get; set; }
        public List<Node> CommentStack { get; set; }
        public int Position { get; set; }
        public int LineStart { get; set; }
        public int CurLine { get; set; }
        public TokenType Type { get; set; }

        /// <summary>
        /// For tokens that include more information than their type, the value
        /// </summary>
        public object Value { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public Position StartLoc { get; set; }
        public Position EndLoc { get; set; }
        public Position LastTokenEndLoc { get; set; }
        public Position LastTokenStartLoc { get; set; }
        public int LastTokenStart { get; set; }
        public int LastTokenEnd { get; set; }

        /// <summary>
        /// The context stack is used to superficially track syntactic
        /// context to predict whether a regular expression is allowed in a
        /// given position.
        /// </summary>
        public List<TokenContext> Context { get; set; }

        public bool ExprAllowed { get; set; }

        /// <summary>
        /// Used to signal to callers of `readWord1` whether the word
        /// contained any escape sequences. This is needed because words with
        /// escape sequences must not be interpreted as keywords.
        /// </summary>
        public bool ContainsEsc { get; set; }
        public bool ContainsOctal { get; set; }
        public int? OctalPosition { get; set; }
        public bool InFunction { get; set; }
        public Position CurrentPosition => new Position(CurLine, Position - LineStart);
        public bool InType { get; set; }

        public State Clone(bool skipArrays = false)
        {
//            var state =  (State) MemberwiseClone();
//
//            if (!skipArrays)
//            {
//                state.Labels = Labels;
//                state.Decorators = Decorators;
//                state.Tokens = Tokens;
//                state.Comments = Comments;
//                state.TrailingComments = TrailingComments;
//                state.LeadingComments = LeadingComments;
//                state.CommentStack = CommentStack;
//                state.Context = Context;
//            }
//
//            return state;

            var state = new State(_parserOptions, Input);
            
            foreach (var prop in typeof(State).GetProperties())
            {
                var val = prop.GetValue(this);
                if ((!skipArrays || prop.Name == "Context") &&
                    (prop.GetType() == typeof (List<Node>) || prop.GetType() == typeof (List<object>) ||
                     prop.GetType() == typeof (List<TokenContext>)))
                {
                    prop.SetValue(state, val);
                }
            }

            return state;
        }
    }
}