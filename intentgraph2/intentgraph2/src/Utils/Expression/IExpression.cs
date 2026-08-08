using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Utils.Expression;

public interface IExpression
{
    int GetInt();

    bool GetBool();

    string GetString();

    Type ExpressionType { get; }

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

    public enum Type
    {
        Unknown,
        Int,
        Bool,
        String,
    }

    static IExpression? Parse(string expression, IVariableContext expressionContext)
    {
        return ExpressionParserHelper.Parse(expression, expressionContext);
    }
}
