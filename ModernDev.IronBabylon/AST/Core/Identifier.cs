namespace ModernDev.IronBabylon
{
    public partial class Identifier : Node, IExpression, IPattern
    {
        public new string Type { get; set; } = "Identifier";
        public string Name { get; set; }
    }
}