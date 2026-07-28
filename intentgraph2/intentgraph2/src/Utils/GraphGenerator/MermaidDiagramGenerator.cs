using IntentGraph2.Models;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntentGraph2.Utils.GraphGenerator;

internal class MermaidDiagramGenerator
{
    private MonsterModel monster;
    private IntentGraphLocalizer localizer;
    private readonly Dictionary<MonsterStateNode, string> nodeAliases = new();
    private readonly Dictionary<string, List<string>> supplementalTextByFullId = new(StringComparer.Ordinal);
    private int nextAliasIndex;

    public MermaidDiagramGenerator(MonsterModel monster, IntentGraphLocalizer localizer)
    {
        this.monster = monster;
        this.localizer = localizer;
    }

    public string StateNodesToGraph(List<MonsterStateNode> stateNodes, IntentDefinition? intentDefinition)
    {
        nodeAliases.Clear();
        supplementalTextByFullId.Clear();
        nextAliasIndex = 0;
        CollectGraphPatchLabels(intentDefinition, stateNodes);

        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine("direction LR");

        foreach (var stateNode in stateNodes)
        {
            var alias = GetAlias(stateNode);
            if (stateNode.IsInitialState)
            {
                sb.AppendLine($"  [*] --> {alias}");
            }
        }

        var visited = new HashSet<MonsterStateNode>();
        foreach (var stateNode in stateNodes)
        {
            AppendNode(stateNode, sb, 1, visited);
        }

        return sb.ToString().TrimEnd();
    }

    private void AppendNode(MonsterStateNode stateNode, StringBuilder sb, int indentLevel, HashSet<MonsterStateNode> visited)
    {
        if (!visited.Add(stateNode))
        {
            return;
        }

        var indent = new string(' ', indentLevel * 2);
        var alias = GetAlias(stateNode);
        if (stateNode.Children == null || stateNode.Children.Count == 0)
        {
            sb.AppendLine($"{indent}state \"{EscapeLabel(BuildMoveLabel(stateNode))}\" as {alias}");
        }
        else
        {
            sb.AppendLine($"{indent}state \"{EscapeLabel(BuildBranchLabel(stateNode))}\" as {alias} {{");
            sb.AppendLine($"{indent}  direction LR");

            foreach (var child in stateNode.Children)
            {
                if (child.Children == null || child.Children.Count == 0)
                {
                    var childAlias = GetAlias(child);
                    sb.AppendLine($"{indent}  state \"{EscapeLabel(BuildMoveLabel(child))}\" as {childAlias}");
                }
                else
                {
                    AppendNode(child, sb, indentLevel + 1, visited);
                }
            }

            foreach (var child in stateNode.Children)
            {
                var childAlias = GetAlias(child);
                sb.AppendLine($"{indent}  [*] --> {childAlias}");
            }

            foreach (var child in stateNode.Children)
            {
                AppendEdgesFromNode(child, sb, indentLevel + 1);
            }

            sb.AppendLine($"{indent}}}");
        }

        AppendEdgesFromNode(stateNode, sb, indentLevel);

        if (stateNode.Children != null)
        {
            foreach (var child in stateNode.Children)
            {
                if (child.Children != null && child.Children.Count > 0)
                {
                    AppendNode(child, sb, indentLevel, visited);
                }

                if (child.NextState != null)
                {
                    AppendNode(child.NextState, sb, indentLevel, visited);
                }
            }
        }

        if (stateNode.NextState != null)
        {
            AppendNode(stateNode.NextState, sb, indentLevel, visited);
        }
    }

    private void AppendEdgesFromNode(MonsterStateNode stateNode, StringBuilder sb, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var alias = GetAlias(stateNode);
        if (stateNode.NextState != null)
        {
            sb.AppendLine($"{indent}{alias} --> {GetAlias(stateNode.NextState)}");
        }
        else if (stateNode.Parent != null)
        {
            sb.AppendLine($"{indent}{alias} --> [*]");
        }
    }

    private string BuildBranchLabel(MonsterStateNode stateNode)
    {
        if (stateNode.State is RandomBranchState randomBranchState)
        {
            var allEqual = randomBranchState.States
                .Select(state => state.GetWeight())
                .Distinct()
                .Skip(1)
                .Any() == false;
            return allEqual ? "fa:fa-cube 等权重随机" : "fa:fa-cube 随机分支";
        }

        if (stateNode.State is ConditionalBranchState)
        {
            return "fa:fa-code 条件分支";
        }

        return stateNode.Id ?? "branch";
    }

    private string BuildMoveLabel(MonsterStateNode stateNode)
    {
        var lines = new List<string>();
        var moveState = stateNode.State as MoveState;
        var moveId = moveState?.Id ?? stateNode.Id ?? string.Empty;
        var moveName = moveState != null ? localizer.GetMoveName(monster, moveState.Id) : null;
        var displayName = string.IsNullOrWhiteSpace(moveName) ? moveId : moveName;
        var intentText = moveState == null ? string.Empty : BuildIntentText(moveState);
        lines.Add(string.IsNullOrWhiteSpace(intentText) ? displayName : $"{intentText} {displayName}");

        if (stateNode.Label?.Cooldown > 0)
        {
            lines.Add($"fa:fa-hourglass-half 冷却：{stateNode.Label.Cooldown}");
        }

        var branchMetadata = GetBranchMetadata(stateNode);
        if (branchMetadata != null)
        {
            foreach (var line in branchMetadata)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
        }

        foreach (var line in GetSupplementalText(stateNode))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return string.Join("\n", lines);
    }

