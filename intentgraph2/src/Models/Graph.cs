using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System.Collections.Generic;

namespace IntentGraph2.Models;

public class Graph
{
    public float Width { get; set; } = 1;
    public float Height { get; set; } = 1;
    public List<Icon> Icons { get; set; } = new();
    public List<IconGroup> IconGroups { get; set; } = new();
    public List<Label> Labels { get; set; } = new();
    public List<Arrow> Arrows { get; set; } = new();

    // Used in intents.json only
    public List<Move> Moves { get; set; } = new();
}

public record class Icon(float X = 0, float Y = 0, IntentType IntentType = IntentType.Hidden, int? Value = null, int Times = 1, string ValueText = "");

public record class IconGroup(float X = 0, float Y = 0, float Width = 1, float Height = 1);

public record class Label(float X = 0, float Y = 0, string Text = "", string Align = "left");

public record class Arrow(float[] Path = null);

public record class Move(float X = 0, float Y = 0, string Id = "");