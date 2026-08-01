using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Utils.Rule;

public class VariableOperand : IRule
{
    private readonly string variableName;
    private readonly IVariableContext context;

    public VariableOperand(string variableName, IVariableContext context)
    {
        this.variableName = variableName;
        this.context = context;
    }

    public int GetInt()
    {
        return context.GetIntVariable(variableName);
    }

    public bool GetBool()
    {
        return context.GetBoolVariable(variableName);
    }
}
