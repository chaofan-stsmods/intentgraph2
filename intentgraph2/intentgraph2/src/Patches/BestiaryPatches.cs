using Godot;
using HarmonyLib;
using IntentGraph2.Scenes;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using System;
using System.Linq;

namespace IntentGraph2.Patches;
public class BestiaryPatches
{
    private static NIntentGraphBestiary? intentGraphBestiary;
    private static MonsterModel? currentMonster;

    [HarmonyPatch(typeof(NBestiary), nameof(NBestiary._Ready))]
    public static class BestiaryReadyPatch
    {
        public static void Postfix(NBestiary __instance)
        {
            try
            {
                if (intentGraphBestiary != null)
                {
                    intentGraphBestiary.QueueFreeSafely();
                    intentGraphBestiary = null;
                }

                var scene = PreloadManager.Cache.GetScene("res://intentgraph2/scenes/intent_graph_bestiary.tscn");
                intentGraphBestiary = scene.Instantiate<NIntentGraphBestiary>();
                __instance.AddChild(intentGraphBestiary);
            }
            catch (Exception ex)
            {
                IgLogger.Error($"Failed to load IntentGraph Bestiary scene: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(NBestiary), "SelectMonster")]
    public static class BestiarySelectMonsterPatch
    {
        public static void Postfix(NBestiary __instance, NBestiaryEntry entry)
        {
            if (intentGraphBestiary == null)
            {
                return;
            }

            var monster = entry.Entry.monsterModel;
            if (monster != null)
            {
                intentGraphBestiary.Monster = currentMonster = monster.CanonicalInstance;
            }
            else
            {
                var encounter = entry.Entry.encounterModel;
                if (encounter != null)
                {
                    monster = encounter.AllPossibleMonsters.FirstOrDefault();
                    intentGraphBestiary.Monster = currentMonster = monster;
                }
                else
                {
                    intentGraphBestiary.Monster = currentMonster = null;
                }
            }
        }
    }

    public static void ReloadIntents()
    {
        if (intentGraphBestiary == null)
        {
            return;
        }

        intentGraphBestiary.Monster = currentMonster;
    }
}
