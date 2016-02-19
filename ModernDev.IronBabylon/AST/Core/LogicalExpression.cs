namespace ModernDev.IronBabylon
{
    public class LogicalExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "LogicalExpression";
        public LogicalOperator Operator { get; set; }
        public IExpression Left { get; set; }
        public IExpression Right { get; set; }

        public static LogicalOperator StrToLogicalOperator(string str)
        {
            switch (str)
            {
                case "||":
                    return LogicalOperator.LogicalOR;

                case "&&":
                    return LogicalOperator.LogicalAND;

                default: //just in case 
                    return LogicalOperator.LogicalOR;
            }
        }
    }
}