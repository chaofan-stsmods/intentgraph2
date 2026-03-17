namespace IntentGraph2.Models;

public class IntentDefinition
{
    public string[]? SecondaryInitialStates { get; set; }

    public Graph? Graph { get; set; }

    public Graph? GraphPatch { get; set; }
}
