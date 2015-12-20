using System.Collections.Generic;

namespace ModernDev.IronBabylon
{
    public partial class Tokenizer
    {
        /// <summary>
        /// Raise an unexpected token error.
        /// </summary>
        protected bool Unexpected(int? pos = null)
        {
            Raise(pos ?? State.Start, "Unexpected token");

            return false;
        }

        /// <summary>
        /// This function is used to raise exceptions on parse errors. It
        /// takes an offset integer (into the current `input`) to indicate
        /// the location of the error, attaches the position to the end
        /// of the error message, and then raises a `SyntaxError` with that
        /// message.
        /// </summary>
        protected void Raise(int pos, string msg)
        {
            var loc = Util.GetLineInfo(Input, pos);

            msg += $" ({loc.Line}:{loc.Column})";

            throw new SyntaxErrorException(msg, loc, pos);
        }

        /// <summary>
        /// Convert list of expression atoms to a list of
        /// </summary>
        protected virtual List<Node> ToReferencedList(List<Node> exprList) => exprList;

        protected virtual Node ParseAssignableListItemTypes(Node param) => param;
    }
}
