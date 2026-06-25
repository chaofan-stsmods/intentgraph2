using Godot;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
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

    public Graph MakeGraphFromIntentDefinition(MonsterMoveStateMachine stateMachine, Graph graph, IntentDefinition intentDefinition, Font font)
    {
        var result = new Graph
        {
            Width = graph.Width,
            Height = graph.Height,
            Icons = graph.Icons,
            IconGroups = [.. graph.IconGroups],
            Arrows = [.. graph.Arrows],
        };

        foreach (var label in graph.Labels)
        {
            var resolvedLabel = new Models.Label(label.X, label.Y, localizer.GetOrElse(label.Text, label.Text), label.Align, label.FontSize);
            result.Labels.Add(resolvedLabel);
            if (graph.Expand)
            {
                var lines = resolvedLabel.Text.Split('\n');
                var labelWidth = lines.Select(l => font.GetStringSize(l, fontSize: resolvedLabel.FontSize).X).Max() / NIntentGraph.GridSize;
                result.Height = Math.Max(result.Height, label.Y + (label.FontSize + NIntentGraph.LabelLinePadding) * (lines.Length - 1) / NIntentGraph.GridSize);
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
                AddMove(moveState, new HashSet<string>([move.Id, ..move.Ids ?? []]).ToArray(), result, move.X, move.Y, intentDefinition, move.PossiblePreviousMoveNodeIndices);
            }
        }

        return result;
    }

    public Graph StateNodesToGraph(List<MonsterStateNode> stateNodes, IntentDefinition? intentDefinition)
    {
        var result = new Graph();
        var y = intentDefinition?.Offset.Y ?? 0f;
        var x = intentDefinition?.Offset.X ?? 0f;
        var arrowTarget = new Dictionary<Arrow, MonsterStateNode>();
        foreach (var stateNode in stateNodes)
        {
            y += stateNode.Offset.Y;
            var previousStateNodes = MonsterStateNodeSimplifier.GetPrecessorDict(stateNode.GetAllNodes());

            // Remove self loop if it's the only next state to avoid unnecessary arrow.
            if (stateNodes.Count == 1 && stateNode.NextState == stateNode && stateNode.Children == null)
            {
                stateNode.NextState = null;
                stateNode.NextStateCount = 0;
            }
            var context = new GraphGenerationContext()
            {
                IntentDefinition = intentDefinition,
                ArrowTarget = arrowTarget,
                PreviousStateNodes = previousStateNodes,
            };
            AddStateNodeToGraph(stateNode, precessorNode: null, result, context, x + stateNode.Offset.X, y);
            AddPossiblePreviousMoveIds(result.Moves, context);
            y = result.Height + 0.25f;
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
                AddMove(moveState, stateNode, graph, x, y, context);
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

    private void AddMove(MoveState moveState, MonsterStateNode node, Graph graph, float x, float y, GraphGenerationContext context)
    {
        var move = AddMove(moveState, node.MoveStateIds.ToArray(), graph, x, y, context.IntentDefinition, possiblePreviousMoveIndices: null);
        context.StateNodeToMove[node] = move;
        context.MoveToStateNode[move] = node;
    }

    private Move AddMove(MoveState moveState, string[] ids, Graph graph, float x, float y, IntentDefinition? intentDefinition, int?[]? possiblePreviousMoveIndices)
    {
        var intents = moveState.Intents;
        var icons = new Icon[intents.Count];
        MoveReplacement[]? replacements = null;
        intentDefinition?.MoveReplacements?.TryGetValue(moveState.Id, out replacements);
        for (int i = 0; i < intents.Count; i++)
        {
            var intent = intents[i];
            var replacement = i < replacements?.Length ? replacements[i] : null;
            if (intent is AttackIntent attackIntent)
            {
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType,
                    (int?)attackIntent.DamageCalc?.Invoke(), attackIntent.Repeats,
                    replacement?.ValueText ?? string.Empty, replacement?.TimesText ?? string.Empty);
            }
            else if (intent is StatusIntent statusIntent)
            {
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType, statusIntent.CardCount, ValueText: replacement?.ValueText ?? string.Empty);
            }
            else
            {
                icons[i] = new Icon(i * (1 + IconPaddingInMove) + x, y, intent.IntentType);
            }
        }

        var move = new Move(ids[0], ids, x, y, icons, possiblePreviousMoveIndices);
        graph.Moves.Add(move);

        if (ShowMonsterMoveNames)
        {
            AddMoveName(moveState, graph, x, y);
        }

        return move;
    }

    private void AddMoveName(MoveState moveState, Graph graph, float x, float y)
    {
        var locTable = LocManager.Instance.GetTable("monsters");
        var monsterName = monster.Id.Entry;
        if (monsterName.StartsWith("DECIMILLIPEDE_SEGMENT_"))
        {
            monsterName = "DECIMILLIPEDE_SEGMENT";
        }

        var moveId = moveState.Id;

        string? title;
        if (!TryGetTitle(locTable, monsterName, moveId, out title) && moveId.Contains('_'))
        {
            var index1 = moveId.LastIndexOf('_');
            var index2 = moveId.IndexOf('_');
            _ = TryGetTitle(locTable, monsterName, moveId, out title, endIndex: index1) ||
                TryGetTitle(locTable, monsterName, moveId, out title, endIndex: moveId.LastIndexOf('_', index1 - 1)) ||
                TryGetTitle(locTable, monsterName, moveId, out title, startIndex: index2 + 1) ||
                TryGetTitle(locTable, monsterName, moveId, out title, startIndex: index2 + 1, endIndex: index1);
        }

        if (title != null)
        {
            var width = moveState.Intents.Count + (moveState.Intents.Count - 1) * IconPaddingInMove;
            graph.Labels.Add(new Models.Label(x + width / 2, y + 0.2f, title, Align: "center", FontSize: 15));
        }
    }

    private bool TryGetTitle(LocTable table, string monsterName, string moveId, out string? title, int startIndex = 0, int? endIndex = default)
    {
        title = null;
        var endIndexValue = endIndex ?? moveId.Length;
        if (startIndex == -1 || endIndexValue == -1 || endIndexValue <= startIndex)
        {
            return false;
        }

        var key = $"{monsterName}.moves.{moveId.Substring(startIndex, endIndexValue - startIndex)}.title";
        if (table.HasEntry(key))
        {
            title = table.GetRawText(key);
            return true;
        }

        return false;
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
                    .SelectMany<MonsterStateNode, MonsterStateNode>(n => [n, ..n.GetAllDescendants()])
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
        public int IndexOnGraph { get; set; }
        public float NextNodeX { get; set; }
        public Dictionary<float, MonsterStateNode> HLineTargetNode { get; set; } = new();
        public Dictionary<float, MonsterStateNode> VLineTargetNode { get; set; } = new();
        public Dictionary<int, MonsterStateNode> IndexOnGraphToNode { get; set; } = new();
        public IntentDefinition? IntentDefinition { get; init; }
        public Dictionary<Arrow, MonsterStateNode> ArrowTarget { get; set; } = new();
        public Dictionary<MonsterStateNode, HashSet<MonsterStateNode>> PreviousStateNodes { get; set; } = new();
        public Dictionary<MonsterStateNode, Move> StateNodeToMove { get; set; } = new();
        public Dictionary<Move, MonsterStateNode> MoveToStateNode { get; set; } = new();
    }
}
