using IntentGraph2.Utils.Expression;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public IntentDefinition? IntentDefinition { get; set; }

    // Used in intents.json only
    public bool Expand { get; set; } = false;

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Width);
        hashCode.Add(Height);
        Icons.ForEach(hashCode.Add);
        IconGroups.ForEach(hashCode.Add);
        Labels.ForEach(hashCode.Add);
        Arrows.ForEach(hashCode.Add);
        Moves.ForEach(hashCode.Add);
        return hashCode.ToHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj?.GetType() != typeof(Graph))
        {
            return false;
        }

        var other = (Graph)obj;
        if (other.Width != Width || other.Height != Height)
        {
            return false;
        }

        if (other.Icons.Count != Icons.Count || other.IconGroups.Count != IconGroups.Count || other.Labels.Count != Labels.Count || other.Arrows.Count != Arrows.Count || other.Moves.Count != Moves.Count)
        {
            return false;
        }

        if (!other.Icons.SequenceEqual(Icons) || !other.IconGroups.SequenceEqual(IconGroups) || !other.Labels.SequenceEqual(Labels) || !other.Arrows.SequenceEqual(Arrows) || !other.Moves.SequenceEqual(Moves))
        {
            return false;
        }

        return true;
    }
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
    string? RelativeTo = null,
    // Generated move detail icons only
    [property: JsonIgnore] string ImageResourcePath = "",
    [property: JsonIgnore] MoveDetailIconType MoveDetailType = MoveDetailIconType.None) : IRelativeToPosition;

public enum MoveDetailIconType
{
    None,
    Power,
    Card,
}

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
    string? RelativeTo = null)
{
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var item in Path)
        {
            hashCode.Add(item);
        }
        hashCode.Add(RelativeTo);
        return hashCode.ToHashCode();
    }

    public virtual bool Equals(Arrow? obj)
    {
        if (obj == null || obj.Path.Length != Path.Length || obj.RelativeTo != RelativeTo)
        {
            return false;
        }
        if (!obj.Path.SequenceEqual(Path))
        {
            return false;
        }
        return true;
    }
}

// If PossiblePreviousMoveNodeIndices is null, means any moves can be previous. If PossiblePreviousMoveNodeIndices is [null], means it's initial move.
public record class Move(
    string Id,
    string[]? Ids = null,
    float X = 0,
    float Y = 0,
    Icon[]? Icons = null,
    int?[]? PossiblePreviousMoveNodeIndices = null,
    // Only works in graph patch
    string? RelativeTo = null,
    // Generated only
    [property: JsonIgnore] IExpression? CurrentMoveCondition = null) : IRelativeToPosition
{
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Id);
        hashCode.Add(X);
        hashCode.Add(Y);
        if (Ids != null)
        {
            foreach (var id in Ids)
            {
                hashCode.Add(id);
            }
        }
        if (Icons != null)
        {
            foreach (var icon in Icons)
            {
                hashCode.Add(icon);
            }
        }
        if (PossiblePreviousMoveNodeIndices != null)
        {
            foreach (var index in PossiblePreviousMoveNodeIndices)
            {
                hashCode.Add(index);
            }
        }
        return hashCode.ToHashCode();
    }

    public virtual bool Equals(Move? obj)
    {
        if (obj == null || obj.Id != Id || obj.X != X || obj.Y != Y || obj.RelativeTo != RelativeTo)
        {
            return false;
        }
        if (Ids?.Length != obj.Ids?.Length || Icons?.Length != obj.Icons?.Length || PossiblePreviousMoveNodeIndices?.Length != obj.PossiblePreviousMoveNodeIndices?.Length)
        {
            return false;
        }
        if (Ids != null && !obj.Ids!.SequenceEqual(Ids))
        {
            return false;
        }
        if (Icons != null && !obj.Icons!.SequenceEqual(Icons))
        {
            return false;
        }
        if (PossiblePreviousMoveNodeIndices != null && !obj.PossiblePreviousMoveNodeIndices!.SequenceEqual(PossiblePreviousMoveNodeIndices))
        {
            return false;
        }
        return true;
    }
}

public interface IRelativeToPosition
{
    float X { get; }
    float Y { get; }
    string? RelativeTo { get; }
}
