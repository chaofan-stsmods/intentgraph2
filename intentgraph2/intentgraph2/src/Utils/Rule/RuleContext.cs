using HarmonyLib;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

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
        return variableName switch
        {
            "act" => Monster.CombatState.RunState.CurrentActIndex,
            "slotIndex" => Monster.CombatState.Encounter?.Slots.IndexOf(Monster.Creature.SlotName) ?? 0,
            "ascension" => Traverse.Create(RunManager.Instance.AscensionManager).Field("_level").GetValue<int>(),
            _ => 0,
        };
    }
}
