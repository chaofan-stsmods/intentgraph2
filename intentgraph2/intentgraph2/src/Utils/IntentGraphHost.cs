using Godot;
using IntentGraph2.Scenes;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Utils;
public static class IntentGraphHost
{
    private const float IntentGraphPanelTop = 90;
    private const float IntentGraphPanelTopPinned = 160;

    private static Dictionary<NCreature, IntentGraphItem> availableIntentGraphs = new();
    private static bool intentGraphVisible = true;

    public static void ToggleIntentGraphVisibility()
    {
        intentGraphVisible = !intentGraphVisible;
        if (intentGraphVisible)
        {
            foreach (var item in availableIntentGraphs.Values)
            {
                item.IntentGraphPanel.Show();
            }
        }
        else
        {
            foreach (var item in availableIntentGraphs.Values)
            {
                item.IntentGraphPanel.Hide();
            }
        }
    }

    public static void Create(NCreature nCreature)
    {
        if (availableIntentGraphs.TryGetValue(nCreature, out var item))
        {
            item.IntentGraphPanel.MoveToFrontSafely();
            return;
        }

        if (NGame.Instance?.HoverTipsContainer == null || NCombatRoom.Instance?.Ui.Hand.InCardPlay != false || nCreature.Entity?.IsMonster != true)
        {
            return;
        }

        var creature = nCreature.Entity;
        if (creature.Monster == null || !IntentGraphMod.GeneratedGraphs.TryGetValue(creature.Monster, out var graph))
        {
            return;
        }

        if (graph.Condition?.GetBool() == false)
        {
            graph = IntentGraphGenerator.GenerateAndCacheGraphForCreature(creature);
            if (graph == null)
            {
                return;
            }
        }

        foreach (var (c, i) in availableIntentGraphs.ToList())
        {
            if (!i.IntentGraphPanel.Pinned)
            {
                i.RemoveIntentGraphPanel();
                availableIntentGraphs.Remove(c);
            }
        }

        var scene = PreloadManager.Cache.GetScene("res://intentgraph2/scenes/intent_graph_panel.tscn");
        var intentGraphPanel = scene.Instantiate<NIntentGraphPanel>();
        intentGraphPanel.NCreature = nCreature;

        var monsterNameLabel = intentGraphPanel.GetNode<Label>("%MonsterName");
        monsterNameLabel.Text = creature.Name;
        monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");

        var intentGraph = intentGraphPanel.GetNode<NIntentGraph>("%IntentGraph");
        intentGraph.Graph = graph;
        intentGraph.Monster = creature.Monster;

        var pinableIntentGraph = IntentGraphMod.Config.PinableIntentGraph;
        var handleResized = OnIntentGraphPanelResized(nCreature, intentGraphPanel, pinableIntentGraph);
        if (!pinableIntentGraph)
        {
            nCreature.Resized += handleResized;
        }
        intentGraphPanel.Resized += handleResized;

        if (graph.Warning != null)
        {
            var outdatedContainer = intentGraphPanel.GetNode<MarginContainer>("%OutdatedMarkContainer");
            var outdatedLabel = outdatedContainer.GetNode<Label>("OutdatedMark");
            outdatedContainer.Show();
            outdatedLabel.Text = "⚠️" + graph.Warning;
            outdatedLabel.ApplyLocaleFontSubstitution(FontType.Regular, "font");
            outdatedLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
        }

        intentGraphPanel.ResetSize();

        if (pinableIntentGraph)
        {
            NCombatRoom.Instance?.Ui.AddChildSafely(intentGraphPanel);
        }
        else
        {
            NGame.Instance.HoverTipsContainer.AddChildSafely(intentGraphPanel);
        }

        if (!intentGraphVisible)
        {
            intentGraphPanel.Hide();
        }

        availableIntentGraphs[nCreature] = new IntentGraphItem
        {
            IntentGraphPanel = intentGraphPanel,
            RemoveIntentGraphPanel = () =>
            {
                try
                {
                    intentGraphPanel.Resized -= handleResized;
                    if (!pinableIntentGraph)
                    {
                        nCreature.Resized -= handleResized;
                    }
                }
                catch (Exception ex)
                {
                    IgLogger.Error("Error unregistering resized event handlers: " + ex);
                }

                intentGraphPanel.QueueFreeSafely();
            }
        };
    }

