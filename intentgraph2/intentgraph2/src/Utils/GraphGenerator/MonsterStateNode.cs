using Godot;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using static IntentGraph2.Utils.GraphGenerator.IntentGraphGenerator;

namespace IntentGraph2.Utils.GraphGenerator;

internal class MonsterStateNode
{
    public float Width { get; set; }
    public float Height { get; set; }
    public MonsterStateNode? Parent { get; set; }
    public List<MonsterStateNode>? Children { get; set; }
    public bool HorizontalLayout { get; set; } // Whether child nodes are arranged horizontally.
    public string? Label { get; set; } // When it's a child
    public bool IsLabelGenerated { get; set; } // Whether the label is auto generated.
    public MonsterState? State { get; set; }
    public MonsterStateNode? NextState { get; set; }
    public List<string> MoveStateIds { get; set; } = new(); // Include children's, MoveState only.
    public bool IsInitialState { get; set; }
    public int NextStateCount { get; set; } // include children's next states
    public bool UnrecognizedStateType { get; set; }
    public bool SimpleLoopStart { get; set; }
    public int SimpleLoopLength { get; set; }
    public int SimpleLoopPrecessorCount { get; set; }

    // Following are used for graph layout

    public float X { get; set; }
    public float Y { get; set; }
    public bool AddedToGraph { get; set; }
    public int IndexOnGraph { get; set; }
    public bool AddedArrow { get; set; }
    public float ArrowRight { get; set; }
    public float ArrowBottom { get; set; }

    public void CalculateNodeSize()
    {
        var children = Children;
        if (children == null)
        {
            return;
        }

        if (HorizontalLayout)
        {
            Width = children.Select(c => c.Width).Sum() + IconGroupPadding * (children.Count - 1) + IconGroupPadding * 2;
            Height = children.Select(c => c.Height).DefaultIfEmpty(1).Max() + IconGroupLabelHeight + IconGroupPadding * 2 +
                (children.All(c => c.Children == null) ? IconGroupSingleMovePadding : 0);
        }
        else
        {
            Width = children.Select(c => c.Width).DefaultIfEmpty(1).Max() + IconGroupPadding * 2;
            Height = IconGroupLabelHeight * children.Count + IconGroupPadding * 2 +
                children.Select(c => c.Height).Sum() +
                IconGroupSingleMovePadding * children.Where(c => c.Children == null).Count();
        }
    }

    public HashSet<MonsterStateNode> GetAllNodes()
    {
        var visited = new HashSet<MonsterStateNode>();
        var queue = new Queue<MonsterStateNode>();
        queue.Enqueue(this);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node))
            {
                continue;
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }
            if (node.NextState != null)
            {
                queue.Enqueue(node.NextState);
            }
        }

        return visited;
    }

    public void AddMoveStateIdsFrom(MonsterStateNode b)
    {
        MoveStateIds.AddRange(b.MoveStateIds);
        if (Children != null && b.Children != null)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].AddMoveStateIdsFrom(b.Children[i]);
            }
        }
    }

    public void SetIsInitialState(bool value)
    {
        IsInitialState = value;
        if (Children != null)
        {
            foreach (var child in Children)
            {
                child.SetIsInitialState(value);
            }
        }
    }
}
