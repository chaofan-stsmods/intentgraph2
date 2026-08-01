using IntentGraph2.Utils.Rule;
using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Test;

public class RuleParserTest
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("true == false", false)]
    [InlineData("true == 1", true)]
    [InlineData("false == 1", false)]
    [InlineData("false == 0", true)]
    [InlineData("1 == 0", false)]
    [InlineData("1 == 1", true)]
    public void Parse(string condition, bool expectedResult)
    {
        var rule = IRule.Parse(condition, new MockRuleContext());
        Assert.NotNull(rule);
        Assert.Equal(expectedResult, rule.GetBool());
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
