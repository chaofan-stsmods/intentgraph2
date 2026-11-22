using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace IntentGraph2.Patches;

public class IsDreadPatches
{
    public static bool? OverwriteIsDread = null;

#if !LESS_THAN_0_113_0
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.IsDread))]
    public static class RunManagerPatch
    {
        public static bool Prefix(object __instance, ref bool __result)
        {
            if (OverwriteIsDread != null)
            {
                __result = OverwriteIsDread.Value;
                return false;
            }

            return true;
        }
    }
#endif
}
