using Godot;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using static IntentGraph2.Utils.GraphGenerator.IntentGraphGenerator;

namespace IntentGraph2.Utils.GraphGenerator;

internal class IntentGraphLayouter
{
    private readonly IntentGraphLocalizer localizer;

    public IntentGraphLayouter(IntentGraphLocalizer localizer)
    {
        this.localizer = localizer;
    }

    public Graph MakeGraphFromIntentDefinition(MonsterMoveStateMachine stateMachine, Graph graph, IntentDefinition intentDefinition, Font font)
    {
        var result = new Graph
        {
            Width = graph.Width,
            Height = graph.Height,
            Icons = [.. graph.Icons],
            IconGroups = [.. graph.IconGroups],
            Arrows = [.. graph.Arrows],
        };

        foreach (var label in graph.Labels)
        {
            var resolvedLabel = new Models.Label(label.X, label.Y, localizer.GetOrElse(label.Text, label.Text), label.Align);
            result.Labels.Add(resolvedLabel);
            if (graph.Expand)
            {
                var labelWidth = font.GetStringSize(resolvedLabel.Text, fontSize: NIntentGraph.LabelFontSize).X / NIntentGraph.GridSize;
                result.Height = Math.Max(result.Height, label.Y);
                if (label.Align != "right")
                {
                    result.Width = label.Align == "left" ? Math.Max(result.Width, label.X + labelWidth) : Math.Max(result.Width, label.X + labelWidth / 2);
                }
            }
        }

        foreach (var move in graph.Moves)
        {
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == move.Id);
            if (state != null && state is MoveState moveState)
            {
                AddIcons(moveState, result.Icons, move.X, move.Y, intentDefinition);
            }
        }

