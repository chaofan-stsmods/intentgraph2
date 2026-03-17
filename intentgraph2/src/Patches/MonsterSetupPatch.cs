using Godot;
using HarmonyLib;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IntentGraph2.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded))]
public class MonsterSetupPatch
{
    public static readonly ConditionalWeakTable<MonsterModel, Graph> GeneratedGraphs = new();

    public static void Postfix(CombatManager __instance, Creature creature)
    {
        if (creature.IsMonster)
        {
            try
            {
                var monster = creature.Monster;
                var graph = GenerateGraph(monster);
                if (monster != null && graph != null)
                {
                    GeneratedGraphs.TryAdd(monster, graph);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex.ToString());
            }
        }
    }

    [return: NotNullIfNotNull(nameof(monster))]
    private static Graph? GenerateGraph(MonsterModel? monster)
    {
        if (monster?.MoveStateMachine == null)
        {
            return null;
        }

        var stateMachine = monster.MoveStateMachine;
        var initialState = stateMachine.GetInitialState();

        var intentDefinition = IntentGraphMod.IntentDefinitions.GetValueOrDefault(monster.GetType().FullName ?? string.Empty);
        if (intentDefinition?.Graph != null)
        {
            return MakeGraphFromIntentDefinition(stateMachine, intentDefinition.Graph);
        }

        var font = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        var stateNodes = ToMonsterStateNode(monster.GetType().FullName ?? "_unknownMonster", font, stateMachine, initialState, intentDefinition?.SecondaryInitialStates);
        var graph = StateNodesToGraph(stateNodes);

        if (intentDefinition?.GraphPatch != null)
        {
            var patch = MakeGraphFromIntentDefinition(stateMachine, intentDefinition.GraphPatch);
            graph.Width = Math.Max(graph.Width, patch.Width);
            graph.Height = Math.Max(graph.Height, patch.Height);
            graph.Icons.AddRange(patch.Icons);
            graph.IconGroups.AddRange(patch.IconGroups);
            graph.Labels.AddRange(patch.Labels);
            graph.Arrows.AddRange(patch.Arrows);
        }

        return graph;
    }

    private static Graph MakeGraphFromIntentDefinition(MonsterMoveStateMachine stateMachine, Graph graph)
    {
        var result = new Graph
        {
            Width = graph.Width,
            Height = graph.Height,
            Icons = [.. graph.Icons],
            IconGroups = [.. graph.IconGroups],
            Labels = [.. graph.Labels],
            Arrows = [.. graph.Arrows],
        };

        foreach (var move in graph.Moves)
        {
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == move.Id);
            if (state != null && state is MoveState moveState)
            {
                AddIcons(moveState.Intents, result.Icons, move.X, move.Y);
            }
        }

