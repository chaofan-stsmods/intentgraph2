namespace IntentGraph2.Utils.Rule;

public class ValueOperand : IRule
{
    private readonly int intValue;
    private readonly bool boolValue;

    public ValueOperand(int value)
    {
        intValue = value;
        boolValue = false;
    }

    public ValueOperand(bool value)
    {
        boolValue = value;
        intValue = 0;
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
