using Godot;
using IntentGraph2.Models;
using IntentGraph2.Utils.Rule;
using MegaCrit.Sts2.Core.Models;
using System;
using System.Collections.Generic;

namespace IntentGraph2.Utils.GraphGenerator;

public class IntentGraphGenerator
{
    public const float IconPaddingInMove = -0.33f;
    public const float IconGroupPadding = 0.1f;
    public const float IconGroupLabelHeight = 0.25f;

    public static bool ShowMonsterMoveNames => IntentGraphMod.Config.ShowMonsterMoveNames;

    public static float IconGroupSingleMovePadding => ShowMonsterMoveNames ? 0 : -0.15f;

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

        var localizer = new IntentGraphLocalizer(overwriteIntentStrings);
        string? warning = null;
        if (intentDefinition?.UpToDateCondition != null)
        {
            try
            {
                var rule = RuleParserHelper.Parse(intentDefinition.UpToDateCondition, new RuleContext(monster));
                if (rule?.GetBool() == false)
                {
                    warning = localizer.GetOrElse("ui.Outdated", "Outdated");
                }
            }
            catch (Exception ex)
            {
                IgLogger.Warn($"Failed to evaluate up to date condition '{intentDefinition.UpToDateCondition}' for monster '{monster.Id}', error message: {ex.Message}");
            }
        }

        var font = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        var layouter = new IntentGraphLayouter(monster, localizer);
        Graph graph;
        if (intentDefinition?.Graph != null)
        {
            graph = layouter.MakeGraphFromIntentDefinition(stateMachine, intentDefinition.Graph, intentDefinition, font);
            graph.Warning = warning;
            return graph;
        }

        var converter = new MonsterStateNodeConverter(localizer);
        List<MonsterStateNode> stateNodes;
        if (intentDefinition?.StateMachine != null)
        {
            stateNodes = converter.FromStateMachineNodes(stateMachine, intentDefinition.StateMachine, font);
        }
        else
        {
            stateNodes = converter.FromMonsterMoveStateMachine(monster.GetType().FullName ?? "_unknownMonster", font, stateMachine, initialState, intentDefinition, ref warning);
        }

        graph = layouter.StateNodesToGraph(stateNodes, intentDefinition);
        graph.Warning = warning;

        if (intentDefinition?.GraphPatch != null)
        {
            var patch = layouter.MakeGraphFromIntentDefinition(stateMachine, intentDefinition.GraphPatch, intentDefinition, font);
            graph.Width = Math.Max(graph.Width, patch.Width);
            graph.Height = Math.Max(graph.Height, patch.Height);
            graph.Icons.AddRange(patch.Icons);
            graph.Moves.AddRange(patch.Moves);
            graph.IconGroups.AddRange(patch.IconGroups);
            graph.Labels.AddRange(patch.Labels);
            graph.Arrows.AddRange(patch.Arrows);
        }

        // Empty intents may have arrows so don't check it.
        if (graph.Moves.Count == 0 && graph.IconGroups.Count == 0 && graph.Labels.Count == 0)
        {
            return null;
        }

        return graph;
    }
}
