using Godot;
using IntentGraph2.Models;
using MegaCrit.Sts2.Core.Models;

namespace IntentGraph2.Scenes;

public partial class NIntentGraph : Control
{
    public const float GridSize = 80;
    public const int LabelFontSize = 18;
    public const float LabelLinePadding = 2;

    private Graph? graph;
    private MonsterModel? monster;
    private NIntentGraphCanvas? canvas;

    public Graph? Graph
    {
        get => graph;
        set
        {
            graph = value;
            CustomMinimumSize = new Vector2(GridSize * graph?.Width ?? GridSize, GridSize * graph?.Height ?? GridSize)
                * new Vector2(IntentGraphMod.Config.IntentGraphScale, IntentGraphMod.Config.IntentGraphScale);
            if (canvas != null)
            {
                canvas.Graph = value;
            }
        }
    }

    public MonsterModel? Monster
    { 
        get => monster;
        set
        {
            monster = value;
            if (canvas != null)
            {
                canvas.Monster = value;
            }
        }
    }

    public override void _Ready()
    {
        this.canvas = GetNode<NIntentGraphCanvas>("%IntentGraphCanvas");
        canvas.Scale = new Vector2(IntentGraphMod.Config.IntentGraphScale, IntentGraphMod.Config.IntentGraphScale);
        canvas.Graph = graph;
        canvas.Monster = monster;
    }
}
