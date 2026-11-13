using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

interface ISelectionService
{
    bool IsSelected { get; }
    bool IsEditMode { get; }
    PointerId SelectedId { get; }
    Pos SelectedPosition { get; }
    Pos SelectedNodePosition { get; }
    Pos SelectedLinePosition { get; }
    Pos SelectedLineClickPosition { get; }

    // Every selected node: the primary one (SelectedId, which anchors the toolbar) plus the
    // ones added with Shift/Ctrl+click. Group actions (move, hide, color, size, delete) apply
    // to all of them.
    IReadOnlyCollection<NodeId> SelectedNodeIds { get; }
    int SelectedNodeCount { get; }

    Task UpdateSelectedPositionAsync();
    void HideSelectedPosition();
    bool IsSelectedNodeMovable(double zoom);
    NodeVisibility SelectedNodeVisibility();
    bool IsSelectedNodeParentNotVisible();
    Task Select(PointerId pointerId, PointerEvent e);
    Task Select(NodeId nodeId);

    // Shift/Ctrl+click: adds the node to the selection, or removes it again when it already is
    // part of it. With nothing (or a line) selected it is an ordinary select.
    Task ToggleInSelectionAsync(PointerId pointerId, PointerEvent e);

    // Adds nodes (e.g. the ones inside a rubber band) to the selection; the first becomes the
    // primary node when nothing is selected yet.
    Task AddToSelectionAsync(IReadOnlyList<NodeId> nodeIds);
    void SetEditMode(bool isEditMode);
    void Unselect();

    // With one of a stub's lines selected while the stub's lines are being explored, selects the
    // stub again, its lines still drawn (Escape's first step); false when there is no such step.
    bool TrySelectExploredStub();
    void SetSelectedNodesVisibility(NodeVisibility visibility);
}

