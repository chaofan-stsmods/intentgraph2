using Godot;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static IntentGraph2.Utils.GraphGenerator.IntentGraphGenerator;

namespace IntentGraph2.Utils.GraphGenerator;

internal class MonsterStateNodeConverter
{
    private const string OtherwiseMark = "{otherwise}";

    private readonly IntentGraphLocalizer localizer;

    public MonsterStateNodeConverter(IntentGraphLocalizer localizer)
    {
        this.localizer = localizer;
    }

    public bool EvaluateInitialState { get; set; } = true;

    public List<MonsterStateNode> FromStateMachineNodes(MonsterMoveStateMachine stateMachine, StateMachineNode[] overwriteStateMachine, Font font)
    {
        var existingNodes = new Dictionary<string, MonsterStateNode>();
        var initialStates = new List<(int, MonsterStateNode)>();

        foreach (var node in overwriteStateMachine)
        {
            if (node.IsInitialState)
            {
                var stateNode = StateMachineNodeToMonsterStateNode(font, stateMachine, overwriteStateMachine, node, existingNodes, parent: null);
                if (stateNode != null)
                {
                    initialStates.Add((node.InitialStatePriority, stateNode));
                }
            }
        }

        var result = initialStates.OrderBy(t => t.Item1).Select(t => t.Item2).ToList();
        MonsterStateNodeSimplifier.FindAndSetSimpleLoops(result);
        result.FirstOrDefault()?.SetIsInitialState(true);
        return result;
    }

