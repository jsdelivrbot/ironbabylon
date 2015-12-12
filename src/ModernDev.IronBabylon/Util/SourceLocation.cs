namespace ModernDev.IronBabylon
{
    public class SourceLocation
    {
        public SourceLocation(Position start = null, Position end = null)
        {
            Start = start;
            End = end;
        }

        public Position Start { get; set; }
        public Position End { get; set; }
    }
}