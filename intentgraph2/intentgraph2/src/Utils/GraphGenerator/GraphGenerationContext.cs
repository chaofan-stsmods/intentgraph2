using IntentGraph2.Models;
using System.Collections.Generic;

namespace IntentGraph2.Utils.GraphGenerator;

internal class GraphGenerationContext
{
    public int IndexOnGraph { get; set; }
    public float NextNodeX { get; set; }
    public Dictionary<float, MonsterStateNode> HLineTargetNode { get; set; } = new();
    public Dictionary<float, MonsterStateNode> VLineTargetNode { get; set; } = new();
    public Dictionary<int, MonsterStateNode> IndexOnGraphToNode { get; set; } = new();
    public IntentDefinition? IntentDefinition { get; init; }
    public Dictionary<Arrow, MonsterStateNode> ArrowTarget { get; set; } = new();
}
