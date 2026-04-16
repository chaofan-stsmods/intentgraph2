using Godot;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace IntentGraph2.Utils;

public class IntentGraphGenerator
{
    private const float IconPaddingInMove = -0.28f;

    public static Graph? GenerateGraph(MonsterModel? monster, IntentDefinition? overwriteIntentDefinition = null, IReadOnlyDictionary<string, string>? overwriteIntentStrings = null)
    {
        if (monster?.MoveStateMachine == null)
        {
            return null;
        }

        var stateMachine = monster.MoveStateMachine;
        var initialState = stateMachine.GetInitialState();

        var intentDefinition = overwriteIntentDefinition;
        if (intentDefinition == null)
        {
            var intentDefinitionList = IntentGraphMod.IntentDefinitions.GetValueOrDefault(monster.GetType().FullName ?? string.Empty);
            intentDefinition = intentDefinitionList?.FindFirstMatchCondition(monster);
        }

        if (intentDefinition?.Graph != null)
        {
            return MakeGraphFromIntentDefinition(stateMachine, intentDefinition.Graph, intentDefinition, overwriteIntentStrings);
        }

        var font = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        List<MonsterStateNode> stateNodes;
        if (intentDefinition?.StateMachine != null)
        {
            stateNodes = ToMonsterStateNodeList(stateMachine, intentDefinition.StateMachine, font, overwriteIntentStrings);
        }
        else
        {
            stateNodes = ToMonsterStateNodeList(monster.GetType().FullName ?? "_unknownMonster", font, stateMachine, initialState, intentDefinition?.SecondaryInitialStates, overwriteIntentStrings);
        }

        var graph = StateNodesToGraph(stateNodes, intentDefinition);

        if (intentDefinition?.GraphPatch != null)
        {
            var patch = MakeGraphFromIntentDefinition(stateMachine, intentDefinition.GraphPatch, intentDefinition, overwriteIntentStrings);
            graph.Width = Math.Max(graph.Width, patch.Width);
            graph.Height = Math.Max(graph.Height, patch.Height);
            graph.Icons.AddRange(patch.Icons);
            graph.IconGroups.AddRange(patch.IconGroups);
            graph.Labels.AddRange(patch.Labels);
            graph.Arrows.AddRange(patch.Arrows);
        }

        // Empty intents may have arrows so don't check it.
        if (graph.Icons.Count == 0 && graph.IconGroups.Count == 0 && graph.Labels.Count == 0)
        {
            return null;
        }

        return graph;
    }

    private static Graph MakeGraphFromIntentDefinition(MonsterMoveStateMachine stateMachine, Graph graph, IntentDefinition intentDefinition, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
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
            result.Labels.Add(new Models.Label(label.X, label.Y, GetIntentString(label.Text, label.Text, overwriteIntentStrings), label.Align));
        }

