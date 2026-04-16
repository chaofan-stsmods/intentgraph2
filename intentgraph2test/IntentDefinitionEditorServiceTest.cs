using IntentGraph2.Models;
using IntentGraph2.Utils;
using System.Collections.Generic;
using System.IO;

namespace IntentGraph2.Test;

public class IntentDefinitionEditorServiceTest : IDisposable
{
    private readonly string tempDirectory;

    public IntentDefinitionEditorServiceTest()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "intentgraph2-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void SaveEditableDefinitions_PreservesOtherMonstersAndWritesCamelCase()
    {
        var filePath = Path.Combine(tempDirectory, "intentgraph-intents-dev.json");
        IntentGraphMod.SaveIntentDefinitionsToFile(filePath, new Dictionary<string, IntentDefinitionList>
        {
            ["Other.Monster"] =
            [
                new IntentDefinition
                {
                    Condition = "ascension >= 9",
                }
            ]
        });

        var definitions = new IntentDefinitionList
        {
            new IntentDefinition
            {
                Condition = "slotIndex == 1",
                StateMachine =
                [
                    new StateMachineNode
                    {
                        Name = "ROOT",
                        MoveName = "ROOT_MOVE",
                        IsInitialState = true,
                        FollowUpState = "NEXT",
                    }
                ],
                MoveReplacements = new Dictionary<string, MoveReplacement[]>
                {
                    ["ROOT_MOVE"] =
                    [
                        new MoveReplacement("N", "T")
                    ]
                },
                GraphPatch = new Graph
                {
                    Labels =
                    [
                        new Models.Label(1.5f, 2.25f, "text.key", "center")
                    ]
                }
            }
        };

        IntentDefinitionEditorService.SaveEditableDefinitions("Test.Monster", definitions, filePath);

        var loaded = IntentGraphMod.LoadIntentDefinitionsFromFile(filePath);
        Assert.True(loaded.ContainsKey("Other.Monster"));
        Assert.True(loaded.ContainsKey("Test.Monster"));
        Assert.Equal("ascension >= 9", loaded["Other.Monster"][0].Condition);
        Assert.Equal("slotIndex == 1", loaded["Test.Monster"][0].Condition);
        Assert.Single(loaded["Test.Monster"][0].StateMachine!);
        Assert.Single(loaded["Test.Monster"][0].GraphPatch!.Labels);

        var rawText = File.ReadAllText(filePath);
        Assert.Contains("\"graphPatch\"", rawText);
        Assert.Contains("\"stateMachine\"", rawText);
        Assert.Contains("\"moveReplacements\"", rawText);
        Assert.Contains("\"valueText\"", rawText);
        Assert.DoesNotContain("\"GraphPatch\"", rawText);
        Assert.DoesNotContain("\"StateMachine\"", rawText);
    }

    [Fact]
    public void LoadEditableDefinitions_PrefersDevOverrideBeforeRuntimeDefinitions()
    {
        var filePath = Path.Combine(tempDirectory, "intentgraph-intents-dev.json");
        IntentGraphMod.SaveIntentDefinitionsToFile(filePath, new Dictionary<string, IntentDefinitionList>
        {
            ["Test.Monster"] =
            [
                new IntentDefinition
                {
                    Condition = "slotIndex == 2",
                }
            ]
        });

        var runtimeDefinitions = new Dictionary<string, IntentDefinitionList>
        {
            ["Test.Monster"] =
            [
                new IntentDefinition
                {
                    Condition = "true",
                }
            ]
        };

        var loaded = IntentDefinitionEditorService.LoadEditableDefinitions("Test.Monster", filePath, runtimeDefinitions);
        Assert.Single(loaded);
        Assert.Equal("slotIndex == 2", loaded[0].Condition);
    }

    [Fact]
    public void LoadEditableDefinitions_FallsBackToRuntimeDefinitionsWhenDevOverrideMissing()
    {
        var runtimeDefinitions = new Dictionary<string, IntentDefinitionList>
        {
            ["Test.Monster"] =
            [
                new IntentDefinition
                {
                    Condition = "ascension >= 9",
                }
            ]
        };

        var loaded = IntentDefinitionEditorService.LoadEditableDefinitions(
            "Test.Monster",
            Path.Combine(tempDirectory, "missing.json"),
            runtimeDefinitions);

        Assert.Single(loaded);
        Assert.Equal("ascension >= 9", loaded[0].Condition);
    }

    [Fact]
    public void SaveIntentStringsToFile_WritesAndLoadsIndentedJson()
    {
        var filePath = Path.Combine(tempDirectory, "intentgraph-strings-zhs-dev.json");
        var strings = new Dictionary<string, string>
        {
            ["ui.UseOnlyOnce"] = "只能用1次",
            ["branch.Test.Monster.ROOT.NEXT"] = "否则",
        };

        IntentGraphMod.SaveIntentStringsToFile(filePath, strings);

        var loaded = IntentGraphMod.LoadIntentStringsFromFile(filePath);
        Assert.Equal(strings, loaded);

        var rawText = File.ReadAllText(filePath);
        Assert.Contains("\n", rawText);
        Assert.Contains("\"ui.UseOnlyOnce\"", rawText);
    }

    [Fact]
    public void BuildReadOnlySummary_OnlyMentionsGraphAsReadOnly()
    {
        var definition = new IntentDefinition
        {
            SecondaryInitialStates = ["FORM_2"],
            Graph = new Graph(),
        };

        var summary = IntentDefinitionEditorService.BuildReadOnlySummary(definition);

        Assert.Contains("Not editable here: graph.", summary);
        Assert.Contains("graph is present and remains read-only in this editor.", summary);
        Assert.DoesNotContain("secondaryInitialStates", summary);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}