using Godot;
using HarmonyLib;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System;
using System.Linq;

namespace IntentGraph2.Patches;

public class ShowIntentGraphPatches
{
    private const float IntentGraphPanelTop = 90;

    private static NCreature? currentCreature;
    private static MarginContainer? intentGraphPanel;
    private static Action? unregisterResizedEvent;
    private static bool intentGraphVisible = true;

    public static void ToggleIntentGraphVisibility()
    {
        intentGraphVisible = !intentGraphVisible;
        if (intentGraphVisible)
        {
            intentGraphPanel?.Show();
        }
        else
        {
            intentGraphPanel?.Hide();
        }
    }

    private static void RemoveCurrentIntentGraphPanel()
    {
        if (intentGraphPanel != null)
        {
            intentGraphPanel.QueueFreeSafely();
            unregisterResizedEvent?.Invoke();
            unregisterResizedEvent = null;
            intentGraphPanel = null;
            currentCreature = null;
        }
    }

    [HarmonyPatch(typeof(NCreature), "OnFocus")]
    public static class OnFocusPatch
    {
        public static void Postfix(NCreature __instance)
        {
            if (__instance == currentCreature)
            {
                return;
            }

            if (NGame.Instance?.HoverTipsContainer == null || NCombatRoom.Instance?.Ui.Hand.InCardPlay != false || __instance.Entity?.IsMonster != true)
            {
                return;
            }

            RemoveCurrentIntentGraphPanel();
            currentCreature = __instance;

            var creature = __instance.Entity;
            if (creature.Monster == null || !IntentGraphMod.GeneratedGraphs.TryGetValue(creature.Monster, out var graph))
            {
                return;
            }

            var scene = PreloadManager.Cache.GetScene("res://intentgraph2/scenes/intent_graph_panel.tscn");
            intentGraphPanel = scene.Instantiate<MarginContainer>();
            var monsterNameLabel = intentGraphPanel.GetNode<Label>("%MonsterName");
            monsterNameLabel.Text = creature.Name;
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Regular, "font");
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");

            var intentGraph = intentGraphPanel.GetNode<NIntentGraph>("%IntentGraph");
            intentGraph.Graph = graph;
            intentGraph.Monster = creature.Monster;
            Action handleResized = OnIntentGraphPanelResized(__instance, intentGraphPanel);

            unregisterResizedEvent = () =>
            {
                try
                {
                    __instance.Resized -= handleResized;
                    intentGraphPanel.Resized -= handleResized;
                }
                catch (Exception ex)
                {
                    IgLogger.Error("Error unregistering resized event handlers: " + ex);
                }
            };

            __instance.Resized += handleResized;
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

            NGame.Instance.HoverTipsContainer.AddChildSafely(intentGraphPanel);
            if (!intentGraphVisible)
            {
                intentGraphPanel.Hide();
            }
        }

        private static Action OnIntentGraphPanelResized(NCreature __instance, MarginContainer intentGraphPanel)
        {
            return () =>
            {
                var parent = intentGraphPanel.GetParent();
                var tipSet = (NHoverTipSet?)parent?.GetChildren().Last(c => c is NHoverTipSet);

                var maxX = NGame.Instance!.GetViewportRect().Size.X - intentGraphPanel.Size.X;
                var candidateX = Math.Clamp(__instance.GlobalPosition.X + __instance.Size.X / 2 - intentGraphPanel.Size.X / 2, 0, maxX);

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

                intentGraphPanel.Position = new Vector2(candidateX, IntentGraphPanelTop);
            };
        }
    }

    [HarmonyPatch(typeof(NCreature), "OnUnfocus")]
    public static class OnUnfocusPatch
    {
        public static void Prefix(NCreature __instance)
        {
            RemoveCurrentIntentGraphPanel();
        }
    }

    [HarmonyPatch(typeof(NCreature), "_ExitTree")]
    public static class ExitTreePatch
    {
        public static void Prefix(NCreature __instance)
        {
            if (currentCreature == __instance)
            {
                RemoveCurrentIntentGraphPanel();
            }
        }
    }
}
