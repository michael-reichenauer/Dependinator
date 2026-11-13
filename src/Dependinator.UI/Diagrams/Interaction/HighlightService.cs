using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Interaction;

// Marks the nodes a "go to" took the view to (search, the dependency explorer, a link, a list),
// so the user spots them among their neighbours: amber corner brackets (NodeSvg.HighlightSvg) that pulse
// a couple of times when the view lands, and the nodes' own lines lit like a selected node's
// (LineEmphasis). Unlike selecting, it leaves the user's selection alone and shows no handles or
// toolbar. It lasts until the user's next action in the diagram: a press (a click, a tap or the
// start of a pan), a key, or the next go to. A wheel zoom keeps it, so the user can zoom out for
// context and still see the node.
interface IHighlightService
{
    // While the brackets pulse, just after the view landed (the canvas adds a CSS class).
    bool IsPulsing { get; }

    // The highlighted nodes, empty when nothing is highlighted.
    IReadOnlyList<NodeId> HighlightedIds { get; }

    // The one highlighted node (a single go to), while it is still marked in the open model: the
    // ids outlive a model load, the node's flag does not. Null for none or several.
    NodeId? SingleHighlightedId { get; }

    // Highlights these nodes, replacing any earlier highlight. A single node that is selected is
    // left as it is: its selection already marks it. In a group (a cycle, a violation) every node
    // is marked, so a selected member does not leave the others looking like a single go to,
    // which would light every line of the one left (LineEmphasis.FindSelection).
    void Highlight(IReadOnlyList<NodeId> nodeIds);
    void Clear();
}

[Scoped]
class HighlightService(IModelMgr modelMgr, IApplicationEvents applicationEvents) : IHighlightService
{
    // Two pulses of the highlight animation in Canvas.razor (dep-highlight-pulse).
    static readonly TimeSpan PulseTime = TimeSpan.FromMilliseconds(1500);

    readonly List<NodeId> highlighted = [];
    long pulseVersion;

    public bool IsPulsing { get; private set; }

    public IReadOnlyList<NodeId> HighlightedIds => highlighted;

    public NodeId? SingleHighlightedId =>
        highlighted is [var id] && modelMgr.WithModel(m => m.Nodes.TryGetValue(id, out var n) && n.IsHighlighted)
            ? id
            : null;

    public void Highlight(IReadOnlyList<NodeId> nodeIds)
    {
        using (var model = modelMgr.UseModel())
        {
            ClearFlags(model);
            var ids = nodeIds.Distinct().ToList();
            foreach (var nodeId in ids)
            {
                if (!model.Nodes.TryGetValue(nodeId, out var node) || node.IsRoot)
                    continue;
                if (ids.Count == 1 && node.IsSelected)
                    continue;
                node.IsHighlighted = true;
                highlighted.Add(nodeId);
            }
        }

        IsPulsing = highlighted.Count > 0;
        var version = Interlocked.Increment(ref pulseVersion);
        applicationEvents.TriggerModelChanged(); // The brackets are baked into the tiles
        applicationEvents.TriggerUIStateChanged();
        if (IsPulsing)
            EndPulseAsync(version).RunInBackground();
    }

    public void Clear()
    {
        Interlocked.Increment(ref pulseVersion);
        IsPulsing = false;
        if (highlighted.Count == 0)
            return;

        using (var model = modelMgr.UseModel())
            ClearFlags(model);
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    void ClearFlags(IModel model)
    {
        foreach (var nodeId in highlighted)
        {
            if (model.Nodes.TryGetValue(nodeId, out var node))
                node.IsHighlighted = false;
        }
        highlighted.Clear();
    }

    // Ends the pulse unless a newer highlight (or a clear) came in the meantime.
    async Task EndPulseAsync(long version)
    {
        await Task.Delay(PulseTime);
        if (Interlocked.Read(ref pulseVersion) != version)
            return;
        IsPulsing = false;
        applicationEvents.TriggerUIStateChanged();
    }
}
