using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using System.Collections.Generic;
using System.Text.Json;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
public class LocManagerSetLanguagePatch
{
    public static void Postfix(LocManager __instance, string language)
    {
        IntentGraphMod.IntentGraphStrings.Clear();

        LoadIntentStringsFromMod(IntentGraphMod.ModId, language);

        foreach (var mod in IntentGraphMod.GetLoadedMods())
        {
            if (mod?.manifest?.id != null && mod.manifest.id != IntentGraphMod.ModId)
            {
                LoadIntentStringsFromMod(mod.manifest.id, language);
            }
        }
    }

    private static void LoadIntentStringsFromMod(string modId, string language)
    {
        IntentGraphMod.LogInfo($"Searching intent strings for mod {modId}, language {language}");

        var file = $"res://{modId}/localization/{language}/intentgraph.json";
        if (!FileAccess.FileExists(file))
        {
            file = $"res://{modId}/localization/eng/intentgraph.json";
        }

        if (FileAccess.FileExists(file))
        {
            IntentGraphMod.LogInfo("Loading intent strings from " + file);
            using var fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
            var asText = fileAccess.GetAsText();
            var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(asText) ?? new Dictionary<string, string>();
            foreach (var kvp in strings)
            {
                IntentGraphMod.IntentGraphStrings[kvp.Key] = kvp.Value;
            }
        }

        // version-specific strings, if exist
        if (ReleaseInfoManager.Instance.ReleaseInfo != null)
        {
            var file2 = $"res://{modId}/localization/{language}/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
            if (!FileAccess.FileExists(file2))
            {
                file2 = $"res://{modId}/localization/eng/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
            }

            if (FileAccess.FileExists(file2))
            {
                IntentGraphMod.LogInfo("Loading intent strings from " + file2);
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
