using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

interface IInteractionService
{
    string Cursor { get; }
    bool IsContainer { get; }
    bool CanShowSource { get; }
    bool CanShowLineSource { get; }
    bool IsEditNodeMode { get; set; }
    Task InitAsync();
    void NodePanZoomToFit();
    void IncreaseNodeSize();
    void DecreaseNodeSize();
}

// Routes raw pointer events (from IPointerEventService) to the matching gesture: selection,
// canvas pan/zoom, and — in editing mode — node move/resize/content-pan and line-point drags.
[Scoped]
class InteractionService(
    IPointerEventService mouseEventService,
    IPanZoomService panZoomService,
    INodeEditService nodeEditService,
    ILineEditService lineEditService,
    IApplicationEvents applicationEvents,
    ISelectionService selectionService,
    IModelMgr modelMgr,
    IDependenciesService dependenciesService,
    IManualEditService manualEditService,
    INoteService noteService,
    IContextMenuService contextMenuService
) : IInteractionService
{
    // Delay before a held-down button switches the cursor to "move" (see OnMoveTimer).
    const int MoveDelay = 300;

    // The gesture state machine: mouseDownId identifies what the press started on (node,
    // resize handle, line, line point or empty canvas). OnMouseMove then activates at most one
    // drag kind, remembered in the isDragging*/isResizing* flags so OnMouseUp knows what to
    // finish (grid snap + toolbar refresh). All state is reset on mouse-up.
    Timer? moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;
    bool isDraggingSelectedNode = false;
    bool isResizingSelectedNode = false;
    bool isDraggingSelectedLinePoint = false;
    PointerId mouseDownId = PointerId.Empty;
    double Zoom => modelMgr.WithModel(m => m.Zoom);
    readonly Debouncer zoomToolbarDebouncer = new();

    public string Cursor { get; private set; } = "default";
    public bool IsContainer
    {
        get
        {
            if (!selectionService.IsSelected)
                return false;

            using var model = modelMgr.UseModel();
            if (!model.Nodes.TryGetValue(NodeId.FromId(selectionService.SelectedId.Id), out var node))
                return false;
            return NodeViewPolicy.IsContainerView(node, Zoom);
        }
    }

    public bool CanShowSource
    {
        get
        {
            if (!selectionService.IsSelected)
                return false;
            using var model = modelMgr.UseModel();
            if (!model.Nodes.TryGetValue(NodeId.FromId(selectionService.SelectedId.Id), out var node))
                return false;
            return node.FileSpanOrParentSpan is not null;
        }
    }

    // A line has no own source location; "show source" navigates to the line's source node.
    public bool CanShowLineSource
    {
        get
        {
            if (!selectionService.SelectedId.IsLine)
                return false;
            if (!dependenciesService.TryGetLine(LineId.FromId(selectionService.SelectedId.Id), out var line))
                return false;
            return line.Source.FileSpanOrParentSpan is not null;
        }
    }

    public bool IsEditNodeMode
    {
        get
        {
            if (!selectionService.IsEditMode)
                return false;

            using var model = modelMgr.UseModel();
            if (!model.Nodes.TryGetValue(NodeId.FromId(selectionService.SelectedId.Id), out var node))
                return false;
            if (NodeViewPolicy.IsContainerView(node, Zoom))
                return true;

            // No longer in edit mode if the node is too large to be seen or shown as an icon.
            selectionService.SetEditMode(false);
            return false;
        }
        set => selectionService.SetEditMode(value);
    }

    public void NodePanZoomToFit()
    {
        if (!selectionService.IsSelected)
            return;
        nodeEditService.PanZoomToFit(selectionService.SelectedId);
    }

    public void IncreaseNodeSize()
    {
        if (!ViewOptions.IsEditingEnabled)
            return;
        if (!selectionService.IsSelected)
            return;
        var nodeId = NodeId.FromId(selectionService.SelectedId.Id);

        nodeEditService.IncreaseNodeSize(nodeId);
    }

    public void DecreaseNodeSize()
    {
        if (!ViewOptions.IsEditingEnabled)
            return;
        if (!selectionService.IsSelected)
            return;
        var nodeId = NodeId.FromId(selectionService.SelectedId.Id);

        nodeEditService.DecreaseNodeSize(nodeId);
    }

    public Task InitAsync()
    {
        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
        applicationEvents.UndoneRedone += UpdateToolbar;

        mouseEventService.Click += OnClick;
        mouseEventService.DblClick += OnDblClick;
        mouseEventService.PointerMove += OnMouseMove;
        mouseEventService.PointerDown += OnMouseDown;
        mouseEventService.PointerUp += OnMouseUp;
        mouseEventService.Wheel += OnMouseWheel;
        mouseEventService.ContextMenu += OnContextMenu;

        return Task.CompletedTask;
    }

    void OnContextMenu(PointerEvent e)
    {
        // A right-click cancels any in-progress placement gesture instead of opening the menu.
        if (noteService.IsPlacingNote)
        {
            noteService.CancelPlaceNote();
            return;
        }
        if (manualEditService.IsAddingLink)
        {
            manualEditService.CancelAddLink();
            return;
        }
        if (manualEditService.IsPlacingNode)
        {
            manualEditService.CancelPlaceNode();
            return;
        }

        contextMenuService.Open(e);
    }

    void OnMouseWheel(PointerEvent e)
    {
        if (IsEditNodeMode)
        {
            // In edit mode, a wheel gesture on the selected node (or anything inside it) zooms
            // the node's content instead of the canvas.
            var targetId = PointerId.Parse(e.TargetId);
            if (targetId.Id == selectionService.SelectedId.Id || IsInsideSelectedNode(targetId))
            {
                nodeEditService.ZoomSelectedNode(e, selectionService.SelectedId);
                return;
            }
        }

        panZoomService.Zoom(e);
        selectionService.HideSelectedPosition();
        UpdateToolbar();
    }

    // True if the pointer target is a descendant of the currently selected node. In edit mode,
    // gestures on any descendant are treated as gestures on the selected node itself.
    bool IsInsideSelectedNode(PointerId targetId)
    {
        var selectId = NodeId.FromId(selectionService.SelectedId.Id);
        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(NodeId.FromId(targetId.Id), out var node))
            return false;
        return node.Ancestors().Any(n => n.Id == selectId && !n.IsRoot);
    }

    void UpdateToolbar()
    {
        if (selectionService.IsSelected)
        {
            zoomToolbarDebouncer.Debounce(300, () => selectionService.UpdateSelectedPositionAsync());
        }
    }

    void OnClick(PointerEvent e)
    {
        var pointerId = PointerId.Parse(e.TargetId);

        // While placing a note, a click drops it at that position (opens the note dialog).
        if (noteService.IsPlacingNote)
        {
            _ = noteService.PlaceNoteAtAsync(e);
            return;
        }

        // While placing a node, a click begins adding it there (opens the inline name prompt).
        if (manualEditService.IsPlacingNode)
        {
            manualEditService.BeginAddNode(e);
            return;
        }

        // While drawing a manual link, a click picks the target node (or cancels on empty space).
        if (manualEditService.IsAddingLink)
        {
            if (pointerId.IsNode)
                manualEditService.TryCompleteAddLink(pointerId);
            else
                manualEditService.CancelAddLink();
            return;
        }

        dependenciesService.Clicked(pointerId);

        selectionService.Select(pointerId, e).RunInBackground();
    }

    void OnDblClick(PointerEvent e)
    {
        var pointerId = PointerId.Parse(e.TargetId);

        // Double-click on a note opens its edit dialog (allowed regardless of edit mode).
        if (pointerId.IsNode && noteService.IsNoteNode(pointerId.NodeId))
        {
            _ = noteService.EditNoteAsync(pointerId.NodeId);
            return;
        }

        // Double-click on empty canvas (or inside a container) starts adding a manual node there.
        if (!ViewOptions.IsEditingEnabled)
            return;
        manualEditService.BeginAddNode(e);
    }

    void OnMouseDown(PointerEvent e)
    {
        isDraggingSelectedNode = false;
        isResizingSelectedNode = false;
        isDraggingSelectedLinePoint = false;
        moveTimerRunning = true;
        moveTimer?.Change(MoveDelay, Timeout.Infinite);
        mouseDownId = PointerId.Parse(e.TargetId);

        if (mouseDownId.Id == selectionService.SelectedId.Id)
            return;
        if (!IsEditNodeMode)
            return;

        // In edit mode, a press inside the selected node acts on the selected node itself.
        if (!IsInsideSelectedNode(mouseDownId))
            return;
        mouseDownId = selectionService.SelectedId;
    }

    // Routes a left-button drag to exactly one gesture. The branch order is the gesture
    // priority: line-point drag → selected-node content pan (edit mode) → resize-handle drag →
    // selected-node move → canvas pan (the fallback for everything else). Each editing branch
    // marks its flag for OnMouseUp and hides the toolbar while dragging.
    void OnMouseMove(PointerEvent e)
    {
        if (!e.IsLeftButton)
            return;
        if (
            ViewOptions.IsEditingEnabled
            && mouseDownId.IsLinePoint
            && selectionService.SelectedId.IsLine
            && mouseDownId.Id == selectionService.SelectedId.Id
        )
        {
            isDraggingSelectedLinePoint = true;
            lineEditService.MoveSegmentPoint(e, Zoom, mouseDownId);
            selectionService.HideSelectedPosition();
            return;
        }

        if (IsEditNodeMode && mouseDownId.Id == selectionService.SelectedId.Id)
        {
            nodeEditService.PanSelectedNode(e, Zoom, mouseDownId);
            return;
        }

        if (ViewOptions.IsEditingEnabled && mouseDownId != PointerId.Empty && mouseDownId.IsResize)
        {
            isResizingSelectedNode = true;
            nodeEditService.ResizeSelectedNode(e, Zoom, mouseDownId);
            selectionService.HideSelectedPosition();
            return;
        }

        if (
            ViewOptions.IsEditingEnabled
            && mouseDownId == selectionService.SelectedId
            && selectionService.IsSelectedNodeMovable(Zoom)
            && mouseDownId.IsNode
        )
        {
            isDraggingSelectedNode = true;
            nodeEditService.MoveSelectedNode(e, Zoom, mouseDownId);
            selectionService.HideSelectedPosition();
            return;
        }

        panZoomService.Pan(e);
        selectionService.HideSelectedPosition();
    }

    // Finishes the active drag (if any): snaps the dragged item to the grid, refreshes the
    // toolbar position, and resets all gesture state.
    void OnMouseUp(PointerEvent e)
    {
        if (isDraggingSelectedLinePoint && mouseDownId.IsLinePoint)
        {
            lineEditService.SnapSegmentPointToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync().RunInBackground();
            isDraggingSelectedLinePoint = false;
        }

        if (isResizingSelectedNode && mouseDownId.IsResize)
        {
            nodeEditService.SnapResizedSelectedNodeToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync().RunInBackground();
            isResizingSelectedNode = false;
        }

        if (isDraggingSelectedNode && mouseDownId.IsNode)
        {
            nodeEditService.SnapSelectedNodeToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync().RunInBackground();
            isDraggingSelectedNode = false;
        }

        mouseDownId = PointerId.Empty;

        if (moveTimerRunning)
        {
            moveTimerRunning = false;
            moveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (isMoving)
        {
            Cursor = "default";
            isMoving = false;
        }
    }

    // Fires when a button has been held for MoveDelay without release: switches to the "move"
    // cursor to signal that the drag gesture is active.
    void OnMoveTimer(object? state)
    {
        moveTimerRunning = false;
        Cursor = "move";
        isMoving = true;
        applicationEvents.TriggerUIStateChanged();
    }
}