        foreach (var move in graph.Moves)
        {
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == move.Id);
            if (state != null && state is MoveState moveState)
            {
                MoveReplacement[]? replacements = null;
                intentDefinition?.MoveReplacements?.TryGetValue(state.Id, out replacements);
                AddIcons(moveState.Intents, result.Icons, move.X, move.Y, replacements);
            }
        }

        return result;
    }

    private static List<MonsterStateNode> ToMonsterStateNodeList(MonsterMoveStateMachine stateMachine, StateMachineNode[] overwriteStateMachine, Font font, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
    {
        var existingNodes = new Dictionary<string, MonsterStateNode>();
        var initialStates = new List<(int, MonsterStateNode)>();

        foreach (var node in overwriteStateMachine)
        {
            if (node.IsInitialState)
            {
                var stateNode = ToMonsterStateNode(font, stateMachine, overwriteStateMachine, node, existingNodes, parent: null, overwriteIntentStrings);
                if (stateNode != null)
                {
                    initialStates.Add((node.InitialStatePriority, stateNode));
                }
            }
        }

        return initialStates.OrderBy(t => t.Item1).Select(t => t.Item2).ToList();
    }

    private static MonsterStateNode? ToMonsterStateNode(Font font, MonsterMoveStateMachine stateMachine, StateMachineNode[] overwriteStateMachine, StateMachineNode? node, Dictionary<string, MonsterStateNode> existingNodes, MonsterStateNode? parent, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
    {
        if (node == null)
        {
            return null;
        }

        var name = node.Name;
        if (parent == null && existingNodes.TryGetValue(name, out var existingNode))
        {
            return existingNode;
        }

        if (node.Children == null || node.Children.Length == 0)
        {
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == (node.MoveName ?? node.Name)) as MoveState;
            if (state == null)
            {
                return null;
            }

            var result = new MonsterStateNode
            {
                State = state,
                Width = state.Intents.Count + (state.Intents.Count - 1) * IconPaddingInMove,
                Height = 1,
                NextStateCount = 1,
                Parent = parent,
            };

            if (parent == null)
            {
                existingNodes[name] = result;
            }

            if (node.FollowUpState != null)
            {
                result.NextState = ToMonsterStateNode(font, stateMachine, overwriteStateMachine, overwriteStateMachine.FirstOrDefault(n => n.Name == node.FollowUpState), existingNodes, parent: null, overwriteIntentStrings);
            }

            return result;
        }
        else
        {
            var result = new MonsterStateNode
            {
                State = null,
                Parent = parent,
            };

            if (parent == null)
            {
                existingNodes[name] = result;
            }

            var children = new List<(string label, MonsterStateNode node)>();
            for (int i = 0; i < node.Children.Length; i++)
            {
                var childNode = node.Children[i].Node;
                var text = node.Children[i].Label;
                text = GetIntentString(text, text, overwriteIntentStrings);
                var childStateNode = ToMonsterStateNode(font, stateMachine, overwriteStateMachine, childNode, existingNodes, parent: result, overwriteIntentStrings);
                if (childStateNode != null)
                {
                    children.Add((text, childStateNode));
                }
            }

            var longestText = children.Select(c => font.GetStringSize(c.label, fontSize: 18).X).DefaultIfEmpty(0).Max() / NIntentGraph.GridSize;

            result.Width = Math.Max(longestText, children.Select(c => c.node.Width).DefaultIfEmpty(1).Max()) + 0.2f; // 0.1 padding
            // 0.1 padding, 0.25 label, -0.15 for single move
            result.Height = 0.25f * children.Count + children.Select(c => c.node.Height).Sum() + 0.2f - 0.15f * children.Where(c => c.node.Children == null).Count();
            result.Children = children;

            if (node.FollowUpState != null)
            {
                result.NextState = ToMonsterStateNode(font, stateMachine, overwriteStateMachine, overwriteStateMachine.FirstOrDefault(n => n.Name == node.FollowUpState), existingNodes, parent: null, overwriteIntentStrings);
            }

            result.NextStateCount = (result.NextState == null ? 0 : 1) + children.Select(c => c.node.NextStateCount).DefaultIfEmpty(0).Max();

            return result;
        }
    }

    private static List<MonsterStateNode> ToMonsterStateNodeList(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState initialState, string[]? secondaryStates, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
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
                initialStateNode = ToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null, overwriteIntentStrings);
            }
        }

        if (initialStateNode == null)
        {
            initialStateNode = ToMonsterStateNode(monsterName, font, stateMachine, initialState, existingNodes, parent: null, overwriteIntentStrings);
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
                    var stateNode = ToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null, overwriteIntentStrings);
                    SimplifyStateNodes(stateNode);
                    result.Add(stateNode);
                }
            }
        }

        return result;
    }

    [return: NotNullIfNotNull(nameof(state))]
    private static MonsterStateNode? ToMonsterStateNode(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState? state, Dictionary<MonsterState, MonsterStateNode> existingNodes, MonsterStateNode? parent, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
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
                Width = moveState.Intents.Count + (moveState.Intents.Count - 1) * IconPaddingInMove,
                Height = 1,
                NextStateCount = 1,
                Parent = parent,
            };

            if (parent == null)
            {
                existingNodes[state] = result;
            }

            result.NextState = ToMonsterStateNode(monsterName, font, stateMachine, moveState.FollowUpState, existingNodes, parent: null, overwriteIntentStrings);

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
                    if (TryGetIntentString($"branch.{monsterName}.{state.Id}.{s.stateId}", overwriteIntentStrings, out var overwriteText))
                    {
                        texts.Add(overwriteText);
                    }
                    else
                    {
                        texts.Add(MakeText(s, sumWeight, overwriteIntentStrings));
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
                        return ToMonsterStateNode(monsterName, font, stateMachine, evaluatedState, existingNodes, parent, overwriteIntentStrings);
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

                texts = states.Select(s => GetIntentString($"branch.{monsterName}.{state.Id}.{s}", "condition", overwriteIntentStrings)).ToList();
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
                var childStateId = states[i];
                var text = texts[i];
                var childState = stateMachine.States.Values.FirstOrDefault(s => s.Id == childStateId);
                if (childState != null)
                {
                    var childStateNode = ToMonsterStateNode(monsterName, font, stateMachine, childState, existingNodes, parent: result, overwriteIntentStrings);
                    if (childStateNode != null)
                    {
                        children.Add((text, childStateNode));
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

    private static string MakeText(RandomBranchState.StateWeight s, float sumWeight, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
    {
        var weight = s.GetWeight();
        var percentage = (int)(weight / sumWeight * 100);
        return percentage + "%" + (s.cooldown > 0 ? ", ⏱" + s.cooldown : s.repeatType switch
        {
            MoveRepeatType.CanRepeatForever => "",
            MoveRepeatType.CanRepeatXTimes => ", ≤" + s.maxTimes,
            MoveRepeatType.CannotRepeat => ", ≤1",
            MoveRepeatType.UseOnlyOnce => ", " + GetIntentString("ui.UseOnlyOnce", "ui.UseOnlyOnce", overwriteIntentStrings),
            _ => ""
        });
    }

    private static bool TryGetIntentString(string key, IReadOnlyDictionary<string, string>? overwriteIntentStrings, [NotNullWhen(true)] out string? value)
    {
        if (overwriteIntentStrings != null && overwriteIntentStrings.TryGetValue(key, out value))
        {
            return true;
        }

        return IntentGraphMod.IntentGraphStrings.TryGetValue(key, out value);
    }

    private static string GetIntentString(string key, string fallbackValue, IReadOnlyDictionary<string, string>? overwriteIntentStrings)
    {
        return TryGetIntentString(key, overwriteIntentStrings, out var value) ? value : fallbackValue;
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

    private static Graph StateNodesToGraph(List<MonsterStateNode> stateNodes, IntentDefinition? intentDefinition)
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
            AddStateNodeToGraph(stateNode, result, context, 0, y);
            y = result.Height;
        }

        TuneArrowPosition(result.Arrows, arrowTarget);
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
                MoveReplacement[]? replacements = null;
                context.IntentDefinition?.MoveReplacements?.TryGetValue(moveState.Id, out replacements);
                AddIcons(moveState.Intents, graph.Icons, x, y, replacements);
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

    private static void AddIcons(IReadOnlyList<AbstractIntent> intents, List<Icon> iconList, float x, float y, MoveReplacement[]? replacements)
    {
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
                    AddArrow(graph, new Arrow([0, stateNode.X + stateNode.Width, centerY - 0.2f, nextStateNode.X]), context, nextStateNode); // -->
                    AddArrow(graph, new Arrow([0, nextStateNode.X, centerY + 0.2f, stateNode.X + stateNode.Width]), context, stateNode); // <--
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
                    AddArrow(graph, arrow, context, nextStateNode); // -->
                    return;
                }
            }
        }

        if (stateNode.IndexOnGraph - 1 == nextStateNode.IndexOnGraph)
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
                    AddArrow(graph, new Arrow([0, stateNode.X, centerY + 0.2f, nextStateNode.X + nextStateNode.Width]), context, nextStateNode); // <--
                    AddArrow(graph, new Arrow([0, nextStateNode.X + nextStateNode.Width, centerY - 0.2f, stateNode.X]), context, stateNode); // -->
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
                    AddArrow(graph, arrow, context, nextStateNode); // <--
                    context.ArrowTarget[arrow] = nextStateNode;
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
                AddArrow(graph, arrow, context, nextStateNode);
                context.ArrowTarget[arrow] = nextStateNode;
                return;
            }
        }

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

        AddArrow(graph, arrow, context, nextStateNode);
        context.ArrowTarget[arrow] = nextStateNode;

        if (arrowRight > graph.Width)
        {
            graph.Width = arrowRight;
        }

        if (arrowBottom > graph.Height)
        {
            graph.Height = arrowBottom;
        }
    }

    private static void AddArrow(Graph graph, Arrow arrow, GraphGenerationContext context, MonsterStateNode target)
    {
        graph.Arrows.Add(arrow);
        context.ArrowTarget[arrow] = target;
    }

    private static void TuneArrowPosition(List<Arrow> arrows, Dictionary<Arrow, MonsterStateNode> arrowTarget)
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

    private static IEnumerable<(bool horizontal, Vector2 start, Vector2 end, int pathIndex)> ArrowSegments(Arrow arrow)
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
    }

    private class MonsterStateNode
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public MonsterStateNode? Parent { get; set; }
        public List<(string label, MonsterStateNode node)>? Children { get; set; }
        public MonsterState? State { get; set; }
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
