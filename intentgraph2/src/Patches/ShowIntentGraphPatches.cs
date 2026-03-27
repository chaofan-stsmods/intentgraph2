using Godot;
using HarmonyLib;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using MegaCrit.Sts2.addons.mega_text;
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

    [HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
    public static class ShowHoverTipsPatch
    {
        public static void Postfix(NCreature __instance)
        {
            if (NGame.Instance?.HoverTipsContainer == null || NCombatRoom.Instance?.Ui.Hand.InCardPlay != false || __instance.Entity?.IsMonster != true)
            {
                return;
            }

            if (intentGraphPanel != null)
            {
                intentGraphPanel.QueueFreeSafely();
                unregisterResizedEvent?.Invoke();
                unregisterResizedEvent = null;
                intentGraphPanel = null;
            }

            var creature = __instance.Entity;
            if (creature.Monster == null || !MonsterSetupPatch.GeneratedGraphs.TryGetValue(creature.Monster, out var graph))
            {
                return;
            }

            var scene = PreloadManager.Cache.GetScene("res://intentgraph2/scenes/intent_graph_panel.tscn");
            intentGraphPanel = scene.Instantiate<MarginContainer>(PackedScene.GenEditState.Disabled);
            var monsterNameLabel = intentGraphPanel.GetNode<Label>("%MonsterName");
            monsterNameLabel.Text = creature.Name;
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Regular, "font");
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");

            var intentGraph = intentGraphPanel.GetNode<NIntentGraph>("%IntentGraph");
            intentGraph.Graph = graph;
            Action handleResized = OnIntentGraphPanelResized(__instance, intentGraphPanel);

            unregisterResizedEvent = () =>
            {
                __instance.Resized -= handleResized;
            };

            __instance.Resized += handleResized;
            intentGraphPanel.Resized += handleResized;

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
                // don't break because we want to find the last hover tip set which is most likely the one related to the current creature
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

    [HarmonyPatch(typeof(NCreature), nameof(NCreature.HideHoverTips))]
    public static class HideHoverTipsPatch
    {
        public static void Postfix(NCreature __instance)
        {
            if (intentGraphPanel != null)
            {
                intentGraphPanel.QueueFreeSafely();
                unregisterResizedEvent?.Invoke();
                unregisterResizedEvent = null;
                intentGraphPanel = null;
            }
        }
    }
}
