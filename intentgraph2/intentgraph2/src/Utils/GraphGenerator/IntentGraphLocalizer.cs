using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace IntentGraph2.Utils.GraphGenerator;

internal class IntentGraphLocalizer
{
    private readonly IReadOnlyDictionary<string, string>? overwriteIntentStrings;

    public IntentGraphLocalizer(IReadOnlyDictionary<string, string>? overwriteIntentStrings)
    {
        this.overwriteIntentStrings = overwriteIntentStrings;
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

    public string? GetMoveName(MonsterModel monster, string moveId)
    {
        var locTable = LocManager.Instance.GetTable("monsters");
        var monsterName = monster.Id.Entry;
        if (monsterName.StartsWith("DECIMILLIPEDE_SEGMENT_"))
        {
            monsterName = "DECIMILLIPEDE_SEGMENT";
        }

        string? title;
        if (!TryGetTitle(locTable, monsterName, moveId, out title) && moveId.Contains('_'))
        {
            var index1 = moveId.LastIndexOf('_');
            var index2 = moveId.IndexOf('_');
            _ = TryGetTitle(locTable, monsterName, moveId, out title, endIndex: index1) ||
                TryGetTitle(locTable, monsterName, moveId, out title, endIndex: moveId.LastIndexOf('_', index1 - 1)) ||
                TryGetTitle(locTable, monsterName, moveId, out title, startIndex: index2 + 1) ||
                TryGetTitle(locTable, monsterName, moveId, out title, startIndex: index2 + 1, endIndex: index1);
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
}