    public static void Remove(NCreature creature)
    {
        if (availableIntentGraphs.TryGetValue(creature, out var intentGraphItem))
        {
            intentGraphItem.RemoveIntentGraphPanel();
            availableIntentGraphs.Remove(creature);
        }
    }

    private static Action OnIntentGraphPanelResized(NCreature __instance, MarginContainer intentGraphPanel, bool pinableIntentGraph)
    {
        return () =>
        {
            var screenWidth = NGame.Instance!.GetViewportRect().Size.X;
            var creatureMid = __instance.GlobalPosition.X + __instance.Size.X / 2;
            if (IntentGraphMod.Config.IntentGraphPosition == IntentGraphPosition.TopLeft ||
                (IntentGraphMod.Config.IntentGraphPosition == IntentGraphPosition.TopLeftOrRight && creatureMid < screenWidth / 2))
            {
                intentGraphPanel.Position = new Vector2(8, pinableIntentGraph ? IntentGraphPanelTopPinned : IntentGraphPanelTop);
                return;
            }
            else if (IntentGraphMod.Config.IntentGraphPosition == IntentGraphPosition.TopRight ||
                (IntentGraphMod.Config.IntentGraphPosition == IntentGraphPosition.TopLeftOrRight && creatureMid >= screenWidth / 2))
            {
                intentGraphPanel.Position = new Vector2(screenWidth - intentGraphPanel.Size.X, pinableIntentGraph ? IntentGraphPanelTopPinned : IntentGraphPanelTop);
                return;
            }
            else if (IntentGraphMod.Config.IntentGraphPosition == IntentGraphPosition.TopCenter)
            {
                intentGraphPanel.Position = new Vector2(screenWidth / 2 - intentGraphPanel.Size.X / 2, pinableIntentGraph ? IntentGraphPanelTopPinned : IntentGraphPanelTop);
                return;
            }

            var maxX = screenWidth - intentGraphPanel.Size.X;
            var candidateX = Math.Clamp(creatureMid - intentGraphPanel.Size.X / 2, 0, maxX);

            var parent = intentGraphPanel.GetParent();
            var tipSet = (NHoverTipSet?)parent?.GetChildren().LastOrDefault(c => c is NHoverTipSet);
            var textTipContainer = tipSet?.GetTextHoverTipContainer();
            if (textTipContainer != null)
            {
                var tipSetPosition = textTipContainer.GlobalPosition;
                var tipSetSize = textTipContainer.Size;
                if (tipSetPosition.Y < IntentGraphPanelTop + intentGraphPanel.Size.Y &&
                    tipSetPosition.X + tipSetSize.X > candidateX &&
                    tipSetPosition.X < candidateX + intentGraphPanel.Size.X)
                {
                    if (tipSetPosition.X + tipSetSize.X / 2 < candidateX + intentGraphPanel.Size.X / 2 && tipSetPosition.X + tipSetSize.X <= maxX)
                    {
                        candidateX = tipSetPosition.X + tipSetSize.X;
                    }
                    else if (tipSetPosition.X - intentGraphPanel.Size.X >= 0)
                    {
                        candidateX = tipSetPosition.X - intentGraphPanel.Size.X;
                    }
                }
            }

            intentGraphPanel.Position = new Vector2(candidateX, pinableIntentGraph ? IntentGraphPanelTopPinned : IntentGraphPanelTop);
        };
    }

    private class IntentGraphItem
    {
        public required NIntentGraphPanel IntentGraphPanel { get; set; }

        public required Action RemoveIntentGraphPanel { get; set; }
    }
}
