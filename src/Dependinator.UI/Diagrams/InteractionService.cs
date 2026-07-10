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

[Scoped]
class InteractionService : IInteractionService
{
    readonly IPointerEventService mouseEventService;
    readonly IPanZoomService panZoomService;
    readonly INodeEditService nodeEditService;
    readonly ILineEditService lineEditService;
    readonly IApplicationEvents applicationEvents;
    readonly ISelectionService selectionService;
    readonly IModelMgr modelMgr;
    readonly IDependenciesService dependenciesService;
    readonly IManualEditService manualEditService;
    readonly INoteService noteService;
    readonly IContextMenuService contextMenuService;

    const int MoveDelay = 300;

    readonly Timer moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;
    bool isDraggingSelectedNode = false;
    bool isResizingSelectedNode = false;
    bool isDraggingSelectedLinePoint = false;
    PointerId mouseDownId = PointerId.Empty;
    double Zoom => modelMgr.WithModel(m => m.Zoom);
    readonly Debouncer zoomToolbarDebouncer = new();

    public InteractionService(
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
    )
    {
        this.mouseEventService = mouseEventService;
        this.panZoomService = panZoomService;
        this.nodeEditService = nodeEditService;
        this.lineEditService = lineEditService;
        this.applicationEvents = applicationEvents;
        this.selectionService = selectionService;
        this.modelMgr = modelMgr;
        this.dependenciesService = dependenciesService;
        this.manualEditService = manualEditService;
        this.noteService = noteService;
        this.contextMenuService = contextMenuService;
        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.applicationEvents.UndoneRedone += UpdateToolbar;
    }

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
            // No longer in edit mode if node is to large to be seen or has an icon

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

        selectionService.Select(pointerId, e);
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
        moveTimer.Change(MoveDelay, Timeout.Infinite);
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

    void OnMouseUp(PointerEvent e)
    {
        if (isDraggingSelectedLinePoint && mouseDownId.IsLinePoint)
        {
            lineEditService.SnapSegmentPointToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync();
            isDraggingSelectedLinePoint = false;
        }

        if (isResizingSelectedNode && mouseDownId.IsResize)
        {
            nodeEditService.SnapResizedSelectedNodeToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync();
            isResizingSelectedNode = false;
        }

        if (isDraggingSelectedNode && mouseDownId.IsNode)
        {
            nodeEditService.SnapSelectedNodeToGrid(mouseDownId);
            selectionService.UpdateSelectedPositionAsync();
            isDraggingSelectedNode = false;
        }

        mouseDownId = PointerId.Empty;

        if (moveTimerRunning)
        {
            moveTimerRunning = false;
            moveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (isMoving)
        {
            Cursor = "default";
            isMoving = false;
        }
    }

    void OnMoveTimer(object? state)
    {
        moveTimerRunning = false;
        Cursor = "move";
        isMoving = true;
        applicationEvents.TriggerUIStateChanged();
    }
}
