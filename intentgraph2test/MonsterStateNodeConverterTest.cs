using Godot;
using IntentGraph2.Utils.GraphGenerator;
using IntentGraph2.Utils.Variable;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IntentGraph2.Test;

public class MonsterStateNodeConverterTest
{
    [Theory]
    [InlineData(null, "Localized incomplete")]
    [InlineData("Existing warning", "Existing warning")]
    public void ToMonsterStateNodes_SkipsSelfReferenceAndKeepsExpectedWarning(
        string? initialWarning, string expectedWarning)
    {
        var branch = new RandomBranchState("ROOT");
        branch.AddBranch(branch, 1);
        var stateMachine = new MonsterMoveStateMachine([branch], branch);
        var monster = new TestMonster();
        var localizer = new IntentGraphLocalizer(
            new Dictionary<string, string> { ["ui.Incomplete"] = "Localized incomplete" },
            new VariableContext(monster),
            null);
        var converter = new MonsterStateNodeConverter(localizer, null,
            static (_, _) => throw new InvalidOperationException("No text should be measured."));
        string? warning = initialWarning;

        var node = Assert.Single(converter.ToMonsterStateNodes(monster, stateMachine, ref warning));

        Assert.Same(branch, node.State);
        Assert.True(node.IsInitialState);
        Assert.Empty(node.Children!);
        Assert.Equal(expectedWarning, warning);
    }

    [Fact]
    public void ToMonsterStateNodes_SkipsReferenceToRootAfterMultipleLevels()
    {
        var root = new RandomBranchState("ROOT");
        var middle = new RandomBranchState("MIDDLE");
        var leaf = new RandomBranchState("LEAF");
        root.AddBranch(middle, 1);
        middle.AddBranch(leaf, 1);
        leaf.AddBranch(root, 1);
        var stateMachine = new MonsterMoveStateMachine([root, middle, leaf], root);
        var monster = new TestMonster();
        var localizer = new IntentGraphLocalizer(
            new Dictionary<string, string> { ["ui.Incomplete"] = "Localized incomplete" },
            new VariableContext(monster),
            null);
        var converter = new MonsterStateNodeConverter(localizer, null, static (_, _) => Vector2.Zero);
        string? warning = null;

        var nodes = converter.ToMonsterStateNodes(monster, stateMachine, ref warning);

        var rootNode = Assert.Single(nodes);
        Assert.Same(root, rootNode.State);
        var middleNode = Assert.Single(rootNode.Children!);
        Assert.Same(middle, middleNode.State);
        var leafNode = Assert.Single(middleNode.Children!);
        Assert.Same(leaf, leafNode.State);
        Assert.Empty(leafNode.Children!);
        Assert.Equal("Localized incomplete", warning);
    }

    private sealed class TestMonster : MonsterModel
    {
        public override int MinInitialHp => 1;
        public override int MaxInitialHp => 1;
        protected override MonsterMoveStateMachine GenerateMoveStateMachine() => throw new NotImplementedException();
    }
}
