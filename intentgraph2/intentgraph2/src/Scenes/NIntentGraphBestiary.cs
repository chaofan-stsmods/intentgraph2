using Godot;
using HarmonyLib;
using IntentGraph2.Models;
using IntentGraph2.Patches;
using IntentGraph2.Utils;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Scenes;

public partial class NIntentGraphBestiary : Control
{
    private MonsterModel? monster;
    private List<Graph> graphs = new List<Graph>();
    private int currentGraphIndex = 0;

    private NIntentGraph? intentGraph;
    private Button? previousGraph;
    private Button? nextGraph;
    private bool ascension9 = false;

    public MonsterModel? Monster
    {
        get => monster;
        set
        {
            monster = value;
            if (monster != null)
            {
                GenerateGraphs();
            }
            else
            {
                ClearGraphs();
            }
        }
    }

    public override void _Ready()
    {
        intentGraph = GetNode<NIntentGraph>("%IntentGraph");
        previousGraph = GetNode<Button>("%PreviousGraph");
        nextGraph = GetNode<Button>("%NextGraph");
        var ascension9Check = GetNode<NIgTickbox>("%Ascension9");
        var showIntentGraphCheck = GetNode<NIgTickbox>("%ShowIntentGraph");
        var description = GetParent()?.GetNode<Control>("%Description");

        intentGraph.AnimatedIcons = IntentGraphMod.Config.UseAnimatedIntentIcon;

        previousGraph.Pressed += () =>
        {
            if (graphs.Count > 0)
            {
                currentGraphIndex = (currentGraphIndex - 1 + graphs.Count) % graphs.Count;
                SetGraph(graphs[currentGraphIndex]);
            }
        };

        nextGraph.Pressed += () =>
        {
            if (graphs.Count > 0)
            {
                currentGraphIndex = (currentGraphIndex + 1) % graphs.Count;
                SetGraph(graphs[currentGraphIndex]);
            }
        };

        ascension9Check.IsTicked = false;
        ascension9Check.Toggled += (tickbox) =>
        {
            ascension9 = tickbox.IsTicked;
            if (monster != null)
            {
                GenerateGraphs();
            }
        };

        var ascension9Label = ascension9Check.Label;
        var ascensionLevelText = LocString.GetIfExists("gameplay_ui", "ASCENSION_LEVEL");
        if (ascensionLevelText != null && ascension9Label != null)
        {
            ascensionLevelText.Add("ascension", 9);
            ascension9Label.Text = ascensionLevelText.GetFormattedText();
        }

        showIntentGraphCheck.IsTicked = true;
        if (description != null)
        {
            description.Visible = false;
        }
        showIntentGraphCheck.Toggled += (tickbox) =>
        {
            intentGraph.Visible = tickbox.IsTicked;
            ascension9Check.Visible = tickbox.IsTicked;
            previousGraph.Visible = tickbox.IsTicked && graphs.Count > 1;
            nextGraph.Visible = tickbox.IsTicked && graphs.Count > 1;
            if (description != null)
            {
                description.Visible = !tickbox.IsTicked;
            }
        };

        var intentGraphText = LocString.GetIfExists("settings_ui", "INTENTGRAPH2.mod_title");
        if (intentGraphText != null && showIntentGraphCheck.Label != null)
        {
            showIntentGraphCheck.Label.Text = intentGraphText.GetFormattedText();
        }

        GenerateGraphs();
    }

    public override void _Input(InputEvent evt)
    {
        if (intentGraph != null && evt is InputEventKey evtKey && evtKey.IsPressed() && IntentGraphMod.Config.ToggleIntentGraphKey == evtKey.Keycode)
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private void GenerateGraphs()
    {
        if (monster == null)
        {
            ClearGraphs();
            return;
        }

        HasAscensionLevelPatches.OverwriteAsensionLevel = ascension9 ? AscensionLevel.DeadlyEnemies : AscensionLevel.None;
        try
        {
            graphs.Clear();

            var addedGraphs = new HashSet<Graph>();
            var encounters = ModelDb.AllEncounters.Where(e => e.AllPossibleMonsters.Any(m => m.Id == monster.Id)).ToList();

            foreach (var canonicalEncounter in encounters)
            {
                var encounter = canonicalEncounter.ToMutable();
                new Traverse(encounter).Field("_rng").SetValue(NewRng());
                encounter.GenerateMonstersWithSlots(NullRunState.Instance);

                foreach (var (monsterModel, slot) in encounter.MonstersWithSlots.Where(t => t.Item1.Id == monster.Id))
                {
                    var graph = IntentGraphGenerator.GenerateGraphForBestiary(monsterModel, encounter, slot);
                    if (graph != null)
                    {
                        addedGraphs.Add(graph);
                    }
                }

                // Consider all possible monsters in case summoning
                foreach (var canonicalMonsterModel in canonicalEncounter.AllPossibleMonsters.Where(m => m.Id == monster.Id))
                {
                    var slots = canonicalEncounter.Slots;
                    if (slots.Count == 0)
                    {
                        slots = new List<string> { "" };
                    }

                    foreach (var slot in slots)
                    {
                        var monsterModel = canonicalMonsterModel.ToMutable();
                        var graph = IntentGraphGenerator.GenerateGraphForBestiary(monsterModel, encounter, slot);
                        if (graph != null)
                        {
                            addedGraphs.Add(graph);
                        }
                    }
                }
            }

            graphs.AddRange(addedGraphs);

            currentGraphIndex = Math.Max(0, Math.Min(currentGraphIndex, graphs.Count - 1));
            if (intentGraph != null)
            {
                SetGraph(graphs.Count > 0 ? graphs[currentGraphIndex] : null);
            }

            if (previousGraph != null && nextGraph != null)
            {
                previousGraph.Visible = graphs.Count > 1;
                nextGraph.Visible = graphs.Count > 1;
            }
        }
        catch (Exception ex)
        {
            IgLogger.Error($"Error generating graphs for monster {monster.Title.GetFormattedText()}: {ex}");
            ClearGraphs();
        }
        finally
        {
            HasAscensionLevelPatches.OverwriteAsensionLevel = null;
        }
    }

    private void ClearGraphs()
    {
        graphs.Clear();
        currentGraphIndex = 0;
        if (intentGraph != null)
        {
            intentGraph.Graph = null;
        }
        if (previousGraph != null && nextGraph != null)
        {
            previousGraph.Visible = false;
            nextGraph.Visible = false;
        }
    }

    private void SetGraph(Graph? graph)
    {
        if (intentGraph == null)
        {
            return;
        }

        intentGraph.Graph = graph;

        if (graph == null)
        {
            return;
        }

        var scale = graph.Height < 3 ? 1 : Math.Max(0.8f, 3 / graph.Height);
        intentGraph.GraphScale = new Vector2(scale, scale);
    }

    private Rng NewRng()
    {
        var rngType = typeof(Rng);
        var constructor = rngType.GetConstructor([typeof(ulong)]);
        if (constructor != null)
        {
            return (Rng)constructor.Invoke([(ulong)0]);
        }

        constructor = rngType.GetConstructor([typeof(uint), typeof(int)]);
        if (constructor != null)
        {
            return (Rng)constructor.Invoke([(uint)0, 0]);
        }

        throw new Exception("No suitable constructor found for Rng.");
    }
}
