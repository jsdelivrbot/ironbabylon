namespace ModernDev.IronBabylon
{
    public class SourceLocation
    {
        public SourceLocation(Position start = null, Position end = null)
        {
            Start = start;
            End = end;
        }

        private Position Start { get; set; }
        public Position End { get; set; }
    }
}