        return result;
    }

    public Graph StateNodesToGraph(List<MonsterStateNode> stateNodes, IntentDefinition? intentDefinition)
    {
        // Remove self loop if it's the only next state to avoid unnecessary arrow.
        if (stateNodes.Count == 1 && stateNodes[0].NextState == stateNodes[0] && stateNodes[0].Children == null)
        {
            stateNodes[0].NextState = null;
            stateNodes[0].NextStateCount = 0;
        }

        var result = new Graph();
        var y = 0f;
        var arrowTarget = new Dictionary<Arrow, MonsterStateNode>();
        foreach (var stateNode in stateNodes)
        {
            var context = new GraphGenerationContext()
            {
                IntentDefinition = intentDefinition,
                ArrowTarget = arrowTarget,
            };
            AddStateNodeToGraph(stateNode, precessorNode: null, result, context, 0, y);
            y = result.Height + 0.1f;
        }

        TuneArrowPosition(result.Arrows, arrowTarget);
        return result;
    }

    private void AddStateNodeToGraph(MonsterStateNode stateNode, MonsterStateNode? precessorNode, Graph graph, GraphGenerationContext context, float x, float y)
    {
        if (stateNode.AddedToGraph)
        {
            return;
        }

        if (stateNode.SimpleLoopStart && stateNode.SimpleLoopPrecessorCount == 1 &&
            (stateNode.SimpleLoopLength >= 4 || (stateNode.SimpleLoopLength == 3 && (context.NextNodeX >= 3 || graph.Height - y >= 2))))
        {
            AddSimpleLoopToGraph(stateNode, precessorNode, graph, context, x, y);
        }
        else
        {
            AddStateNodeToGraphNormal(stateNode, graph, context, x, y);
        }
    }

    private void AddStateNodeToGraphNormal(MonsterStateNode stateNode, Graph graph, GraphGenerationContext context, float x, float y)
    {
        if (stateNode.AddedToGraph)
        {
            return;
        }

        stateNode.AddedToGraph = true;
        stateNode.X = x;
        stateNode.Y = y;
        if (stateNode.Parent != null)
        {
            stateNode.IndexOnGraph = stateNode.Parent.IndexOnGraph;
        }
        else
        {
            stateNode.IndexOnGraph = context.IndexOnGraph++;
            context.IndexOnGraphToNode[stateNode.IndexOnGraph] = stateNode;
        }

        if (context.NextNodeX < x + stateNode.Width + 0.25f + 0.25f * stateNode.NextStateCount)
        {
            context.NextNodeX = x + stateNode.Width + 0.25f + 0.25f * stateNode.NextStateCount;
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
                AddIcons(moveState, graph.Icons, x, y, context.IntentDefinition);
            }
        }
        else
        {
            float childY = y + IconGroupLabelHeight + IconGroupPadding;
            float childX = x + IconGroupPadding;
            for (int i = 0; i < stateNode.Children.Count; i++)
            {
                var childNode = stateNode.Children[i];
                graph.Labels.Add(new Models.Label(childX, childY - 0.04f, childNode.Label ?? string.Empty));
                if (stateNode.HorizontalLayout)
                {
                    AddStateNodeToGraphNormal(childNode, graph, context, childX, childY + (childNode.Children == null ? IconGroupSingleMovePadding : 0));
                    childX += childNode.Width + IconGroupPadding;
                }
                else
                {
                    if (childNode.Children == null)
                    {
                        childY += IconGroupSingleMovePadding; // reduce padding for single move child
                    }
                    AddStateNodeToGraphNormal(childNode, graph, context, childX, childY);
                    childY += childNode.Height + IconGroupLabelHeight;
                }
            }

            graph.IconGroups.Add(new IconGroup(x, y, stateNode.Width, stateNode.Height));
        }

        if (stateNode.NextState != null)
        {
            var rootNode = stateNode;
            while (rootNode.Parent != null)
            {
                rootNode = rootNode.Parent;
            }

            var nextNode = stateNode.NextState;
            AddStateNodeToGraph(nextNode, stateNode, graph, context, context.NextNodeX, rootNode.Y);
            AddArrow(stateNode, nextNode, graph, context);
        }
    }

    private void AddIcons(MoveState moveState, List<Icon> iconList, float x, float y, IntentDefinition? intentDefinition)
    {
        var intents = moveState.Intents;
        MoveReplacement[]? replacements = null;
        intentDefinition?.MoveReplacements?.TryGetValue(moveState.Id, out replacements);
        for (int i = 0; i < intents.Count; i++)
        {
            var intent = intents[i];
            var replacement = i < replacements?.Length ? replacements[i] : null;
            if (intent is AttackIntent attackIntent)
            {
                iconList.Add(new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType,
                    (int?)attackIntent.DamageCalc?.Invoke(), attackIntent.Repeats,
                    replacement?.ValueText ?? string.Empty, replacement?.TimesText ?? string.Empty));
            }
            else if (intent is StatusIntent statusIntent)
            {
                iconList.Add(new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType, statusIntent.CardCount, ValueText: replacement?.ValueText ?? string.Empty));
            }
            else
            {
                iconList.Add(new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType));
            }
        }
    }

    private void AddSimpleLoopToGraph(MonsterStateNode loopStart, MonsterStateNode? precessorNode, Graph graph, GraphGenerationContext context, float x, float y)
    {
        if (loopStart.AddedToGraph)
        {
            return;
        }

        // Left: o -> o -> o  Bottom: o    o -> o -> o
        //            ^    |          |    ^         |
        //            |    v          |    |         v
        //            o <- o          +----+--- o <- o
        var inputFromLeft = precessorNode == null || (precessorNode.IndexOnGraph == context.IndexOnGraph - 1 && precessorNode.Y < y + 0.5f);

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
        if (loopNodes.Count - secondHalfIndex == 1 && loopNodes.Count > 3 && loopNodes[secondHalfIndex].Width < 1.5f && context.NextNodeX < 2)
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
                AddIcons(moveState, graph.Icons, node.X, node.Y, context.IntentDefinition);
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
        }

        x -= 0.5f;
        context.NextNodeX = x + 0.25f;
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
                    AddIcons(moveState, graph.Icons, node.X, node.Y, context.IntentDefinition);
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
        }
        else
        {
            var prevNode = loopNodes[secondHalfIndex - 1];
            var node = loopNodes[secondHalfIndex];
            node.X = loopStartX + (x - loopStartX) / 2 - node.Width / 2;
            node.Y = secondHalfY;
            if (node.State is MoveState moveState)
            {
                AddIcons(moveState, graph.Icons, node.X, node.Y, context.IntentDefinition);
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
        }

        for (var i = 0; i < loopNodes.Count; i++)
        {
            MonsterStateNode? node = loopNodes[i];
            node.AddedToGraph = true;
            node.AddedArrow = true;
            node.IndexOnGraph = context.IndexOnGraph++;
            context.IndexOnGraphToNode[node.IndexOnGraph] = node;
        }
    }

    private void AddArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        if (stateNode.AddedArrow)
        {
            return;
        }

        stateNode.AddedArrow = true;

        // o--->o or o<---o or both
        if (Math.Abs(stateNode.IndexOnGraph - nextStateNode.IndexOnGraph) == 1 &&
            TryAddHorizontalStraightArrow(stateNode, nextStateNode, graph, context))
        {
            return;
        }

        //      o    o
        //      ^ or ^
        // o----+    +----o
        if (stateNode.Y > nextStateNode.Y + 0.25f && stateNode.IndexOnGraph != nextStateNode.IndexOnGraph)
        {
            if (TryAddHorizontalThenUpArrow(stateNode, nextStateNode, graph, context))
            {
                return;
            }
        }

        //  o       o--+
        //  ^          |
        //  +----------+
        AddDefaultArrow(stateNode, nextStateNode, graph, context);
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
                if (nextStateNode.NextState == stateNode)
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
                if (nextStateNode.NextState == stateNode)
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
        var canDrawStraightLine = true;
        for (int i = Math.Min(stateNode.IndexOnGraph, nextStateNode.IndexOnGraph) + 1; i < Math.Max(stateNode.IndexOnGraph, nextStateNode.IndexOnGraph); i++)
        {
            var midNode = context.IndexOnGraphToNode[i];
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

    private void AddDefaultArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        //  o       o--+
        //  ^          |
        //  +----------+
        var arrowRight = stateNode.ArrowRight;
        var arrowBottom = nextStateNode.IndexOnGraph <= stateNode.IndexOnGraph ? stateNode.ArrowBottom : stateNode.Y + stateNode.Height + 0.25f;

        while (context.VLineTargetNode.TryGetValue(arrowRight, out var vLineTarget) && vLineTarget != nextStateNode)
        {
            arrowRight += 0.25f;
        }

        for (int i = Math.Min(stateNode.IndexOnGraph, nextStateNode.IndexOnGraph) + 1; i < Math.Max(stateNode.IndexOnGraph, nextStateNode.IndexOnGraph); i++)
        {
            var midNode = context.IndexOnGraphToNode[i];
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

    private void AddArrow(Graph graph, Arrow arrow, GraphGenerationContext context, MonsterStateNode target)
    {
        graph.Arrows.Add(arrow);
        context.ArrowTarget[arrow] = target;
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

                        if (h1 && Math.Abs(s1.Y - s2.Y) < 0.12f) // horizontal
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
}
