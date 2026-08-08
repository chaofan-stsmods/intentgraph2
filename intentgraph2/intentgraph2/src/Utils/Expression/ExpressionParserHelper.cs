using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using IntentGraph2.Antlr;
using IntentGraph2.Utils.Variable;
using System.Collections.Generic;

namespace IntentGraph2.Utils.Expression;

public static class ExpressionParserHelper
{
    private static readonly Dictionary<string, IExpression.Operator> OperatorMap = new()
    {
        [">"] = IExpression.Operator.GT,
        ["<"] = IExpression.Operator.LT,
        ["=="] = IExpression.Operator.EQ,
        [">="] = IExpression.Operator.GE,
        ["<="] = IExpression.Operator.LE,
        ["!="] = IExpression.Operator.NE,
        ["&&"] = IExpression.Operator.AND,
        ["||"] = IExpression.Operator.OR,
    };

    public static IExpression? Parse(string expression, IVariableContext expressionContext)
    {
        var lexer = new ExpressionLexer(CharStreams.fromString(expression));
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new ExpressionParser(tokenStream);
        var tree = parser.prog().expr();
        return Expr(tree, expressionContext);
    }

    private static IExpression? Expr(IParseTree tree, IVariableContext expressionContext)
    {
        if (tree.ChildCount == 1)
        {
            if (tree.GetChild(0) is ITerminalNode node)
            {
                var token = node.Symbol;
                if (token.Type == ExpressionLexer.VAR)
                {
                    return new VariableOperand(token.Text, expressionContext);
                }

                if (token.Type == ExpressionLexer.INT)
                {
                    return new ValueOperand(int.Parse(token.Text));
                }

                if (token.Type == ExpressionLexer.BOOL)
                {
                    return new ValueOperand(bool.Parse(token.Text));
                }
            }
        }
        else if (tree.ChildCount == 2)
        {
            if (tree.GetChild(0) is ITerminalNode node && node.Symbol.Text == "!")
            {
                var expr = Expr(tree.GetChild(1), expressionContext);
                if (expr != null)
                {
                    return new OneOperandExpression(IExpression.Operator.NOT, expr);
                }
            }
        }
        else if (tree.ChildCount == 3)
        {
            var first = tree.GetChild(0);
            var second = tree.GetChild(1);

            if (first is ITerminalNode firstNode)
            {
                if (firstNode.Symbol.Text == "(")
                {
                    return Expr(tree.GetChild(1), expressionContext);
                }
            }
            else if (second is ITerminalNode secondNode)
            {
                if (OperatorMap.TryGetValue(secondNode.Symbol.Text, out var @operator))
                {
                    var expr1 = Expr(first, expressionContext);
                    var expr2 = Expr(tree.GetChild(2), expressionContext);
                    if (expr1 != null && expr2 != null)
                    {
                        return new TwoOperandExpression(expr1, @operator, expr2);
                    }
                }
            }
        }

        return null;
    }
}