        return result;
    }

    private static List<MonsterStateNode> ToMonsterStateNode(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState initialState, string[]? secondaryStates)
    {
        var existingNodes = new Dictionary<MonsterState, MonsterStateNode>();

        var result = new List<MonsterStateNode>();
        MonsterStateNode? initialStateNode = null;

        // For conditional branch to check monster slot.
        if (initialState is ConditionalBranchState conditionalBranchState)
        {
            var stateName = conditionalBranchState.EvaluateStates();
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == stateName);
            if (state != null)
            {
                initialStateNode = ToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null);
            }
        }

        if (initialStateNode == null)
        {
            initialStateNode = ToMonsterStateNode(monsterName, font, stateMachine, initialState, existingNodes, parent: null);
        }

        SimplifyStateNodes(initialStateNode);
        result.Add(initialStateNode);

        if (secondaryStates != null)
        {
            foreach (var stateName in secondaryStates)
            {
                var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == stateName);
                if (state != null && !existingNodes.ContainsKey(state))
                {
                    var stateNode = ToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null);
                    SimplifyStateNodes(stateNode);
                    result.Add(stateNode);
                }
            }
        }

        return result;
    }

    [return: NotNullIfNotNull(nameof(state))]
    private static MonsterStateNode? ToMonsterStateNode(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState? state, Dictionary<MonsterState, MonsterStateNode> existingNodes, MonsterStateNode? parent)
    {
        if (state == null)
        {
            return null;
        }

        if (parent == null && existingNodes.TryGetValue(state, out var existingNode))
        {
            return existingNode;
        }

        if (state is MoveState moveState)
        {
            var result = new MonsterStateNode
            {
                State = state,
                Width = moveState.Intents.Count - (moveState.Intents.Count - 1) * 0.28f,
                Height = 1,
                NextStateCount = 1,
                Parent = parent,
            };

            if (parent == null)
            {
                existingNodes[state] = result;
            }

            result.NextState = ToMonsterStateNode(monsterName, font, stateMachine, moveState.FollowUpState, existingNodes, parent: null);

            return result;
        }
        else
        {
            var texts = new List<string>();
            var states = new List<string>();
            if (state is RandomBranchState randomBranchState)
            {
                var sumWeight = randomBranchState.States.Sum(s => s.GetWeight());
                foreach (var s in randomBranchState.States)
                {
                    if (IntentGraphMod.IntentGraphStrings.TryGetValue($"branch.{monsterName}.{state.Id}.{s.stateId}", out var overwriteText))
                    {
                        texts.Add(overwriteText);
                    }
                    else
                    {
                        texts.Add(MakeText(s, sumWeight));
                    }
                    states.Add(s.stateId);
                }
            }
            else if (state is ConditionalBranchState conditionalBranchState)
            {
                // INIT_MOVE is related to monster slot, which is determined at the beginning of the combat, so we can evaluate it directly to get a more accurate graph.
                if (state.Id == "INIT_MOVE")
                {
                    var evaluatedSstateName = conditionalBranchState.EvaluateStates();
                    var evaluatedState = stateMachine.States.Values.FirstOrDefault(s => s.Id == evaluatedSstateName);
                    if (evaluatedState != null)
                    {
                        return ToMonsterStateNode(monsterName, font, stateMachine, evaluatedState, existingNodes, parent);
                    }
                }

                var conditionalStates = conditionalBranchState.GetStates();
                foreach (var s in conditionalStates)
                {
                    if (!states.Contains(s))
                    {
                        states.Add(s);
                    }
                }

                texts = states.Select(s => IntentGraphMod.IntentGraphStrings.GetValueOrDefault($"branch.{monsterName}.{state.Id}.{s}", "condition")).ToList();
            }

            var result = new MonsterStateNode
            {
                State = state,
                Parent = parent,
            };

            if (parent == null)
            {
                existingNodes[state] = result;
            }

            var children = new List<(string label, MonsterStateNode node)>();
            for (int i = 0; i < states.Count; i++)
            {
                var nextStateId = states[i];
                var text = texts[i];
                var nextState = stateMachine.States.Values.FirstOrDefault(s => s.Id == nextStateId);
                if (nextState != null)
                {
                    var nextStateNode = ToMonsterStateNode(monsterName, font, stateMachine, nextState, existingNodes, parent: result);
                    if (nextStateNode != null)
                    {
                        children.Add((text, nextStateNode));
                    }
                }
            }

            var nextStateOfChildren = children.Select(c => c.node.NextState).Distinct().ToList();
            if (nextStateOfChildren.Count == 1)
            {
                foreach (var child in children)
                {
                    child.node.NextState = null;
                    child.node.NextStateCount = 0;
                }
            }

            var longestText = texts.Select(t => font.GetStringSize(t, fontSize: 18).X).DefaultIfEmpty(0).Max() / NIntentGraph.GridSize;

            result.Width = Math.Max(longestText, children.Select(c => c.node.Width).DefaultIfEmpty(1).Max()) + 0.2f; // 0.1 padding
            // 0.1 padding, 0.25 label, -0.15 for single move
            result.Height = 0.25f * children.Count + children.Select(c => c.node.Height).Sum() + 0.2f - 0.15f * children.Where(c => c.node.Children == null).Count();
            result.Children = children;
            result.NextState = nextStateOfChildren.Count == 1 ? nextStateOfChildren[0] : null;
            result.NextStateCount = (result.NextState == null ? 0 : 1) + children.Select(c => c.node.NextStateCount).DefaultIfEmpty(0).Max();

            return result;
        }
    }

    private static string MakeText(RandomBranchState.StateWeight s, float sumWeight)
    {
        var weight = s.GetWeight();
        var percentage = (int)(weight / sumWeight * 100);
        return percentage + "%" + (s.cooldown > 0 ? ", ⏱" + s.cooldown : s.repeatType switch
        {
            MoveRepeatType.CanRepeatForever => "",
            MoveRepeatType.CanRepeatXTimes => ", ≤" + s.maxTimes,
            MoveRepeatType.CannotRepeat => ", ≤1",
            MoveRepeatType.UseOnlyOnce => ", " + IntentGraphMod.IntentGraphStrings.GetValueOrDefault("ui.UseOnlyOnce"),
            _ => ""
        });
    }

    private static void SimplifyStateNodes(MonsterStateNode stateNode)
    {
        var visited = new HashSet<MonsterStateNode>();
        var queue = new Queue<MonsterStateNode>();
        queue.Enqueue(stateNode);
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
                    queue.Enqueue(child.node);
                }
            }
            if (node.NextState != null)
            {
                queue.Enqueue(node.NextState);
            }
        }

        var rootNodes = new List<MonsterStateNode>(visited.Where(n => n.Parent == null));
        var existingSameNodes = new List<(MonsterStateNode, MonsterStateNode)>();
        for (int i = 0; i < rootNodes.Count; i++)
        {
            MonsterStateNode? nodeA = rootNodes[i];
            for (int j = i + 1; j < rootNodes.Count; j++)
            {
                MonsterStateNode? nodeB = rootNodes[j];
                if (AreSameNode(nodeA, nodeB, existingSameNodes))
                {
                    existingSameNodes.Add((nodeA, nodeB));
                }
            }
        }

        var replacement = new Dictionary<MonsterStateNode, MonsterStateNode>();
        foreach (var (a, b) in existingSameNodes)
        {
            if (!replacement.ContainsKey(a) && !replacement.ContainsKey(b))
            {
                replacement[b] = a;
            }
        }

        foreach (var node in visited)
        {
            if (node.NextState != null && replacement.TryGetValue(node.NextState, out var replacementNextState))
            {
                node.NextState = replacementNextState;
            }
        }
    }

    private static bool AreSameNode(MonsterStateNode a, MonsterStateNode b, List<(MonsterStateNode, MonsterStateNode)> exisitingSameNodes)
    {
        if (exisitingSameNodes.Contains((a, b)) || exisitingSameNodes.Contains((b, a)))
        {
            return true;
        }

        if (a == b)
        {
            return true;
        }

        if ((a.Children != null) != (b.Children != null))
        {
            return false;
        }

        if ((a.NextState != null) != (b.NextState != null))
        {
            return false;
        }

        // pretend a and b are the same and check next states.
        exisitingSameNodes.Add((a, b));
        try
        {
            if (a.Children != null)
            {
                if (a.Children.Count != b.Children!.Count)
                {
                    return false;
                }
                for (int i = 0; i < a.Children.Count; i++)
                {
                    if (a.Children[i].label != b.Children[i].label)
                    {
                        return false;
                    }
                    if (!AreSameNode(a.Children[i].node, b.Children[i].node, exisitingSameNodes))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (a.State != b.State)
                {
                    return false;
                }
            }

            if (a.NextState != null)
            {
                if (!AreSameNode(a.NextState, b.NextState!, exisitingSameNodes))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            exisitingSameNodes.Remove((a, b));
        }
    }

    private static Graph StateNodesToGraph(List<MonsterStateNode> stateNodes)
    {
        var result = new Graph();
        var y = 0f;
        foreach (var stateNode in stateNodes)
        {
            AddStateNodeToGraph(stateNode, result, new GraphGenerationContext(), 0, y);
            y = result.Height;
        }
        return result;
    }

    private static void AddStateNodeToGraph(MonsterStateNode stateNode, Graph graph, GraphGenerationContext context, float x, float y)
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
                AddIcons(moveState.Intents, graph.Icons, x, y);
            }
        }
        else
        {
            float childY = y + 0.35f;
            for (int i = 0; i < stateNode.Children.Count; i++)
            {
                var (label, childNode) = stateNode.Children[i];
                graph.Labels.Add(new Models.Label(x + 0.1f, childY - 0.025f, label));
                if (childNode.Children == null)
                {
                    childY -= 0.15f; // reduce padding for single move child
                }
                AddStateNodeToGraph(childNode, graph, context, x + 0.1f, childY);
                childY += childNode.Height + 0.25f;
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

            AddStateNodeToGraph(stateNode.NextState, graph, context, context.NextNodeX, y: rootNode.Y);
            AddArrow(stateNode, stateNode.NextState, graph, context);
        }
    }

    private static void AddIcons(IReadOnlyList<AbstractIntent> intents, List<Icon> iconList, float x, float y)
    {
        for (int i = 0; i < intents.Count; i++)
        {
            AbstractIntent intent = intents[i];
            if (intent is AttackIntent attackIntent)
            {
                iconList.Add(new Icon(i * 0.72f + x, y, intent.IntentType, (int?)attackIntent.DamageCalc?.Invoke(), attackIntent.Repeats));
            }
            else if (intent is StatusIntent statusIntent)
            {
                iconList.Add(new Icon(i * 0.72f + x, y, intent.IntentType, statusIntent.CardCount));
            }
            else
            {
                iconList.Add(new Icon(i * 0.72f + x, y, intent.IntentType));
            }
        }
    }

    private static void AddArrow(MonsterStateNode stateNode, MonsterStateNode nextStateNode, Graph graph, GraphGenerationContext context)
    {
        if (stateNode.AddedArrow)
        {
            return;
        }

        stateNode.AddedArrow = true;

        Arrow arrow;
        if (stateNode.IndexOnGraph + 1 == nextStateNode.IndexOnGraph)
        {
            var minY = Math.Max(stateNode.Y + 0.25f, nextStateNode.Y + 0.25f);
            var maxY = Math.Min(stateNode.Y + stateNode.Height - 0.25f, nextStateNode.Y + nextStateNode.Height - 0.25f);

            if (minY <= maxY)
            {
                var centerY = (minY + maxY) / 2;

                // -->
                // <--
                if (nextStateNode.NextState == stateNode)
                {
                    graph.Arrows.Add(new Arrow([0, stateNode.X + stateNode.Width, centerY - 0.2f, nextStateNode.X])); // -->
                    graph.Arrows.Add(new Arrow([0, nextStateNode.X, centerY + 0.2f, stateNode.X + stateNode.Width])); // <--
                    nextStateNode.AddedArrow = true;
                    return;
                }
                else // -->
                {
                    arrow = new Arrow([0, stateNode.X + stateNode.Width, centerY, nextStateNode.X]);
                    if (stateNode.Parent != null)
                    {
                        arrow.Path[1] -= 0.1f;
                    }
                    graph.Arrows.Add(arrow); // -->
                    return;
                }
            }
        }
        else if (stateNode.IndexOnGraph - 1 == nextStateNode.IndexOnGraph)
        {
            var minY = Math.Max(stateNode.Y + 0.25f, nextStateNode.Y + 0.25f);
            var maxY = Math.Min(stateNode.Y + stateNode.Height - 0.25f, nextStateNode.Y + nextStateNode.Height - 0.25f);

            if (minY <= maxY)
            {
                var centerY = (minY + maxY) / 2;
                // <--
                // -->
                if (nextStateNode.NextState == stateNode)
                {
                    graph.Arrows.Add(new Arrow([0, stateNode.X, centerY + 0.2f, nextStateNode.X + nextStateNode.Width])); // <--
                    graph.Arrows.Add(new Arrow([0, nextStateNode.X + nextStateNode.Width, centerY - 0.2f, stateNode.X])); // -->
                    nextStateNode.AddedArrow = true;
                    return;
                }
                else // <--
                {
                    arrow = new Arrow([0, stateNode.X, centerY, nextStateNode.X + nextStateNode.Width]);
                    if (stateNode.Parent != null)
                    {
                        arrow.Path[1] += 0.1f;
                    }
                    graph.Arrows.Add(arrow); // <--
                    return;
                }
            }
        }

        //      o
        //      ^
        // o----+
        if (stateNode.Y > nextStateNode.Y + 0.25f && stateNode.IndexOnGraph != nextStateNode.IndexOnGraph)
        {
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
                graph.Arrows.Add(arrow);
                return;
            }
        }

        //  o       o--+
        //  ^          |
        //  +----------+
        var arrowRight = stateNode.ArrowRight;
        var arrowBottom = nextStateNode.IndexOnGraph <= stateNode.IndexOnGraph ? stateNode.ArrowBottom : stateNode.Y + stateNode.Height + 0.25f;

        while (context.VLineSourceNode.TryGetValue(arrowRight, out var vLineSource) && vLineSource != stateNode)
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
        context.VLineSourceNode[arrowRight] = stateNode;
        context.HLineTargetNode[arrowBottom] = nextStateNode;
        arrow = new Arrow([0,
                stateNode.X + stateNode.Width, stateNode.Y + stateNode.Height / 2,
                arrowRight,
                arrowBottom,
                nextStateNode.X + nextStateNode.Width / 2,
                nextStateNode.Y + nextStateNode.Height]);
        if (stateNode.Parent != null)
        {
            arrow.Path[1] -= 0.1f;
        }

        graph.Arrows.Add(arrow);

        if (arrowRight > graph.Width)
        {
            graph.Width = arrowRight;
        }

        if (arrowBottom > graph.Height)
        {
            graph.Height = arrowBottom;
        }
    }

    private class GraphGenerationContext {
        public int IndexOnGraph { get; set; }
        public float NextNodeX { get; set; }
        public  Dictionary<float, MonsterStateNode> HLineTargetNode { get; set; } = new();
        public Dictionary<float, MonsterStateNode> VLineSourceNode { get; set; } = new();
        public Dictionary<int, MonsterStateNode> IndexOnGraphToNode { get; set; } = new();
    }

    private class MonsterStateNode
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public MonsterStateNode? Parent { get; set; }
        public List<(string label, MonsterStateNode node)>? Children { get; set; }
        public required MonsterState State { get; init; }
        public MonsterStateNode? NextState { get; set; }
        public int NextStateCount { get; set; } // include children's next states
        public float X { get; set; }
        public float Y { get; set; }
        public bool AddedToGraph { get; set; }
        public int IndexOnGraph { get; set; }
        public bool AddedArrow { get; set; }
        public float ArrowRight { get; set; }
        public float ArrowBottom { get; set; }
    }
}
