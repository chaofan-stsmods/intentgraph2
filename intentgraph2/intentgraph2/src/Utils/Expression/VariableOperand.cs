using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Utils.Expression;

public class VariableOperand : IExpression
{
    private readonly string variableName;
    private readonly IVariableContext context;

    public VariableOperand(string variableName, IVariableContext context)
    {
        this.variableName = variableName;
        this.context = context;
    }

    public IExpression.Type ExpressionType
    {
        get
        {
            var type = context.GetVariableType(variableName);
            if (type == null)
            {
                return IExpression.Type.Unknown;
            }
            else if (type == typeof(string))
            {
                return IExpression.Type.String;
            }
            else if (type == typeof(bool))
            {
                return IExpression.Type.Bool;
            }
            else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong) ||
                type == typeof(nint) || type == typeof(nuint) ||
                type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                return IExpression.Type.Int;
            }
            else
            {
                return IExpression.Type.Unknown;
            }
        }
    }

    public int GetInt()
    {
        return context.GetIntVariable(variableName);
    }

    public bool GetBool()
    {
        return context.GetBoolVariable(variableName);
    }

    public string GetString()
    {
        return context.GetStringVariable(variableName);
    }
}
