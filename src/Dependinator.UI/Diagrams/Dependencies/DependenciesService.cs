using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

// The dependency explorer tree that shows a selected node's references and dependencies
// alongside the diagram.
namespace Dependinator.UI.Diagrams.Dependencies;

enum TreeType
{
    References,
    Dependencies,
}

interface IDependenciesService
{
    bool IsShowExplorer { get; }

    TreeType TreeType { get; }
    string Title { get; }
    string Subtitle { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    Task ShowNodeAsync(NodeId nodeId);
    void ToggleExpandAll(TreeItem treeItem);
    Task ShowEditorAsync(NodeId nodeId);
    void ShowDirectLine(NodeId nodeId);
    bool TryGetLine(LineId lineId, out Line line);
    void HideDirectLine(LineId lineId);
    bool CanSplitLine(LineId lineId);
    void SplitLine(LineId lineId);
    bool CanSplitLineSource(LineId lineId);
    void SplitLineSource(LineId lineId);
    void ShowReferences();
    void ShowDependencies();
    void Close();
    void Clicked(PointerId pointerId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelMgr modelMgr,
    INavigationService navigationService
) : IDependenciesService
{
    string selectedId = "";
    TreeType treeType = TreeType.References;

    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];
    public TreeType TreeType => treeType;
    public string Title { get; private set; } = "";
    public string Subtitle { get; private set; } = "";
    public bool IsShowExplorer { get; private set; }

    public void ShowReferences() => Show(TreeType.References);

    public void ShowDependencies() => Show(TreeType.Dependencies);

    public void Clicked(PointerId pointerId)
    {
        if (IsShowExplorer && pointerId.Id != selectedId)
        {
            Close();
        }
    }

    public void ShowDirectLine(NodeId otherNodeId)
    {
        if (!selectionService.SelectedId.IsNode)
            return;

        var thisNodeId = NodeId.FromId(selectionService.SelectedId.Id);
        if (thisNodeId == otherNodeId)
            return;

        var (sourceId, targetId) =
            treeType is TreeType.Dependencies ? (thisNodeId, otherNodeId) : (otherNodeId, thisNodeId);

        using var model = modelMgr.UseModel();

        if (!model.Nodes.TryGetValue(sourceId, out var sourceNode))
            return;
        if (!model.Nodes.TryGetValue(targetId, out var targetNode))
            return;

        var directLineId = LineId.FromDirect(sourceNode.Name, targetNode.Name);
        if (model.Lines.TryGetValue(directLineId, out var existingLine))
            return;

        var ancestor = sourceNode.LowestCommonAncestor(targetNode);
        var directLine = new Line(sourceNode, targetNode, isDirect: true, id: directLineId)
        {
            RenderAncestor = ancestor,
            IsHidden = false,
        };

        ancestor.AddDirectLine(directLine);
        model.TryAddLine(directLine);

        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    public bool TryGetLine(LineId lineId, out Line line)
    {
        using var model = modelMgr.UseModel();
        return model.Lines.TryGetValue(lineId, out line!);
    }

    public void HideDirectLine(LineId lineId)
    {
        var shouldUnselect = selectionService.SelectedId.IsLine && selectionService.SelectedId.Id == lineId.Value;

        using var model = modelMgr.UseModel();

        if (!model.Lines.TryGetValue(lineId, out var line))
            return;

        // Split lines carry their links; detach them so the links do not keep referencing the
        // removed line (plain dialog direct lines have no links, so this is a no-op there).
        foreach (var link in line.Links.ToList())
        {
            line.Remove(link);
            link.RemoveLine(line);
        }

        model.RemoveLine(line);

        applicationEvents.TriggerModelChanged();

        if (shouldUnselect)
            selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
    }

    // Which end of a line a split fans out: Target reveals more targets (fixing the source),
    // Source reveals more sources (fixing the target).
    enum SplitSide
    {
        Source,
        Target,
    }

    public bool CanSplitLine(LineId lineId) => CanSplit(lineId, SplitSide.Target);

    public bool CanSplitLineSource(LineId lineId) => CanSplit(lineId, SplitSide.Source);

    public void SplitLine(LineId lineId) => Split(lineId, SplitSide.Target);

    public void SplitLineSource(LineId lineId) => Split(lineId, SplitSide.Source);

    bool CanSplit(LineId lineId, SplitSide side)
    {
        using var model = modelMgr.UseModel();
        if (!model.Lines.TryGetValue(lineId, out var line))
            return false;
        return GetSplitGroups(line, side, RepLineService.GetRenderedZoom(model)).Count > 0;
    }

    // Splits an aggregated line at one end down to the deepest currently-visible level: for
    // each distinct visible representative descendant of that end which the line's links
    // continue into, a dashed direct-style line from/to that node (with the other end fixed) is
    // shown, carrying those links so it can be split again after zooming in deeper. Splitting
    // reaches the deepest visible nodes in one step — an expanded intermediate container
    // already shows its own structure, so stopping at its edge would add nothing. The original
    // line hides while all its links are represented by split lines; links whose split-side
    // endpoint is the line's own endpoint keep it visible. Split lines are hidden like direct
    // lines and are never persisted.
    void Split(LineId lineId, SplitSide side)
    {
        using var model = modelMgr.UseModel();
        if (!model.Lines.TryGetValue(lineId, out var line))
            return;

        var groups = GetSplitGroups(line, side, RepLineService.GetRenderedZoom(model));
        if (groups.Count == 0)
            return;

        var fixedEnd = side == SplitSide.Target ? line.Source : line.Target;
        var splitEnd = side == SplitSide.Target ? line.Target : line.Source;

        int splitLinkCount = 0;
        foreach (var (rep, links) in groups)
        {
            // A rep container the user never expanded on screen (e.g. resolved via zoom alone)
            // may have unpositioned children; the split line's anchors need real positions.
            EnsureLayout(rep, splitEnd);

            var (splitSource, splitTarget) = side == SplitSide.Target ? (fixedEnd, rep) : (rep, fixedEnd);
            var splitLineId = LineId.FromDirect(splitSource.Name, splitTarget.Name);
            if (!model.Lines.TryGetValue(splitLineId, out var splitLine))
            {
                var ancestor = splitSource.LowestCommonAncestor(splitTarget);
                splitLine = new Line(splitSource, splitTarget, isDirect: true, id: splitLineId)
                {
                    RenderAncestor = ancestor,
                };
                ancestor.AddDirectLine(splitLine);
                model.TryAddLine(splitLine);
            }

            if (splitLine.SplitParent is null)
            {
                splitLine.SplitParent = line;
                line.SplitLines.Add(splitLine);
            }

            foreach (var link in links)
            {
                splitLine.Add(link);
                link.AddLine(splitLine);
                splitLinkCount++;
            }
        }

        line.IsSplitSuppressed = splitLinkCount == line.Links.Count;

        applicationEvents.TriggerModelChanged();
        if (line.IsSplitSuppressed)
            selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
    }

    static Dictionary<Node, List<Link>> GetSplitGroups(Line line, SplitSide side, double zoom)
    {
        var container = side == SplitSide.Target ? line.Target : line.Source;
        Dictionary<Node, List<Link>> groups = [];
        foreach (var link in line.Links)
        {
            var endpoint = side == SplitSide.Target ? link.Target : link.Source;
            if (!TryGetVisibleRep(container, endpoint, zoom, out var rep))
                continue; // The link's endpoint is the line's endpoint itself; nothing deeper
            if (!groups.TryGetValue(rep, out var links))
            {
                links = [];
                groups[rep] = links;
            }
            links.Add(link);
        }
        return groups;
    }

    // The deepest node below container on the path toward node that is still visible at this
    // zoom: descend through expanded containers (which already show their own children),
    // stopping at the first icon/member or at node itself. False when node is not a proper
    // descendant of container.
    static bool TryGetVisibleRep(Node container, Node node, double zoom, out Node rep)
    {
        rep = null!;
        if (!TryGetChildTowardNode(container, node, out var child))
            return false;

        rep = child;
        while (
            rep != node && NodeViewPolicy.IsChildrenShown(rep, zoom) && TryGetChildTowardNode(rep, node, out var next)
        )
        {
            rep = next;
        }
        return true;
    }

    // The child of container on the path down to node; false when node is not a proper
    // descendant of container.
    static bool TryGetChildTowardNode(Node container, Node node, out Node child)
    {
        child = null!;
        for (Node? current = node; current?.Parent is not null; current = current.Parent)
        {
            if (current.Parent == container)
            {
                child = current;
                return true;
            }
        }
        return false;
    }

    // Ensures every container from upToInclusive down to from's parent has its children laid
    // out, so from and its ancestors have real boundaries for anchor calculation.
    static void EnsureLayout(Node from, Node upToInclusive)
    {
        for (Node? node = from.Parent; node is not null; node = node.Parent)
        {
            if (node.IsChildrenLayoutRequired)
                NodeLayout.AdjustChildren(node);
            if (node == upToInclusive)
                break;
        }
    }

    public async Task ShowNodeAsync(NodeId nodeId)
    {
        await navigationService.ShowNodeAsync(nodeId);
    }

    public async Task ShowEditorAsync(NodeId nodeId)
    {
        await navigationService.ShowEditor(nodeId);
    }

    public void ToggleExpandAll(TreeItem treeItem)
    {
        bool shouldExpand = treeItem.GetThisAndDescendants().Any(ti => !ti.Expanded);
        SetExpandedAll(treeItem, shouldExpand);

        applicationEvents.TriggerUIStateChanged();
    }

    // Expands the item before recursing, since expanding an item creates its lazy children,
    // which must happen before they can be visited.
    static void SetExpandedAll(TreeItem treeItem, bool expanded)
    {
        treeItem.Expanded = expanded;
        foreach (var child in (treeItem.Children ?? []).Cast<TreeItem>())
        {
            SetExpandedAll(child, expanded);
        }
    }

    public void Close()
    {
        IsShowExplorer = false;
        selectedId = "";
        applicationEvents.TriggerUIStateChanged();
    }

    void Show(TreeType type)
    {
        treeType = type;
        TreeItems = GetTreeItems(type);

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    IReadOnlyList<TreeItem> GetTreeItems(TreeType treeType)
    {
        selectedId = selectionService.SelectedId.Id;

        using var model = modelMgr.UseModel();

        if (model.Nodes.TryGetValue(NodeId.FromId(selectedId), out var selectedNode))
        {
            Title = selectedNode.ShortName;
            Subtitle = treeType is TreeType.References ? "Nodes that use this node" : "Nodes that this node uses";
            return GetNodeItems(selectedNode, treeType);
        }
        if (model.Lines.TryGetValue(LineId.FromId(selectedId), out var selectedLine))
        {
            Title = $"{selectedLine.Source.ShortName}→{selectedLine.Target.ShortName}";
            Subtitle =
                treeType is TreeType.References
                    ? "Source nodes of this line's links"
                    : "Target nodes of this line's links";
            return GetLineItems(selectedLine, [.. selectedLine.Links], treeType);
        }

        Title = "No items found";
        Subtitle = "";
        return [];
    }

    // Returns one subtree for each line into the node (references) or out of the node
    // (dependencies) that carries at least one link actually ending at the node or a descendant
    // (other links just pass by on their way to some other node).
    static IReadOnlyList<TreeItem> GetNodeItems(Node node, TreeType treeType)
    {
        List<TreeItem> items = [];

        var lines = treeType is TreeType.References ? node.TargetLines : node.SourceLines;
        foreach (var line in lines)
        {
            var isLineForNode = line.Links.Any(link =>
            {
                var endpoint = treeType is TreeType.References ? link.Target : link.Source;
                return endpoint == node || endpoint.Ancestors().Contains(node);
            });
            if (!isLineForNode)
                continue;

            items.AddRange(GetLineItems(line, [.. line.Links], treeType));
        }

        if (!items.Any())
        {
            var text = treeType is TreeType.References ? "No references found" : "No dependencies found";
            items.Add(new TreeItem() { Text = text });
        }
        return items;
    }

    // Returns an item for the node at the far end of the line (the source for references, the
    // target for dependencies). Each tree branch traces one chain of lines that carry the root
    // line's links; the item's children continue that chain from the far node.
    static IReadOnlyList<TreeItem> GetLineItems(Line line, HashSet<Link> rootLinks, TreeType treeType)
    {
        var (farNode, nearNode) =
            treeType is TreeType.References ? (line.Source, line.Target) : (line.Target, line.Source);

        var farLines = treeType is TreeType.References ? farNode.TargetLines : farNode.SourceLines;
        List<Line> nextLines = [.. farLines.Where(l => l.Links.Any(rootLinks.Contains))];

        if (nearNode.Parent == farNode)
        {
            // A line from/to the direct parent adds no information; continue the chain past it.
            return nextLines.SelectMany(l => GetLineItems(l, rootLinks, treeType)).ToList();
        }

        // Children are created lazily on first expand, after the model lock has been released;
        // the captured lines/nodes may be stale if the model has been re-parsed since.
        GetTreeItemChildren? getChildren = nextLines.Any()
            ? () => [.. nextLines.SelectMany(l => GetLineItems(l, rootLinks, treeType))]
            : null;

        var linkCount = line.Links.Count(rootLinks.Contains);
        return [new TreeItem(farNode, linkCount, getChildren)];
    }
}
