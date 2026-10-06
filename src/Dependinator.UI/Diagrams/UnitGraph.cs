using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// The dependency graph between "units": every non-member node, where a member counts for the
// type (or other non-member node) that holds it, so a method calling a method is the class
// depending on the class. An edge A→B remembers the links behind it. Built per query from the
// model's links; shared by the path finder and the explorer's indirect dependencies.
sealed class UnitGraph
{
    readonly Dictionary<Node, Dictionary<Node, List<Link>>> forward = [];
    readonly Dictionary<Node, HashSet<Node>> backward = [];

    UnitGraph() { }

    public static UnitGraph Build(IModel model)
    {
        var graph = new UnitGraph();
        foreach (var link in model.Links.Values)
        {
            var source = UnitOf(link.Source);
            var target = UnitOf(link.Target);
            if (source == target)
                continue;
            if (!graph.forward.TryGetValue(source, out var targets))
                graph.forward[source] = targets = [];
            if (!targets.TryGetValue(target, out var links))
                targets[target] = links = [];
            links.Add(link);
            if (!graph.backward.TryGetValue(target, out var sources))
                graph.backward[target] = sources = [];
            sources.Add(source);
        }
        return graph;
    }

    // The units one step away (what the unit uses, or reversed, what uses it), in name order so
    // every result built on top is deterministic.
    public IEnumerable<Node> Next(Node unit, bool isReverse)
    {
        IEnumerable<Node> next;
        if (isReverse)
            next = backward.TryGetValue(unit, out var sources) ? sources : [];
        else
            next = forward.TryGetValue(unit, out var targets) ? targets.Keys : [];
        return next.OrderBy(n => n.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<Link> LinksBetween(Node from, Node to) =>
        forward.TryGetValue(from, out var targets) && targets.TryGetValue(to, out var links) ? links : [];

    // Every unit reachable from (or, reversed, reaching) the units inside the subject, with the
    // number of hops from the subject. Direct dependencies are at 1. The subject's own units are
    // left out.
    public IReadOnlyDictionary<Node, int> Reach(Node subject, bool isReverse)
    {
        var sources = UnitsOf(subject).ToHashSet();
        var distance = new Dictionary<Node, int>();
        var queue = new Queue<Node>();
        foreach (var source in sources)
        {
            distance[source] = 0;
            queue.Enqueue(source);
        }
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var next in Next(node, isReverse))
            {
                if (distance.ContainsKey(next))
                    continue;
                distance[next] = distance[node] + 1;
                queue.Enqueue(next);
            }
        }
        foreach (var source in sources)
            distance.Remove(source);
        return distance;
    }

    // A member counts for the type (or whatever non-member node) that holds it.
    public static Node UnitOf(Node node)
    {
        while (node.Type.IsMember && node.Parent is not null)
            node = node.Parent;
        return node;
    }

    // Every non-member node at or inside the given node (a member stands for its type).
    public static IEnumerable<Node> UnitsOf(Node node) =>
        node.Type.IsMember ? [UnitOf(node)] : node.DescendantsAndSelfPreOrder().Where(n => !n.Type.IsMember);
}
