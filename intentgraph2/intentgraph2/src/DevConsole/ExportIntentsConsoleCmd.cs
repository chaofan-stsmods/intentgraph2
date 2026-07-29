using Godot;
using HarmonyLib;
using IntentGraph2.Models;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
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
                monsterModel.Rng = Rng.Chaotic;
                monsterModel.SetUpForCombat();
                Creature entity = new Creature(monsterModel, CombatSide.Enemy, null)
                {
                    CombatState = new NullCombatState(),
                    SlotName = slot,
                };
                var graph = IntentGraphGenerator.GenerateGraph(monsterModel);
                if (graph == null)
                {
                    continue;
                }
                if (addedGraphs.Contains(graph))
                {
                    continue;
                }
                addedGraphs.Add(graph);
                graphMetadata[graph] = (monsterModel, $"_{canonicalEncounter.Title.GetFormattedText()}_{(string.IsNullOrEmpty(slot) ? index : slot)}");
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
                    monsterModel.Rng = Rng.Chaotic;
                    monsterModel.SetUpForCombat();
                    Creature entity = new Creature(monsterModel, CombatSide.Enemy, null)
                    {
                        CombatState = new NullCombatState(),
                        SlotName = slot,
                    };
                    var graph = IntentGraphGenerator.GenerateGraph(monsterModel);
                    if (graph == null)
                    {
                        continue;
                    }

                    if (addedGraphs.Contains(graph))
                    {
                        continue;
                    }

                    addedGraphs.Add(graph);
                    graphMetadata[graph] = (monsterModel, $"_{canonicalEncounter.Title.GetFormattedText()}_{(string.IsNullOrEmpty(slot) ? index : slot)}");
                }
            }
        }

        var singleGraphMonsterTypes = addedGraphs
            .Select(g => graphMetadata[g])
            .GroupBy(x => x.monster.GetType())
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .Select(g => g.monster.GetType())
            .ToHashSet();

        foreach (var graph in addedGraphs)
        {
            var (monster, suffix) = graphMetadata[graph];
            if (singleGraphMonsterTypes.Contains(monster.GetType()))
            {
                suffix = "";
            }
            tasks.Add(ExportGraphAsImageAsync(graph, monster, suffix));
        }

        var task = Task.WhenAll(tasks).ContinueWith(task =>
        {
            IntentGraphMod.Config.IntentGraphScale = intentGraphScale;
            IntentGraphMod.Config.UseAnimatedIntentIcon = animatedIcons;
            IntentGraphMod.Config.ShowCurrentMove = currentMove;
        });

        return new CmdResult(task, success: true, "Intent graph images exported");
    }

    public async Task ExportGraphAsImageAsync(Graph graph, MonsterModel monster, string suffix)
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

            SaveAsPng(intentGraph, $"user://intentgraphs/{monster.Title.GetFormattedText()}{suffix}.png");
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

    public void SaveAsPng(NIntentGraph intentGraph, string filePath)
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
