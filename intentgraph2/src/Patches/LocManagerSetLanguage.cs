using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Localization;
using System.Collections.Generic;
using System.Text.Json;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
public class LocManagerSetLanguage
{
    public static void Postfix(LocManager __instance, string language)
    {
        IntentGraphMod.IntentGraphStrings.Clear();

        var file = $"res://intentgraph2/localization/{language}/intentgraph.json";
        if (!ResourceLoader.Exists(file))
        {
            file = "res://intentgraph2/localization/eng/intentgraph.json";
        }

        using var fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
        var asText = fileAccess.GetAsText();
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(asText) ?? new Dictionary<string, string>();
        foreach (var kvp in strings)
        {
            IntentGraphMod.IntentGraphStrings[kvp.Key] = kvp.Value;
        }

        // version-specific strings, if exist
        if (ReleaseInfoManager.Instance.ReleaseInfo != null)
        {
            var file2 = $"res://intentgraph2/localization/{language}/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
            if (!ResourceLoader.Exists(file2))
            {
                file2 = $"res://intentgraph2/localization/eng/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
            }

            if (ResourceLoader.Exists(file2))
            {
                using var fileAccess2 = FileAccess.Open(file2, FileAccess.ModeFlags.Read);
                var asText2 = fileAccess2.GetAsText();
                var strings2 = JsonSerializer.Deserialize<Dictionary<string, string>>(asText2) ?? new Dictionary<string, string>();
                foreach (var kvp in strings2)
                {
                    IntentGraphMod.IntentGraphStrings[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
