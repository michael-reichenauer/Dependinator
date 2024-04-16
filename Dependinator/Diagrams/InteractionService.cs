using Dependinator.Models;

namespace Dependinator.Diagrams;

interface IInteractionService
{
    string Cursor { get; }
    bool IsShowNodeToolbar { get; }
    bool IsEditNodeMode { get; set; }
    Pos SelectedNodePosition { get; }

    Task InitAsync();
    void NodePanZoomToFit();
}


[Scoped]
class InteractionService : IInteractionService
{
    readonly IPointerEventService mouseEventService;
    readonly IPanZoomService panZoomService;
    readonly INodeEditService nodeEditService;
    readonly IApplicationEvents applicationEvents;
    readonly ISelectionService selectionService;
    readonly IScreenService screenService;
    readonly IModelService modelService;

    const int MoveDelay = 300;

    readonly Timer moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;
    PointerId mouseDownId = PointerId.Empty;
    double Zoom => modelService.Zoom;
    readonly Debouncer zoomToolbarDebouncer = new();


    public InteractionService(
        IPointerEventService mouseEventService,
        IPanZoomService panZoomService,
        INodeEditService nodeEditService,
        IApplicationEvents applicationEvents,
        ISelectionService selectionService,
        IScreenService screenService,
        IModelService modelService)
    {
        this.mouseEventService = mouseEventService;
        this.panZoomService = panZoomService;
        this.nodeEditService = nodeEditService;
        this.applicationEvents = applicationEvents;
        this.selectionService = selectionService;
        this.screenService = screenService;
        this.modelService = modelService;

        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.applicationEvents.UndoneRedone += UpdateToolbar;
    }


    public string Cursor { get; private set; } = "default";
    public bool IsShowNodeToolbar
    {
        get
        {
            if (!selectionService.IsSelected) return false;
            if (!modelService.TryGetNode(selectionService.SelectedId.Id, out var node)) return false;
            var nodeZoom = 1 / (node.GetZoom() * Zoom);
            return !Node.IsToLargeToBeSeen(nodeZoom) && !node.IsShowIcon(nodeZoom);
        }
    }


    public Pos SelectedNodePosition { get; set; } = Pos.None;

    public bool IsEditNodeMode
    {
        get
        {
            if (!selectionService.IsEditMode) return false;
            if (!modelService.TryGetNode(selectionService.SelectedId.Id, out var node)) return false;
            var nodeZoom = 1 / (node.GetZoom() * Zoom);
            if (!Node.IsToLargeToBeSeen(nodeZoom) && !node.IsShowIcon(nodeZoom))
            {   // No longer in edit mode if node is to large to be seen or has an icon
                return true;
            }

            selectionService.SetEditMode(false);
            return false;
        }

        set => selectionService.SetEditMode(value);
    }

    public void NodePanZoomToFit()
    {
        if (!selectionService.IsSelected) return;
        nodeEditService.PanZoomToFit(selectionService.SelectedId);
    }

    public Task InitAsync()
    {
        mouseEventService.Click += OnClick;
        mouseEventService.DblClick += OnDblClick;
        mouseEventService.PointerMove += OnMouseMove;
        mouseEventService.PointerDown += OnMouseDown;
        mouseEventService.PointerUp += OnMouseUp;
        mouseEventService.Wheel += OnMouseWheel;

        return Task.CompletedTask;
    }


    void OnMouseWheel(PointerEvent e)
    {
        if (IsEditNodeMode)
        {
            // Node is in edit mode
            var targetId = PointerId.Parse(e.TargetId);
            if (targetId.Id != selectionService.SelectedId.Id)
            {
                // Node is in edit mode, check if mouse down is inside the selected node, if so trate as selected node
                var selectId = NodeId.FromId(selectionService.SelectedId.Id);

                using (var model = modelService.UseModel())
                {
                    if (model.TryGetNode(targetId.Id, out var node))
                    {
                        var ancestor = node.Ancestors().FirstOrDefault(n => n.Id == selectId);
                        if (ancestor != null)
                        {
                            targetId = selectionService.SelectedId;
                        }
                    }
                }
            }
            if (targetId.Id == selectionService.SelectedId.Id)
            {
                nodeEditService.ZoomSelectedNode(e, selectionService.SelectedId);
                return;
            }
        }

        panZoomService.Zoom(e);
        UpdateToolbar();
    }

