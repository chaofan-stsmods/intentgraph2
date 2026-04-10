namespace IntentGraph2.Utils.Rule;

public class TwoOperandRule : IRule
{
    private readonly IRule.Operator compareOperator;
    private readonly IRule operandA;
    private readonly IRule operandB;

    public TwoOperandRule(IRule operandA, IRule.Operator compareOperator, IRule operandB)
    {
        this.compareOperator = compareOperator;
        this.operandA = operandA;
        this.operandB = operandB;
    }

    private bool Check()
    {
        return compareOperator switch
        {
            IRule.Operator.EQ => operandA.GetInt() == operandB.GetInt(),
            IRule.Operator.LT => operandA.GetInt() < operandB.GetInt(),
            IRule.Operator.GT => operandA.GetInt() > operandB.GetInt(),
            IRule.Operator.LE => operandA.GetInt() <= operandB.GetInt(),
            IRule.Operator.GE => operandA.GetInt() >= operandB.GetInt(),
            IRule.Operator.NE => operandA.GetInt() != operandB.GetInt(),
            IRule.Operator.AND => operandA.GetBool() && operandB.GetBool(),
            IRule.Operator.OR => operandA.GetBool() || operandB.GetBool(),
            _ => false,
        };
    }

    public int GetInt()
    {
        return Check() ? 1 : 0;
    }

    public bool GetBool()
    {
        return Check();
    }
}
