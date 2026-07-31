using HarmonyLib;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace IntentGraph2.Patches;

#if LARGER_THAN_0_110_0
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded), typeof(Creature), typeof(CombatState))]
#else
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded), typeof(Creature))]
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
