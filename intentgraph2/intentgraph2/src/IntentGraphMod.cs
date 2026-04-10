using Godot;
using Godot.Bridge;
using HarmonyLib;
using IntentGraph2.Crossovers;
using IntentGraph2.Models;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;

using Path = System.IO.Path;

namespace IntentGraph2;

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
    public static Dictionary<string, IntentDefinitionList> IntentDefinitions = new Dictionary<string, IntentDefinitionList>();

    public const string ModId = "intentgraph2";

    private static IBaseLibHelper? baseLibHelper;

    public static void InitializeMod()
    {
        LogInfo("IntentGraphMod initialize");
        var assembly = typeof(IntentGraphMod).Assembly;

        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        LogInfo("Patching...");
        var harmony = new Harmony("chaofan.sts2.intentgraph2");
        harmony.PatchAll(assembly);

        LogInfo("IntentGraphMod initialize done.");
    }

    public static void PostInitializeMod()
    {
        LogInfo("IntentGraphMod post initialize");

        if (GetLoadedMods().Any(m => m.manifest?.id == "BaseLib"))
        {
            try
            {
                var currentAssembly = typeof(IntentGraphMod).Assembly;
                var loadContext = AssemblyLoadContext.GetLoadContext(currentAssembly);
                if (loadContext != null)
                {
                    var helperAssemblyPath = Path.Join(Path.GetDirectoryName(currentAssembly.Location), "intentgraph2baselib.dll");
                    var assembly = loadContext.LoadFromAssemblyPath(helperAssemblyPath);
                    var type = assembly.GetType("IntentGraph2.BaseLib.BaseLibHelper");
                    baseLibHelper = (IBaseLibHelper)type.CreateInstance();
                    baseLibHelper.RegisterConfig();
                }
                else
                {
                    LogInfo("Failed to get assembly load context for IntentGraphMod.");
                }
            }
            catch (Exception ex)
            {
                LogInfo("Failed to load BaseLib helper: " + ex);
            }
        }

        LoadIntentDefinitions();
        LogInfo("IntentGraphMod post initialize done.");
    }

    public static void LoadIntentDefinitions()
    {
        IntentDefinitions.Clear();

        LoadIntentDefinitionForMod(ModId);

        foreach (var mod in GetLoadedMods())
        {
            if (mod?.manifest?.id != null && mod.manifest.id != ModId)
            {
                LoadIntentDefinitionForMod(mod.manifest.id);
            }
        }
    }

    public static Key GetToggleHotKey()
    {
        return baseLibHelper?.Config.ToggleIntentGraphKey ?? Key.F1;
    }

    public static void LogInfo(string message)
    {
        Log.Info($"[IntentGraph] {message}");
    }

    public static IEnumerable<Mod> GetLoadedMods()
    {
        var loadedMods1 = typeof(ModManager).GetProperty("LoadedMods", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        if (loadedMods1 != null)
        {
            return (IEnumerable<Mod>)loadedMods1;
        }

        var loadedMods2 = typeof(ModManager).GetMethod("GetLoadedMods", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        if (loadedMods2 != null)
        {
            return (IEnumerable<Mod>)loadedMods2;
        }

        return Enumerable.Empty<Mod>();
    }

    private static void LoadIntentDefinitionForMod(string modId)
    {
        LogInfo($"Searching intent definitions for mod {modId}");

        try
        {
            var file = $"res://{modId}/intentgraph.json";
            if (FileAccess.FileExists(file))
            {
                LogInfo("Loading intent definitions from " + file);
                using var fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
                var asText = fileAccess.GetAsText();
                var intents = JsonSerializer.Deserialize<Dictionary<string, IntentDefinitionList>>(asText, SerializeOptions) ?? new Dictionary<string, IntentDefinitionList>();
                foreach (var kv in intents)
                {
                    IntentDefinitions[kv.Key] = kv.Value;
                }
            }
        }
        catch (Exception ex)
        {
            LogInfo($"Failed to load intent definitions for mod {modId}: {ex}");
        }

        try
        {
            // version-specific intent definitions, if exist
            if (ReleaseInfoManager.Instance.ReleaseInfo != null)
            {
                var file2 = $"res://{modId}/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
                if (FileAccess.FileExists(file2))
                {
                    LogInfo("Loading intent definitions from " + file2);
                    using var fileAccess2 = FileAccess.Open(file2, FileAccess.ModeFlags.Read);
                    var asText2 = fileAccess2.GetAsText();
                    var intents2 = JsonSerializer.Deserialize<Dictionary<string, IntentDefinitionList>>(asText2, SerializeOptions) ?? new Dictionary<string, IntentDefinitionList>();
                    foreach (var kv in intents2)
                    {
                        IntentDefinitions[kv.Key] = kv.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogInfo($"Failed to load version-specific intent definitions for mod {modId}: {ex}");
        }
    }
}

