using IntentGraph2.Utils.GraphGenerator;

namespace IntentGraph2.Test;

public class MermaidDiagramGeneratorTest
{
    [Fact]
    public void StateNodesToGraph_DoesNotRepeatRandomLabelTextForEqualWeights()
    {
        var root = CreateBranchNode("random",
            CreateLeafNode("A", weight: 1, text: "50%, ≤1"),
            CreateLeafNode("B", weight: 1, text: "50%, ≤1"));

        var diagram = CreateGenerator().StateNodesToGraph([root], intentDefinition: null);

        Assert.DoesNotContain("fa:fa-spinner", diagram);
        Assert.DoesNotContain("50%", diagram);
        Assert.DoesNotContain("≤1", diagram);
        Assert.DoesNotContain("MonsterStateNodeLabel", diagram);
    }

    [Fact]
    public void StateNodesToGraph_OutputsOnlyProbabilityForUnequalRandomWeights()
    {
        var root = CreateBranchNode("random",
            CreateLeafNode("A", weight: 1, text: "25%, ≤1", cooldown: 3, maxRepeat: 1),
            CreateLeafNode("B", weight: 3, text: "75%, ≤1", maxRepeat: 1));

        var diagram = CreateGenerator().StateNodesToGraph([root], intentDefinition: null);

        Assert.Contains("fa:fa-spinner 25%", diagram);
        Assert.Contains("fa:fa-spinner 75%", diagram);
        Assert.Contains("fa:fa-hourglass-half 冷却：3", diagram);
        Assert.DoesNotContain("fa:fa-spinner 25%,", diagram);
        Assert.DoesNotContain("fa:fa-spinner 75%,", diagram);
        Assert.DoesNotContain("≤1", diagram);
        Assert.DoesNotContain("MonsterStateNodeLabel", diagram);
    }

    private static MermaidDiagramGenerator CreateGenerator()
    {
        return new MermaidDiagramGenerator(null!, new IntentGraphLocalizer(new Dictionary<string, string>()));
    }

    private static MonsterStateNode CreateBranchNode(string id, params MonsterStateNode[] children)
    {
        var root = new MonsterStateNode
        {
            Id = id,
            IsInitialState = true,
            Children = [.. children],
        };

        foreach (var child in children)
        {
            child.Parent = root;
        }

        return root;
    }

    private static MonsterStateNode CreateLeafNode(string id, float weight, string text, int cooldown = 0, int maxRepeat = 0)
    {
        return new MonsterStateNode
        {
            Id = id,
            Label = new MonsterStateNodeLabel
            {
                Type = MonsterStateNodeLabel.LabelType.Random,
                Text = text,
                IsTextGenerated = true,
                Weight = weight,
                Cooldown = cooldown,
                MaxRepeat = maxRepeat,
            }
        };
    }
}