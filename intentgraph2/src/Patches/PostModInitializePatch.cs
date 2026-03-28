using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
public class PostModInitializePatch
{
    public static void Prefix()
    {
        IntentGraphMod.PostInitializeMod();
    }
}
