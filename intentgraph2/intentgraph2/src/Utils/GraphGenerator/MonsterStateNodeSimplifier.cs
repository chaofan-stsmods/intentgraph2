using IntentGraph2.Utils.GraphGenerator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Utils.GraphGenerator;

internal class MonsterStateNodeSimplifier
{
    public static void SimplifyStateNodes(MonsterStateNode stateNode, HashSet<MonsterStateNode> allNodes)
    {
        var rootNodes = new List<MonsterStateNode>(allNodes.Where(n => n.Parent == null));

        MergeSameNodes(allNodes, rootNodes);
        ChangeToHorizontalLayout(allNodes, rootNodes);
    }

    public static void FindAndSetSimpleLoops(List<MonsterStateNode> nodes)
    {
        foreach (var node in nodes)
        {
            FindAndSetSimpleLoops(node);
        }
    }

    private static void MergeSameNodes(HashSet<MonsterStateNode> allNodes, List<MonsterStateNode> rootNodes)
    {
        var existingSameNodes = new List<(MonsterStateNode, MonsterStateNode)>();
        for (int i = 0; i < rootNodes.Count; i++)
        {
            MonsterStateNode? nodeA = rootNodes[i];
            for (int j = i + 1; j < rootNodes.Count; j++)
            {
                MonsterStateNode? nodeB = rootNodes[j];
                if (AreSameNode(nodeA, nodeB, existingSameNodes))
                {
                    existingSameNodes.Add((nodeA, nodeB));
                }
            }
        }

        var replacement = new Dictionary<MonsterStateNode, MonsterStateNode>();
        foreach (var (a, b) in existingSameNodes)
        {
            if (!replacement.ContainsKey(a) && !replacement.ContainsKey(b))
            {
                replacement[b] = a;
            }
        }

        foreach (var node in allNodes)
        {
            if (node.NextState != null && replacement.TryGetValue(node.NextState, out var replacementNextState))
            {
                node.NextState = replacementNextState;
            }
        }
    }

    private static bool AreSameNode(MonsterStateNode a, MonsterStateNode b, List<(MonsterStateNode, MonsterStateNode)> exisitingSameNodes)
    {
        if (exisitingSameNodes.Contains((a, b)) || exisitingSameNodes.Contains((b, a)))
        {
            return true;
        }

        if (a == b)
        {
            return true;
        }

        if (a.Children != null != (b.Children != null))
        {
            return false;
        }

        if (a.NextState != null != (b.NextState != null))
        {
            return false;
        }

        if (a.Label != b.Label)
        {
            return false;
        }

        // pretend a and b are the same and check next states.
        exisitingSameNodes.Add((a, b));
        try
        {
            if (a.Children != null)
            {
                if (a.Children.Count != b.Children!.Count)
                {
                    return false;
                }
                for (int i = 0; i < a.Children.Count; i++)
                {
                    if (!AreSameNode(a.Children[i], b.Children[i], exisitingSameNodes))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (a.State != b.State)
                {
                    return false;
                }
            }

            if (a.NextState != null)
            {
                if (!AreSameNode(a.NextState, b.NextState!, exisitingSameNodes))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            exisitingSameNodes.Remove((a, b));
        }
    }

    private static void ChangeToHorizontalLayout(HashSet<MonsterStateNode> allNodes, List<MonsterStateNode> rootNodes)
    {
        foreach (var node in allNodes)
        {
            var children = node.Children;

            // 3 or more children and each is small and don't have out arrows
            if (children == null || children.Count <= 2 || node.Parent?.HorizontalLayout == true ||
                children.Any(c => c.HorizontalLayout || c.NextState != null || c.Width > 1.5f))
            {
                continue;
            }

            // Intent graph is already too wide
            if ((node.Parent == null || node.Parent.Width < children.Sum(c => c.Width)) && rootNodes.Count > 4)
            {
                continue;
            }

            node.HorizontalLayout = true;
            node.CalculateNodeSize();
            var n = node.Parent;
            while (n != null)
            {
                n.CalculateNodeSize();
                n = n.Parent;
            }
        }
    }

    private static void FindAndSetSimpleLoops(MonsterStateNode initNode)
    {
        // A Simple loop contains only no-child nodes, and only one node has precessor outside the loop.
        var allNodes = initNode.GetAllNodes();
        var precessorCount = new Dictionary<MonsterStateNode, int>();
        precessorCount[initNode] = 1;
        foreach (var node in allNodes)
        {
            if (node.NextState != null)
            {
                precessorCount[node.NextState] = precessorCount.GetValueOrDefault(node.NextState) + 1;
            }
        }

        var candidates = allNodes.Where(n => n.Parent == null && n.Children == null && precessorCount.GetValueOrDefault(n) > 1).ToList();
        foreach (var node in candidates)
        {
            var loopNodes = new HashSet<MonsterStateNode>();
            var current = node;
            while (current != null && !loopNodes.Contains(current))
            {
                if (current != node && (current.Children != null || precessorCount.GetValueOrDefault(current) > 1))
                {
                    goto nextCandidate;
                }

                loopNodes.Add(current);
                current = current.NextState;
            }
            if (current == node && loopNodes.Count > 1)
            {
                node.SimpleLoopStart = true;
                node.SimpleLoopLength = loopNodes.Count;
                node.SimpleLoopPrecessorCount = precessorCount.GetValueOrDefault(node) - 1; // -1 for inside loop precessor
            }

        nextCandidate:;
        }
    }
}
