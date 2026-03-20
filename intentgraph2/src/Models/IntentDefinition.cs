using System.Collections.Generic;

namespace IntentGraph2.Models;

public class IntentDefinition
{
    public string[]? SecondaryInitialStates { get; set; }

    public Graph? Graph { get; set; }

    public Graph? GraphPatch { get; set; }

    public IDictionary<string, MoveReplacement[]>? MoveReplacements { get; set; }
}

public record class MoveReplacement(string? ValueText, string? TimesText);
