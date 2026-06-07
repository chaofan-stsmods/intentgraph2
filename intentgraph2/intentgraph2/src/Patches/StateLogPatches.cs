using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace IntentGraph2.Patches;

public class StateLogPatches
{
    public static readonly ConditionalWeakTable<MonsterMoveStateMachine, List<MonsterState>> FullStateLog = new();

    private static readonly MoveState NormalStun = new MoveState("NormalStun", (c) => Task.CompletedTask, new StunIntent());

    [HarmonyPatch(typeof(MonsterMoveStateMachine), "SetCurrentState")]
    public static class SetCurrentStatePatch
    {
        public static void Prefix(MonsterMoveStateMachine __instance, MonsterState state)
        {
            if (!state.ShouldAppearInLogs)
            {
                return;
            }

            var loggedState = state;
            if (state is MoveState moveState && moveState.Intents.Count == 1 && moveState.Intents[0].IntentType == IntentType.Stun)
            {
                var stackTrace = new StackTrace();
                var stunCaller = stackTrace.GetFrames()
                    .Where(t =>
                    {
                        var type = t.GetMethod()?.DeclaringType;
                        return type?.IsAssignableTo(typeof(IAsyncStateMachine)) == true && type?.DeclaringType?.IsAssignableTo(typeof(AbstractModel)) == true;
                    })
                    .FirstOrDefault();
                var isStunFromCard = stunCaller != null && stunCaller.GetMethod()?.DeclaringType?.DeclaringType?.IsAssignableTo(typeof(CardModel)) == true;
                var stateUpdated = false;
                if (!isStunFromCard)
                {
                    var stunMoves = __instance.States.Values.Where(s => s is MoveState ms && ms.Intents.Count == 1 && ms.Intents[0].IntentType == IntentType.Stun).ToList();
                    if (stunMoves.Count == 1)
                    {
                        loggedState = stunMoves[0];
                        stateUpdated = true;
                    }
                }

                if (!stateUpdated)
                {
                    loggedState = NormalStun;
                }
            }

            var logs = FullStateLog.GetOrCreateValue(__instance);
            if (logs.Count >= 2 && logs[^1] == NormalStun && logs[^2] == loggedState)
            {
                logs.RemoveAt(logs.Count - 1);
            }
            else if (logs.Count >= 1 && logs[^1] == NormalStun)
            {
                logs[^1] = loggedState;
            }
            else
            {
                logs.Add(loggedState);
            }
        }
    }

    [HarmonyPatch(typeof(MonsterMoveStateMachine), MethodType.Constructor, typeof(IEnumerable<MonsterState>), typeof(MonsterState))]
    public static class StateMachineConstructorPatch
    {
        public static void Postfix(MonsterMoveStateMachine __instance, IEnumerable<MonsterState> states, MonsterState initialState)
        {
            var logs = new List<MonsterState>();
            FullStateLog.Add(__instance, logs);
            if (initialState.ShouldAppearInLogs)
            {
                logs.Add(initialState);
            }
        }
    }
}
