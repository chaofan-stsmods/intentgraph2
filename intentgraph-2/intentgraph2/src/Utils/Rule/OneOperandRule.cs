namespace IntentGraph2.Utils.Rule;

public class OneOperandRule : IRule
{
    private readonly IRule.Operator compareOperator;
    private readonly IRule operandA;

    public OneOperandRule(IRule.Operator compareOperator, IRule operandA)
    {
        this.compareOperator = compareOperator;
        this.operandA = operandA;
    }

    private bool Check()
    {
        if (compareOperator == IRule.Operator.NOT)
        {
            return !operandA.GetBool();
        }

        return false;
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
