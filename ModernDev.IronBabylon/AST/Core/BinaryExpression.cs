namespace ModernDev.IronBabylon
{
    public class BinaryExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "BinaryExpression";
        public BinaryOperator Operator { get; set; }
        public IExpression Left { get; set; }
        public IExpression Right { get; set; }

        public static BinaryOperator StrToBinaryOperator(string str)
        {
            switch (str)
            {
                case "==":
                    return BinaryOperator.Equal;

                case "!=":
                    return BinaryOperator.NotEqual;

                case "===":
                    return BinaryOperator.StrictEqual;

                case "!==":
                    return BinaryOperator.StrictNotEqual;

                case "<":
                    return BinaryOperator.LessThan;

                case "<=":
                    return BinaryOperator.LessThanOrEqual;

                case ">":
                    return BinaryOperator.GreaterThan;

                case ">=":
                    return BinaryOperator.GreaterThanOrEqual;

                case "<<":
                    return BinaryOperator.LeftShift;

                case ">>":
                    return BinaryOperator.RightShift;

                case ">>>":
                    return BinaryOperator.UnsignedRightShift;

                case "+":
                    return BinaryOperator.Addition;

                case "-":
                    return BinaryOperator.Substraction;

                case "*":
                    return BinaryOperator.Multiplication;

                case "/":
                    return BinaryOperator.Division;

                case "%":
                    return BinaryOperator.Remainder;

                case "|":
                    return BinaryOperator.BitwiseOR;

                case "^":
                    return BinaryOperator.BitwiseXOR;

                case "&":
                    return BinaryOperator.BitwiseAND;

                case "in":
                    return BinaryOperator.In;

                case "instanceof":
                    return BinaryOperator.Instanceof;

                default: // just in case
                    return BinaryOperator.Equal;

            }
        }
    }
}