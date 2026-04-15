using HarmonyLib;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using System;

namespace IntentGraph2.Utils.Rule;

public class RuleContext : IRuleContext
{
    public RuleContext(MonsterModel monster)
    {
        Monster = monster;
    }

    public MonsterModel Monster { get; }

    public int GetIntVariable(string variableName)
    {
        try
        {
            if (variableName.StartsWith("m."))
            {
                var fieldName = variableName.Substring(2);
                var monsterType = Traverse.Create(Monster);
                return Convert.ToInt32(monsterType.Property(fieldName).GetValue() ?? monsterType.Field(fieldName).GetValue() ?? 0);
            }

            return variableName switch
            {
                "act" => Monster.CombatState.RunState.CurrentActIndex,
                "slotIndex" => Monster.CombatState.Encounter?.Slots.IndexOf(Monster.Creature.SlotName) ?? 0,
                "ascension" => Traverse.Create(RunManager.Instance.AscensionManager).Field("_level").GetValue<int>(),
                _ => 0,
            };
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting variable '{variableName}': {ex}. Return 0.");
            return 0;
        }
    }
}
