// Copyright (c) Bohdan Shtepan. All rights reserved.
// http://modern-dev.com/
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ModernDev.IronBabylon
{
    public class State
    {
        #region Class constructors

        public State(ParserOptions options, string input)
        {
            Strict = options.StrictMode && options.SourceType == "module";
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

        #endregion

        #region Class fields

        private readonly ParserOptions _parserOptions;

        #endregion

        #region Class properties

        public bool Strict { get; set; }

        private string Input { get; }

        /// <summary>
        /// Used to signify the start of a potential arrow function
        /// </summary>
        public int PotentialArrowAt { get; set; }

        public bool InAsync { get; set; }

        public bool InGenerator { get; set; }

        public object InMethod { get; set; }

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
        public List<object> Tokens { get; private set; }

        /// <summary>
        /// Comment store.
        /// </summary>
        public List<Node> Comments { get; private set; }

        public List<Node> TrailingComments { get; set; }

        public List<Node> LeadingComments { get; set; }

        public List<Node> CommentStack { get; private set; }

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
        public List<TokenContext> Context { get; private set; }

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

        #endregion

        #region CLass methods

        public State Clone(bool skipArrays = false)
        {
            var state = new State(_parserOptions, Input);

            foreach (var prop in typeof (State).GetProperties().Where(prop => prop.CanWrite))
            {
                var val = prop.GetValue(this);

                if ((!skipArrays || prop.Name == "Context") && prop.GetType() == typeof (IList))
                {
                    val = (val as IList<object>)?.ToList();
                }

                prop.SetValue(state, val);
            }

            return state;
        }

        #endregion
    }
}