using IntentGraph2.Utils.Rule;

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

    private class MockRuleContext : IRuleContext
    {
        public int GetIntVariable(string variableName)
        {
            return 1;
        }
    }
}
