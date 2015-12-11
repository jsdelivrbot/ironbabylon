namespace ModernDev.IronBabylon
{
    /// <summary>
    /// Object type used to represent tokens. Note that normally, tokens
    /// simply exist as properties on the parser object. This is only
    /// used for the onToken callback and the external tokenizer.
    /// </summary>
    public class Token
    {
        #region Class constructors

        public Token(State state)
        {
            Type = state.Type;
            Value = state.Value;
            Start = state.Start;
            End = state.End;
            Location = new SourceLocation(state.StartLoc, state.EndLoc);
        }

        #endregion

        #region Class fields

        public TokenType Type { get; set; }

        public object Value { get; set; }

        public int Start { get; set; }

        public int End { get; set; }

        public SourceLocation Location { get; set; }

        #endregion
    }
}