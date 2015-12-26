using System.Collections.Generic;
using static ModernDev.IronBabylon.Util;

namespace ModernDev.IronBabylon
{
    public partial class Parser
    {
        #region Class fields

        /// <summary>
        /// Test whether a semicolon can be inserted at the current position.
        /// </summary>
        private bool CanInsertSemicolon => Match(TT["eof"]) || Match(TT["braceR"]) ||
                                          LineBreak.IsMatch(Input.Slice(State.LastTokenEnd, State.Start));

        private bool IsLineTerminator => Eat(TT["semi"]) || CanInsertSemicolon;

        #endregion

        #region Class methods

        private static void AddExtra(Node node, string key, object val)
        {
            if (node == null)
            {
                return;
            }

            node.Extra = node.Extra ?? new Dictionary<string, object>();
            node.Extra.Add(key, val);
        }

        private bool IsRelational(string op) => Match(TT["relational"]) && (string) State.Value == op;

        private void ExpectRelational(string op)
        {
            if (IsRelational(op))
            {
                Next();
            }
            else
            {
                Unexpected();
            }
        }

        /// <summary>
        /// Tests whether parsed token is a contextual keyword.
        /// </summary>
        private bool IsContextual(string name) => Match(TT["name"]) && (string) State.Value == name;

        /// <summary>
        /// Consumes contextual keyword if possible.
        /// </summary>
        private bool EatContextual(string name) => (string) State.Value == name && Eat(TT["name"]);

        /// <summary>
        /// Asserts that following token is given contextual keyword.
        /// </summary>
        private void ExpectContextual(string name)
        {
            if (!EatContextual(name))
            {
                Unexpected();
            }
        }

        /// <summary>
        /// Consume a semicolon, or, failing that, see if we are allowed to pretend that there is a semicolon at this position.
        /// </summary>
        private void Semicolon()
        {
            if (!IsLineTerminator)
            {
                Unexpected();
            }
        }

        /// <summary>
        /// Expect a token of a given type. If found, consume it, otherwise, raise an unexpected token error.
        /// </summary>
        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool Expect(TokenType type) => Eat(type) || Unexpected();

        #endregion
    }
}
