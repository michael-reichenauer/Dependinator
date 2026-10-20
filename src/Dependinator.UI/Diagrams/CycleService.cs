using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// Name is what the panel shows; NodeName is the node's full name, the key an acceptance stores.
readonly record struct CycleMember(NodeId Id, string Name, string NodeName);

// A set of sibling nodes inside one container that depend on each other in a circle
// (a strongly connected component of the container's child graph). AcceptedBefore is an earlier
// acceptance in the container that the cycle has outgrown (a node joined it since), so the panel
// can say why it is back.
record Cycle(
    NodeId ContainerId,
    string ContainerName,
    string ContainerNodeName,
    IReadOnlyList<CycleMember> Members,
    AcceptedCycle? AcceptedBefore = null
);

// An accepted cycle, or one to fix later, as the panel shows it: display names where the nodes
// still exist (the stored names otherwise), and whether a cycle in the model still matches it;
// false once a refactor broke the cycle, then the entry is only kept for the user to remove.
// Cycle is the matching cycle of one to fix, for its fix prompt.
record AcceptedCycleReport(
    AcceptedCycle Accepted,
    NodeId? ContainerId,
    string ContainerName,
    IReadOnlyList<string> MemberNames,
    bool StillOccurs,
    Cycle? Cycle = null
);

// What the accept-cycle dialog returns: the reason to save, that the user stopped accepting, or
// that the cycle is to be fixed later (the reason is then the planned change).
record AcceptCycleDialogResult(string Reason, bool IsUnaccept, bool IsFixLater = false);

// Finds circular dependencies: for every container, the graph of its children (an edge A→B for
// each link from inside A to inside B) is searched for strongly connected components with at
// least two nodes. Those are the cycles a developer wants to know about at that level: two
// projects that reference each other, namespaces that call back and forth, classes that need
// each other. Member-level cycles (methods of one class calling each other) are left out.
// Cycles the user accepted as intended (with a reason, saved with the model) are reported apart
// and not highlighted. Cycles the user marked to fix later (with the planned change) are reported
// apart too, but still highlighted, since they are still a problem. Results are cached per
// structure and acceptances version.
interface ICycleService
{
    // The cycles that are neither accepted nor marked to fix later.
    IReadOnlyList<Cycle> GetCycles();

    IReadOnlyList<AcceptedCycleReport> GetAcceptedCycles();

    IReadOnlyList<AcceptedCycleReport> GetCyclesToFix();

    // The ids of the sibling lines that are part of a cycle that is not accepted (both
    // directions), for highlighting.
    IReadOnlySet<LineId> GetCyclicLineIds();

    // Accepts a cycle as intended, with why, replacing the earlier entries it covers (the cycle
    // grew since). Like the other changes below it is undoable and saved with the model.
    void Accept(Cycle cycle, string reason);

    // Marks a cycle to fix later, with the planned change, replacing the entries it covers.
    void FixLater(Cycle cycle, string plannedChange);
    void SetReason(AcceptedCycle accepted, string reason);
    void Unaccept(IReadOnlyList<AcceptedCycle> accepted);
}

[Scoped]
class CycleService(IModelMgr modelMgr, ICommandService commandService) : ICycleService
{
    (int Version, int Nodes, int Links, int Accepted) cachedFor = (-1, -1, -1, -1);
    IReadOnlyList<Cycle> cycles = [];
    IReadOnlyList<AcceptedCycleReport> acceptedCycles = [];
    IReadOnlyList<AcceptedCycleReport> cyclesToFix = [];
    IReadOnlySet<LineId> cyclicLineIds = new HashSet<LineId>();

    public IReadOnlyList<Cycle> GetCycles()
    {
        EnsureUpToDate();
        return cycles;
    }

    public IReadOnlyList<AcceptedCycleReport> GetAcceptedCycles()
    {
        EnsureUpToDate();
        return acceptedCycles;
    }

    public IReadOnlyList<AcceptedCycleReport> GetCyclesToFix()
    {
        EnsureUpToDate();
        return cyclesToFix;
    }

    public IReadOnlySet<LineId> GetCyclicLineIds()
    {
        EnsureUpToDate();
        return cyclicLineIds;
    }

    public void Accept(Cycle cycle, string reason) => Decide(cycle, reason, isToFix: false);

    public void FixLater(Cycle cycle, string plannedChange) => Decide(cycle, plannedChange, isToFix: true);

