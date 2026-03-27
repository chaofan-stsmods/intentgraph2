using Godot;
using Godot.Bridge;
using HarmonyLib;
using IntentGraph2.Models;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntentGraph2;

[ModInitializer(nameof(InitializeMod))]
public class IntentGraphMod
{
    private static readonly JsonSerializerOptions SerializeOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = 
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static Dictionary<string, string> IntentGraphStrings = new Dictionary<string, string>();
    public static Dictionary<string, IntentDefinition> IntentDefinitions = new Dictionary<string, IntentDefinition>();

    public static void InitializeMod()
    {
        Log.Info("IntentGraphMod initialized!");
        var assembly = typeof(IntentGraphMod).Assembly;

        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        Log.Info("Patching...");
        var harmony = new Harmony("chaofan.sts2.intentgraph2");
        harmony.PatchAll(assembly);

        LoadIntentDefinitions();

        Log.Info("IntentGraphMod initialize done.");
    }

    public static void LoadIntentDefinitions()
    {
        IntentDefinitions.Clear();

        var file = $"res://intentgraph2/intentgraph.json";
        using var fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
        var asText = fileAccess.GetAsText();
        var intents = JsonSerializer.Deserialize<Dictionary<string, IntentDefinition>>(asText, SerializeOptions) ?? new Dictionary<string, IntentDefinition>();
        foreach (var kv in intents)
        {
            IntentDefinitions[kv.Key] = kv.Value;
        }

        // version-specific intent definitions, if exist
        if (ReleaseInfoManager.Instance.ReleaseInfo != null)
        {
            var file2 = $"res://intentgraph2/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
            if (FileAccess.FileExists(file2))
            {
                using var fileAccess2 = FileAccess.Open(file2, FileAccess.ModeFlags.Read);
                var asText2 = fileAccess2.GetAsText();
                var intents2 = JsonSerializer.Deserialize<Dictionary<string, IntentDefinition>>(asText2, SerializeOptions) ?? new Dictionary<string, IntentDefinition>();
                foreach (var kv in intents2)
                {
                    IntentDefinitions[kv.Key] = kv.Value;
                }
            }
        }
    }
}

