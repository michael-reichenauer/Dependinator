using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Diagrams;

readonly record struct PathHop(NodeId Id, string Name, string LongName);

// One chain of dependencies from the "from" node to the "to" node: the first hop lies inside
// "from", the last inside "to", and every consecutive pair is a direct link in the model.
// LineIds are the diagram lines that carry those links (at every zoom level), for highlighting.
record DependencyPath(IReadOnlyList<PathHop> Hops, IReadOnlySet<LineId> LineIds)
{
    public int HopCount => Hops.Count - 1;
}

enum PathStatus
{
    Incomplete, // "from" or "to" is not chosen (or no longer in the model)
    Nested, // one endpoint contains the other, so "depends on" has no meaning between them
    NotFound,
    Found,
}

record PathResult(PathStatus Status, IReadOnlyList<DependencyPath> Paths)
{
    public static readonly PathResult Incomplete = new(PathStatus.Incomplete, []);
}

// Answers "why does A depend on B?": the shortest chains of dependencies from one node to
// another. Members count for their type (a method calling a method is the class depending on the
// class), so hops are types, namespaces or projects, never members. A container endpoint stands
// for everything inside it: a path from a project starts at whichever of its types first reaches
// out. All shortest chains are found (capped), and the selected one is drawn in the diagram with
// every other line faded. The panel (PathPanel.razor) shows the state; the menus feed it.
interface IPathFinderService
{
    bool IsOpen { get; }
    NodeId? FromId { get; }
    NodeId? ToId { get; }
    string FromName { get; }
    string ToName { get; }
    int SelectedIndex { get; }

    event Action? Changed;

    // Raised when "to" still has to be chosen after the user asked for a path from a node; the
    // panel reacts by opening the node picker (a service does not open dialogs itself).
    event Action? PickToRequested;

    PathResult GetResult();

    // The lines of the selected path, or null when no path is shown (nothing to highlight).
    IReadOnlySet<LineId>? GetPathLineIds();

    void Open();
    void OpenFrom(NodeId fromId);

    // Opens the panel with both ends chosen (e.g. from an explorer row).
    void Show(NodeId fromId, NodeId toId);
    void SetFrom(NodeId id);
    void SetTo(NodeId id);
    void Swap();
    void SelectPath(int index);
    void Close();
}

