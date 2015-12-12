namespace ModernDev.IronBabylon
{
    public class Position
    {
        public Position(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int? Line { get; private set; }
        public int? Column { get; private set; }
    }
}