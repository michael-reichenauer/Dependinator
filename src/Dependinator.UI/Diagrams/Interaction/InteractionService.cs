using Dependinator.Core;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

interface IInteractionService
{
    string Cursor { get; }
    bool IsContainer { get; }
    bool CanShowSource { get; }
    bool CanShowLineSource { get; }
    bool IsEditNodeMode { get; set; }
    Task InitAsync();
    void NodePanZoomToFit();

    // Zooms the view out one container level (to the parent of the container under the view
    // center), or fits the whole diagram when already at the top level.
    void ZoomOutOneLevel();
    void IncreaseNodeSize();
    void DecreaseNodeSize();
}

// Routes raw pointer events (from IPointerEventService) to the matching gesture: selection,
// canvas pan/zoom, area selection, and — in editing mode — node move/resize/content-pan and
// line-point drags.
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
    IContextMenuService contextMenuService,
    IScreenService screenService,
    IAreaSelectionService areaSelectionService,
    IViewOptions viewOptions,
    IKeyboardService keyboardService,
    INavigationService navigationService,
    IViewContextService viewContext,
    IViewHistoryService viewHistory
) : IInteractionService
{
    // Keyboard pan step in screen pixels (Shift multiplies it) and zoom factor per key press.
    const double KeyPanStep = 40;
    const double KeyPanStepShift = 200;
    const double KeyZoomFactor = 1.25;

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
    bool isDraggingLink = false;
    bool isAreaSelecting = false;

    // Swallows the click that follows a completed area-selection drag, so the drag does not
    // also select whatever node the pointer was released on.
    bool suppressNextClick = false;

    // Press position in viewport (client) coords, the coordinate space of the link-drag
    // preview overlay. Client coords are used because pointer capture retargets events to the
    // pressed element, which makes e.OffsetX/Y unreliable during a drag (existing drags only
    // use MovementX/Y).
    Pos mouseDownClient = Pos.None;
    PointerId mouseDownId = PointerId.Empty;
    double Zoom => modelMgr.WithModel(m => m.Zoom);
    readonly Debouncer zoomToolbarDebouncer = new();

    string cursor = "default";
    public string Cursor
    {
        get => IsPlacementMode ? "crosshair" : cursor;
        private set => cursor = value;
    }

    // A mode where the next click/drag places something rather than selects: the cursor says so.
    bool IsPlacementMode =>
        areaSelectionService.IsArmed
        || areaSelectionService.IsSelecting
        || noteService.IsPlacingNote
        || manualEditService.IsPlacingNode;
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

    // Source navigation opens an editor, which only the VS Code host has.
    public bool CanShowSource
    {
        get
        {
            if (!Build.IsVsCodeExtWasm || !selectionService.IsSelected)
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
            if (!Build.IsVsCodeExtWasm || !selectionService.SelectedId.IsLine)
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
        if (!viewOptions.IsEditingEnabled || !selectionService.SelectedId.IsNode)
            return;
        nodeEditService.IncreaseNodeSize(selectionService.SelectedNodeIds);
    }

    public void DecreaseNodeSize()
    {
        if (!viewOptions.IsEditingEnabled || !selectionService.SelectedId.IsNode)
            return;
        nodeEditService.DecreaseNodeSize(selectionService.SelectedNodeIds);
    }

    public async Task InitAsync()
    {
        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
        applicationEvents.UndoneRedone += UpdateToolbar;
        applicationEvents.ViewChanged += OnViewChanged;

        mouseEventService.Click += OnClick;
        mouseEventService.DblClick += OnDblClick;
        mouseEventService.PointerMove += OnMouseMove;
        mouseEventService.PointerDown += OnMouseDown;
        mouseEventService.PointerUp += OnMouseUp;
        mouseEventService.Wheel += OnMouseWheel;
        mouseEventService.ContextMenu += OnContextMenu;
        keyboardService.KeyDown += OnKeyDown;
        await Task.CompletedTask;
    }

    // The canvas keyboard gestures: Escape backs out of whatever is in progress, arrows pan,
    // plus/minus zoom. (Search, undo/redo and fit live in the app bar; Delete in the toolbars.)
    void OnKeyDown(KeyPress key)
    {
        if (key.Is("Escape") && key.IsPlain)
        {
            CancelOrDeselect();
            return;
        }

        if (key.IsAlt && key.Is("ArrowUp"))
        {
            ZoomOutOneLevel();
            return;
        }

        if (key.IsPlain || (key.Shift && !key.Ctrl && !key.Alt))
        {
            var step = key.Shift ? KeyPanStepShift : KeyPanStep;
            switch (key.Key)
            {
                case "ArrowLeft":
                    panZoomService.PanBy(-step, 0);
                    break;
                case "ArrowRight":
                    panZoomService.PanBy(step, 0);
                    break;
                case "ArrowUp":
                    panZoomService.PanBy(0, -step);
                    break;
                case "ArrowDown":
                    panZoomService.PanBy(0, step);
                    break;
                case "+":
                case "=":
                    panZoomService.ZoomBy(KeyZoomFactor);
                    break;
                case "-":
                    panZoomService.ZoomBy(1 / KeyZoomFactor);
                    break;
                default:
                    return;
            }
            selectionService.HideSelectedPosition();
            UpdateToolbar();
            applicationEvents.TriggerUIStateChanged();
            return;
        }

        if (key.Ctrl && !key.Alt && key.Key is "+" or "=" or "-")
        {
            panZoomService.ZoomBy(key.Key == "-" ? 1 / KeyZoomFactor : KeyZoomFactor);
            selectionService.HideSelectedPosition();
            UpdateToolbar();
            applicationEvents.TriggerUIStateChanged();
        }
    }

    // Escape backs out one step at a time: an in-progress placement or drag first, then the
    // per-node arrange mode, then the selection. With nothing to cancel it does nothing, so a
    // reflexive Escape never moves the view (zooming out is Alt+Up).
    void CancelOrDeselect()
    {
        if (contextMenuService.IsOpen)
            contextMenuService.Close();
        else if (areaSelectionService.IsArmed || areaSelectionService.IsSelecting)
        {
            isAreaSelecting = false;
            areaSelectionService.Cancel();
        }
        else if (noteService.IsPlacingNote)
            noteService.CancelPlaceNote();
        else if (manualEditService.IsPlacingNode)
            manualEditService.CancelPlaceNode();
        else if (manualEditService.IsLinkDragActive)
            manualEditService.CancelLinkDrag();
        else if (manualEditService.IsNameEntryOpen)
            manualEditService.CancelNameEntry();
        else if (selectionService.IsEditMode)
            selectionService.SetEditMode(false);
        else if (selectionService.IsSelected)
            selectionService.Unselect();
        else
            return;

        applicationEvents.TriggerUIStateChanged();
    }

    // Framing the innermost open container as a node in its parent is exactly one level out:
    // the user sees where that container sits among its siblings.
    public void ZoomOutOneLevel()
    {
        var chain = viewContext.GetViewCenterChain();
        if (chain.Count >= 1)
        {
            navigationService.ShowNodeAsync(chain[^1].Id).RunInBackground();
            return;
        }

        var bounds = modelMgr.WithModel(m => m.Root.GetTotalBounds());
        if (bounds == Rect.None)
            return;
        viewHistory.RecordJump();
        panZoomService.PanZoomToFit(bounds, Math.Min(1, Zoom));
        applicationEvents.TriggerUIStateChanged();
    }

    void OnContextMenu(PointerEvent e)
    {
        // A right-click cancels any in-progress placement gesture instead of opening the menu.
        if (areaSelectionService.IsArmed || areaSelectionService.IsSelecting)
        {
            isAreaSelecting = false;
            areaSelectionService.Cancel();
            return;
        }
        if (noteService.IsPlacingNote)
        {
            noteService.CancelPlaceNote();
            return;
        }
        if (manualEditService.IsLinkDragActive)
        {
            manualEditService.CancelLinkDrag();
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

    // The toolbar hides while the view moves and re-anchors once it settles.
    void OnViewChanged()
    {
        selectionService.HideSelectedPosition();
        UpdateToolbar();
    }

    void OnClick(PointerEvent e)
    {
        // Ignore clicks while armed for area selection (only a drag is meaningful) and the
        // click that follows a completed selection drag.
        if (areaSelectionService.IsArmed || suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        var pointerId = PointerId.Parse(e.TargetId);

        // While placing a note, a click drops it at that position (opens the note dialog).
        if (noteService.IsPlacingNote)
        {
            _ = noteService.PlaceNoteAtAsync(e);
            return;
        }

        // While placing a node, a click begins adding it there (opens the icon selector dialog).
        if (manualEditService.IsPlacingNode)
        {
            _ = manualEditService.AddNodeAtAsync(e);
            return;
        }

        dependenciesService.Clicked(pointerId);

        // Shift/Ctrl+click builds a group selection; a plain click selects just that item.
        if (pointerId.IsNode && (e.ShiftKey || e.CtrlKey))
            selectionService.ToggleInSelectionAsync(pointerId, e).RunInBackground();
        else
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

        // In edit mode a double-click on empty canvas (or inside an open container) adds a
        // manual node there; otherwise a double-click zooms the view to the node.
        if (viewOptions.IsEditingEnabled && (pointerId.IsCanvas || IsContainerNode(pointerId)))
        {
            _ = manualEditService.AddNodeAtAsync(e);
            return;
        }

        if (pointerId.IsNode)
            navigationService.ShowNodeAsync(pointerId.NodeId).RunInBackground();
    }

    bool IsContainerNode(PointerId pointerId)
    {
        if (!pointerId.IsNode)
            return false;
        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(pointerId.NodeId, out var node))
            return false;
        return node.IsRoot || NodeViewPolicy.IsContainerView(node, Zoom);
    }

    void OnMouseDown(PointerEvent e)
    {
        // A suppressed click (if any) would have fired before this next press; clear a stale flag
        // from a selection drag that was too long to produce a click event at all.
        suppressNextClick = false;
        isDraggingSelectedNode = false;
        isResizingSelectedNode = false;
        isDraggingSelectedLinePoint = false;
        isDraggingLink = false;

        // An armed area selection captures the press; no move timer or edit-mode remapping.
        if (areaSelectionService.IsArmed)
        {
            isAreaSelecting = true;
            areaSelectionService.PointerDown(e);
            return;
        }

        moveTimerRunning = true;
        moveTimer?.Change(MoveDelay, Timeout.Infinite);
        mouseDownId = PointerId.Parse(e.TargetId);
        mouseDownClient = new Pos(e.ClientX, e.ClientY);

        // A link-handle press must stay a link-handle press; the edit-mode remap below would
        // otherwise turn a handle press inside an edit-mode container into a content-pan press.
        if (mouseDownId.IsLinkHandle)
            return;

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
    // priority: link-handle drag → line-point drag → selected-node content pan (edit mode) →
    // resize-handle drag → selected-node move → canvas pan (the fallback for everything else).
    // Each editing branch marks its flag for OnMouseUp and hides the toolbar while dragging.
    void OnMouseMove(PointerEvent e)
    {
        if (!e.IsLeftButton)
            return;

        if (isAreaSelecting)
        {
            areaSelectionService.PointerMove(e);
            return;
        }

        if (viewOptions.IsEditingEnabled && mouseDownId.IsLinkHandle)
        {
            if (!isDraggingLink)
            {
                isDraggingLink = true;
                manualEditService.BeginLinkDrag(mouseDownId.NodeId, mouseDownClient);
                moveTimerRunning = false;
                moveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                Cursor = "crosshair";
                isMoving = true; // Makes OnMouseUp restore the default cursor
            }
            manualEditService.UpdateLinkDrag(new Pos(e.ClientX, e.ClientY));
            selectionService.HideSelectedPosition();
            return;
        }

        if (
            viewOptions.IsEditingEnabled
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

        if (viewOptions.IsEditingEnabled && mouseDownId != PointerId.Empty && mouseDownId.IsResize)
        {
            isResizingSelectedNode = true;
            nodeEditService.ResizeSelectedNode(e, Zoom, mouseDownId);
            selectionService.HideSelectedPosition();
            return;
        }

        // Dragging the selected node (or any node of a group selection) moves the whole selection.
        if (
            viewOptions.IsEditingEnabled
            && mouseDownId.IsNode
            && selectionService.IsSelectedNodeMovable(Zoom)
            && selectionService.SelectedNodeIds.Contains(mouseDownId.NodeId)
        )
        {
            isDraggingSelectedNode = true;
            if (selectionService.SelectedNodeCount > 1)
                nodeEditService.MoveSelectedNodes(e, Zoom, selectionService.SelectedNodeIds);
            else
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
        if (isAreaSelecting)
        {
            isAreaSelecting = false;
            suppressNextClick = true;
            areaSelectionService.PointerUpAsync(e).RunInBackground();
            return;
        }

        if (isDraggingLink)
        {
            isDraggingLink = false;
            CompleteLinkDragAsync(e).RunInBackground();
        }

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
            if (selectionService.SelectedNodeCount > 1)
                nodeEditService.SnapSelectedNodesToGrid(selectionService.SelectedNodeIds);
            else
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
            applicationEvents.TriggerUIStateChanged(); // Restore the cursor without waiting for the next event
        }
    }

    // Finds the drop target under the cursor and completes (or cancels) the link drag. Pointer
    // capture keeps e.TargetId at the pressed handle for the whole drag, so the drop target must
    // be hit-tested at the release position instead.
    async Task CompleteLinkDragAsync(PointerEvent e)
    {
        if (e.Type == "pointercancel")
        {
            manualEditService.CancelLinkDrag();
            applicationEvents.TriggerUIStateChanged();
            return;
        }

        var elementId = await screenService.GetElementIdAtPointAsync(e.ClientX, e.ClientY);
        var canvasPos = await GetCanvasPosAsync(e);

        // Canvas/container drops open the icon selector dialog before completing; the drag
        // state itself is reset synchronously, so refresh the UI right away to hide the
        // preview line while the dialog is open.
        var completion = manualEditService.CompleteLinkDragAsync(PointerId.Parse(elementId), canvasPos);
        applicationEvents.TriggerUIStateChanged();
        await completion;
        applicationEvents.TriggerUIStateChanged();
    }

    // The pointer-up position in canvas (svg-local) pixels; null if the canvas bounds are
    // unknown. e.OffsetX/Y cannot be used: pointer capture makes them relative to the pressed
    // link handle.
    async Task<Pos?> GetCanvasPosAsync(PointerEvent e)
    {
        if (
            await screenService.GetBoundingRectangle(PointerId.CanvasElementId) is not ElementBoundingRectangle svgBound
        )
            return null;
        return new Pos(e.ClientX - svgBound.X, e.ClientY - svgBound.Y);
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