[Scoped]
class SelectionService(
    IModelMgr modelMgr,
    IApplicationEvents applicationEvents,
    IScreenService screenService,
    ICommandService commandService
) : ISelectionService
{
    // Offset from the clicked point on a line to where its toolbar is anchored.
    const double ToolbarOffsetX = 40;
    const double ToolbarOffsetY = 20;

    // Cover-ratio limits for IsNodeMovable (fractions of the viewport a node may cover).
    const double MinCover = 0.5;
    const double MaxCover = 0.8;

    Pos selectedPosition = Pos.None;
    Pos selectedLineClickPosition = Pos.None;
    double clickedRelativePosition = 0.5;
    PointerId selectedId = PointerId.Empty;
    bool isEditMode = false;

    // The stub whose lines stay drawn while one of them is selected (Line.IsExplored): set when a
    // line drawn for the selected stub is clicked, kept while the user goes from one of its lines
    // to another, and cleared by any other selection.
    // Its relative position is where the stub was clicked, for its toolbar when Escape selects it
    // again. The click's screen position is not kept: after a pan or zoom it would point elsewhere
    // (add-point goes by it), so the stub comes back as if selected without a click spot.
    ExploredStub? exploredStub;

    record ExploredStub(LineId StubId, double RelativePosition);

    // Nodes added to the selection beyond the primary one (insertion order kept).
    readonly List<NodeId> extraSelected = [];

    public PointerId SelectedId => selectedId;
    public bool IsSelected => selectedId != PointerId.Empty;

    public IReadOnlyCollection<NodeId> SelectedNodeIds =>
        selectedId.IsNode ? [selectedId.NodeId, .. extraSelected] : [];

    public int SelectedNodeCount => SelectedNodeIds.Count;

    public bool IsEditMode => isEditMode;

    public Pos SelectedPosition => IsSelected ? selectedPosition : Pos.None;
    public Pos SelectedNodePosition => selectedId.IsNode ? SelectedPosition : Pos.None;
    public Pos SelectedLinePosition => selectedId.IsLine ? SelectedPosition : Pos.None;
    public Pos SelectedLineClickPosition => selectedId.IsLine ? selectedLineClickPosition : Pos.None;

    public void HideSelectedPosition()
    {
        if (!IsSelected)
            return;

        selectedPosition = Pos.None;
        return;
    }

    public async Task UpdateSelectedPositionAsync()
    {
        if (!IsSelected)
            return;

        var id = SelectedId.ElementId;
        if (await screenService.GetBoundingRectangle(id) is not ElementBoundingRectangle bound)
        {
            // Selected Element is not visible on the screen
            if (selectedPosition != Pos.None)
            {
                selectedPosition = Pos.None;
                applicationEvents.TriggerUIStateChanged();
            }
            return;
        }
        (double x, double y) = (bound.X, bound.Y);

        if (selectedId.IsLine)
        {
            // Calculate the clicked relative position on the line, to show the toolbar at the clicked position on the line
            x = bound.X + clickedRelativePosition * bound.Width - ToolbarOffsetX;
            y = bound.Y + clickedRelativePosition * bound.Height - ToolbarOffsetY;

            using (var model = modelMgr.UseModel())
            {
                if (model.Lines.TryGetValue(selectedId.LineId, out var line))
                {
                    // For some lines based int its direction we need to flip the y coordinate
                    if (LinePathGeometry.IsUpHill(line))
                        y = bound.Bottom - clickedRelativePosition * bound.Height - ToolbarOffsetY;
                }
            }
        }

        if (selectedPosition.X == x && selectedPosition.Y == y)
            return;

        selectedPosition = new Pos(x, y);
        applicationEvents.TriggerUIStateChanged();
    }

    public void SetEditMode(bool isEditMode)
    {
        if (!IsSelected)
            return;

        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(selectedId.NodeId, out var node))
                return;
            node.IsEditMode = isEditMode;
        }

        this.isEditMode = isEditMode;
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    public Task Select(NodeId nodeId) => Select(PointerId.FromNode(nodeId), new PointerEvent());

    public async Task AddToSelectionAsync(IReadOnlyList<NodeId> nodeIds)
    {
        if (nodeIds.Count == 0)
            return;
        IEnumerable<NodeId> remaining = nodeIds;
        if (!selectedId.IsNode)
        {
            Unselect(); // A selected line makes way for the nodes
            await Select(nodeIds[0]);
            if (!selectedId.IsNode)
                return;
            remaining = nodeIds.Skip(1);
        }

        using (var model = modelMgr.UseModel())
        {
            foreach (var nodeId in remaining)
            {
                if (nodeId == selectedId.NodeId || extraSelected.Contains(nodeId))
                    continue;
                if (!model.Nodes.TryGetValue(nodeId, out var node) || node.IsRoot)
                    continue;
                extraSelected.Add(nodeId);
                node.IsSelected = true;
                node.IsEditMode = false;
            }
        }
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task ToggleInSelectionAsync(PointerId pointerId, PointerEvent e)
    {
        if (!pointerId.IsNode || !selectedId.IsNode)
        {
            await Select(pointerId, e);
            return;
        }
        if (pointerId.Id == selectedId.Id)
            return; // The primary node stays; Escape or a plain click elsewhere clears the selection

        var nodeId = pointerId.NodeId;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || node.IsRoot)
                return;
            if (extraSelected.Remove(nodeId))
            {
                node.IsSelected = false;
            }
            else
            {
                extraSelected.Add(nodeId);
                node.IsSelected = true;
                node.IsEditMode = false;
            }
        }
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task Select(PointerId pointerId, PointerEvent e)
    {
        if (IsSelected && selectedId.Id == pointerId.Id)
        {
            if (pointerId.IsLine)
                await TrySelectOrRefreshLineAsync(pointerId, e, isNewSelection: false);
            else if (selectedPosition == Pos.None)
            {
                // The node was selected before it was on screen (e.g. selected right after being
                // added, ahead of the render), so its toolbar is not placed yet: place it now.
                await UpdateSelectedPositionAsync();
            }
            return;
        }

        if (pointerId.IsLine)
        {
            // The line replaces the current selection itself, once it has been measured.
            await TrySelectOrRefreshLineAsync(pointerId, e, isNewSelection: true);
            return;
        }

        if (IsSelected)
            Unselect(); // Clicked on some other item or outside the diagram

        if (pointerId.IsNode)
        {
            var isMovable = false;

            using (var model = modelMgr.UseModel())
            {
                if (model.Nodes.TryGetValue(pointerId.NodeId, out var node))
                {
                    // A zone is selectable however much of the screen it covers (it is drawn
                    // around a group and often fills the view); only moving it keeps the limit.
                    if (node.IsZone || IsNodeMovable(node, model.Zoom))
                    {
                        isMovable = true;
                        node.IsSelected = true;
                        node.IsEditMode = false;
                    }
                }
            }
            if (isMovable)
            {
                selectedId = pointerId;
                this.isEditMode = false;
                selectedLineClickPosition = Pos.None;
                applicationEvents.TriggerModelChanged();
                await UpdateSelectedPositionAsync();
            }
        }
    }

    public void Unselect()
    {
        if (!IsSelected)
            return;

        ClearSelection();
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    // Clears the selection's state without telling anyone: the caller raises the events, at
    // once or after it has selected something else.
    void ClearSelection()
    {
        if (selectedId.IsNode)
        {
            using (var model = modelMgr.UseModel())
            {
                if (model.Nodes.TryGetValue(selectedId.NodeId, out var node))
                {
                    node.IsSelected = false;
                    node.IsEditMode = false;
                }
                foreach (var extraId in extraSelected)
                {
                    if (model.Nodes.TryGetValue(extraId, out var extra))
                        extra.IsSelected = false;
                }
            }
        }
        extraSelected.Clear();
        if (selectedId.IsLine || exploredStub is not null)
        {
            using (var model = modelMgr.UseModel())
            {
                if (selectedId.IsLine && model.Lines.TryGetValue(selectedId.LineId, out var line))
                    line.IsSelected = false;
                if (exploredStub is not null && model.Lines.TryGetValue(exploredStub.StubId, out var stub))
                    stub.IsExplored = false;
            }
        }
        exploredStub = null;
        selectedId = PointerId.Empty;
        this.isEditMode = false;
        selectedLineClickPosition = Pos.None;
        // Forget the toolbar position too: UpdateSelectedPositionAsync skips the repaint when
        // the position is unchanged, so re-selecting the same node at the same spot would
        // otherwise select it in the model without ever showing it (no border, no toolbar).
        selectedPosition = Pos.None;
    }

    // A newly selected line replaces the current selection here, not in the caller, and only
    // after it has been measured and without a repaint in between: a selected stub's lines are
    // drawn end to end only while it is selected (RepLineService's LineReveal), so deselecting
    // the stub first takes the clicked line off the screen, and a line that is not on screen
    // cannot be selected. (The browser app repaints before the measuring call returns; there the
    // click only deselected.)
    async Task<bool> TrySelectOrRefreshLineAsync(PointerId pointerId, PointerEvent e, bool isNewSelection)
    {
        if (await screenService.GetBoundingRectangle(pointerId.ElementId) is not ElementBoundingRectangle bound)
        {
            if (isNewSelection)
                Unselect(); // Clicked on a line that is no longer on the screen
            return false;
        }
        var stubToExplore = isNewSelection ? GetStubToExplore(pointerId.LineId) : null;
        var stubRelativePosition =
            exploredStub is { } explored && explored.StubId == stubToExplore
                ? explored.RelativePosition
                : clickedRelativePosition;
        if (isNewSelection)
            ClearSelection();

        var (x1, y1, x2, y2) = (bound.X, bound.Y, bound.Right, bound.Bottom);
        var (x, y) = (e.ClientX, e.ClientY);
        selectedLineClickPosition = new Pos(x, y);

        using (var model = modelMgr.UseModel())
        {
            if (model.Lines.TryGetValue(pointerId.LineId, out var line))
            {
                if (isNewSelection)
                    line.IsSelected = true;
                if (stubToExplore is { } stubId && model.Lines.TryGetValue(stubId, out var stub))
                {
                    stub.IsExplored = true;
                    exploredStub = new ExploredStub(stubId, stubRelativePosition);
                }

                // Calculate the clicked relative position on the line, this is used to
                // show the toolbar at the clicked position on the line
                if (LinePathGeometry.IsUpHill(line))
                    (y1, y2) = (y2, y1);
                if (x1 != x2 || y1 != y2)
                {
                    clickedRelativePosition = LinePathGeometry.ProjectionFactor(
                        new Pos(x, y),
                        new Pos(x1, y1),
                        new Pos(x2, y2)
                    );
                }
            }
        }

        if (isNewSelection)
        {
            selectedId = pointerId;
            this.isEditMode = false;
        }

        applicationEvents.TriggerModelChanged();
        await UpdateSelectedPositionAsync();
        if (isNewSelection)
            applicationEvents.TriggerUIStateChanged(); // Also when the toolbar did not move
        return true;
    }

    // The stub to keep exploring when the clicked line is selected: the stub is selected now or
    // is already being explored, and the clicked line is one of its lines, a line across
    // containers with only the stub's links. (Not Line.IsRevealed: that holds the newest tile's
    // lines, and a view can show several tiles.) Clicking the stub itself selects it as usual.
    // The stub must be drawn as one: a labelled or hand-routed edge line draws no stub lines.
    LineId? GetStubToExplore(LineId clickedId)
    {
        if (!selectedId.IsLine)
            return null;
        var stubId = exploredStub?.StubId ?? selectedId.LineId;
        using var model = modelMgr.UseModel();
        if (
            !model.Lines.TryGetValue(stubId, out var stub)
            || !LineStub.IsEdgeLine(stub)
            || !LineStub.IsStub(stub, isOnPath: false)
            || !model.Lines.TryGetValue(clickedId, out var clicked)
            || !clicked.IsCousin
            || clicked.Links.Count == 0
            || !clicked.Links.All(stub.Contains)
        )
            return null;
        return stubId;
    }

    public bool TrySelectExploredStub()
    {
        if (exploredStub is not { } explored || !selectedId.IsLine || selectedId.LineId == explored.StubId)
            return false;
        using (var model = modelMgr.UseModel())
        {
            // The stub's line can be gone (a refresh), or replaced by one that is not explored.
            if (!model.Lines.TryGetValue(explored.StubId, out var stub) || !stub.IsExplored)
            {
                exploredStub = null;
                return false;
            }
            // In the same lock as the check, so the stub cannot go in between (the lock is
            // reentrant, ClearSelection takes it too).
            ClearSelection();
            stub.IsSelected = true;
        }
        selectedId = PointerId.FromLine(explored.StubId);
        clickedRelativePosition = explored.RelativePosition;
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
        UpdateSelectedPositionAsync().RunInBackground(); // The toolbar follows once the stub is measured
        return true;
    }

    // Returns true if the node is movable, i.e. the node is not too large (too zoomed in) for
    // the current screen.
    public bool IsSelectedNodeMovable(double zoom)
    {
        if (!IsSelected || IsEditMode)
            return false;

        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(selectedId.NodeId, out var node))
            return false;

        if (!node.IsVisible && !node.Parent.IsVisible)
            return false;
        // A zone is drawn around a group and usually fills much of the screen; the user selected
        // it on purpose, so dragging it moves it (and the nodes inside) rather than panning.
        return node.IsZone || IsNodeMovable(node, zoom);
    }

    // The selected node's own state as the user sees it (inherited from a dimmed or hidden
    // ancestor included).
    public NodeVisibility SelectedNodeVisibility()
    {
        if (!IsSelected)
            return NodeVisibility.Visible;

        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(selectedId.NodeId, out var node))
            return NodeVisibility.Visible;
        return node.Visibility;
    }

    // A node inside a dimmed or hidden ancestor follows that ancestor and cannot be changed on
    // its own.
    public bool IsSelectedNodeParentNotVisible()
    {
        if (!IsSelected)
            return false;

        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(selectedId.NodeId, out var node))
            return false;
        return !node.Parent.IsVisible;
    }

    // Dims, hides or shows every selected node as one undoable step like every other edit.
    public void SetSelectedNodesVisibility(NodeVisibility visibility)
    {
        if (!IsSelected)
            return;

        commandService.Do(new SetNodeVisibilityCommand([.. SelectedNodeIds], visibility));
    }

    // A node is movable while it reads as an object on screen rather than as the screen itself:
    // once it covers more than MinCover of the viewport in both directions (or MaxCover in one),
    // the user is effectively "inside" it and dragging should pan the canvas instead.
    private bool IsNodeMovable(Node node, double zoom)
    {
        var v = screenService.SvgRect;
        var nodeZoom = 1 / node.GetZoom();
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }
}
