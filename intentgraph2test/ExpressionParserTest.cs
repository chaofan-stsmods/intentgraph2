using IntentGraph2.Utils.Expression;
using IntentGraph2.Utils.Variable;

namespace IntentGraph2.Test;

public class ExpressionParserTest
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
        var expression = IExpression.Parse(condition, new MockExpressionContext());
        Assert.NotNull(expression);
        Assert.Equal(expectedResult, expression.GetBool());
    }

    private class MockExpressionContext : IVariableContext
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

        public Type? GetVariableType(string variableName)
        {
            return typeof(string);
        }
    }
}
