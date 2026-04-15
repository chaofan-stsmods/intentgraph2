using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
public class PostModInitializePatch
{
    public static void Prefix()
    {
        IntentGraphMod.PostInitializeMod();
    }
}
