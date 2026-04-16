using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using System;
using System.Linq;

namespace IntentGraph2.DevConsole;

public class EditIntentConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "editintent";

    public override string Args => "<monster model full name>";

    public override string Description => "Open the intent graph editor for the given monster model in the current combat";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var monsterModelFullName = string.Join(" ", args).Trim();
        if (string.IsNullOrWhiteSpace(monsterModelFullName))
        {
            return new CmdResult(false, "Monster model full name is required.");
        }

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || combatState.Encounter == null)
        {
            return new CmdResult(false, "You must be in combat to edit an intent graph.");
        }

        var creature = combatState.Enemies.FirstOrDefault(enemy =>
            enemy.Monster?.GetType().FullName?.Equals(monsterModelFullName, StringComparison.OrdinalIgnoreCase) == true);
        if (creature?.Monster == null)
        {
            var availableMonsters = combatState.Enemies
                .Select(enemy => enemy.Monster?.GetType().FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToArray();
            var message = availableMonsters.Length == 0
                ? $"Monster model '{monsterModelFullName}' is not active in this combat."
                : $"Monster model '{monsterModelFullName}' is not active in this combat. Active models: {string.Join(", ", availableMonsters)}";
            return new CmdResult(false, message);
        }

        if (!IntentGraphEditorHost.TryOpenEditor(creature.Monster, creature.Name, out var openMessage))
        {
            return new CmdResult(false, openMessage);
        }

        return new CmdResult(true, openMessage);
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(GetAvailableMonsterModelNames(), Array.Empty<string>(), args.FirstOrDefault() ?? string.Empty);
        }

        return base.GetArgumentCompletions(player, args);
    }

    private static string[] GetAvailableMonsterModelNames()
    {
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState?.Encounter == null)
        {
            return [];
        }

        return combatState.Enemies
            .Select(enemy => enemy.Monster?.GetType().FullName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }
}