    private string BuildIntentText(MoveState moveState)
    {
        var intents = moveState.Intents;
        if (intents.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", intents.Select(BuildIntentToken).Where(token => !string.IsNullOrWhiteSpace(token)));
    }

    private string BuildIntentToken(object intent)
    {
        var intentName = intent.GetType().GetProperty("IntentType")?.GetValue(intent)?.ToString()?.ToLowerInvariant() ?? "unknown";
        return intent switch
        {
            AttackIntent attackIntent => AppendNumericSuffix(intentName, (int?)attackIntent.DamageCalc?.Invoke(), attackIntent.Repeats),
            StatusIntent statusIntent => AppendNumericSuffix(intentName, statusIntent.CardCount),
            _ => intentName,
        };
    }

    private string AppendNumericSuffix(string intentName, int? value, int repeats = 1)
    {
        var sb = new StringBuilder(intentName);
        if (value != null)
        {
            sb.Append(value.Value);
        }

        if (repeats > 1)
        {
            sb.Append('x');
            sb.Append(repeats);
        }

        return sb.ToString();
    }

    private IEnumerable<string> GetSupplementalText(MonsterStateNode stateNode)
    {
        var stateId = stateNode.State?.Id;
        if (stateNode.FullId != null && supplementalTextByFullId.TryGetValue(stateNode.FullId, out var patchLines))
        {
            foreach (var line in patchLines)
            {
                yield return line;
            }
        }

        if (stateId == null)
        {
            yield break;
        }

        var nodeKey = $"text.{monster.GetType().FullName}.{stateId}";
        if (localizer.TryGet(nodeKey, out var nodeValue))
        {
            foreach (var line in SplitLines(nodeValue))
            {
                yield return line;
            }
        }

        var nodeKey2 = $"text.{monster.GetType().FullName}.{stateId}_2";
        if (localizer.TryGet(nodeKey2, out var nodeExtraValue))
        {
            foreach (var line in SplitLines(nodeExtraValue))
            {
                yield return line;
            }
        }

        if (stateNode.Parent?.State?.Id is string parentStateId)
        {
            var key = $"text.{monster.GetType().FullName}.{parentStateId}.{stateId}";
            if (localizer.TryGet(key, out var value))
            {
                foreach (var line in SplitLines(value))
                {
                    yield return line;
                }
            }

            var key2 = $"text.{monster.GetType().FullName}.{parentStateId}.{stateId}_2";
            if (localizer.TryGet(key2, out var extraValue))
            {
                foreach (var line in SplitLines(extraValue))
                {
                    yield return line;
                }
            }
        }
    }

    private IEnumerable<string>? GetBranchMetadata(MonsterStateNode stateNode)
    {
        if (stateNode.Label?.Type == MonsterStateNodeLabel.LabelType.Random)
        {
            var siblings = stateNode.Parent?.Children;
            if (siblings == null)
            {
                return null;
            }

            var siblingWeights = siblings
                .Select(child => child.Label)
                .Where(label => label?.Type == MonsterStateNodeLabel.LabelType.Random)
                .Select(label => label!.Weight)
                .ToList();
            if (siblingWeights.Count == 0)
            {
                return null;
            }

            var allEqual = siblingWeights
                .Distinct()
                .Skip(1)
                .Any() == false;
            if (allEqual)
            {
                return null;
            }

            var sumWeight = siblingWeights.Sum();
            if (sumWeight <= 0)
            {
                return null;
            }

            var percentage = (int)(stateNode.Label.Weight / sumWeight * 100);
            return [ $"fa:fa-spinner {percentage}%" ];
        }

        if (!string.IsNullOrWhiteSpace(stateNode.Label?.Text) && stateNode.Label?.Type == MonsterStateNodeLabel.LabelType.Condition)
        {
            return [ $"fa:fa-spinner {stateNode.Label.Text}" ];
        }

        return null;
    }

    private IEnumerable<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void CollectGraphPatchLabels(IntentDefinition? intentDefinition, List<MonsterStateNode> stateNodes)
    {
        var labels = intentDefinition?.GraphPatch?.Labels;
        if (labels == null || labels.Count == 0)
        {
            return;
        }

        var allNodes = stateNodes.GetAllNodes();
        var nodeByFullId = allNodes
            .Where(node => node.FullId != null)
            .ToDictionary(node => node.FullId!, StringComparer.Ordinal);

        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label.RelativeTo) || !nodeByFullId.TryGetValue(label.RelativeTo, out var node))
            {
                continue;
            }

            var resolvedText = localizer.GetOrElse(label.Text, label.Text);
            var lines = SplitLines(resolvedText).ToList();
            if (lines.Count == 0 || node.FullId == null)
            {
                continue;
            }

            if (!supplementalTextByFullId.TryGetValue(node.FullId, out var existingLines))
            {
                existingLines = new List<string>();
                supplementalTextByFullId[node.FullId] = existingLines;
            }

            existingLines.AddRange(lines);
        }
    }

    private string GetAlias(MonsterStateNode stateNode)
    {
        if (nodeAliases.TryGetValue(stateNode, out var alias))
        {
            return alias;
        }

        string candidate;
        if (!string.IsNullOrWhiteSpace(stateNode.Id))
        {
            candidate = SanitizeAlias(stateNode.Id!);
            if (nodeAliases.Values.Contains(candidate, StringComparer.Ordinal))
            {
                candidate = $"{candidate}_{nextAliasIndex++}";
            }
        }
        else
        {
            candidate = $"node_{nextAliasIndex++}";
        }

        nodeAliases[stateNode] = candidate;
        return candidate;
    }

    private static string SanitizeAlias(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        if (sb.Length == 0 || !char.IsLetter(sb[0]))
        {
            sb.Insert(0, 'n');
        }

        return sb.ToString();
    }

    private static string EscapeLabel(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
