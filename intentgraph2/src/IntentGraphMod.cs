using Godot;
using Godot.Bridge;
using HarmonyLib;
using IntentGraph2.Models;
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
        var file = $"res://intentgraph2/intentgraph.json";
        using FileAccess fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
        string asText = fileAccess.GetAsText();
        IntentDefinitions = JsonSerializer.Deserialize<Dictionary<string, IntentDefinition>>(asText, SerializeOptions) ?? new Dictionary<string, IntentDefinition>();
    }
}

