using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using IntentGraph2.Antlr;
using System.Collections.Generic;

namespace IntentGraph2.Utils.Rule;

public static class RuleParserHelper
{
    private static readonly Dictionary<string, IRule.Operator> OperatorMap = new()
    {
        [">"] = IRule.Operator.GT,
        ["<"] = IRule.Operator.LT,
        ["=="] = IRule.Operator.EQ,
        [">="] = IRule.Operator.GE,
        ["<="] = IRule.Operator.LE,
        ["!="] = IRule.Operator.NE,
        ["&&"] = IRule.Operator.AND,
        ["||"] = IRule.Operator.OR,
    };

    public static IRule? Parse(string expression, IRuleContext ruleContext)
    {
        var lexer = new RuleLexer(CharStreams.fromString(expression));
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new RuleParser(tokenStream);
        var tree = parser.prog().expr();
        return Expr(tree, ruleContext);
    }

    private static IRule? Expr(IParseTree tree, IRuleContext ruleContext)
    {
        if (tree.ChildCount == 1)
        {
            if (tree.GetChild(0) is ITerminalNode node)
            {
                var token = node.Symbol;
                if (token.Type == RuleLexer.VAR)
                {
                    return new VariableOperand(token.Text, ruleContext);
                }

                if (token.Type == RuleLexer.INT)
                {
                    return new ValueOperand(int.Parse(token.Text));
                }

                if (token.Type == RuleLexer.BOOL)
                {
                    return new ValueOperand(bool.Parse(token.Text));
                }
            }
        }
        else if (tree.ChildCount == 2)
        {
            if (tree.GetChild(0) is ITerminalNode node && node.Symbol.Text == "!")
            {
                var expr = Expr(tree.GetChild(1), ruleContext);
                if (expr != null)
                {
                    return new OneOperandRule(IRule.Operator.NOT, expr);
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
                    return Expr(tree.GetChild(1), ruleContext);
                }
            }
            else if (second is ITerminalNode secondNode)
            {
                if (OperatorMap.TryGetValue(secondNode.Symbol.Text, out var @operator))
                {
                    var expr1 = Expr(first, ruleContext);
                    var expr2 = Expr(tree.GetChild(2), ruleContext);
                    if (expr1 != null && expr2 != null)
                    {
                        return new TwoOperandRule(expr1, @operator, expr2);
                    }
                }
            }
        }

        return null;
    }
}
