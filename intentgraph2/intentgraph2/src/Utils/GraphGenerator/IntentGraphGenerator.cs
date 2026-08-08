using Godot;
using IntentGraph2.Models;
using IntentGraph2.Utils.Rule;
using IntentGraph2.Utils.Variable;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IntentGraph2.Utils.GraphGenerator;

public class IntentGraphGenerator
{
    public const float IconPaddingInMove = -0.33f;
    public const float IconGroupPadding = 0.1f;
    public const float IconGroupLabelHeight = 0.25f;

    public static bool ShowMonsterMoveNames => IntentGraphMod.Config.ShowMonsterMoveNames;

    public static float IconGroupSingleMovePadding => ShowMonsterMoveNames ? 0 : -0.15f;

    public static Graph? GenerateAndCacheGraphForCreature(Creature creature)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var monster = creature.Monster;
            IgLogger.Info($"Generating intent graph for monster: {creature.Name}.");
            var graph = GenerateGraph(monster);
            if (monster != null && graph != null)
            {
                IntentGraphMod.GeneratedGraphs.AddOrUpdate(monster, graph);
            }
            return graph;
        }
        catch (Exception ex)
        {
            Log.Warn(ex.ToString());
            return null;
        }
        finally
        {
            stopwatch.Stop();
            IgLogger.Info($"Finished generating intent graph for monster: {creature.Name} in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    public static Graph? GenerateGraphForBestiary(MonsterModel monsterModel, EncounterModel encounter, string? slot)
    {
        try
        {
            monsterModel.Rng = Rng.Chaotic;
            monsterModel.SetUpForCombat();
            Creature entity = new Creature(monsterModel, CombatSide.Enemy, null)
            {
                CombatState = new NullCombatState(),
                SlotName = slot,
            };
            var monster = MonsterSpecificInitialize(monsterModel);
            if (monster == null)
            {
                return null;
            }

            return GenerateGraph(monster, inBestiary: true);
        }
        catch (Exception ex)
        {
            IgLogger.Error($"Failed to generate intent graph for {monsterModel.Title.GetFormattedText()} in encounter {encounter.Title.GetFormattedText()} with slot {slot}: {ex}");
        }

        return null;
    }

    public static Graph? GenerateGraph(
        MonsterModel? monster,
        IntentDefinition? overwriteIntentDefinition = null,
        IReadOnlyDictionary<string, string>? overwriteIntentStrings = null,
        bool inBestiary = false)
    {
        if (monster?.MoveStateMachine == null)
        {
            return null;
        }

        var stateMachine = monster.MoveStateMachine;
        var initialState = stateMachine.GetInitialState();

        var intentDefinition = overwriteIntentDefinition;
        var variableContext = new VariableContext(monster) { InBestiary = inBestiary };
        if (intentDefinition == null)
        {
            intentDefinition = GetIntentDefinition(monster, variableContext);
        }

        var localizer = new IntentGraphLocalizer(overwriteIntentStrings, variableContext, intentDefinition);
        string? warning = null;
        if (intentDefinition?.UpToDateCondition != null)
        {
            try
            {
                var rule = RuleParserHelper.Parse(intentDefinition.UpToDateCondition, variableContext);
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
            graph.IntentDefinition = intentDefinition;
            return graph;
        }

        var converter = new MonsterStateNodeConverter(localizer, intentDefinition);
        var stateNodes = converter.ToMonsterStateNodes(monster, stateMachine, font, ref warning);

        graph = layouter.StateNodesToGraph(stateNodes, intentDefinition);
        graph.Warning = warning;
        graph.IntentDefinition = intentDefinition;

        if (intentDefinition?.GraphPatch != null)
        {
            var patch = layouter.MakeGraphFromIntentDefinition(stateMachine, intentDefinition.GraphPatch, intentDefinition, font, stateNodes);
            graph.Width = Math.Max(graph.Width, patch.Width);
            graph.Height = Math.Max(graph.Height, patch.Height);
            graph.Icons.AddRange(patch.Icons);
            graph.Moves.AddRange(patch.Moves);
            graph.IconGroups.AddRange(patch.IconGroups);
            graph.Labels.AddRange(patch.Labels);
            graph.Arrows.AddRange(patch.Arrows);
        }

        // Empty intents may have arrows so don't check it.
        if (graph.Moves.Sum(m => m.Icons?.Length ?? 0) == 0 && graph.IconGroups.Count == 0 && graph.Labels.Count == 0 && graph.Icons.Count == 0)
        {
            return null;
        }

        return graph;
    }

    internal static IntentDefinition? GetIntentDefinition(MonsterModel monster, VariableContext variableContext)
    {
        var intentDefinitionList = IntentGraphMod.IntentDefinitions.GetValueOrDefault(monster.GetType().FullName ?? string.Empty);
        if (intentDefinitionList != null)
        {
            return intentDefinitionList.FindFirstMatchIntentDefinition(variableContext);
        }

        return null;
    }

    internal static float GetMoveWidth(int iconCount)
    {
        return iconCount == 0 ? 0 : iconCount + (iconCount - 1) * IconPaddingInMove;
    }

    private static MonsterModel? MonsterSpecificInitialize(MonsterModel monsterModel)
    {
        // Keep this method in case I need it in the future.
        return monsterModel;
    }
}
