using HarmonyLib;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using System;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded))]
public class MonsterSetupPatch
{
    public static void Postfix(CombatManager __instance, Creature creature)
    {
        if (creature.IsMonster)
        {
            try
            {
                var monster = creature.Monster;
                IgLogger.Info($"Generating intent graph for monster: {creature.Name}.");
                var graph = IntentGraphGenerator.GenerateGraph(monster);
                if (monster != null && graph != null)
                {
                    IntentGraphMod.GeneratedGraphs.TryAdd(monster, graph);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex.ToString());
            }
        }
    }
}
