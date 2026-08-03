using Godot;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static IntentGraph2.Utils.GraphGenerator.IntentGraphGenerator;

namespace IntentGraph2.Utils.GraphGenerator;

internal class IntentGraphLayouter
{
    private readonly MonsterModel monster;
    private readonly IntentGraphLocalizer localizer;

    public IntentGraphLayouter(MonsterModel monster, IntentGraphLocalizer localizer)
    {
        this.monster = monster;
        this.localizer = localizer;
    }

    public Graph MakeGraphFromIntentDefinition(MonsterMoveStateMachine stateMachine, Graph graph, IntentDefinition intentDefinition, Font font, List<MonsterStateNode>? stateNodes = null)
    {
        var allStateNodes = stateNodes?.GetAllNodes().Where(n => n.FullId != null).ToDictionary(n => n.FullId!);
        var result = new Graph
        {
            Width = graph.Width,
            Height = graph.Height,
            Icons = graph.Icons.Select(i => (i, ResolveRelative(i, allStateNodes))).Select(t => t.i with { X = t.Item2.x, Y = t.Item2.y, RelativeTo = null }).ToList(),
            IconGroups = graph.IconGroups.Select(i => (i, ResolveRelative(i, allStateNodes))).Select(t => t.i with { X = t.Item2.x, Y = t.Item2.y, RelativeTo = null }).ToList(),
            Arrows = graph.Arrows.Select(i => ResolveRelative(i, allStateNodes)).ToList(),
        };

        foreach (var label in graph.Labels)
        {
            var (x, y) = ResolveRelative(label, allStateNodes);
            var resolvedLabel = label with
            {
                X = x,
                Y = y,
                Text = localizer.FormatWithVariables(localizer.GetOrElse(label.Text, label.Text)),
                RelativeTo = null,
            };
            result.Labels.Add(resolvedLabel);
            if (graph.Expand)
            {
                var lines = resolvedLabel.Text.Split('\n');
                var labelWidth = lines.Select(l => font.GetStringSize(l, fontSize: resolvedLabel.FontSize).X).Max() / NIntentGraph.GridSize;
                result.Height = Math.Max(result.Height, resolvedLabel.Y + (resolvedLabel.FontSize + NIntentGraph.LabelLinePadding) * (lines.Length - 1) / NIntentGraph.GridSize);
                if (resolvedLabel.Align != "right")
                {
                    result.Width = resolvedLabel.Align == "left" ? Math.Max(result.Width, resolvedLabel.X + labelWidth) : Math.Max(result.Width, resolvedLabel.X + labelWidth / 2);
                }
            }
        }

        foreach (var move in graph.Moves)
        {
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == move.Id);
            if (state != null && state is MoveState moveState)
            {
                var (x, y) = ResolveRelative(move, allStateNodes);
                AddMove(moveState,
                    new HashSet<string>([move.Id, .. move.Ids ?? []]).ToArray(),
                    result,
                    x, y,
                    intentOverrides: null,
                    move.PossiblePreviousMoveNodeIndices,
                    subGraph: null);
            }
        }

