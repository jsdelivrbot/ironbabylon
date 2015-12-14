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

        private bool IsRelational(object op) => Match(TT["relational"]) && State.Value == op;

        private void ExpectRelational(object op)
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
        private bool IsContextual(object name) => Match(TT["name"]) && State.Value == name;

        /// <summary>
        /// Consumes contextual keyword if possible.
        /// </summary>
        private bool EatContextual(object name) => State.Value == name && Eat(TT["name"]);

        /// <summary>
        /// Asserts that following token is given contextual keyword.
        /// </summary>
        private void ExpectContextual(object name)
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
        private bool Expect(TokenType type)
        {
            return Eat(type) || Unexpected();
        }

        #endregion
    }
}
