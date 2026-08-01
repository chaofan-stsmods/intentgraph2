namespace IntentGraph2.Utils.Rule;

public class ValueOperand : IRule
{
    private readonly int intValue;
    private readonly bool boolValue;

    public ValueOperand(int value)
    {
        intValue = value;
        boolValue = value != 0;
    }

    public ValueOperand(bool value)
    {
        boolValue = value;
        intValue = value ? 1 : 0;
    }

    public int GetInt()
    {
        return intValue;
    }

    public bool GetBool()
    {
        return boolValue;
    }
}
