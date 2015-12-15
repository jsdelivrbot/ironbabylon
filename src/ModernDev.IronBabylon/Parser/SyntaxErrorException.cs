using System;

namespace ModernDev.IronBabylon
{
    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException(string msg) : base(msg) { }

        public SyntaxErrorException(string msg, Position loc, int pos) : base(msg)
        {
            Location = loc;
            Position = pos;
        }

        public Position Location { get; private set; }
        public int Position { get; private set; }
    }
}