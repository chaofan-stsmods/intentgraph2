using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Utils.Rule;

public interface IRule
{
    int GetInt();

    bool GetBool();

    public enum Operator
    {
        EQ,
        LT,
        GT,
        LE,
        GE,
        NE,
        AND,
        OR,
        NOT,
    }

    static IRule? Parse(string expression, IVariableContext ruleContext)
    {
        return RuleParserHelper.Parse(expression, ruleContext);
    }
}
