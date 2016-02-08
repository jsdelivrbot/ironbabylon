namespace ModernDev.IronBabylon
{
    public class UnaryExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "UnaryExpression";
        public UnaryOperator Operator { get; set; }
        public bool Prefix { get; set; }
        public IExpression Argument { get; set; }

        public static UnaryOperator StrToUnaryOperator(string str)
        {
            switch (str)
            {
                case "-":
                    return UnaryOperator.UnaryNegation;

                case "+":
                    return UnaryOperator.UnaryPlus;

                case "!":
                    return UnaryOperator.LogicalNot;

                case "~":
                    return UnaryOperator.BitwiseNot;

                case "typeof":
                    return UnaryOperator.Typeof;

                case "void":
                    return UnaryOperator.Void;

                case "delete":
                    return UnaryOperator.Delete;

                // just in case
                default:
                    return UnaryOperator.UnaryNegation;
            }
        }
    }
}