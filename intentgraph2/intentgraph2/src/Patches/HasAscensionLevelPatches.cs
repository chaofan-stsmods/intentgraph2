using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Runs;

namespace IntentGraph2.Patches;

public class HasAscensionLevelPatches
{
    public static AscensionLevel? OverwriteAsensionLevel = null;

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.HasAscension))]
    public static class RunManagerPatch
    {
        public static bool Prefix(object __instance, AscensionLevel level, ref bool __result)
        {
            if (OverwriteAsensionLevel != null)
            {
                __result = OverwriteAsensionLevel >= level;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AscensionManager), nameof(AscensionManager.HasLevel))]
    public static class AscensionManagerPatch
    {
        public static bool Prefix(object __instance, AscensionLevel level, ref bool __result)
        {
            if (OverwriteAsensionLevel != null)
            {
                __result = OverwriteAsensionLevel >= level;
                return false;
            }

            return true;
        }
    }
}