        return result;
    }

    public Graph StateNodesToGraph(List<MonsterStateNode> stateNodes, IntentDefinition? intentDefinition)
    {
        var result = new Graph();
        var x = intentDefinition?.Offset.X ?? 0f;
        var context = new GraphGenerationContext()
        {
            IntentDefinition = intentDefinition,
            PreviousStateNodes = MonsterStateNodeSimplifier.GetPrecessorDict(stateNodes.GetAllNodes()),
            Y = intentDefinition?.Offset.Y ?? 0f,
        };
        foreach (var stateNode in stateNodes)
        {
            context.SubGraphs[context.YIndex] = new SubGraph()
            {
                Y = context.Y,
            };
            context.Y += stateNode.Offset.Y;

            // Remove self loop if it's the only next state to avoid unnecessary arrow.
            if (stateNodes.Count == 1 && stateNode.NextState == stateNode && stateNode.Children == null)
            {
                stateNode.NextState = null;
                stateNode.NextStateCount = 0;
            }
            AddStateNodeToGraph(stateNode, precessorNode: null, result, context, x + stateNode.Offset.X);
            AddPossiblePreviousMoveIds(result.Moves, context);
            context.NewLine(result.Height + 0.25f);
        }

        TuneArrowPosition(result.Arrows, context.ArrowTarget);
        return result;
    }

    private (float x, float y) ResolveRelative(IRelativeToPosition relativeToPosition, Dictionary<string, MonsterStateNode>? stateNodes)
    {
        if (stateNodes == null || relativeToPosition.RelativeTo == null || !stateNodes.TryGetValue(relativeToPosition.RelativeTo, out var node))
        {
            return (relativeToPosition.X, relativeToPosition.Y);
        }

        return (node.X + relativeToPosition.X, node.Y + relativeToPosition.Y);
    }

    private Arrow ResolveRelative(Arrow relativeToPosition, Dictionary<string, MonsterStateNode>? stateNodes)
    {
        if (stateNodes == null || relativeToPosition.RelativeTo == null || !stateNodes.TryGetValue(relativeToPosition.RelativeTo, out var node))
        {
            return relativeToPosition;
        }

        var path = (float[])relativeToPosition.Path.Clone();
        path[1] += node.X;
        path[2] += node.Y;
        for (int i = 3; i < path.Length; i++)
        {
            path[i] += path[0] == i % 2 ? node.Y : node.X;
        }
        return new Arrow(path);
    }

    private void AddStateNodeToGraph(MonsterStateNode stateNode, MonsterStateNode? precessorNode, Graph graph, GraphGenerationContext context, float x)
    {
        if (stateNode.AddedToGraph)
        {
            return;
        }

        if (stateNode.SimpleLoopStart && stateNode.SimpleLoopPrecessorCount == 1 &&
            (stateNode.SimpleLoopLength >= 4 || (stateNode.SimpleLoopLength == 3 && (context.NextX >= 3 || graph.Height - context.Y >= 2))))
        {
            AddSimpleLoopToGraph(stateNode, precessorNode, graph, context, x);
        }
        else
        {
            AddStateNodeToGraphNormal(stateNode, graph, context, x, 0);
        }
    }

    private void AddStateNodeToGraphNormal(MonsterStateNode stateNode, Graph graph, GraphGenerationContext context, float x, float yOffset)
    {
        if (stateNode.AddedToGraph)
        {
            return;
        }

        stateNode.AddedToGraph = true;
        stateNode.X = x;
        stateNode.Y = context.Y + yOffset;
        float y = stateNode.Y;
        if (stateNode.Parent != null)
        {
            stateNode.XIndex = stateNode.Parent.XIndex;
            stateNode.YIndex = stateNode.Parent.YIndex;
        }
        else
        {
            stateNode.XIndex = context.NextXIndex++;
            stateNode.YIndex = context.YIndex;
            context.IndexToNode[(stateNode.XIndex, stateNode.YIndex)] = stateNode;
        }

        context.SubGraphs[stateNode.YIndex].Nodes.Add(stateNode);

        if (context.NextX < x + stateNode.Width + 0.25f + 0.25f * stateNode.NextStateCount)
        {
            context.NextX = x + stateNode.Width + 0.25f + 0.25f * stateNode.NextStateCount;
        }

        if (x + stateNode.Width > graph.Width)
        {
            graph.Width = x + stateNode.Width;
        }

        if (y + stateNode.Height > graph.Height)
        {
            graph.Height = y + stateNode.Height;
        }

        if (stateNode.Parent == null)
        {
            stateNode.ArrowRight = x + stateNode.Width + 0.25f;
            stateNode.ArrowBottom = y + stateNode.Height + 0.25f;
        }
        else
        {
            stateNode.ArrowRight = stateNode.Parent.ArrowRight;
            stateNode.ArrowBottom = stateNode.Parent.ArrowBottom;
        }

        if (stateNode.Children == null)
        {
            if (stateNode.State is MoveState moveState)
            {
                AddMove(moveState, stateNode, graph, x, y, context);
            }
        }
        else
        {
            var subGraph = context.SubGraphs[stateNode.YIndex];
            var childYOffset = yOffset + IconGroupLabelHeight + IconGroupPadding;
            var childX = x + IconGroupPadding;
            for (int i = 0; i < stateNode.Children.Count; i++)
            {
                var childNode = stateNode.Children[i];
                graph.Labels.Add(new Models.Label(childX, context.Y + childYOffset - 0.04f, childNode.Label?.Text ?? string.Empty));
                subGraph.LabelIndices.Add(graph.Labels.Count - 1);
                if (stateNode.HorizontalLayout)
                {
                    AddStateNodeToGraphNormal(childNode, graph, context, childX, childYOffset + (childNode.Children == null ? IconGroupSingleMovePadding : 0));
                    childX += childNode.Width + IconGroupPadding;
                }
                else
                {
                    if (childNode.Children == null)
                    {
                        childYOffset += IconGroupSingleMovePadding; // reduce padding for single move child
                    }
                    AddStateNodeToGraphNormal(childNode, graph, context, childX, childYOffset);
                    childYOffset += childNode.Height + IconGroupLabelHeight;
                }
            }

            graph.IconGroups.Add(new IconGroup(stateNode.X, stateNode.Y, stateNode.Width, stateNode.Height));
            subGraph.IconGroupIndices.Add(graph.IconGroups.Count - 1);
        }

        if (stateNode.NextState != null)
        {
            var rootNode = stateNode;
            while (rootNode.Parent != null)
            {
                rootNode = rootNode.Parent;
            }

            var nextNode = stateNode.NextState;
            AddStateNodeToGraph(nextNode, stateNode, graph, context, context.NextX);
            AddArrow(stateNode, nextNode, graph, context);
        }
    }

    private void AddMove(MoveState moveState, MonsterStateNode node, Graph graph, float x, float y, GraphGenerationContext context)
    {
        var subGraph = context.SubGraphs[node.YIndex];
        MoveReplacement? replacement = null;
        var replacements = context.IntentDefinition?.MoveReplacements;
        replacements?.TryGetValue(moveState.Id, out replacement);
        if (node.FullId != null && replacements?.TryGetValue(node.FullId, out var fullIdReplacement) == true)
        {
            replacement = fullIdReplacement;
        }
        var intentOverrides = replacement?.IntentOverrides;
        var move = AddMove(moveState, node.MoveStateIds.ToArray(), graph, x, y, intentOverrides, possiblePreviousMoveIndices: null, subGraph);
        context.StateNodeToMove[node] = move;
        context.MoveToStateNode[move] = node;
    }

    private Move AddMove(
        MoveState moveState,
        string[] ids,
        Graph graph,
        float x,
        float y,
        IntentOverride[]? intentOverrides,
        int?[]? possiblePreviousMoveIndices,
        SubGraph? subGraph)
    {
        var intents = moveState.Intents;
        var icons = new Icon[intents.Count];
        for (int i = 0; i < intents.Count; i++)
        {
            var intent = intents[i];
            var intentOverride = i < intentOverrides?.Length ? intentOverrides[i] : null;
            if (intent is AttackIntent attackIntent)
            {
                var value = (int?)attackIntent.DamageCalc?.Invoke();
                var times = attackIntent.Repeats;
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType,
                    value, times,
                    GetLocalizedValueText(intentOverride?.ValueText, value ?? 0),
                    GetLocalizedValueText(intentOverride?.TimesText, times));
            }
            else if (intent is StatusIntent statusIntent)
            {
                var value = statusIntent.CardCount;
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType, value,
                    ValueText: GetLocalizedValueText(intentOverride?.ValueText, value));
            }
            else
            {
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType);
            }
        }

        var move = new Move(ids[0], ids, x, y, icons, possiblePreviousMoveIndices);
        graph.Moves.Add(move);
        if (subGraph != null)
        {
            subGraph.MoveIndices.Add(graph.Moves.Count - 1);
        }

        if (ShowMonsterMoveNames && icons.Length > 0)
        {
            AddMoveName(moveState, graph, x, y, subGraph);
        }

        return move;
    }

    private string GetLocalizedValueText(string? text, int originalValue)
    {
        if (text == null)
        {
            return string.Empty;
        }

        text = localizer.GetOrElse(text, text);
        text = localizer.FormatWithVariables(text, (v, t) => v == "originalValue" ? originalValue.ToString() : null);
        return text;
    }

    private void AddMoveName(MoveState moveState, Graph graph, float x, float y, SubGraph? subGraph)
    {
        var title = localizer.GetMoveName(this.monster, moveState.Id);

        if (title != null)
        {
            var width = moveState.Intents.Count + (moveState.Intents.Count - 1) * IconPaddingInMove;
            var label = new Models.Label(x + width / 2, y + 0.2f, title, Align: "center", FontSize: 15);
            graph.Labels.Add(label);
            if (subGraph != null)
            {
                subGraph.LabelIndices.Add(graph.Labels.Count - 1);
            }
        }
    }

    private void AddSimpleLoopToGraph(MonsterStateNode loopStart, MonsterStateNode? precessorNode, Graph graph, GraphGenerationContext context, float x)
    {
        if (loopStart.AddedToGraph)
        {
            return;
        }

        float y = context.Y;

        // Left: o -> o -> o  Bottom: o    o -> o -> o
        //            ^    |          |    ^         |
        //            |    v          |    |         v
        //            o <- o          +----+--- o <- o
        var inputFromLeft = precessorNode == null || (precessorNode.XIndex == context.NextXIndex - 1 && precessorNode.Y < y + 0.5f);

        var loopNodes = new List<MonsterStateNode>();
        var node1 = loopStart;
        while (node1 != null && !loopNodes.Contains(node1))
        {
            loopNodes.Add(node1);
            node1 = node1.NextState;
        }

        var loopLength = loopStart.SimpleLoopLength;
        var loopStartX = x;
        var loopStartIndex = loopNodes.IndexOf(loopStart);
        var secondHalfIndex = loopStartIndex + (loopLength + 1) / 2;
        while (loopNodes.Skip(loopStartIndex).Take(secondHalfIndex - loopStartIndex).Sum(n => n.Width) +
            0.5f * (secondHalfIndex - loopStartIndex - 1) + (inputFromLeft ? 0.5f : -loopStart.Width / 2 - 0.5f) <
            loopNodes.Skip(secondHalfIndex).Sum(n => n.Width) + 0.5 * (loopNodes.Count - secondHalfIndex - 1))
        {
            secondHalfIndex++;
        }

        if (loopNodes.Count - secondHalfIndex <= 0)
        {
            AddStateNodeToGraphNormal(loopStart, graph, context, x, y);
            return;
        }

        // Too unbalanced, 3+ vs 1 and the single node is too short
        if (loopNodes.Count - secondHalfIndex == 1 && loopNodes.Count > 3 && loopNodes[secondHalfIndex].Width < 1.5f && context.NextX < 2)
        {
            AddStateNodeToGraphNormal(loopStart, graph, context, x, y);
            return;
        }

        var secondHalfSingleLayout = loopNodes.Count - secondHalfIndex <= 1 && inputFromLeft;
        var secondHalfY = y + 1.35f;

        for (var i = 0; i < secondHalfIndex; i++)
        {
            var node = loopNodes[i];
            node.X = x;
            node.Y = y;
            if (i == loopStartIndex)
            {
                loopStartX = x;
            }
            if (node.State is MoveState moveState)
            {
                AddMove(moveState, node, graph, node.X, node.Y, context);
            }
            if (node.NextState != null)
            {
                if (i == secondHalfIndex - 1)
                {
                    if (!secondHalfSingleLayout)
                    {
                        AddArrow(graph, new Arrow([1, x + node.Width - Math.Min(node.Width, node.NextState.Width) / 2, node.Y + node.Height, secondHalfY]), context, node.NextState);
                    }
                }
                else
                {
                    AddArrow(graph, new Arrow([0, x + node.Width, node.Y + 0.5f, x + node.Width + 0.5f]), context, node.NextState);
                }
            }
            x += node.Width + 0.5f;
        }

        x -= 0.5f;
        context.NextX = x + 0.25f;
        graph.Width = x;
        graph.Height = Math.Max(secondHalfY + 1f, graph.Height);

        if (!secondHalfSingleLayout)
        {
            var loopWidth = x - loopStartX;
            var secondHalfDistance = inputFromLeft && loopNodes.Count - secondHalfIndex != 1 ?
                (loopWidth - loopNodes.Skip(secondHalfIndex).Sum(n => n.Width)) / (loopNodes.Count - secondHalfIndex - 1) : 0.5f;
            for (var i = secondHalfIndex; i < loopNodes.Count; i++)
            {
                var node = loopNodes[i];
                node.X = x - node.Width;
                node.Y = secondHalfY;
                if (node.State is MoveState moveState)
                {
                    AddMove(moveState, node, graph, node.X, node.Y, context);
                }
                if (node.NextState != null)
                {
                    if (i == loopNodes.Count - 1)
                    {
                        if (inputFromLeft)
                        {
                            AddArrow(graph, new Arrow([1, node.X + Math.Min(node.Width, node.NextState.Width) / 2, node.Y, loopStart.Y + loopStart.Height]), context, node.NextState);
                        }
                        else
                        {
                            AddArrow(graph, new Arrow([0, node.X, node.Y + 0.5f, loopStartX + loopStart.Width / 2, loopStart.Y + loopStart.Height]), context, node.NextState);
                        }
                    }
                    else
                    {
                        AddArrow(graph, new Arrow([0, node.X, node.Y + 0.5f, node.X - secondHalfDistance]), context, node.NextState);
                    }
                }
                x -= node.Width + secondHalfDistance;
            }
        }
        else
        {
            var prevNode = loopNodes[secondHalfIndex - 1];
            var node = loopNodes[secondHalfIndex];
            node.X = loopStartX + (x - loopStartX) / 2 - node.Width / 2;
            node.Y = secondHalfY;
            if (node.State is MoveState moveState)
            {
                AddMove(moveState, node, graph, node.X, node.Y, context);
            }
            if (prevNode.X + prevNode.Width / 2 < node.X + node.Width - 0.25f)
            {
                AddArrow(graph, new Arrow([1, prevNode.X + prevNode.Width / 2, prevNode.Y + prevNode.Height, node.Y]), context, node);
            }
            else
            {
                AddArrow(graph, new Arrow([1,
                        Math.Max(prevNode.X + prevNode.Width / 2, node.X + node.Width + 0.25f), prevNode.Y + prevNode.Height,
                        node.Y + 0.5f,
                        node.X + node.Width]), context, node);
            }

            if (loopStart.X + loopStart.Width / 2 > node.X + 0.25f)
            {
                AddArrow(graph, new Arrow([1, loopStart.X + loopStart.Width / 2, node.Y, loopStart.Y + loopStart.Height]), context, loopStart);
            }
            else
            {
                AddArrow(graph, new Arrow([0,
                        node.X, node.Y + 0.5f,
                        Math.Min(loopStart.X + loopStart.Width / 2, node.X - 0.25f),
                        loopStart.Y + loopStart.Height]), context, loopStart);
            }
        }

        for (var i = 0; i < loopNodes.Count; i++)
        {
            MonsterStateNode? node = loopNodes[i];
            node.AddedToGraph = true;
            node.AddedArrow = true;
            node.XIndex = context.NextXIndex++;
            node.YIndex = context.YIndex;
            context.IndexToNode[(node.XIndex, node.YIndex)] = node;
            context.SubGraphs[node.YIndex].Nodes.Add(node);
        }
    }

    private void AddArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        if (stateNode.AddedArrow)
        {
            return;
        }

        stateNode.AddedArrow = true;

        if (TryAddOverrideArrow(stateNode, nextStateNode, graph, context))
        {
            return;
        }

        if (stateNode.YIndex == nextStateNode.YIndex)
        {
            // o--->o or o<---o or both
            if (Math.Abs(stateNode.XIndex - nextStateNode.XIndex) == 1 &&
                TryAddHorizontalStraightArrow(stateNode, nextStateNode, graph, context))
            {
                return;
            }

            //      o    o
            //      ^ or ^
            // o----+    +----o
            if (stateNode.XIndex != nextStateNode.XIndex)
            {
                if (TryAddHorizontalThenUpArrow(stateNode, nextStateNode, graph, context))
                {
                    return;
                }
            }

            //  o   o   o--+    o--+   o   o
            //  ^          | or    |       ^
            //  +----------+       +-------+
            AddDefaultSameYArrow(stateNode, nextStateNode, graph, context);
        }
        else
        {
            // o
            // ^
            // |
            // o
            if (stateNode.YIndex == nextStateNode.YIndex + 1 &&
                TryAddVerticalStraightArrow(stateNode, nextStateNode, graph, context))
            {
                return;
            }

            // o            o
            // ^            ^
            // +---+ or +---+
            //     o    o
            if (TryAddVerticalStartDifferentYArrow(stateNode, nextStateNode, graph, context))
            {
                return;
            }

            // o              o
            // ^              ^
            // +---+ or   +---+
            //     |      |
            //   o-+    o-+
            AddDefaultDifferentYArrow(stateNode, nextStateNode, graph, context);
        }
    }

    private bool TryAddOverrideArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        if (!TryGetArrowOverride(stateNode, context, out var arrowOverride))
        {
            return false;
        }

        // Not long enough. Simply hide the arrow.
        if (arrowOverride.Path.Length <= 3)
        {
            return true;
        }

        var path = (float?[])arrowOverride.Path.Clone();
        var startGenerated = 0;
        var endGenerated = path.Length;
        if (path[0] != null)
        {
            var startHorizontal = path[0] == 0;
            if (startHorizontal)
            {
                if (path[1] == null && path[3] != null)
                {
                    var outX = path[3] ?? 0;
                    path[1] = outX > stateNode.X + stateNode.Width / 2 ? stateNode.X + stateNode.Width : stateNode.X;
                    startGenerated = 1;
                }
                if (path[2] == null)
                {
                    path[2] = stateNode.Y + stateNode.Height / 2;
                    startGenerated = 2;
                }
            }
            else
            {
                if (path[1] == null)
                {
                    path[1] = stateNode.X + stateNode.Width / 2;
                    startGenerated = 1;
                }
                if (path[2] == null && path[3] != null)
                {
                    var outY = path[3] ?? 0;
                    path[2] = outY > stateNode.Y + stateNode.Height / 2 ? stateNode.Y + stateNode.Height : stateNode.Y;
                    startGenerated = 2;
                }
            }

            var endHorizontal = path.Length % 2 == path[0];
            if (endHorizontal)
            {
                var inXIndex = path.Length > 5 ? path.Length - 3 : 1;
                if (path[^1] == null && path[inXIndex] != null)
                {
                    var inX = path[inXIndex] ?? 0;
                    path[^1] = inX > nextStateNode.X + nextStateNode.Width / 2 ? nextStateNode.X + nextStateNode.Width : nextStateNode.X;
                    endGenerated = path.Length - 1;
                }
                if (path[^2] == null)
                {
                    path[^2] = nextStateNode.Y + nextStateNode.Height / 2;
                    endGenerated = path.Length - 2;
                }
            }
            else
            {
                var inYIndex = path.Length > 4 ? path.Length - 3 : 2;
                if (path[^1] == null && path[inYIndex] != null)
                {
                    var inY = path[inYIndex] ?? 0;
                    path[^1] = inY > nextStateNode.Y + nextStateNode.Height / 2 ? nextStateNode.Y + nextStateNode.Height : nextStateNode.Y;
                    endGenerated = path.Length - 1;
                }
                if (path[^2] == null && path.Length > 4)
                {
                    path[^2] = nextStateNode.X + nextStateNode.Width / 2;
                    endGenerated = path.Length - 2;
                }
            }
        }

        var arrow = new Arrow(path.Select(p => p ?? 0).ToArray());
        AddArrow(graph, arrow, context, nextStateNode, addSubGraph: false);
        if (startGenerated > 0)
        {
            context.SubGraphs[stateNode.YIndex].Arrows.Add((arrow, 0, startGenerated + 1));
        }
        if (endGenerated < path.Length)
        {
            context.SubGraphs[nextStateNode.YIndex].Arrows.Add((arrow, endGenerated, path.Length - endGenerated));
        }

        return true;
    }

    private bool TryAddHorizontalStraightArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        var minY = Math.Max(stateNode.Y + 0.25f, nextStateNode.Y + 0.25f);
        var maxY = Math.Min(stateNode.Y + stateNode.Height - 0.25f, nextStateNode.Y + nextStateNode.Height - 0.25f);
        if (minY <= maxY)
        {
            var centerY = (minY + maxY) / 2;
            if (stateNode.X < nextStateNode.X)
            {
                // -->
                // <--
                if (nextStateNode.NextState == stateNode && !nextStateNode.AddedArrow && !TryGetArrowOverride(nextStateNode, context, out _))
                {
                    AddArrow(graph, new Arrow([0, stateNode.X + stateNode.Width, centerY - 0.2f, nextStateNode.X]), context, nextStateNode); // -->
                    AddArrow(graph, new Arrow([0, nextStateNode.X, centerY + 0.2f, stateNode.X + stateNode.Width]), context, stateNode); // <--
                    nextStateNode.AddedArrow = true;
                    return true;
                }
                else // -->
                {
                    var arrow = new Arrow([0, stateNode.X + stateNode.Width, centerY, nextStateNode.X]);
                    if (stateNode.Parent != null)
                    {
                        arrow.Path[1] -= 0.1f;
                    }
                    AddArrow(graph, arrow, context, nextStateNode); // -->
                    return true;
                }
            }
            else
            {
                // <--
                // -->
                if (nextStateNode.NextState == stateNode && !nextStateNode.AddedArrow && !TryGetArrowOverride(nextStateNode, context, out _))
                {
                    AddArrow(graph, new Arrow([0, stateNode.X, centerY + 0.2f, nextStateNode.X + nextStateNode.Width]), context, nextStateNode); // <--
                    AddArrow(graph, new Arrow([0, nextStateNode.X + nextStateNode.Width, centerY - 0.2f, stateNode.X]), context, stateNode); // -->
                    nextStateNode.AddedArrow = true;
                    return true;
                }
                else // <--
                {
                    var arrow = new Arrow([0, stateNode.X, centerY, nextStateNode.X + nextStateNode.Width]);
                    if (stateNode.Parent != null)
                    {
                        arrow.Path[1] += 0.1f;
                    }
                    AddArrow(graph, arrow, context, nextStateNode); // <--
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryAddHorizontalThenUpArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        //      o    o
        //      ^ or ^
        // o----+    +----o
        var lineY = stateNode.Y + stateNode.Height / 2;
        if (lineY < nextStateNode.Y + nextStateNode.Height + 0.25f)
        {
            return false;
        }

        var canDrawStraightLine = true;
        for (int i = Math.Min(stateNode.XIndex, nextStateNode.XIndex) + 1; i < Math.Max(stateNode.XIndex, nextStateNode.XIndex); i++)
        {
            var midNode = context.IndexToNode[(i, stateNode.YIndex)];
            if (midNode.Y + midNode.Height + 0.2f > lineY)
            {
                canDrawStraightLine = false;
                break;
            }
        }

        if (canDrawStraightLine && !context.HLineTargetNode.ContainsKey(lineY))
        {
            context.HLineTargetNode[lineY] = nextStateNode;
            Arrow arrow;
            if (stateNode.X < nextStateNode.X)
            {
                arrow = new Arrow([0, stateNode.X + stateNode.Width, lineY, nextStateNode.X + nextStateNode.Width / 2, nextStateNode.Y + nextStateNode.Height]);
                if (stateNode.Parent != null)
                {
                    arrow.Path[1] -= 0.1f;
                }
            }
            else
            {
                arrow = new Arrow([0, stateNode.X, lineY, nextStateNode.X + nextStateNode.Width / 2, nextStateNode.Y + nextStateNode.Height]);
                if (stateNode.Parent != null)
                {
                    arrow.Path[1] += 0.1f;
                }
            }
            AddArrow(graph, arrow, context, nextStateNode);
            return true;
        }

        return false;
    }

    private void AddDefaultSameYArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        //  o   o   o--+    o--+   o   o
        //  ^          | or    |       ^
        //  +----------+       +-------+
        var arrowRight = stateNode.ArrowRight;
        while (context.VLineTargetNode.TryGetValue(arrowRight, out var vLineTarget) && vLineTarget != nextStateNode)
        {
            arrowRight += 0.25f;
        }

        var arrowBottom = nextStateNode.XIndex <= stateNode.XIndex ? stateNode.ArrowBottom : 0;
        for (int i = Math.Min(stateNode.XIndex, nextStateNode.XIndex) + 1; i < Math.Max(stateNode.XIndex, nextStateNode.XIndex); i++)
        {
            var midNode = context.IndexToNode[(i, stateNode.YIndex)];
            if (arrowBottom < midNode.Y + midNode.Height + 0.25f)
            {
                arrowBottom = midNode.Y + midNode.Height + 0.25f;
            }
        }
        if (arrowBottom < nextStateNode.Y + nextStateNode.Height + 0.25f)
        {
            arrowBottom = nextStateNode.Y + nextStateNode.Height + 0.25f;
        }

        while (context.HLineTargetNode.TryGetValue(arrowBottom, out var hLineTarget) && hLineTarget != nextStateNode)
        {
            arrowBottom += 0.25f;
        }
        context.VLineTargetNode[arrowRight] = nextStateNode;
        context.HLineTargetNode[arrowBottom] = nextStateNode;
        var arrow = new Arrow([0,
                stateNode.X + stateNode.Width, stateNode.Y + stateNode.Height / 2,
                arrowRight,
                arrowBottom,
                nextStateNode.X + nextStateNode.Width / 2,
                nextStateNode.Y + nextStateNode.Height]);
        if (stateNode.Parent != null && stateNode.Children == null)
        {
            arrow.Path[1] -= 0.1f;
        }

        AddArrow(graph, arrow, context, nextStateNode);

        if (arrowRight > graph.Width)
        {
            graph.Width = arrowRight;
        }

        if (arrowBottom > graph.Height)
        {
            graph.Height = arrowBottom;
        }
    }

    private bool TryAddVerticalStraightArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        // o
        // ^
        // |
        // o
        if (stateNode.Parent != null && !stateNode.Parent.HorizontalLayout)
        {
            return false;
        }

        var minX = Math.Max(stateNode.X + 0.25f, nextStateNode.X + 0.25f);
        var maxX = Math.Min(stateNode.X + stateNode.Width - 0.25f, nextStateNode.X + nextStateNode.Width - 0.25f);

        if (minX <= maxX)
        {
            var centerX = (minX + maxX) / 2;
            var arrow = new Arrow([1, centerX, stateNode.Y, nextStateNode.Y + nextStateNode.Height]);
            AddArrow(graph, arrow, context, nextStateNode, addSubGraph: false);
            context.SubGraphs[stateNode.YIndex].Arrows.Add((arrow, 0, 3));
            context.SubGraphs[nextStateNode.YIndex].Arrows.Add((arrow, 3, 1));
            return true;
        }

        return false;
    }

    private bool TryAddVerticalStartDifferentYArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        // o            o
        // ^            ^
        // +---+ or +---+
        //     o    o
        if (stateNode.Parent != null && !stateNode.Parent.HorizontalLayout)
        {
            return false;
        }

        var startX = stateNode.X + stateNode.Width / 2;
        var targetX = nextStateNode.X + nextStateNode.Width / 2;
        var midY = context.HLineTargetNode.Where(p => p.Value == nextStateNode).Select(p => p.Key).OrderBy(p => p).Select<float, float?>(k => k).FirstOrDefault();
        var startSubGraph = context.SubGraphs[stateNode.YIndex];
        if (midY == null)
        {
            var offset = 0.25f;
            midY = startSubGraph.Y;
            context.Y += offset;
            graph.Height += offset;
            startSubGraph.MoveY(graph, offset);
            context.HLineTargetNode[midY.Value] = nextStateNode;
            foreach (var (k, v) in context.HLineTargetNode.Where(p => p.Key > midY.Value).ToList())
            {
                context.HLineTargetNode[k + offset] = v;
                context.HLineTargetNode.Remove(k);
            }
        }

        var arrow = new Arrow([1, startX, stateNode.Y, midY.Value, targetX, nextStateNode.Y + nextStateNode.Height]);
        AddArrow(graph, arrow, context, nextStateNode, addSubGraph: false);
        context.SubGraphs[stateNode.YIndex].Arrows.Add((arrow, 0, 3));
        GetSubGraphByY(context.SubGraphs, midY.Value).Arrows.Add((arrow, 3, 2));
        context.SubGraphs[nextStateNode.YIndex].Arrows.Add((arrow, 5, 1));

        return true;
    }

    private void AddDefaultDifferentYArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        // o              o
        // ^              ^
        // +---+ or   +---+
        //     |      |
        //   o-+    o-+
        var arrowRight = stateNode.ArrowRight;
        while (context.VLineTargetNode.TryGetValue(arrowRight, out var vLineTarget) && vLineTarget != nextStateNode)
        {
            arrowRight += 0.25f;
        }

        var targetX = nextStateNode.X + nextStateNode.Width / 2;
        var midY = context.HLineTargetNode.Where(p => p.Value == nextStateNode).Select(p => p.Key).Select<float, float?>(k => k).FirstOrDefault();
        var startSubGraph = context.SubGraphs[stateNode.YIndex];
        if (midY == null)
        {
            var offset = 0.25f;
            midY = startSubGraph.Y;
            context.Y += offset;
            graph.Height += offset;
            startSubGraph.MoveY(graph, offset);
            context.HLineTargetNode[midY.Value] = nextStateNode;
            foreach (var (k, v) in context.HLineTargetNode.Where(p => p.Key > midY.Value).ToList())
            {
                context.HLineTargetNode[k + offset] = v;
                context.HLineTargetNode.Remove(k);
            }
        }

        context.VLineTargetNode[arrowRight] = nextStateNode;
        var arrow = new Arrow([0,
                stateNode.X + stateNode.Width, stateNode.Y + stateNode.Height / 2,
                arrowRight,
                midY.Value,
                targetX,
                nextStateNode.Y + nextStateNode.Height]);
        if (stateNode.Parent != null && stateNode.Children == null)
        {
            arrow.Path[1] -= 0.1f;
        }

        AddArrow(graph, arrow, context, nextStateNode, addSubGraph: false);
        context.SubGraphs[stateNode.YIndex].Arrows.Add((arrow, 0, 4));
        GetSubGraphByY(context.SubGraphs, midY.Value).Arrows.Add((arrow, 4, 2));
        context.SubGraphs[nextStateNode.YIndex].Arrows.Add((arrow, 6, 1));

        if (arrowRight > graph.Width)
        {
            graph.Width = arrowRight;
        }
    }

    private SubGraph GetSubGraphByY(Dictionary<int, SubGraph> subGraphs, float value)
    {
        var lastSubGraph = subGraphs[0];
        for (var i = 0; subGraphs.TryGetValue(i, out var subGraph); i++)
        {
            if (subGraph.Y >= value)
            {
                break;
            }

            lastSubGraph = subGraph;
        }

        return lastSubGraph;
    }

    private bool TryGetArrowOverride(MonsterStateNode stateNode, GraphGenerationContext context, [NotNullWhen(true)] out ArrowOverride? arrowOverride)
    {
        arrowOverride = null;
        return stateNode.FullId != null &&
            context.IntentDefinition?.MoveReplacements?.TryGetValue(stateNode.FullId, out var replacement) == true &&
            (arrowOverride = replacement?.ArrowOverride) != null;
    }

    private void AddArrow(Graph graph, Arrow arrow, GraphGenerationContext context, MonsterStateNode target, bool addSubGraph = true)
    {
        graph.Arrows.Add(arrow);
        context.ArrowTarget[arrow] = target;
        if (addSubGraph)
        {
            context.SubGraphs[target.YIndex].Arrows.Add((arrow, 0, arrow.Path.Length));
        }
    }

    private void AddPossiblePreviousMoveIds(List<Move> moves, GraphGenerationContext context)
    {
        var newMoves = new List<Move>(moves.Count);
        foreach (var move in moves)
        {
            var node = context.MoveToStateNode.GetValueOrDefault(move);
            if (node == null)
            {
                newMoves.Add(move);
                continue;
            }

            var prevNodes = context.PreviousStateNodes.GetValueOrDefault(node);
            int?[]? previousMoveIndices;
            if (prevNodes != null)
            {
                var indices = prevNodes
                    .SelectMany<MonsterStateNode, MonsterStateNode>(n => [n, .. n.GetAllDescendants()])
                    .Where(n => n.Children == null)
                    .Select<MonsterStateNode, int?>(n => moves.IndexOf(context.StateNodeToMove.GetValueOrDefault(n)!)).Where(v => v != -1 && v != null).ToHashSet();
                if (node.IsInitialState)
                {
                    previousMoveIndices = [null, .. indices];
                }
                else
                {
                    previousMoveIndices = [.. indices];
                }
            }
            else
            {
                if (node.IsInitialState)
                {
                    previousMoveIndices = [null];
                }
                else // should be secondary initial state
                {
                    previousMoveIndices = null;
                }
            }

            newMoves.Add(move with
            {
                PossiblePreviousMoveNodeIndices = previousMoveIndices,
            });
        }

        moves.Clear();
        moves.AddRange(newMoves);
    }

    private void TuneArrowPosition(List<Arrow> arrows, Dictionary<Arrow, MonsterStateNode> arrowTarget)
    {
        for (var i = 0; i < arrows.Count; i++)
        {
            var arrow1 = arrows[i];
            for (var j = i + 1; j < arrows.Count; j++)
            {
                var arrow2 = arrows[j];
                var sameTarget = arrowTarget[arrow1] == arrowTarget[arrow2];

                foreach (var (h1, s1, e1, p1) in ArrowSegments(arrow1))
                {
                    foreach (var (h2, s2, e2, p2) in ArrowSegments(arrow2))
                    {
                        if (h1 != h2)
                        {
                            break;
                        }

                        if (h1 && Math.Abs(s1.Y - s2.Y) < 0.2f) // horizontal
                        {
                            // same target & same end arrows don't need to adjust
                            if (sameTarget && Math.Abs(e1.X - e2.X) < 0.001f)
                            {
                                var centerY = (s1.Y + s2.Y) / 2;
                                arrow1.Path[p1] = centerY;
                                arrow2.Path[p2] = centerY;
                                continue;
                            }

                            var min1x = Math.Min(s1.X, e1.X);
                            var min2x = Math.Min(s2.X, e2.X);
                            var max1x = Math.Max(s1.X, e1.X);
                            var max2x = Math.Max(s2.X, e2.X);
                            if (Math.Max(min1x, min2x) < Math.Min(max1x, max2x))
                            {
                                var centerY = (s1.Y + s2.Y) / 2;
                                arrow1.Path[p1] = centerY + (s1.X < s2.X ? -0.15f : 0.15f);
                                arrow2.Path[p2] = centerY + (s1.X < s2.X ? 0.15f : -0.15f);
                            }
                        }

                        if (!h1 && Math.Abs(s1.X - s2.X) < 0.12) // vertical
                        {
                            // same target & same end arrows don't need to adjust
                            if (sameTarget && Math.Abs(e1.Y - e2.Y) < 0.001f)
                            {
                                var centerX = (s1.X + s2.X) / 2;
                                arrow1.Path[p1] = centerX;
                                arrow2.Path[p2] = centerX;
                                continue;
                            }

                            var min1y = Math.Min(s1.Y, e1.Y);
                            var min2y = Math.Min(s2.Y, e2.Y);
                            var max1y = Math.Max(s1.Y, e1.Y);
                            var max2y = Math.Max(s2.Y, e2.Y);
                            if (Math.Max(min1y, min2y) < Math.Min(max1y, max2y))
                            {
                                var centerX = (s1.X + s2.X) / 2;
                                arrow1.Path[p1] = centerX + (s1.Y < s2.Y ? -0.15f : 0.15f);
                                arrow2.Path[p2] = centerX + (s1.Y < s2.Y ? 0.15f : -0.15f);
                            }
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<(bool horizontal, Vector2 start, Vector2 end, int pathIndex)> ArrowSegments(Arrow arrow)
    {
        var horizontal = arrow.Path[0] == 0;
        var x = arrow.Path[1];
        var y = arrow.Path[2];
        var xIndex = 1;
        var yIndex = 2;
        for (var i = 3; i < arrow.Path.Length; i++)
        {
            if (horizontal)
            {
                yield return (horizontal, new Vector2(x, y), new Vector2(arrow.Path[i], y), yIndex);
                x = arrow.Path[i];
                xIndex = i;
            }
            else
            {
                yield return (horizontal, new Vector2(x, y), new Vector2(x, arrow.Path[i]), xIndex);
                y = arrow.Path[i];
                yIndex = i;
            }

            horizontal = !horizontal;
        }

        yield break;
    }

    private class GraphGenerationContext
    {
        public int NextXIndex { get; set; }
        public int YIndex { get; set; }
        public float NextX { get; set; }
        public float Y { get; set; }
        public Dictionary<float, MonsterStateNode> HLineTargetNode { get; set; } = new();
        public Dictionary<float, MonsterStateNode> VLineTargetNode { get; set; } = new();
        public Dictionary<(int x, int y), MonsterStateNode> IndexToNode { get; set; } = new();
        public IntentDefinition? IntentDefinition { get; init; }
        public Dictionary<Arrow, MonsterStateNode> ArrowTarget { get; set; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<MonsterStateNode, HashSet<MonsterStateNode>> PreviousStateNodes { get; set; } = new();
        public Dictionary<MonsterStateNode, Move> StateNodeToMove { get; set; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<Move, MonsterStateNode> MoveToStateNode { get; set; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<int, SubGraph> SubGraphs { get; set; } = new();

        internal void NewLine(float y)
        {
            NextXIndex = 0;
            NextX = 0;
            YIndex++;
            VLineTargetNode.Clear();
            Y = y;
        }
    }

    private class SubGraph
    {
        public float Y { get; set; }
        public List<int> MoveIndices { get; set; } = new();
        public List<int> LabelIndices { get; set; } = new();
        public List<int> IconGroupIndices { get; set; } = new();
        public List<(Arrow arrow, int startIndex, int length)> Arrows { get; set; } = new();
        public List<MonsterStateNode> Nodes { get; set; } = new();

        internal void MoveY(Graph graph, float yOffset)
        {
            Y += yOffset;
            foreach (var moveIndex in MoveIndices)
            {
                var move = graph.Moves[moveIndex];
                graph.Moves[moveIndex] = move with
                {
                    Y = move.Y + yOffset,
                    Icons = move.Icons?.Select(icon => icon with { Y = icon.Y + yOffset }).ToArray()
                };
            }
            foreach (var labelIndex in LabelIndices)
            {
                var label = graph.Labels[labelIndex];
                graph.Labels[labelIndex] = label with
                {
                    Y = label.Y + yOffset
                };
            }
            foreach (var iconGroupIndex in IconGroupIndices)
            {
                var iconGroup = graph.IconGroups[iconGroupIndex];
                graph.IconGroups[iconGroupIndex] = iconGroup with
                {
                    Y = iconGroup.Y + yOffset
                };
            }
            foreach (var node in Nodes)
            {
                node.Y += yOffset;
            }
            foreach (var (arrow, startIndex, length) in Arrows)
            {
                if (2 >= startIndex && 2 < startIndex + length)
                {
                    arrow.Path[2] += yOffset;
                }

                for (var i = Math.Max(3, startIndex); i < startIndex + length; i++)
                {
                    if (i % 2 == arrow.Path[0])
                    {
                        arrow.Path[i] += yOffset;
                    }
                }
            }
        }
    }
}
