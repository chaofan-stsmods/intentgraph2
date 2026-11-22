using HarmonyLib;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace IntentGraph2.Patches;

public class ShowIntentGraphPatches
{
    [HarmonyPatch(typeof(NCreature), "OnFocus")]
    public static class OnFocusPatch
    {
        public static void Postfix(NCreature __instance)
        {
            if (IntentGraphMod.Config.IntentGraphHoverTrigger == IntentGraphHoverTrigger.Monster ||
                IntentGraphMod.Config.IntentGraphHoverTrigger == IntentGraphHoverTrigger.Both)
            {
                IntentGraphHost.Create(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(NCreature), "OnUnfocus")]
    public static class OnUnfocusPatch
    {
        public static void Prefix(NCreature __instance)
        {
            if (!IntentGraphMod.Config.PinableIntentGraph)
            {
                IntentGraphHost.Remove(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(NIntent), "OnHovered")]
    public static class OnHoveredPatch
    {
        public static void Postfix(NIntent __instance)
        {
            if (IntentGraphMod.Config.IntentGraphHoverTrigger == IntentGraphHoverTrigger.Intents ||
                IntentGraphMod.Config.IntentGraphHoverTrigger == IntentGraphHoverTrigger.Both)
            {
                var creature = Traverse.Create(__instance).Field<Creature?>("_owner").Value;
                var creatureNode = creature?.GetCreatureNode();
                if (creatureNode != null)
                {
                    IntentGraphHost.Create(creatureNode);
                }
            }
        }
    }

    [HarmonyPatch(typeof(NIntent), "OnUnhovered")]
    public static class OnUnhoveredPatch
    {
        public static void Prefix(NIntent __instance)
        {
            var creature = Traverse.Create(__instance).Field<Creature?>("_owner").Value;
            var creatureNode = creature?.GetCreatureNode();
            if (creatureNode != null && !IntentGraphMod.Config.PinableIntentGraph)
            {
                IntentGraphHost.Remove(creatureNode);
            }
        }
    }

    [HarmonyPatch(typeof(NCreature), "_ExitTree")]
    public static class ExitTreePatch
    {
        public static void Prefix(NCreature __instance)
        {
            IntentGraphHost.Remove(__instance);
        }
    }
}
