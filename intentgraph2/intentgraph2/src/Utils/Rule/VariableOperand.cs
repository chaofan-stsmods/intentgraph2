namespace IntentGraph2.Utils.Rule;

public class VariableOperand : IRule
{
    private readonly string variableName;
    private readonly IRuleContext context;

    public VariableOperand(string variableName, IRuleContext context)
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
        return GetInt() != 0;
    }
}
