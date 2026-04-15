using HarmonyLib;
using IntentGraph2.DevConsole;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using System;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.DevConsole.DevConsole), MethodType.Constructor, typeof(bool))]
public class DevConsoleConstructorPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.DevConsole.DevConsole __instance)
    {
        new Traverse(__instance).Method("RegisterCommand", [typeof(AbstractConsoleCmd)], null).GetValue(new ReloadIntentsConsoleCmd());
        IgLogger.Info("Registered reloadintents to DevConsole.");
    }
}
