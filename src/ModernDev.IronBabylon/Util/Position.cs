namespace ModernDev.IronBabylon
{
    public class Position
    {
        public Position(int line, int col)
        {
            Line = line;
            Col = col;
        }

        public int? Line { get; set; }
        public int? Col { get; set; }
    }
}