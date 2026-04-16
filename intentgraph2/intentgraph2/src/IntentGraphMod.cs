using Godot;
using Godot.Bridge;
using HarmonyLib;
using IntentGraph2.Crossovers;
using IntentGraph2.Models;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace IntentGraph2;

public class IntentGraphMod
{
    public static JsonSerializerOptions SerializeOptions { get; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = 
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public const string ModId = "intentgraph2";

    public static Dictionary<string, string> IntentGraphStrings = new Dictionary<string, string>();
    public static Dictionary<string, IntentDefinitionList> IntentDefinitions = new Dictionary<string, IntentDefinitionList>();

    public static readonly ConditionalWeakTable<MonsterModel, Graph> GeneratedGraphs = new();

    private static IBaseLibHelper? baseLibHelper;

    public static void InitializeMod()
    {
        IgLogger.Info("IntentGraphMod initialize");
        var assembly = typeof(IntentGraphMod).Assembly;

        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        IgLogger.Info("Patching...");
        var harmony = new Harmony("chaofan.sts2.intentgraph2");
        harmony.PatchAll(assembly);

        IgLogger.Info("IntentGraphMod initialize done.");
    }

    public static void PostInitializeMod()
    {
        IgLogger.Info("IntentGraphMod post initialize");

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
                    IgLogger.Warn("Failed to get assembly load context for IntentGraphMod.");
                }
            }
            catch (Exception ex)
            {
                IgLogger.Warn("Failed to load BaseLib helper: " + ex);
            }
        }

        LoadIntentDefinitions();
        IgLogger.Info("IntentGraphMod post initialize done.");
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

        LoadIntentDefinitionForDev();
    }

    public static void ReloadIntentDefinitionsAndGraphs()
    {
        LoadIntentDefinitions();
        LoadIntentStrings(MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language);

        var combatState = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || combatState.Encounter == null)
        {
            return;
        }

        GeneratedGraphs.Clear();
        foreach (var creature in combatState.Enemies)
        {
            Patches.MonsterSetupPatch.Postfix(MegaCrit.Sts2.Core.Combat.CombatManager.Instance, creature);
        }
    }

    public static Key GetToggleHotKey()
    {
        return baseLibHelper?.Config.ToggleIntentGraphKey ?? Key.F1;
    }

    public static void LoadIntentStrings(string language)
    {
        IntentGraphStrings.Clear();

        LoadIntentStringsFromMod(ModId, language);

        foreach (var mod in GetLoadedMods())
        {
            if (mod?.manifest?.id != null && mod.manifest.id != ModId)
            {
                LoadIntentStringsFromMod(mod.manifest.id, language);
            }
        }

        LoadIntentStringsForDev(language);
    }

    public static string GetDevIntentDefinitionFilePath()
    {
        return Path.GetFullPath(Path.Join(Path.GetDirectoryName(typeof(ModManager).Assembly.Location), "..", "intentgraph-intents-dev.json"));
    }

    public static string GetDevIntentStringFilePath(string language)
    {
        return Path.GetFullPath(Path.Join(Path.GetDirectoryName(typeof(ModManager).Assembly.Location), "..", $"intentgraph-strings-{language}-dev.json"));
    }

    public static Dictionary<string, IntentDefinitionList> LoadIntentDefinitionsFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, IntentDefinitionList>();
            }

            var asText = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, IntentDefinitionList>>(asText, SerializeOptions)
                ?? new Dictionary<string, IntentDefinitionList>();
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Failed to load intent definitions from {filePath}: {ex}");
            return new Dictionary<string, IntentDefinitionList>();
        }
    }

    public static void SaveIntentDefinitionsToFile(string filePath, Dictionary<string, IntentDefinitionList> definitions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");

        var serializeOptions = new JsonSerializerOptions(SerializeOptions)
        {
            WriteIndented = true,
        };
        var asText = JsonSerializer.Serialize(definitions, serializeOptions);
        File.WriteAllText(filePath, asText);
    }

    public static Dictionary<string, string> LoadIntentStringsFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string>();
            }

            var asText = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(asText)
                ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Failed to load intent strings from {filePath}: {ex}");
            return new Dictionary<string, string>();
        }
    }

    public static void SaveIntentStringsToFile(string filePath, Dictionary<string, string> strings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        var asText = JsonSerializer.Serialize(strings, serializeOptions);
        File.WriteAllText(filePath, asText);
    }

    private static IEnumerable<Mod> GetLoadedMods()
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
        IgLogger.Info($"Searching intent definitions for mod {modId}");

        try
        {
            var file = $"res://{modId}/intentgraph.json";
            if (FileAccess.FileExists(file))
            {
                IgLogger.Info("Loading intent definitions from " + file);
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
            IgLogger.Warn($"Failed to load intent definitions for mod {modId}: {ex}");
        }

        try
        {
            // version-specific intent definitions, if exist
            if (ReleaseInfoManager.Instance.ReleaseInfo != null)
            {
                var file2 = $"res://{modId}/intentgraph-{ReleaseInfoManager.Instance.ReleaseInfo.Version}.json";
                if (FileAccess.FileExists(file2))
                {
                    IgLogger.Info("Loading intent definitions from " + file2);
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
            IgLogger.Warn($"Failed to load version-specific intent definitions for mod {modId}: {ex}");
        }
    }

    private static void LoadIntentDefinitionForDev()
    {
        var devIntentFile = GetDevIntentDefinitionFilePath();
        IgLogger.Info("Loading intent definitions from " + devIntentFile);
        var intents = LoadIntentDefinitionsFromFile(devIntentFile);
        foreach (var kv in intents)
        {
            IntentDefinitions[kv.Key] = kv.Value;
        }
    }

    private static void LoadIntentStringsFromMod(string modId, string language)
    {
        IgLogger.Info($"Searching intent strings for mod {modId}, language {language}");

        try
        {
            var file = $"res://{modId}/localization/{language}/intentgraph.json";
            if (!FileAccess.FileExists(file))
            {
                file = $"res://{modId}/localization/eng/intentgraph.json";
            }

            if (FileAccess.FileExists(file))
            {
                IgLogger.Info("Loading intent strings from " + file);
                using var fileAccess = FileAccess.Open(file, FileAccess.ModeFlags.Read);
                var asText = fileAccess.GetAsText();
                var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(asText) ?? new Dictionary<string, string>();
                foreach (var kvp in strings)
                {
                    IntentGraphStrings[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Failed to load intent strings for mod {modId}, language {language}: {ex}");
        }

        try
        {
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
                    IgLogger.Info("Loading intent strings from " + file2);
                    using var fileAccess2 = FileAccess.Open(file2, FileAccess.ModeFlags.Read);
                    var asText2 = fileAccess2.GetAsText();
                    var strings2 = JsonSerializer.Deserialize<Dictionary<string, string>>(asText2) ?? new Dictionary<string, string>();
                    foreach (var kvp in strings2)
                    {
                        IntentGraphStrings[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Failed to load version-specific intent strings for mod {modId}, language {language}: {ex}");
        }
    }

    private static void LoadIntentStringsForDev(string language)
    {
        var devIntentStringFile = GetDevIntentStringFilePath(language);
        IgLogger.Info("Loading intent strings from " + devIntentStringFile);
        var strings = LoadIntentStringsFromFile(devIntentStringFile);
        foreach (var kvp in strings)
        {
            IntentGraphStrings[kvp.Key] = kvp.Value;
        }
    }
}

