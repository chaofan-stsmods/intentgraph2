using HarmonyLib;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded))]
public class MonsterSetupPatch
{
    public static void Postfix(CombatManager __instance, Creature creature)
    {
        if (creature.IsMonster)
        {
            IntentGraphGenerator.GenerateAndCacheGraphForCreature(creature);
        }
    }
}
