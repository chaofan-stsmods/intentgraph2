using HarmonyLib;
using IntentGraph2.Patches;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Linq;

namespace IntentGraph2.Utils.Variable;

public class VariableContext : IVariableContext
{
    public VariableContext(MonsterModel monster)
    {
        Monster = monster;
    }

    public MonsterModel Monster { get; }

    public bool InBestiary { get; set; }

    public int GetIntVariable(string variableName)
    {
        try
        {
            return Convert.ToInt32(GetObjectVariable(variableName) ?? 0);
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting int variable '{variableName}': {ex}. Return 0.");
            return 0;
        }
    }

    public bool GetBoolVariable(string variableName)
    {
        try
        {
            return Convert.ToBoolean(GetObjectVariable(variableName) ?? false);
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting bool variable '{variableName}': {ex}. Return false.");
            return false;
        }
    }

    public string GetStringVariable(string variableName)
    {
        try
        {
            return Convert.ToString(GetObjectVariable(variableName) ?? string.Empty) ?? string.Empty;
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting string variable '{variableName}': {ex}. Return empty string.");
            return string.Empty;
        }
    }

    public object? GetObjectVariable(string variableName)
    {
        try
        {
            if (variableName.StartsWith("m."))
            {
                if (variableName.StartsWith("m.hasPower_"))
                {
                    var powerId = variableName.Substring(11);
                    return Monster.Creature.Powers.Any(p => p.Id.Entry == powerId);
                }
                else if (variableName.StartsWith("m.powerAmount_"))
                {
                    var powerId = variableName.Substring(14);
                    var power = Monster.Creature.Powers.FirstOrDefault(p => p.Id.Entry == powerId);
                    return power?.Amount ?? 0;
                }

                var fieldName = variableName.Substring(2);
                var monsterType = Traverse.Create(Monster);
                return monsterType.Property(fieldName).GetValue() ?? monsterType.Field(fieldName).GetValue();
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
                    return stateMachine.States.Any(s => s.Key == moveName);
                }
                else if (fieldName.StartsWith("startsWith_"))
                {
                    var moveName = fieldName.Substring(11);
                    return stateMachine.GetInitialState().Id == moveName;
                }
                else if (fieldName.StartsWith("nextMoveOf_") && fieldName.Contains("_is_"))
                {
                    var isIndex = fieldName.IndexOf("_is_");
                    if (isIndex == -1)
                    {
                        return false;
                    }

                    var moveName = fieldName.Substring(11, isIndex - 11);
                    var nextMoveName = fieldName.Substring(isIndex + 4);
                    var move = stateMachine.States.FirstOrDefault(s => s.Key == moveName).Value;
                    var nextMove = stateMachine.States.FirstOrDefault(s => s.Key == nextMoveName).Value;
                    if (move is MoveState moveState && nextMove != null)
                    {
                        return moveState.FollowUpStateId == nextMoveName || moveState.FollowUpState == nextMove;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return null;
                }
            }

            if (variableName.StartsWith("slotName_is_"))
            {
                var slotName = variableName.Substring(12);
                return Monster.Creature.SlotName == slotName;
            }

            return variableName switch
            {
                "act" => Monster.CombatState.RunState.CurrentActIndex,
                "slotIndex" => Monster.CombatState.Encounter?.Slots.IndexOf(Monster.Creature.SlotName),
                "ascension" => ((int?)HasAscensionLevelPatches.OverwriteAsensionLevel) ??
                    Traverse.Create(RunManager.Instance.AscensionManager).Field("_level").GetValue<int>(),
                "showMoveNames" => IntentGraphMod.Config.ShowMonsterMoveNames,
                "inBestiary" => InBestiary,
                "showMoveDetail" => IntentGraphMod.Config.ShowMoveDetail,
                _ => null,
            };
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting object variable '{variableName}': {ex}. Return null.");
            return null;
        }
    }

    public Type? GetVariableType(string variableName)
    {
        try
        {
            if (variableName.StartsWith("m."))
            {
                if (variableName.StartsWith("m.hasPower_"))
                {
                    return typeof(bool);
                }
                else if (variableName.StartsWith("m.powerAmount_"))
                {
                    return typeof(int);
                }

                var fieldName = variableName.Substring(2);
                var monsterType = Traverse.Create(Monster);
                return monsterType.Property(fieldName).GetValueType() ?? monsterType.Field(fieldName).GetValueType();
            }

            // For outdate check
            if (variableName.StartsWith("mm.") && Monster.MoveStateMachine != null)
            {
                var stateMachine = Monster.MoveStateMachine;
                var fieldName = variableName.Substring(3);
                if (fieldName == "count")
                {
                    return typeof(int);
                }
                else if (fieldName.StartsWith("hasMove_"))
                {
                    return typeof(bool);
                }
                else if (fieldName.StartsWith("startsWith_"))
                {
                    return typeof(bool);
                }
                else if (fieldName.StartsWith("nextMoveOf_") && fieldName.Contains("_is_"))
                {
                    return typeof(bool);
                }
                else
                {
                    return null;
                }
            }

            if (variableName.StartsWith("slotName_is_"))
            {
                return typeof(bool);
            }

            return variableName switch
            {
                "act" => typeof(int),
                "slotIndex" => typeof(int),
                "ascension" => typeof(int),
                "showMoveNames" => typeof(bool),
                "inBestiary" => typeof(bool),
                "showMoveDetail" => typeof(bool),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Error getting object variable '{variableName}': {ex}. Return null.");
            return null;
        }
    }
}
