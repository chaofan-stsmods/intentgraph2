using IntentGraph2.Patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace IntentGraph2.DevConsole;

public class ReloadIntentsConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "reloadintents";

    public override string Args => string.Empty;

    public override string Description => "Reload intent graph for developing";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        IntentGraphMod.LoadIntentDefinitions();
        IntentGraphMod.LoadIntentStrings(LocManager.Instance.Language);

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState != null && combatState.Encounter != null)
        {
            IntentGraphMod.GeneratedGraphs.Clear();
            foreach (var creature in combatState.Enemies)
            {
                MonsterSetupPatch.Postfix(CombatManager.Instance, creature);
            }
        }

        return new CmdResult(success: true, "Intent graph reloaded");
    }
}