    void UpdateToolbar()
    {
        if (selectionService.IsSelected)
        {
            zoomToolbarDebouncer.Debounce(20, async () =>
                {
                    if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(selectionService.SelectedId.Id))) return;
                    SelectedNodePosition = new Pos(bound.X, bound.Y);
                    applicationEvents.TriggerUIStateChanged();
                });
        }
    }


    async void OnClick(PointerEvent e)
    {
        Log.Info("mouse click", e.TargetId);
        var targetId = PointerId.Parse(e.TargetId);

        selectionService.Select(targetId);
        if (selectionService.IsSelected)
        {
            if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(targetId.Id))) return;
            SelectedNodePosition = new Pos(bound.X, bound.Y);
            applicationEvents.TriggerUIStateChanged();
        }
    }

    void OnDblClick(PointerEvent e)
    {
        Log.Info($"OnDoubleClick {e.Type}");
    }


    void OnMouseDown(PointerEvent e)
    {
        moveTimerRunning = true;
        moveTimer.Change(MoveDelay, Timeout.Infinite);
        mouseDownId = PointerId.Parse(e.TargetId);

        if (mouseDownId.Id == selectionService.SelectedId.Id) return;
        if (!IsEditNodeMode) return;

        // Node is in edit mode, check if mouse down is inside the selected node, if so trate as selected node
        var downId = NodeId.FromId(mouseDownId.Id);
        var selectId = NodeId.FromId(selectionService.SelectedId.Id);

        using var model = modelService.UseModel();
        if (!model.TryGetNode(downId, out var node)) return;
        var ancestorId = node.Ancestors().FirstOrDefault(n => n.Id == selectId && !n.IsRoot);
        if (ancestorId == null) return;
        mouseDownId = selectionService.SelectedId;
    }


    void OnMouseMove(PointerEvent e)
    {
        if (!e.IsLeftButton) return;
        if (IsEditNodeMode && mouseDownId.Id == selectionService.SelectedId.Id)
        {
            nodeEditService.PanSelectedNode(e, Zoom, mouseDownId);
            return;
        }

        if (mouseDownId != PointerId.Empty && mouseDownId.IsResize)
        {
            nodeEditService.ResizeSelectedNode(e, Zoom, mouseDownId);
            ResizedMoveToolbar(e);
            return;
        }

        if (mouseDownId == selectionService.SelectedId && selectionService.IsNodeMovable(Zoom) && mouseDownId.IsNode)
        {
            nodeEditService.MoveSelectedNode(e, Zoom, mouseDownId);
            PanedMoveToolbar(e);
            return;
        }

        panZoomService.Pan(e);
        PanedMoveToolbar(e);
    }

    void ResizedMoveToolbar(PointerEvent e)
    {
        var (dx, dy) = mouseDownId.SubId switch
        {
            "tl" => (e.MovementX, e.MovementY),
            "tm" => (0, e.MovementY),
            "tr" => (0, e.MovementY),
            "ml" => (e.MovementX, 0),
            "bl" => (e.MovementX, 0),
            _ => (0, 0)
        };
        SelectedNodePosition = new Pos(SelectedNodePosition.X + dx, SelectedNodePosition.Y + dy);
    }

    void PanedMoveToolbar(PointerEvent e)
    {
        if (selectionService.IsSelected)
        {
            var (dx, dy) = (e.MovementX, e.MovementY);
            SelectedNodePosition = new Pos(SelectedNodePosition.X + dx, SelectedNodePosition.Y + dy);
        }
    }

    void OnMouseUp(PointerEvent e)
    {
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
