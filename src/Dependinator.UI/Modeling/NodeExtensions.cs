using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

static class NodeExtensions
{
    public static IEnumerable<Node> Ancestors(this Node node)
    {
        while (node.Parent != null)
        {
            yield return node.Parent;
            node = node.Parent;
        }
    }

    public static IEnumerable<Node> AncestorsAndSelf(this Node node)
    {
        yield return node;
        foreach (var ancestor in node.Ancestors())
        {
            yield return ancestor;
        }
    }

    // The node and all its descendants in post-order (children before their parent), so callers
    // that remove nodes handle leaves before the containers that hold them.
    public static IEnumerable<Node> DescendantsAndSelfPostOrder(this Node node)
    {
        foreach (var child in node.Children)
        {
            foreach (var descendant in child.DescendantsAndSelfPostOrder())
                yield return descendant;
        }
        yield return node;
    }

    // The node and all its descendants in pre-order (parents before their children), so callers
    // that rebuild nodes create each parent before the children attached to it.
    public static IEnumerable<Node> DescendantsAndSelfPreOrder(this Node node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in child.DescendantsAndSelfPreOrder())
                yield return descendant;
        }
    }

    public static Node LowestCommonAncestor(this Node first, Node second)
    {
        if (first == second)
            return first;

        var ancestors = new HashSet<Node>(first.AncestorsAndSelf());
        foreach (var candidate in second.AncestorsAndSelf())
        {
            if (ancestors.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"Node {first} and {second} do not have a common ancestor");
    }
}
