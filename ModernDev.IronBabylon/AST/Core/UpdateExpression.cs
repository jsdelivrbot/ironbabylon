namespace ModernDev.IronBabylon
{
    public class UpdateExpression : Node, IExpression
    {
        public new string Type  { get; set; } = "UpdateExpression";
        public UpdateOperator Operator { get; set; }
        public IExpression Argument { get; set; }
        public bool Prefix { get; set; }

        public static UpdateOperator StrToUpdateOperator(string str)
        {
            switch (str)
            {
                case "++":
                    return UpdateOperator.Increment;

                case "--":
                    return UpdateOperator.Decrement;

                default: //just in case
                    return UpdateOperator.Increment;
            }
        }
    }
}