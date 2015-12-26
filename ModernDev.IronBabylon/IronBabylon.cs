namespace ModernDev.IronBabylon
{
    public static class IronBabylon
    {
        public static Node Parse(string input, ParserOptions options = null)
            => new Parser(options ?? ParserOptions.Default, input.Replace("\r\n", "\n")).Parse();
    }
}