using IntentGraph2.Models;
using IntentGraph2.Utils.Variable;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace IntentGraph2.Utils.GraphGenerator;

internal class IntentGraphLocalizer
{
    private readonly IReadOnlyDictionary<string, string>? overwriteIntentStrings;
    private readonly VariableContext variableContext;
    private readonly IntentDefinition? intentDefinition;

    private static Regex PlaceholderFinder = new Regex(@"{{|{([^}]+)}", RegexOptions.Compiled);

    public delegate string? OnFoundVariableDelegate(string variableName, string? variableType);

    public IntentGraphLocalizer(IReadOnlyDictionary<string, string>? overwriteIntentStrings, VariableContext variableContext, IntentDefinition? intentDefinition)
    {
        this.overwriteIntentStrings = overwriteIntentStrings;
        this.variableContext = variableContext;
        this.intentDefinition = intentDefinition;
    }

    public bool TryGet(string key, [NotNullWhen(true)] out string? value)
    {
        if (overwriteIntentStrings != null && overwriteIntentStrings.TryGetValue(key, out value))
        {
            return true;
        }

        return IntentGraphMod.IntentGraphStrings.TryGetValue(key, out value);
    }

    public string GetOrElse(string key, string fallbackValue)
    {
        return TryGet(key, out var value) ? value : fallbackValue;
    }

    public string FormatWithVariables(string text, OnFoundVariableDelegate? onFoundVariable = null)
    {
        return PlaceholderFinder.Replace(text, match =>
        {
            if (match.Value == "{{")
            {
                return "{";
            }

            var variable = match.Groups[1].Value;
            var split = variable.Split(':', 2);
            var variableName = split[0];
            var variableType = split.Length > 1 ? split[1] : null;
            if (onFoundVariable != null)
            {
                var replacement = onFoundVariable(variableName, variableType);
                if (replacement != null)
                {
                    return replacement;
                }
            }

            return variableType switch
            {
                "int" => variableContext.GetIntVariable(variableName).ToString(),
                "bool" => variableContext.GetBoolVariable(variableName).ToString(),
                _ => variableContext.GetStringVariable(variableName),
            };
        });
    }

    public string? GetMoveName(MonsterModel monster, string moveId)
    {
        var title = GetMoveName(monster.Id.Entry, moveId);

        if (title == null && intentDefinition?.AlternativeMonsterId != null)
        {
            return GetMoveName(intentDefinition.AlternativeMonsterId, moveId);
        }

        return title;
    }

    private string? GetMoveName(string monsterName, string moveId)
    {
        var locTable = LocManager.Instance.GetTable("monsters");

        var moveIdChanged = true;
        while (moveIdChanged)
        {
            moveIdChanged = false;
            if (moveId.EndsWith("_MOVE"))
            {
                moveId = moveId.Substring(0, moveId.Length - "_MOVE".Length);
                moveIdChanged = true;
            }

            var lastIndex = moveId.Length - 1;
            while (lastIndex >= 0 && ((moveId[lastIndex] >= '0' && moveId[lastIndex] <= '9') || moveId[lastIndex] == '_'))
            {
                lastIndex--;
            }
            if (lastIndex < moveId.Length - 1)
            {
                moveId = moveId.Substring(0, lastIndex + 1);
                moveIdChanged = true;
            }
        }

        string? title;
        if (!TryGetTitle(locTable, monsterName, moveId, out title) && moveId.Contains('_'))
        {
            var index2 = moveId.IndexOf('_');
            _ = TryGetTitle(locTable, monsterName, moveId, out title, startIndex: index2 + 1);
        }

        if (title == null)
        {
            if (!TryGetTitle(monsterName, moveId, out title) && moveId.Contains('_'))
            {
                var index2 = moveId.IndexOf('_');
                _ = TryGetTitle(monsterName, moveId, out title, startIndex: index2 + 1);
            }
        }

        return title;
    }

    private bool TryGetTitle(LocTable table, string monsterName, string moveId, out string? title, int startIndex = 0, int? endIndex = default)
    {
        title = null;
        var endIndexValue = endIndex ?? moveId.Length;
        if (startIndex == -1 || endIndexValue == -1 || endIndexValue <= startIndex)
        {
            return false;
        }

        var key = $"{monsterName}.moves.{moveId.Substring(startIndex, endIndexValue - startIndex)}.title";
        if (table.HasEntry(key))
        {
            title = table.GetRawText(key);
            return true;
        }

        return false;
    }

    private bool TryGetTitle(string monsterName, string moveId, out string? title, int startIndex = 0, int? endIndex = default)
    {
        title = null;
        var endIndexValue = endIndex ?? moveId.Length;
        if (startIndex == -1 || endIndexValue == -1 || endIndexValue <= startIndex)
        {
            return false;
        }

        var key = $"{monsterName}.moves.{moveId.Substring(startIndex, endIndexValue - startIndex)}.title";
        if (TryGet(key, out var overwriteTitle))
        {
            title = overwriteTitle;
            return true;
        }

        return false;
    }
}
