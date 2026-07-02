using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IntentGraph2.Utils.GraphGenerator;

internal class MonsterStateNodeSimplifier
{
    public static void SimplifyStateNodes(MonsterStateNode stateNode, HashSet<MonsterStateNode> allNodes, MonsterStateNodeConverter converter)
    {
        RemoveUnreachableNoRepeat(allNodes, converter);

        var rootNodes = new List<MonsterStateNode>(allNodes.Where(n => n.Parent == null));
        MergeSameNodes(allNodes, rootNodes);
        ChangeToHorizontalLayout(allNodes, rootNodes);
    }

    public static void FindAndSetSimpleLoops(List<MonsterStateNode> initNodes)
    {
        // A Simple loop contains only no-child nodes, and only one node has precessor outside the loop.
        var precessorCount = new Dictionary<MonsterStateNode, int>();
        foreach (var initNode in initNodes)
        {
            precessorCount[initNode] = 1;
        }

        var allNodes = initNodes.GetAllNodes();
        foreach (var node in allNodes)
        {
            if (node.NextState != null)
            {
                precessorCount[node.NextState] = precessorCount.GetValueOrDefault(node.NextState) + 1;
            }
        }

        var candidates = allNodes.Where(n => n.Parent == null && n.Children == null && !n.ForceNotSimpleLoop && precessorCount.GetValueOrDefault(n) > 1).ToList();
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
                    IgLogger.Info($"MergeSameNodes detected same node {nodeA.State?.Id} and {nodeB.State?.Id}.");
                }
            }
        }

        var replacement = new Dictionary<MonsterStateNode, MonsterStateNode>();
        foreach (var (a, b) in existingSameNodes)
        {
            if (!replacement.ContainsKey(a) && !replacement.ContainsKey(b))
            {
                replacement[b] = a;
                a.AddMoveStateIdsFrom(b);
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

    private static void RemoveUnreachableNoRepeat(HashSet<MonsterStateNode> allNodes, MonsterStateNodeConverter converter)
    {
        bool modified;
        do
        {
            modified = false;
            var precessors = GetPrecessorDict(allNodes);

            var nodesToRemove = new List<MonsterStateNode>();
            foreach (var node in allNodes)
            {
                var parent = node.Parent;
                if (parent == null || node.State is not MoveState moveState || node.Children != null ||
                    parent.State is not RandomBranchState parentBranchState ||
                    parent.Children == null || parent.Children.Any(c => !c.IsLabelGenerated))
                {
                    continue;
                }

                var stateWeight = parentBranchState.States.FirstOrDefault(s => s.stateId == moveState.Id);
                if (stateWeight.stateId == null)
                {
                    continue;
                }

                if (TryRemoveNoRepeatNodePrecededBySame(node, precessors, nodesToRemove, converter, moveState, parentBranchState, stateWeight))
                {
                    modified = true;
                }
                else if (TryRemoveNoRepeatTextIfPrecededByOther(node, precessors, converter, stateWeight))
                {
                    modified = true;
                }
            }

            foreach (var node in nodesToRemove)
            {
                allNodes.Remove(node);
            }
        }
        while (modified);
    }

    public static Dictionary<MonsterStateNode, HashSet<MonsterStateNode>> GetPrecessorDict(HashSet<MonsterStateNode> allNodes)
    {
        var precessors = new Dictionary<MonsterStateNode, HashSet<MonsterStateNode>>();
        foreach (var node in allNodes)
        {
            if (node.NextState != null)
            {
                if (!precessors.ContainsKey(node.NextState))
                {
                    precessors[node.NextState] = new HashSet<MonsterStateNode>();
                }
                precessors[node.NextState].Add(node);
            }
        }

        foreach (var node in allNodes)
        {
            var root = node;
            while (root.Parent != null)
            {
                root = root.Parent;
            }
            if (root != node && precessors.TryGetValue(root, out var rootPrecessors))
            {
                precessors[node] = rootPrecessors;
            }
        }

        return precessors;
    }

    private static bool TryRemoveNoRepeatNodePrecededBySame(
        MonsterStateNode node,
        Dictionary<MonsterStateNode, HashSet<MonsterStateNode>> allPrecessors,
        List<MonsterStateNode> nodesToRemove,
        MonsterStateNodeConverter converter,
        MoveState moveState,
        RandomBranchState parentBranchState,
        RandomBranchState.StateWeight stateWeight)
    {
        var parent = node.Parent;
        if (stateWeight.repeatType != MoveRepeatType.CannotRepeat && stateWeight.repeatType != MoveRepeatType.UseOnlyOnce)
        {
            return false;
        }

        if (!allPrecessors.ContainsKey(node))
        {
            return false;
        }

        var precessors = allPrecessors[node];
        if (precessors.Any(n => n.Children != null || n.State is not MoveState))
        {
            return false;
        }

        var distinctMoveStateIds = precessors.Select(n => n.State!.Id).Distinct().ToList();
        if (distinctMoveStateIds.Count != 1 || distinctMoveStateIds[0] != moveState.Id)
        {
            return false;
        }

        Debug.Assert(parent?.Children != null, "TryRemoveNoRepeatNodePrecededBySame: parent?.Children != null");

        nodesToRemove.Add(node);
        parent.Children.Remove(node);
        foreach (var (_, v) in allPrecessors)
        {
            v.Remove(node);
        }

        IgLogger.Info($"TryRemoveNoRepeatNodePrecededBySame removed node {node.State?.Id} in {parent.State?.Id}.");
        if (parent.Children.Count == 1)
        {
            var onlyChild = parent.Children[0];
            var parentPrecessors = allPrecessors[parent];
            foreach (var precessor in parentPrecessors)
            {
                if (precessor.NextState == parent)
                {
                    precessor.NextState = onlyChild;
                }
            }
            nodesToRemove.Add(parent);
            onlyChild.Parent = parent.Parent;
            onlyChild.Label = parent.Label;
            onlyChild.IsLabelGenerated = parent.IsLabelGenerated;
            if (onlyChild.NextState == null)
            {
                onlyChild.NextState = parent.NextState;
                onlyChild.NextStateCount = onlyChild.NextState != null ? 1 : 0;
                if (onlyChild.NextState != null)
                {
                    var nextStatePrecessors = allPrecessors[onlyChild.NextState];
                    nextStatePrecessors.Add(onlyChild);
                    nextStatePrecessors.Remove(parent);
                }
            }
        }
        else
        {
            var sumWeight = parentBranchState.States.Where(s => parent.Children.Any(c => c.State?.Id == s.stateId)).Sum(w => w.GetWeight());
            foreach (var child in parent.Children)
            {
                var childStateWeight = parentBranchState.States.FirstOrDefault(s => s.stateId == child.State?.Id);
                if (childStateWeight.stateId == null)
                {
                    continue;
                }
                child.Label = converter.MakeText(childStateWeight, sumWeight);
                child.IsLabelGenerated = true;
            }

            var nextStateOfChildren = parent.Children.Select(c => c.NextState).Distinct().ToList();
            if (nextStateOfChildren.Count == 1)
            {
                foreach (var child in parent.Children)
                {
                    child.NextState = null;
                    child.NextStateCount = 0;
                }
                parent.NextState = nextStateOfChildren[0];
            }

            parent.NextStateCount = (parent.NextState == null ? 0 : 1) + parent.Children.Select(c => c.NextStateCount).DefaultIfEmpty(0).Max();
            parent.CalculateNodeSize();
        }

        return true;
    }

    private static bool TryRemoveNoRepeatTextIfPrecededByOther(
        MonsterStateNode node,
        Dictionary<MonsterStateNode, HashSet<MonsterStateNode>> allPrecessors,
        MonsterStateNodeConverter converter,
        RandomBranchState.StateWeight stateWeight)
    {
        var parent = node.Parent;
        if (stateWeight.repeatType != MoveRepeatType.CannotRepeat)
        {
            return false;
        }

        if (allPrecessors.TryGetValue(node, out var precessors) &&
            precessors.Any(n => n.Children != null || n.State is not MoveState || n.State.Id == node.State?.Id))
        {
            return false;
        }

        if (node.Label?.Contains("≤1") != true)
        {
            return false;
        }

        IgLogger.Info($"TryRemoveNoRepeatTextIfPrecededByOther removed repeat restriction on {node.State?.Id}.");
        node.Label = node.Label?.Replace(", ≤1", string.Empty);
        return true;
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
}
