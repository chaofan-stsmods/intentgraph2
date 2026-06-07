using IntentGraph2.Utils;
using IntentGraph2.Utils.Rule;
using MegaCrit.Sts2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Models;

public class IntentDefinitionList : List<IntentDefinition>
{
    public IntentDefinition? FindFirstMatchCondition(MonsterModel monster)
    {
        foreach (var def in this.Reverse<IntentDefinition>())
        {
            try
            {
                var rule = IRule.Parse(def.Condition, new RuleContext(monster));
                if (rule?.GetBool() == true)
                {
                    return def;
                }
            }
            catch (Exception ex)
            {
                IgLogger.Warn($"Error parsing condition '{def.Condition}' for monster '{monster.Id}': {ex.Message}");
            }
        }

        return null;
    }
}

public class IntentDefinition
{
    public string Condition { get; set; } = "true";

    public string? UpToDateCondition { get; set; }

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

    public bool HorizontalLayout { get; set; } = false;
}

public record class StateMachinNodeChildren(string Label = "", StateMachineNode? Node = null);

public record class MoveReplacement(string? ValueText, string? TimesText);