    public List<MonsterStateNode> FromMonsterMoveStateMachine(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState initialState, IntentDefinition? intentDefinition, ref string? warning)
    {
        var existingNodes = new Dictionary<MonsterState, MonsterStateNode>();

        var result = new List<MonsterStateNode>();
        MonsterStateNode? initialStateNode = null;

        // For conditional branch to check monster slot.
        if (EvaluateInitialState && initialState is ConditionalBranchState conditionalBranchState)
        {
            var stateName = conditionalBranchState.EvaluateStates();
            var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == stateName);
            if (state != null)
            {
                initialStateNode = MonsterStateToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null, ref warning);
                initialStateNode.SetIsInitialState(true);
                // If next state is the same as initial state, use the initial state can simplify the graph.
                if (initialStateNode.NextState?.State == initialState)
                {
                    initialStateNode = null;
                }
            }
        }

        if (initialStateNode == null)
        {
            initialStateNode = MonsterStateToMonsterStateNode(monsterName, font, stateMachine, initialState, existingNodes, parent: null, ref warning);
            initialStateNode.SetIsInitialState(true);
        }

        var allNodes = initialStateNode.GetAllNodes();
        MonsterStateNodeSimplifier.SimplifyStateNodes(initialStateNode, allNodes, this);
        result.Add(initialStateNode);

        var secondaryStates = intentDefinition?.SecondaryInitialStates;
        if (secondaryStates != null)
        {
            foreach (var secondaryState in secondaryStates)
            {
                var state = stateMachine.States.Values.FirstOrDefault(s => s.Id == secondaryState.Id);
                if (state != null && !existingNodes.ContainsKey(state))
                {
                    var stateNode = MonsterStateToMonsterStateNode(monsterName, font, stateMachine, state, existingNodes, parent: null, ref warning);
                    var secondaryAllNodes = stateNode.GetAllNodes();
                    MonsterStateNodeSimplifier.SimplifyStateNodes(stateNode, secondaryAllNodes, this);
                    foreach (var item in secondaryAllNodes)
                    {
                        allNodes.Add(item);
                    }
                    stateNode.Offset = secondaryState.Offset;
                    result.Add(stateNode);
                }
            }
        }

        foreach (var stateId in stateMachine.States.Keys)
        {
            // It's by design to resolve INIT_MOVE
            if (stateId != "INIT_MOVE" && !allNodes.Any(n => n.State?.Id == stateId))
            {
                IgLogger.Warn($"State '{stateId}' is not included in the graph for monster '{monsterName}'.");
                break;
            }
        }

        if (warning == null && allNodes.Any(n => n.UnrecognizedStateType))
        {
            warning = localizer.GetOrElse("ui.Incomplete", "Incomplete");
        }

        MonsterStateNodeSimplifier.FindAndSetSimpleLoops(result);
        return result;
    }

    public string MakeText(MonsterStateNodeLabel label, float sumWeight)
    {
        var weight = label.Weight;
        var cooldown = label.Cooldown;
        var useOnlyOnce = label.UseOnlyOnce;
        var maxRepeat = label.MaxRepeat;
        var percentage = (int)(weight / sumWeight * 100);
        return percentage + "%" + (cooldown > 0 ? ", ⏱" + cooldown :
            (useOnlyOnce ? ", " + localizer.GetOrElse("ui.UseOnlyOnce", "one use") :
            (maxRepeat > 0 ? ", ≤" + maxRepeat : "")));
    }

    private MonsterStateNode? StateMachineNodeToMonsterStateNode(Font font, MonsterMoveStateMachine stateMachine, StateMachineNode[] overwriteStateMachine, StateMachineNode? node, Dictionary<string, MonsterStateNode> existingNodes, MonsterStateNode? parent)
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
            MonsterStateNode result;
            if (state != null)
            {
                result = new MonsterStateNode
                {
                    Id = node.Name,
                    State = state,
                    Width = state.Intents.Count + (state.Intents.Count - 1) * IconPaddingInMove,
                    Height = 1,
                    NextStateCount = 1,
                    Parent = parent,
                    ForceNotSimpleLoop = node.NotSimpleLoopStart,
                    Offset = node.Offset,
                };

                result.MoveStateIds.Add(state.Id);
                if (node.AlternativeMoveNames != null)
                {
                    result.MoveStateIds.AddRange(node.AlternativeMoveNames);
                }
            }
            else if (node.PlaceholderIntentCount > 0)
            {
                result = new MonsterStateNode
                {
                    Id = node.Name,
                    State = null,
                    Width = node.PlaceholderIntentCount + (node.PlaceholderIntentCount - 1) * IconPaddingInMove,
                    Height = 1,
                    NextStateCount = 1,
                    Parent = parent,
                    ForceNotSimpleLoop = node.NotSimpleLoopStart,
                    Offset = node.Offset,
                };

                if (node.AlternativeMoveNames != null)
                {
                    result.MoveStateIds.AddRange(node.AlternativeMoveNames);
                }
            }
            else
            {
                return null;
            }

            if (parent == null)
            {
                existingNodes[name] = result;
            }

            if (node.FollowUpState != null)
            {
                result.NextState = StateMachineNodeToMonsterStateNode(font, stateMachine, overwriteStateMachine, overwriteStateMachine.FirstOrDefault(n => n.Name == node.FollowUpState), existingNodes, parent: null);
            }

            return result;
        }
        else
        {
            var result = new MonsterStateNode
            {
                Id = node.Name,
                State = null,
                Parent = parent,
                ForceNotSimpleLoop = node.NotSimpleLoopStart,
                Offset = node.Offset,
            };

            if (parent == null)
            {
                existingNodes[name] = result;
            }

            var children = new List<MonsterStateNode>();
            for (int i = 0; i < node.Children.Length; i++)
            {
                var childNode = node.Children[i].Node;
                var text = node.Children[i].Label;
                text = localizer.GetOrElse(text, text);
                var childStateNode = StateMachineNodeToMonsterStateNode(font, stateMachine, overwriteStateMachine, childNode, existingNodes, parent: result);
                if (childStateNode != null)
                {
                    childStateNode.Label = new MonsterStateNodeLabel { Text = text };
                    childStateNode.Width = Math.Max(childStateNode.Width, font.GetStringSize(text, fontSize: NIntentGraph.LabelFontSize).X / NIntentGraph.GridSize);
                    children.Add(childStateNode);
                    result.MoveStateIds.AddRange(childStateNode.MoveStateIds);
                }
            }

            if (node.AlternativeMoveNames != null)
            {
                result.MoveStateIds.AddRange(node.AlternativeMoveNames);
            }

            result.Children = children;
            result.HorizontalLayout = node.HorizontalLayout;
            result.CalculateNodeSize();

            if (node.FollowUpState != null)
            {
                result.NextState = StateMachineNodeToMonsterStateNode(font, stateMachine, overwriteStateMachine, overwriteStateMachine.FirstOrDefault(n => n.Name == node.FollowUpState), existingNodes, parent: null);
            }

            result.NextStateCount = (result.NextState == null ? 0 : 1) + children.Select(c => c.NextStateCount).DefaultIfEmpty(0).Max();

            return result;
        }
    }

    [return: NotNullIfNotNull(nameof(state))]
    private MonsterStateNode? MonsterStateToMonsterStateNode(string monsterName, Font font, MonsterMoveStateMachine stateMachine, MonsterState? state, Dictionary<MonsterState, MonsterStateNode> existingNodes, MonsterStateNode? parent, ref string? warning)
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
                Id = state.Id,
                State = state,
                Width = moveState.Intents.Count + (moveState.Intents.Count - 1) * IconPaddingInMove,
                Height = 1,
                NextStateCount = 1,
                Parent = parent,
            };

            result.MoveStateIds.Add(moveState.Id);

            if (parent == null)
            {
                existingNodes[state] = result;
            }

            result.NextState = MonsterStateToMonsterStateNode(monsterName, font, stateMachine, moveState.FollowUpState, existingNodes, parent: null, ref warning);

            return result;
        }
        else
        {
            var unrecognizedStateType = false;
            var childCandidates = new List<(string state, MonsterStateNodeLabel label)>();
            if (state is RandomBranchState randomBranchState)
            {
                var sumWeight = randomBranchState.States.Sum(s => s.GetWeight());
                foreach (var s in randomBranchState.States)
                {
                    var label = new MonsterStateNodeLabel
                    {
                        Type = MonsterStateNodeLabel.LabelType.Random,
                        Text = string.Empty, // stub
                        IsTextGenerated = true,
                        Weight = s.GetWeight(),
                        MaxRepeat = s.repeatType switch 
                        {
                            MoveRepeatType.CanRepeatForever => 0,
                            MoveRepeatType.CanRepeatXTimes => s.maxTimes,
                            MoveRepeatType.CannotRepeat or MoveRepeatType.UseOnlyOnce => 1,
                            _ => 0
                        },
                        Cooldown = s.cooldown,
                        UseOnlyOnce = s.repeatType == MoveRepeatType.UseOnlyOnce,
                    };
                    if (localizer.TryGet($"branch.{monsterName}.{state.Id}.{s.stateId}", out var overwriteText))
                    {
                        label.Text = overwriteText;
                        label.IsTextGenerated = false;
                    }
                    else
                    {
                        label.Text = MakeText(label, sumWeight);
                    }

                    childCandidates.Add((s.stateId, label));
                }
            }
            else if (state is ConditionalBranchState conditionalBranchState)
            {
                // INIT_MOVE is related to monster slot, which is determined at the beginning of the combat, so we can evaluate it directly to get a more accurate graph.
                if (EvaluateInitialState && state.Id == "INIT_MOVE")
                {
                    var evaluatedSstateName = conditionalBranchState.EvaluateStates();
                    var evaluatedState = stateMachine.States.Values.FirstOrDefault(s => s.Id == evaluatedSstateName);
                    if (evaluatedState != null)
                    {
                        return MonsterStateToMonsterStateNode(monsterName, font, stateMachine, evaluatedState, existingNodes, parent, ref warning);
                    }
                }

                var conditionalStates = conditionalBranchState.GetStates();
                foreach (var s in conditionalStates)
                {
                    if (!childCandidates.Any(c => c.state == s))
                    {
                        if (localizer.TryGet($"branch.{monsterName}.{state.Id}.{s}", out var overwriteText))
                        {
                            childCandidates.Add((s, new MonsterStateNodeLabel
                            {
                                Type = MonsterStateNodeLabel.LabelType.Condition,
                                Text = overwriteText,
                            }));
                        }
                        else
                        {
                            warning ??= localizer.GetOrElse("ui.UnknownConditions", "Unknown conditions");
                            childCandidates.Add((s, new MonsterStateNodeLabel
                            {
                                Type = MonsterStateNodeLabel.LabelType.Condition,
                                Text = localizer.GetOrElse("ui.UnknownCondition", "condition?")
                            }));
                        }
                    }
                }
            }
            else
            {
                unrecognizedStateType = true;
            }

            // Move otherwise to last
            var childCandidatesCount = childCandidates.Count;
            for (int i = 0; i < childCandidatesCount; i++)
            {
                var child = childCandidates[i];
                if (child.label.Text == OtherwiseMark)
                {
                    childCandidates.RemoveAt(i);
                    childCandidates.Add((child.state, child.label));
                    child.label.Text = localizer.GetOrElse("ui.Otherwise", "Otherwise");
                    i--;
                    childCandidatesCount--;
                }
            }

            var result = new MonsterStateNode
            {
                Id = state.Id,
                State = state,
                Parent = parent,
                UnrecognizedStateType = unrecognizedStateType,
            };

            if (parent == null)
            {
                existingNodes[state] = result;
            }

            var children = new List<MonsterStateNode>();
            for (int i = 0; i < childCandidates.Count; i++)
            {
                var (childStateId, label) = childCandidates[i];
                var childState = stateMachine.States.Values.FirstOrDefault(s => s.Id == childStateId);
                if (childState != null)
                {
                    var childStateNode = MonsterStateToMonsterStateNode(monsterName, font, stateMachine, childState, existingNodes, parent: result, ref warning);
                    if (childStateNode != null)
                    {
                        childStateNode.Label = label;
                        childStateNode.Width = Math.Max(childStateNode.Width, font.GetStringSize(label.Text, fontSize: NIntentGraph.LabelFontSize).X / NIntentGraph.GridSize);
                        children.Add(childStateNode);
                        result.MoveStateIds.AddRange(childStateNode.MoveStateIds);
                    }
                }
            }

            var nextStateOfChildren = children.Select(c => c.NextState).Distinct().ToList();
            if (nextStateOfChildren.Count == 1)
            {
                foreach (var child in children)
                {
                    child.NextState = null;
                    child.NextStateCount = 0;
                }
            }

            result.Children = children;
            result.CalculateNodeSize();
            result.NextState = nextStateOfChildren.Count == 1 ? nextStateOfChildren[0] : null;
            result.NextStateCount = (result.NextState == null ? 0 : 1) + children.Select(c => c.NextStateCount).DefaultIfEmpty(0).Max();

            return result;
        }
    }
}
