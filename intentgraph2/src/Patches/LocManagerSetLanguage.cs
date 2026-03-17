using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using System.Collections.Generic;
using System.Text.Json;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
public class LocManagerSetLanguage
{
    public static void Postfix(LocManager __instance, string language)
    {
        var file = $"res://intentgraph2/localization/{language}/intentgraph.json";
        if (!ResourceLoader.Exists(file))
        {
            file = "res://intentgraph2/localization/eng/intentgraph.json";
        }

        using FileAccess fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
        string asText = fileAccess.GetAsText();
        IntentGraphMod.IntentGraphStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(asText) ?? new Dictionary<string, string>();
    }
}