    void Decide(Cycle cycle, string reason, bool isToFix)
    {
        var accepted = new AcceptedCycle(
            cycle.ContainerNodeName,
            [.. cycle.Members.Select(m => m.NodeName)],
            reason.Trim(),
            isToFix
        );
        var covered = modelMgr.WithModel(m =>
            m.AcceptedCycles.Where(a =>
                    a.ContainerName == accepted.ContainerName && a.MemberNames.All(accepted.MemberNames.Contains)
                )
                .ToList()
        );
        commandService.Do(new AcceptedCycleCommand([accepted], covered));
    }

    public void SetReason(AcceptedCycle accepted, string reason)
    {
        var updated = accepted with { Reason = reason.Trim() };
        if (updated == accepted)
            return;
        commandService.Do(new AcceptedCycleCommand([updated], [accepted]));
    }

    public void Unaccept(IReadOnlyList<AcceptedCycle> accepted)
    {
        if (accepted.Count == 0)
            return;
        commandService.Do(new AcceptedCycleCommand([], accepted));
    }

    void EnsureUpToDate()
    {
        using var model = modelMgr.UseModel();
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count, model.AcceptedCyclesVersion);
        if (key == cachedFor)
            return;

        (cycles, acceptedCycles, cyclesToFix, cyclicLineIds) = Compute(model);
        cachedFor = key;
    }

    internal static (
        IReadOnlyList<Cycle> Cycles,
        IReadOnlyList<AcceptedCycleReport> Accepted,
        IReadOnlyList<AcceptedCycleReport> ToFix,
        IReadOnlySet<LineId> LineIds
    ) Compute(IModel model)
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

        var acceptedByContainer = model.AcceptedCycles.ToLookup(a => a.ContainerName, StringComparer.Ordinal);
        var occurring = new HashSet<AcceptedCycle>();
        var toFixCycles = new Dictionary<AcceptedCycle, Cycle>();
        var found = new List<Cycle>();
        var lineIds = new HashSet<LineId>();
        foreach (var (container, graph) in graphs)
        {
            foreach (var component in StronglyConnectedComponents(graph))
            {
                if (component.Count < 2)
                    continue;

                // Decided on while every member is in an entry: a cycle that shrank still is, one
                // that a node joined is not. An accepted one is quiet; one to fix is listed apart
                // but stays highlighted.
                var names = component.Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
                var candidates = acceptedByContainer[container.Name].ToList();
                var matching = candidates.Where(a => names.IsSubsetOf(a.MemberNames)).ToList();
                occurring.UnionWith(matching);
                if (matching.Any(a => !a.IsToFix))
                    continue;

                var members = component.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
                var cycle = new Cycle(
                    container.Id,
                    ContainerLabel(container),
                    container.Name,
                    members.Select(n => new CycleMember(n.Id, n.DisplayName, n.Name)).ToList(),
                    matching.Count == 0
                        ? candidates.FirstOrDefault(a => !a.IsToFix && a.MemberNames.Any(names.Contains))
                        : null
                );
                if (matching.Count == 0)
                    found.Add(cycle);
                foreach (var toFix in matching)
                    toFixCycles[toFix] = cycle;

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

        var reports = model
            .AcceptedCycles.Select(a =>
            {
                model.Nodes.TryGetValue(NodeId.FromName(a.ContainerName), out var container);
                return new AcceptedCycleReport(
                    a,
                    container?.Id,
                    DisplayName(model, a.ContainerName),
                    [.. a.MemberNames.Select(name => DisplayName(model, name)).Order(StringComparer.OrdinalIgnoreCase)],
                    occurring.Contains(a),
                    toFixCycles.GetValueOrDefault(a)
                );
            })
            .OrderBy(r => r.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => string.Join(" ", r.MemberNames), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (
            found,
            [.. reports.Where(r => !r.Accepted.IsToFix)],
            [.. reports.Where(r => r.Accepted.IsToFix)],
            lineIds
        );
    }

    // The root has no name of its own; its children are the model's top-level nodes.
    static string ContainerLabel(Node container) => container.IsRoot ? "Top level" : container.DisplayName;

    // The node's display name, or the short form of a stored name whose node is gone.
    static string DisplayName(IModel model, string nodeName) =>
        model.Nodes.TryGetValue(NodeId.FromName(nodeName), out var node)
            ? ContainerLabel(node)
            : NodeName.GetDisplayNames(nodeName, NodeType.None, false).shortName;

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
