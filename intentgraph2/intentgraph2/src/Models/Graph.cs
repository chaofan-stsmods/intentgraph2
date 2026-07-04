using IntentGraph2.Utils.Rule;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IntentGraph2.Models;

public class Graph
{
    public float Width { get; set; } = 1;
    public float Height { get; set; } = 1;
    public List<Icon> Icons { get; set; } = new();
    public List<IconGroup> IconGroups { get; set; } = new();
    public List<Label> Labels { get; set; } = new();
    public List<Arrow> Arrows { get; set; } = new();
    public List<Move> Moves { get; set; } = new();

    // Generated only
    [JsonIgnore]
    public string? Warning { get; set; }

    [JsonIgnore]
    public IRule? Condition { get; set; }

    // Used in intents.json only
    public bool Expand { get; set; } = false;
}

public record class Icon(
    float X = 0,
    float Y = 0,
    IntentType IntentType = IntentType.Hidden,
    int? Value = null,
    int Times = 1,
    string ValueText = "",
    string TimesText = "",
    // Only works in graph patch
    string? RelativeTo = null) : IRelativeToPosition;

public record class IconGroup(
    float X = 0,
    float Y = 0,
    float Width = 1,
    float Height = 1,
    // Only works in graph patch
    string? RelativeTo = null) : IRelativeToPosition;

public record class Label(
    float X = 0, 
    float Y = 0,
    string Text = "",
    string Align = "left",
    int FontSize = 18,
    // Only works in graph patch
    string? RelativeTo = null) : IRelativeToPosition;

public record class Arrow(
    float[] Path,
    // Only works in graph patch
    string? RelativeTo = null);

// If PossiblePreviousMoveNodeIndices is null, means any moves can be previous. If PossiblePreviousMoveNodeIndices is [null], means it's initial move.
public record class Move(
    string Id,
    string[]? Ids = null,
    float X = 0,
    float Y = 0,
    Icon[]? Icons = null,
    int?[]? PossiblePreviousMoveNodeIndices = null,
    // Only works in graph patch
    string? RelativeTo = null) : IRelativeToPosition;

public interface IRelativeToPosition
{
    float X { get; }
    float Y { get; }
    string? RelativeTo { get; }
}