using System.Collections.Generic;

namespace Dependinator.Models;

static class NodeExtensions
{
    public static IEnumerable<Node> AncestorsAndSelf(this Node node)
    {
        yield return node;
        foreach (var ancestor in node.Ancestors())
        {
            yield return ancestor;
        }
    }

    public static Node? LowestCommonAncestor(this Node first, Node second)
    {
        if (first == second)
            return first;

        var ancestors = new HashSet<Node>(first.AncestorsAndSelf());
        foreach (var candidate in second.AncestorsAndSelf())
        {
            if (ancestors.Contains(candidate))
                return candidate;
        }

        return null;
    }
}
