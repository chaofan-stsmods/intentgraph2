using HarmonyLib;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace IntentGraph2.Patches;

#if LESS_THAN_0_110_0
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded), typeof(Creature))]
#else
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded), typeof(Creature), typeof(CombatState))]
#endif
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
