using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace IntentGraph2.Utils;

public static class StateMachineExtensions
{
    public static MonsterState GetInitialState(this MonsterMoveStateMachine stateMachine)
    {
        return stateMachine.GetType().GetField("_initialState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(stateMachine) is MonsterState initialState
            ? initialState
            : throw new Exception("Failed to get initial state from state machine.");
    }

    public static List<string> GetStates(this ConditionalBranchState conditionalBranchState)
    {
        var list = conditionalBranchState.GetType().GetProperty("States", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(conditionalBranchState) as IList;
        var result = new List<string>();
        if (list != null)
        {
            foreach (var item in list)
            {
                var id = item.GetType().GetField("id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(item) as string;
                if (id != null)
                {
                    result.Add(id);
                }
            }
        }

        return result;
    }

    public static string? EvaluateStates(this ConditionalBranchState conditionalBranchState)
    {
        var list = conditionalBranchState.GetType().GetProperty("States", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(conditionalBranchState) as IList;
        if (list != null)
        {
            foreach (var item in list)
            {
                var result = item.GetType().GetMethod("Evaluate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.Invoke(item, null) as float?;
                if (result != null && result > 0)
                {
                    return item.GetType().GetField("id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(item) as string;
                }
            }
        }

        return null;
    }
}
