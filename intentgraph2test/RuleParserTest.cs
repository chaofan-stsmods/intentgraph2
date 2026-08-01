using IntentGraph2.Utils.Rule;
using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Test;

public class RuleParserTest
{
    [Fact]
    public void Test1()
    {
        var rule = IRule.Parse("true", new MockRuleContext());
        Assert.NotNull(rule);
        Assert.True(rule.GetBool());
    }

    private class MockRuleContext : IVariableContext
    {
        public int GetIntVariable(string variableName)
        {
            return 1;
        }

        public bool GetBoolVariable(string variableName)
        {
            return true;
        }

        public string GetStringVariable(string variableName)
        {
            return "mock";
        }
    }
}
