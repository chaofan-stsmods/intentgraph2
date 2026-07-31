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
    private Vector2 graphScale = new Vector2(1, 1);
    private MonsterModel? monster;
    private NIntentGraphCanvas? canvas;
    private bool animatedIcons = false;
    private bool showCurrentMove = false;

    public Graph? Graph
    {
        get => graph;
        set
        {
            graph = value;
            CustomMinimumSize = new Vector2(GridSize * graph?.Width ?? GridSize, GridSize * graph?.Height ?? GridSize) * graphScale;
            if (canvas != null)
            {
                canvas.Graph = value;
            }
        }
    }

    public Vector2 GraphScale
    {
        get => graphScale;
        set
        {
            graphScale = value;
            CustomMinimumSize = new Vector2(GridSize * graph?.Width ?? GridSize, GridSize * graph?.Height ?? GridSize) * graphScale;
            if (canvas != null)
            {
                canvas.Scale = value;
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

    public bool AnimatedIcons
    {
        get => animatedIcons;
        set
        {
            animatedIcons = value;
            if (canvas != null)
            {
                canvas.AnimatedIcons = value;
            }
        }
    }

    public bool ShowCurrentMove
    {
        get => showCurrentMove;
        set
        {
            showCurrentMove = value;
            if (canvas != null)
            {
                canvas.ShowCurrentMove = value;
            }
        }
    }

    public override void _Ready()
    {
        this.canvas = GetNode<NIntentGraphCanvas>("%IntentGraphCanvas");
        canvas.Scale = graphScale;
        canvas.Graph = graph;
        canvas.Monster = monster;
        canvas.AnimatedIcons = animatedIcons;
        canvas.ShowCurrentMove = showCurrentMove;
    }
}