[Scoped]
class PathFinderService(
    IModelMgr modelMgr,
    IModelService modelService,
    IApplicationEvents applicationEvents,
    IViewOptions viewOptions
) : IPathFinderService
{
    // More than this many equally short chains is noise; the first ones (by name) are kept.
    internal const int MaxPaths = 10;

    (int Version, int Nodes, int Links, NodeId? From, NodeId? To) cachedFor = (-1, -1, -1, null, null);
    PathResult result = PathResult.Incomplete;

    public bool IsOpen { get; private set; }
    public NodeId? FromId { get; private set; }
    public NodeId? ToId { get; private set; }
    public int SelectedIndex { get; private set; }

    public event Action? Changed;
    public event Action? PickToRequested;

    public string FromName => NameOf(FromId);
    public string ToName => NameOf(ToId);

    public PathResult GetResult()
    {
        EnsureUpToDate();
        return result;
    }

    public IReadOnlySet<LineId>? GetPathLineIds()
    {
        if (!IsOpen)
            return null;
        EnsureUpToDate();
        if (result.Status != PathStatus.Found)
            return null;
        return result.Paths[Math.Min(SelectedIndex, result.Paths.Count - 1)].LineIds;
    }

    public void Open()
    {
        IsOpen = true;
        viewOptions.SetCyclesShown(false); // The analysis panels sit top-right and all recolor lines
        viewOptions.SetRulesShown(false);
        Notify();
    }

    public void OpenFrom(NodeId fromId)
    {
        FromId = fromId;
        if (ToId == fromId)
            ToId = null;
        SelectedIndex = 0;
        Open();
        if (ToId is null)
            PickToRequested?.Invoke();
    }

    public void Show(NodeId fromId, NodeId toId)
    {
        FromId = fromId;
        ToId = toId == fromId ? null : toId;
        SelectedIndex = 0;
        Open();
    }

    public void SetFrom(NodeId id)
    {
        FromId = id;
        if (ToId == id)
            ToId = null;
        SelectedIndex = 0;
        Notify();
    }

    public void SetTo(NodeId id)
    {
        ToId = id;
        if (FromId == id)
            FromId = null;
        SelectedIndex = 0;
        Notify();
    }

    public void Swap()
    {
        (FromId, ToId) = (ToId, FromId);
        SelectedIndex = 0;
        Notify();
    }

    public void SelectPath(int index)
    {
        SelectedIndex = Math.Max(0, index);
        Notify();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        Notify();
    }

    // The highlighted lines are baked into the tiles, so every change redraws the diagram.
    void Notify()
    {
        Changed?.Invoke();
        modelService.ClearCache();
        applicationEvents.TriggerUIStateChanged();
    }

    string NameOf(NodeId? id)
    {
        if (id is null)
            return "";
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(id, out var node) ? node.ShortName : "";
    }

    void EnsureUpToDate()
    {
        using var model = modelMgr.UseModel();
        var key = (model.StructureVersion, model.Nodes.Count, model.Links.Count, FromId, ToId);
        if (key == cachedFor)
            return;

        result =
            FromId is not null
            && ToId is not null
            && model.Nodes.TryGetValue(FromId, out var from)
            && model.Nodes.TryGetValue(ToId, out var to)
                ? Compute(model, from, to)
                : PathResult.Incomplete;
        cachedFor = key;
    }

    internal static PathResult Compute(IModel model, Node from, Node to, int maxPaths = MaxPaths)
    {
        if (from == to || from.Ancestors().Contains(to) || to.Ancestors().Contains(from))
            return new PathResult(PathStatus.Nested, []);

        var graph = UnitGraph.Build(model);
        var sources = UnitGraph.UnitsOf(from).ToHashSet();
        var targetUnits = UnitGraph.UnitsOf(to).ToHashSet();

        // Breadth first from everything inside "from", keeping every predecessor on a shortest
        // route, until the first layer that reaches inside "to" is complete.
        var distance = new Dictionary<Node, int>();
        var predecessors = new Dictionary<Node, List<Node>>();
        var queue = new Queue<Node>();
        foreach (var source in sources.OrderBy(n => n.Name, StringComparer.Ordinal))
        {
            distance[source] = 0;
            queue.Enqueue(source);
        }

        int? foundAt = null;
        var reached = new List<Node>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (foundAt is { } layer && distance[node] >= layer)
                break;
            if (targetUnits.Contains(node))
            {
                foundAt = distance[node];
                reached.Add(node);
                continue;
            }
            foreach (var neighbor in graph.Next(node, isReverse: false))
            {
                if (!distance.TryGetValue(neighbor, out var known))
                {
                    distance[neighbor] = distance[node] + 1;
                    predecessors[neighbor] = [node];
                    queue.Enqueue(neighbor);
                }
                else if (known == distance[node] + 1)
                {
                    predecessors[neighbor].Add(node);
                }
            }
        }

        if (foundAt is null)
            return new PathResult(PathStatus.NotFound, []);

        var paths = new List<DependencyPath>();
        foreach (var target in reached.OrderBy(n => n.Name, StringComparer.Ordinal))
        {
            Walk(target, [target]);
        }
        return new PathResult(PathStatus.Found, paths);

        void Walk(Node node, List<Node> chainFromEnd)
        {
            if (paths.Count >= maxPaths)
                return;
            if (distance[node] == 0)
            {
                var chain = Enumerable.Reverse(chainFromEnd).ToList();
                paths.Add(new DependencyPath(chain.Select(ToHop).ToList(), LineIdsOf(chain)));
                return;
            }
            foreach (var previous in predecessors[node].OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                Walk(previous, [.. chainFromEnd, previous]);
            }
        }

        IReadOnlySet<LineId> LineIdsOf(IReadOnlyList<Node> chain)
        {
            var lineIds = new HashSet<LineId>();
            for (var i = 0; i + 1 < chain.Count; i++)
            {
                foreach (var link in graph.LinksBetween(chain[i], chain[i + 1]))
                {
                    foreach (var line in link.Lines)
                        lineIds.Add(line.Id);
                }
            }
            return lineIds;
        }
    }

    static PathHop ToHop(Node node) => new(node.Id, node.ShortName, node.LongName);
}
