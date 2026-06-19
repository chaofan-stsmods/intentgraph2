using HarmonyLib;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Linq;

namespace IntentGraph2.Utils.Rule;

public class RuleContext : IRuleContext
{
    public RuleContext(MonsterModel monster)
    {
        Monster = monster;
    }

    public MonsterModel Monster { get; }

    public int GetIntVariable(string variableName)
    {
        try
        {
            if (variableName.StartsWith("m."))
            {
                var fieldName = variableName.Substring(2);
                var monsterType = Traverse.Create(Monster);
                return Convert.ToInt32(monsterType.Property(fieldName).GetValue() ?? monsterType.Field(fieldName).GetValue() ?? 0);
            }

            // For outdate check
            if (variableName.StartsWith("mm.") && Monster.MoveStateMachine != null)
            {
                var stateMachine = Monster.MoveStateMachine;
                var fieldName = variableName.Substring(3);
                if (fieldName == "count")
                {
                    return stateMachine.States.Count;
                }
                else if (fieldName.StartsWith("hasMove_"))
                {
                    var moveName = fieldName.Substring(8);
                    return stateMachine.States.Any(s => s.Key == moveName) ? 1 : 0;
                }
                else if (fieldName.StartsWith("startsWith_"))
                {
                    var moveName = fieldName.Substring(11);
                    return stateMachine.GetInitialState().Id == moveName ? 1 : 0;
                }
                else if (fieldName.StartsWith("nextMoveOf_") && fieldName.Contains("_is_"))
                {
                    var isIndex = fieldName.IndexOf("_is_");
                    if (isIndex == -1)
                    {
                        return 0;
                    }

                    var moveName = fieldName.Substring(11, isIndex - 11);
                    var nextMoveName = fieldName.Substring(isIndex + 4);
                    var move = stateMachine.States.FirstOrDefault(s => s.Key == moveName).Value;
                    var nextMove = stateMachine.States.FirstOrDefault(s => s.Key == nextMoveName).Value;
                    if (move is MoveState moveState && nextMove != null)
                    {
                        return moveState.FollowUpStateId == nextMoveName || moveState.FollowUpState == nextMove ? 1 : 0;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    return 0;
                }
            }

            return variableName switch
            {
                "act" => Monster.CombatState.RunState.CurrentActIndex,
                "slotIndex" => Monster.CombatState.Encounter?.Slots.IndexOf(Monster.Creature.SlotName) ?? 0,
                "ascension" => Traverse.Create(RunManager.Instance.AscensionManager).Field("_level").GetValue<int>(),
                "showMoveNames" => IntentGraphMod.Config.ShowMonsterMoveNames ? 1 : 0,
                _ => 0,
            };
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting variable '{variableName}': {ex}. Return 0.");
            return 0;
        }
    }
}
