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

    [Theory]
    [InlineData("0 || 12", 12, "12", true)]
    [InlineData("10 || 12", 10, "10", true)]
    [InlineData("0 || 0", 0, "0", false)]
    [InlineData("10 && 12", 12, "12", true)]
    [InlineData("0 && 12", 0, "0", false)]
    [InlineData("10 && 0", 0, "0", false)]
    [InlineData("false || 12", 12, "12", true)]
    [InlineData("true || 12", 1, "True", true)]
    [InlineData("true && 12", 12, "12", true)]
    [InlineData("false && 12", 0, "False", false)]
    [InlineData("(0 || 10) && 12", 12, "12", true)]
    public void Parse_LogicalOperatorsReturnSelectedValue(
        string condition, int expectedInt, string expectedString, bool expectedBool)
    {
        var expression = IExpression.Parse(condition, new MockExpressionContext());

        Assert.NotNull(expression);
        Assert.Equal(expectedInt, expression.GetInt());
        Assert.Equal(expectedString, expression.GetString());
        Assert.Equal(expectedBool, expression.GetBool());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("true false")]
    [InlineData("1 2")]
    [InlineData("true &&")]
    [InlineData("(true")]
    [InlineData("true)")]
    public void Parse_RejectsSyntaxErrors(string condition)
    {
        Assert.Null(IExpression.Parse(condition, new MockExpressionContext()));
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
