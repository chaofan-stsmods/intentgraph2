using Godot;
using HarmonyLib;
using IntentGraph2.Models;
using IntentGraph2.Patches;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IntentGraph2.DevConsole;
public class ExportIntentsConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "exportintents";

    public override string Args => string.Empty;

    public override string Description => "Export intent graphs as image.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var tasks = new List<Task>();

        var intentGraphScale = IntentGraphMod.Config.IntentGraphScale;
        var animatedIcons = IntentGraphMod.Config.UseAnimatedIntentIcon;
        var currentMove = IntentGraphMod.Config.ShowCurrentMove;

        IntentGraphMod.Config.IntentGraphScale = 1;
        IntentGraphMod.Config.UseAnimatedIntentIcon = false;
        IntentGraphMod.Config.ShowCurrentMove = false;

        try
        {
            HasAscensionLevelPatches.OverwriteAsensionLevel = AscensionLevel.None;
            GenerateGraphForAllEncounters(tasks);

            HasAscensionLevelPatches.OverwriteAsensionLevel = AscensionLevel.DeadlyEnemies;
            GenerateGraphForAllEncounters(tasks, "a9_");
        }
        finally
        {
            HasAscensionLevelPatches.OverwriteAsensionLevel = null;
        }

        var task = Task.WhenAll(tasks).ContinueWith(task =>
        {
            IntentGraphMod.Config.IntentGraphScale = intentGraphScale;
            IntentGraphMod.Config.UseAnimatedIntentIcon = animatedIcons;
            IntentGraphMod.Config.ShowCurrentMove = currentMove;

            OS.ShellShowInFileManager(Path.Combine(OS.GetUserDataDir(), "intentgraphs"));
        });

        return new CmdResult(task, success: true, "Intent graph images exported");
    }

    private static void GenerateGraphForAllEncounters(List<Task> tasks, string prefix = "")
    {
        var addedGraphs = new HashSet<Graph>();
        var graphMetadata = new Dictionary<Graph, (MonsterModel monster, string suffix)>();

        foreach (var canonicalEncounter in ModelDb.AllEncounters)
        {
            var encounter = canonicalEncounter.ToMutable();
            new Traverse(encounter).Field("_rng").SetValue(Rng.Chaotic);
            encounter.GenerateMonstersWithSlots(NullRunState.Instance);

            var index = 0;
            foreach (var (monsterModel, slot) in encounter.MonstersWithSlots)
            {
                index++;
                GenerateGraph(monsterModel, encounter, addedGraphs, graphMetadata, index, slot);
            }

            // Consider all possible monsters in case summoning
            foreach (var canonicalMonsterModel in canonicalEncounter.AllPossibleMonsters)
            {
                var slots = canonicalEncounter.Slots;
                if (slots.Count == 0)
                {
                    slots = new List<string> { "" };
                }

                index = 0;
                foreach (var slot in slots)
                {
                    index++;
                    var monsterModel = canonicalMonsterModel.ToMutable();
                    GenerateGraph(monsterModel, encounter, addedGraphs, graphMetadata, index, slot);
                }
            }
        }

        var singleGraphMonsterTypes = addedGraphs
            .Select(g => graphMetadata[g])
            .GroupBy(x => x.monster.Title.GetFormattedText())
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .Select(g => g.monster.Title.GetFormattedText())
            .ToHashSet();

        foreach (var graph in addedGraphs)
        {
            var (monster, suffix) = graphMetadata[graph];
            if (singleGraphMonsterTypes.Contains(monster.Title.GetFormattedText()))
            {
                suffix = "";
            }
            tasks.Add(ExportGraphAsImageAsync(graph, monster, prefix, suffix));
        }
    }

    private static void GenerateGraph(MonsterModel monsterModel, EncounterModel encounter, HashSet<Graph> addedGraphs, Dictionary<Graph, (MonsterModel monster, string suffix)> graphMetadata, int index, string? slot)
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
            MonsterSpecificInitialize(monsterModel);
            var graph = IntentGraphGenerator.GenerateGraph(monsterModel);
            if (graph == null)
            {
                return;
            }
            if (addedGraphs.Contains(graph))
            {
                return;
            }
            addedGraphs.Add(graph);
            graphMetadata[graph] = (monsterModel, $"_{encounter.Title.GetFormattedText()}_{(string.IsNullOrEmpty(slot) ? index : slot)}");
        }
        catch (Exception ex)
        {
            IgLogger.Error($"Failed to generate intent graph for {monsterModel.Title} in encounter {encounter.Title} with slot {slot} ({index}): {ex}");
        }
    }

    private static void MonsterSpecificInitialize(MonsterModel monsterModel)
    {
        if (monsterModel is WaterfallGiant waterfallGiant)
        {
            waterfallGiant.AfterAddedToRoom().Wait();
        }
    }

    private static async Task ExportGraphAsImageAsync(Graph graph, MonsterModel monster, string prefix, string suffix)
    {
        if (NGame.Instance == null)
        {
            return;
        }

        var scene = PreloadManager.Cache.GetScene("res://intentgraph2/scenes/intent_graph.tscn");
        var intentGraph = scene.Instantiate<NIntentGraph>();
        var subViewport = new SubViewport();
        try
        {
            intentGraph.Graph = graph;
            intentGraph.Monster = monster;
            intentGraph.Position = new Vector2(0.1f * NIntentGraph.GridSize, 0.1f * NIntentGraph.GridSize);

            subViewport.Size = new Vector2I(
                (int)Math.Ceiling(intentGraph.CustomMinimumSize.X + 0.2f * NIntentGraph.GridSize),
                (int)Math.Ceiling(intentGraph.CustomMinimumSize.Y + 0.2f * NIntentGraph.GridSize));
            subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            subViewport.TransparentBg = true;

            subViewport.AddChildSafely(intentGraph);
            NGame.Instance.AddChildSafely(subViewport);
            await intentGraph.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            SaveAsPng(intentGraph, $"user://intentgraphs/{prefix}{monster.Title.GetFormattedText()}{suffix}.png");
        }
        catch (Exception ex)
        {
            IgLogger.Error($"Failed to export intent graph for {monster.Title}: {ex}");
        }
        finally
        {
            subViewport.QueueFreeSafely();
            subViewport = null;
            intentGraph = null;
        }
    }

    public static void SaveAsPng(NIntentGraph intentGraph, string filePath)
    {
        Error error;
        var folder = filePath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(folder))
        {
            error = DirAccess.MakeDirAbsolute(folder);
            if (error != Error.Ok)
            {
                throw new Exception($"Failed to create directory {folder}: {error}.");
            }
        }

        var img = intentGraph.GetViewport().GetTexture().GetImage();
        error = img.SavePng(filePath);
        if (error != Error.Ok)
        {
            throw new Exception($"Failed to save intent graph to {filePath}: {error}.");
        }
    }
}
