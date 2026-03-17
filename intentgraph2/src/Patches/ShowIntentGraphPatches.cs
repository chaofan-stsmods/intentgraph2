using Godot;
using HarmonyLib;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System;

namespace IntentGraph2.Patches;

public class ShowIntentGraphPatches
{
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
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.font);
            monsterNameLabel.ApplyLocaleFontSubstitution(FontType.Bold, ThemeConstants.Label.font);

            var intentGraph = intentGraphPanel.GetNode<NIntentGraph>("%IntentGraph");
            intentGraph.Graph = graph;

            var handleResized = () =>
            {
                intentGraphPanel.Position = new Vector2(
                    Math.Clamp(__instance.GlobalPosition.X + __instance.Size.X / 2 - intentGraphPanel.Size.X / 2, 0, NGame.Instance.GetViewportRect().Size.X - intentGraphPanel.Size.X),
                    90);
            };

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
