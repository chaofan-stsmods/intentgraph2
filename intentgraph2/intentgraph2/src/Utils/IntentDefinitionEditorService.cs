using IntentGraph2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IntentGraph2.Utils;

public static class IntentDefinitionEditorService
{
    public static IntentDefinitionList LoadEditableDefinitions(
        string monsterModelFullName,
        string? devOverrideFilePath = null,
        IReadOnlyDictionary<string, IntentDefinitionList>? runtimeDefinitions = null)
    {
        var devDefinitions = IntentGraphMod.LoadIntentDefinitionsFromFile(devOverrideFilePath ?? IntentGraphMod.GetDevIntentDefinitionFilePath());
        if (devDefinitions.TryGetValue(monsterModelFullName, out var devList))
        {
            return Clone(devList) ?? new IntentDefinitionList();
        }

        runtimeDefinitions ??= IntentGraphMod.IntentDefinitions;
        if (runtimeDefinitions.TryGetValue(monsterModelFullName, out var existingList))
        {
            return Clone(existingList) ?? new IntentDefinitionList();
        }

        return new IntentDefinitionList();
    }

    public static void SaveEditableDefinitions(string monsterModelFullName, IntentDefinitionList definitions, string? filePath = null)
    {
        var path = filePath ?? IntentGraphMod.GetDevIntentDefinitionFilePath();
        var allDefinitions = IntentGraphMod.LoadIntentDefinitionsFromFile(path);
        allDefinitions[monsterModelFullName] = Clone(definitions) ?? new IntentDefinitionList();
        IntentGraphMod.SaveIntentDefinitionsToFile(path, allDefinitions);
    }

    public static T? Clone<T>(T? value)
    {
        if (value == null)
        {
            return default;
        }

        var json = JsonSerializer.Serialize(value, IntentGraphMod.SerializeOptions);
        return JsonSerializer.Deserialize<T>(json, IntentGraphMod.SerializeOptions);
    }

    public static string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, GetIndentedSerializerOptions());
    }

    public static T? DeserializeJson<T>(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(text, IntentGraphMod.SerializeOptions);
    }

    public static string BuildReadOnlySummary(IntentDefinition definition)
    {
        var lines = new List<string>
        {
            IntentGraphMod.IntentGraphStrings.GetValueOrDefault("ui.editor.read_only.summary.none", "Not editable here: graph."),
        };

        if (definition.Graph != null)
        {
            lines.Add(IntentGraphMod.IntentGraphStrings.GetValueOrDefault("ui.editor.read_only.summary.graph_present", "graph is present and remains read-only in this editor."));
        }

        return string.Join(Environment.NewLine, lines.Distinct());
    }

    private static JsonSerializerOptions GetIndentedSerializerOptions()
    {
        return new JsonSerializerOptions(IntentGraphMod.SerializeOptions)
        {
            WriteIndented = true,
        };
    }
}