using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

readonly record struct CycleMember(NodeId Id, string Name);

// A set of sibling nodes inside one container that depend on each other in a circle
// (a strongly connected component of the container's child graph).
record Cycle(NodeId ContainerId, string ContainerName, IReadOnlyList<CycleMember> Members);

// Finds circular dependencies: for every container, the graph of its children (an edge A→B for
// each link from inside A to inside B) is searched for strongly connected components with at
// least two nodes. Those are the cycles a developer wants to know about at that level: two
// projects that reference each other, namespaces that call back and forth, classes that need
// each other. Member-level cycles (methods of one class calling each other) are left out.
// Results are cached per structure version.
interface ICycleService
{
    IReadOnlyList<Cycle> GetCycles();

    // The ids of the sibling lines that are part of a cycle (both directions), for highlighting.
    IReadOnlySet<LineId> GetCyclicLineIds();
}

[Scoped]
class CycleService(IModelMgr modelMgr) : ICycleService
{
    (int Version, int Nodes, int Links) cachedFor = (-1, -1, -1);
    IReadOnlyList<Cycle> cycles = [];
    IReadOnlySet<LineId> cyclicLineIds = new HashSet<LineId>();

    public IReadOnlyList<Cycle> GetCycles()
    {
        EnsureUpToDate();
        return cycles;
    }

    public IReadOnlySet<LineId> GetCyclicLineIds()
    {
        EnsureUpToDate();
        return cyclicLineIds;
    }

    void EnsureUpToDate()
    {
        using var model = modelMgr.UseModel();
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count);
        if (key == cachedFor)
            return;

        (cycles, cyclicLineIds) = Compute(model);
        cachedFor = key;
    }

    internal static (IReadOnlyList<Cycle> Cycles, IReadOnlySet<LineId> LineIds) Compute(IModel model)
    {
        // Per container: the child graph, from the links whose endpoints sit in different children.
        var graphs = new Dictionary<Node, Dictionary<Node, HashSet<Node>>>();
        foreach (var link in model.Links.Values)
        {
            if (!TryGetSiblingsUnderCommonAncestor(link.Source, link.Target, out var from, out var to))
                continue;
            // Methods of one class calling each other is recursion, not an architecture cycle.
            if (from.Type.IsMember || to.Type.IsMember)
                continue;
            if (!graphs.TryGetValue(from.Parent, out var graph))
                graphs[from.Parent] = graph = [];
            if (!graph.TryGetValue(from, out var targets))
                graph[from] = targets = [];
            targets.Add(to);
        }

        var found = new List<Cycle>();
        var lineIds = new HashSet<LineId>();
        foreach (var (container, graph) in graphs)
        {
            foreach (var component in StronglyConnectedComponents(graph))
            {
                if (component.Count < 2)
                    continue;

                var members = component.OrderBy(n => n.ShortName, StringComparer.OrdinalIgnoreCase).ToList();
                found.Add(
                    new Cycle(
                        container.Id,
                        container.ShortName,
                        members.Select(n => new CycleMember(n.Id, n.ShortName)).ToList()
                    )
                );

                var inComponent = component.ToHashSet();
                foreach (var from in component)
                {
                    foreach (var to in graph.TryGetValue(from, out var targets) ? targets : [])
                    {
                        if (!inComponent.Contains(to))
                            continue;
                        lineIds.Add(LineId.From(from.Name, to.Name));
                        lineIds.Add(LineId.FromInheritance(from.Name, to.Name));
                    }
                }
            }
        }

        found.Sort((a, b) => string.Compare(a.ContainerName, b.ContainerName, StringComparison.OrdinalIgnoreCase));
        return (found, lineIds);
    }

    // The two children of the closest common ancestor that contain the endpoints, or false
    // when one endpoint contains the other (no sibling pair, so no cycle at any level).
    static bool TryGetSiblingsUnderCommonAncestor(Node source, Node target, out Node from, out Node to)
    {
        from = to = null!;
        var sourceChain = source.AncestorsAndSelf().Reverse().ToList();
        var targetChain = target.AncestorsAndSelf().Reverse().ToList();
        var depth = 0;
        while (depth < sourceChain.Count && depth < targetChain.Count && sourceChain[depth] == targetChain[depth])
            depth++;
        if (depth >= sourceChain.Count || depth >= targetChain.Count)
            return false;
        from = sourceChain[depth];
        to = targetChain[depth];
        return true;
    }

    // Tarjan's algorithm, iterative per root node to keep deep graphs off the call stack.
    static List<List<Node>> StronglyConnectedComponents(Dictionary<Node, HashSet<Node>> graph)
    {
        var index = 0;
        var indices = new Dictionary<Node, int>();
        var lowLinks = new Dictionary<Node, int>();
        var onStack = new HashSet<Node>();
        var stack = new Stack<Node>();
        var components = new List<List<Node>>();

        foreach (var root in graph.Keys)
        {
            if (indices.ContainsKey(root))
                continue;

            var work = new Stack<(Node Node, IEnumerator<Node> Edges)>();
            Visit(root);
            while (work.Count > 0)
            {
                var (node, edges) = work.Peek();
                if (edges.MoveNext())
                {
                    var next = edges.Current;
                    if (!indices.ContainsKey(next))
                        Visit(next);
                    else if (onStack.Contains(next))
                        lowLinks[node] = Math.Min(lowLinks[node], indices[next]);
                    continue;
                }

                work.Pop();
                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    lowLinks[parent] = Math.Min(lowLinks[parent], lowLinks[node]);
                }
                if (lowLinks[node] != indices[node])
                    continue;

                var component = new List<Node>();
                Node popped;
                do
                {
                    popped = stack.Pop();
                    onStack.Remove(popped);
                    component.Add(popped);
                } while (popped != node);
                components.Add(component);
            }

            void Visit(Node node)
            {
                indices[node] = lowLinks[node] = index++;
                stack.Push(node);
                onStack.Add(node);
                var edges = graph.TryGetValue(node, out var targets) ? targets : [];
                work.Push((node, edges.GetEnumerator()));
            }
        }

        return components;
    }
}
