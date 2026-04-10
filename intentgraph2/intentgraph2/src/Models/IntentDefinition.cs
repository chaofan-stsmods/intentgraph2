using IntentGraph2.Utils.Rule;
using MegaCrit.Sts2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace IntentGraph2.Models;

public class IntentDefinitionList : List<IntentDefinition>
{
    public IntentDefinition? FindFirstMatchCondition(MonsterModel monster)
    {
        foreach (var def in this.Reverse<IntentDefinition>())
        {
            try
            {
                if (def.ParsedCondition == null)
                {
                    def.ParsedCondition = IRule.Parse(def.Condition, new RuleContext(monster));
                }

                if (def.ParsedCondition?.GetBool() == true)
                {
                    return def;
                }
            }
            catch (Exception ex)
            {
                IntentGraphMod.LogInfo($"Error parsing condition '{def.Condition}' for monster '{monster.Id}': {ex.Message}");
            }
        }

        return null;
    }
}

public class IntentDefinition
{
    public string Condition { get; set; } = "true";

    [JsonIgnore]
    public IRule? ParsedCondition { get; set; }

    public string[]? SecondaryInitialStates { get; set; }

    public Graph? Graph { get; set; }

    public Graph? GraphPatch { get; set; }

    public StateMachineNode[]? StateMachine { get; set; }

    public Dictionary<string, MoveReplacement[]>? MoveReplacements { get; set; }
}

public class StateMachineNode
{
    public string Name { get; set; } = string.Empty;

    public string? MoveName { get; set; }

    public bool IsInitialState { get; set; } = false;

    public int InitialStatePriority { get; set; } = 0;

    public StateMachinNodeChildren[]? Children { get; set; }

    public string? FollowUpState { get; set; }
}

public record class StateMachinNodeChildren(string Label, StateMachineNode Node);

public record class MoveReplacement(string? ValueText, string? TimesText);
