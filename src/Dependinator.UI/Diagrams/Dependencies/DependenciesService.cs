using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

// The dependency explorer tree that shows a selected node's references and dependencies
// alongside the diagram. While its lines are shown, the diagram follows the tree: the subject's
// links are drawn from the subject itself and split into the far-side containers the user
// expands in the tree (Model.LineFocus, resolved by RepLineService).
namespace Dependinator.UI.Diagrams.Dependencies;

enum TreeType
{
    References,
    Dependencies,
}

interface IDependenciesService
{
    bool IsShowExplorer { get; }
    bool IsShowLines { get; }
    bool IsMinimized { get; }

    // Keeps the lines even after another node or line is selected (otherwise a new selection
    // clears lines left behind by a closed explorer).
    bool IsLinesPinned { get; }

    // Also list the nodes the subject reaches (or is reached from) through other nodes, merged
    // into the same containers with their hop counts.
    bool IsIncludeIndirect { get; }

    TreeType TreeType { get; }

    // The subject's short name, and the subtitle sentence around it: the view renders
    // SubtitleBefore + Title + SubtitleAfter with the name highlighted (e.g. "Nodes that Main uses").
    string Title { get; }
    string SubtitleBefore { get; }
    string SubtitleAfter { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    Task ShowNodeAsync(NodeId nodeId);
    void SetExpanded(TreeItem treeItem, bool expanded);
    void ToggleExpandAll(TreeItem treeItem);
    Task ShowEditorAsync(NodeId nodeId);
    void ShowDirectLine(NodeId nodeId);
    bool TryGetLine(LineId lineId, out Line line);
    void HideDirectLine(LineId lineId);
    void ShowReferences();
    void ShowDependencies();
    void SetShowLines(bool isShowLines);
    void SetIncludeIndirect(bool isIncluded);

    // Opens the path finder with the chain between the subject and an indirectly reached node.
    void ShowChain(NodeId nodeId);
    void SetLinesPinned(bool isPinned);
    void SetMinimized(bool isMinimized);
    void Close();
    void Clicked(PointerId pointerId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelMgr modelMgr,
    INavigationService navigationService,
    IScreenService screenService,
    IPathFinderService pathFinderService
) : IDependenciesService
{
    // Below this viewport width (MudBlazor's md breakpoint) the explorer covers most of the
    // diagram, so a click on the diagram folds it down to its title bar.
    const double AutoMinimizeMaxWidth = 960;

    string selectedId = "";
    TreeType treeType = TreeType.References;
    bool isShowLines = true;
    bool isIncludeIndirect;

    // Set by Close: the subject's lines stay in the diagram after the explorer is gone, until
    // the user selects something else (unless pinned) or hides them.
    bool isLinesKeptAfterClose;

    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];
    public TreeType TreeType => treeType;
    public string Title { get; private set; } = "";
    public string SubtitleBefore { get; private set; } = "";
    public string SubtitleAfter { get; private set; } = "";
    public bool IsShowExplorer { get; private set; }
    public bool IsShowLines => isShowLines;
    public bool IsMinimized { get; private set; }
    public bool IsLinesPinned { get; private set; }
    public bool IsIncludeIndirect => isIncludeIndirect;

    public void ShowReferences() => Show(TreeType.References);

    public void ShowDependencies() => Show(TreeType.Dependencies);

    // On a narrow screen a click elsewhere in the diagram folds the explorer down to its title
    // bar instead of closing it (it would otherwise cover the diagram): the user keeps the
    // subject's lines while looking around, and a click on the bar brings the tree back. On a
    // wide screen the explorer stays open beside the diagram. Clicking the subject itself
    // leaves it as is. Lines left behind by a closed explorer go away once another node or
    // line is selected, unless pinned.
    public void Clicked(PointerId pointerId)
    {
        if (IsShowExplorer && !IsMinimized && pointerId.Id != selectedId && IsNarrowViewport)
        {
            SetMinimized(true);
        }

        if (
            !IsShowExplorer
            && isLinesKeptAfterClose
            && !IsLinesPinned
            && (pointerId.IsNode || pointerId.IsLine)
            && pointerId.Id != selectedId
        )
        {
            ClearKeptLines();
        }
    }

    bool IsNarrowViewport => screenService.SvgRect.Width < AutoMinimizeMaxWidth;

    public void SetLinesPinned(bool isPinned)
    {
        if (IsLinesPinned == isPinned)
            return;
        IsLinesPinned = isPinned;
        applicationEvents.TriggerUIStateChanged();
    }

    void ClearKeptLines()
    {
        isLinesKeptAfterClose = false;
        selectedId = "";
        TreeItems = [];
        UpdateFocus();
        applicationEvents.TriggerUIStateChanged();
    }

    public void SetMinimized(bool isMinimized)
    {
        if (!IsShowExplorer || IsMinimized == isMinimized)
            return;
        IsMinimized = isMinimized;
        applicationEvents.TriggerUIStateChanged();
    }

    // Lines in the diagram follow the explorer while on: the subject's links are drawn from the
    // subject and split as tree rows are expanded. Off shows the tree only. The choice is kept
    // for the session, also across explorer sessions.
    public void SetShowLines(bool isShowLines)
    {
        if (this.isShowLines == isShowLines)
            return;
        this.isShowLines = isShowLines;
        if (!isShowLines && !IsShowExplorer)
            isLinesKeptAfterClose = false; // Hiding lines that only lingered after a close drops them for good
        UpdateFocus();
        applicationEvents.TriggerUIStateChanged();
    }

    // The choice is kept for the session; the tree is rebuilt, which folds its rows again.
    public void SetIncludeIndirect(bool isIncluded)
    {
        if (isIncludeIndirect == isIncluded)
            return;
        isIncludeIndirect = isIncluded;
        if (IsShowExplorer)
        {
            TreeItems = GetTreeItems(treeType);
            UpdateFocus();
        }
        applicationEvents.TriggerUIStateChanged();
    }

    public void ShowChain(NodeId nodeId)
    {
        if (!selectionService.SelectedId.IsNode)
            return;
        var subjectId = NodeId.FromId(selectionService.SelectedId.Id);
        var (fromId, toId) = treeType is TreeType.Dependencies ? (subjectId, nodeId) : (nodeId, subjectId);
        pathFinderService.Show(fromId, toId);
    }

    // Expanding a row lists the far container's children in the tree, and splits the line into
    // that container one level in the diagram; collapsing merges it back. The tree view writes
    // the new state into the item before raising the change, so the item is not consulted for
    // whether anything changed; UpdateFocus compares the resulting focus instead.
    public void SetExpanded(TreeItem treeItem, bool expanded)
    {
        treeItem.Expanded = expanded;
        UpdateFocus();
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

        model.RemoveLine(line);

        applicationEvents.TriggerModelChanged();

        if (shouldUnselect)
            selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
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

        UpdateFocus();
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

    // Closing the panel keeps the subject's lines in the diagram (they are what the user
    // opened it for); a later selection or the lines toggle clears them.
    public void Close()
    {
        IsShowExplorer = false;
        IsMinimized = false;
        isLinesKeptAfterClose = isShowLines;
        if (!isLinesKeptAfterClose)
            selectedId = "";
        UpdateFocus();
        applicationEvents.TriggerUIStateChanged();
    }

    void Show(TreeType type)
    {
        treeType = type;
        TreeItems = GetTreeItems(type);

        IsShowExplorer = true;
        IsMinimized = false;
        isLinesKeptAfterClose = false;
        UpdateFocus();
        applicationEvents.TriggerUIStateChanged();
    }

    // Mirrors the explorer state into the model's line focus: the subject and direction shown
    // and the far-side rows currently expanded. Any change re-resolves the lines (the structure
    // version invalidates RepLineService's memo), rebuilds the tiles and repaints the canvas.
    void UpdateFocus()
    {
        using (var model = modelMgr.UseModel())
        {
            var focus = (IsShowExplorer || isLinesKeptAfterClose) && isShowLines ? CreateFocus(model) : null;
            if (focus is null && model.LineFocus is null)
                return;
            if (focus is not null && model.LineFocus is not null && focus.IsSameAs(model.LineFocus))
                return;
            model.LineFocus = focus;
            model.BumpStructureVersion();
        }

        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    LineFocus? CreateFocus(IModel model)
    {
        var isReferences = treeType is TreeType.References;
        LineFocus focus;
        if (model.Nodes.TryGetValue(NodeId.FromId(selectedId), out var node))
            focus = LineFocus.ForNode(node, isReferences);
        else if (model.Lines.TryGetValue(LineId.FromId(selectedId), out var line))
            focus = LineFocus.ForLine(line, isReferences);
        else
            return null;

        foreach (var item in TreeItems.SelectMany(item => item.GetThisAndDescendants()))
        {
            if (!item.Expanded || item.NodeId == NodeId.Empty || item.IsIndirect)
                continue; // No line of the subject leads to an indirect-only row, so nothing to split
            if (model.Nodes.TryGetValue(item.NodeId, out var farNode))
                focus.ExpandedFarNodes.Add(farNode);
        }

        return focus;
    }

    IReadOnlyList<TreeItem> GetTreeItems(TreeType treeType)
    {
        selectedId = selectionService.SelectedId.Id;

        using var model = modelMgr.UseModel();

        if (model.Nodes.TryGetValue(NodeId.FromId(selectedId), out var selectedNode))
        {
            Title = selectedNode.ShortName;
            (SubtitleBefore, SubtitleAfter) =
                treeType is TreeType.References ? ("Nodes that use ", "") : ("Nodes that ", " uses");
            if (isIncludeIndirect)
                SubtitleAfter += " (dimmed: through other nodes, with hop counts)";
            var items = DependencyTree.ForNode(model, selectedNode, treeType, isIncludeIndirect);
            if (items.Count > 0)
                return items;
            var text = treeType is TreeType.References ? "No references found" : "No dependencies found";
            return [new TreeItem() { Text = text }];
        }
        if (model.Lines.TryGetValue(LineId.FromId(selectedId), out var selectedLine))
        {
            Title = $"{selectedLine.Source.ShortName}→{selectedLine.Target.ShortName}";
            SubtitleBefore =
                treeType is TreeType.References ? "Source nodes of the links in " : "Target nodes of the links in ";
            SubtitleAfter = "";
            return DependencyTree.ForLine(selectedLine, treeType);
        }

        Title = "";
        SubtitleBefore = "No items found";
        SubtitleAfter = "";
        return [];
    }
}
