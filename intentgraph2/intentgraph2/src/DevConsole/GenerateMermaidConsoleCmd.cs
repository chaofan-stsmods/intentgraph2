using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using System.IO;
using System.Text;

namespace IntentGraph2.DevConsole;
public class GenerateMermaidConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "generatemermaid";

    public override string Args => string.Empty;

    public override string Description => "Generate Mermaid diagrams for all monsters";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var sb = new StringBuilder();

        foreach (var canonicalMonsterModel in ModelDb.Monsters)
        {
            var monsterModel = canonicalMonsterModel.ToMutable();
            monsterModel.Rng = Rng.Chaotic;
            monsterModel.SetUpForCombat();
            Creature entity = new Creature(monsterModel, CombatSide.Enemy, null)
            {
                CombatState = new NullCombatState()
            };

            sb.AppendLine($"# {monsterModel.Title.GetFormattedText()}");
            var diagram = IntentGraphGenerator.GenerateMermaidDiagram(monsterModel);
            sb.Append("```mermaid\n");
            sb.AppendLine(diagram);
            sb.Append('\n');
            sb.AppendLine("```");
            sb.Append('\n');
        }

        var saveFilePath = Path.GetFullPath(Path.Join(Path.GetDirectoryName(typeof(ModManager).Assembly.Location), "..", $"mermaidGraphs.md"));
        File.WriteAllText(saveFilePath, sb.ToString());

        return new CmdResult(success: true, "Mermaid diagrams generated");
    }
}
