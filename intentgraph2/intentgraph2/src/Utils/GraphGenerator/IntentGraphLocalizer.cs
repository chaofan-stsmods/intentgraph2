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
}
