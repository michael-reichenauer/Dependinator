﻿using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;

interface IInteractionService
{
    string Cursor { get; }
    bool IsNodeSelected { get; }
    Pos SelectedNodePosition { get; }

    Task InitAsync();
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
    }


    public string Cursor { get; private set; } = "default";
    public bool IsNodeSelected => selectionService.IsSelected;
    public Pos SelectedNodePosition { get; set; } = Pos.None;


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
        panZoomService.Zoom(e);
    }


    async void OnClick(PointerEvent e)
    {
        Log.Info("mouse click", e.TargetId);
        var targetId = PointerId.Parse(e.TargetId);

        selectionService.Select(targetId);
        if (selectionService.IsSelected)
        {
            var bound = await screenService.GetBoundingRectangle(targetId.Id);
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
    }


    void OnMouseMove(PointerEvent e)
    {
        if (!e.IsLeftButton) return;

        if (mouseDownId != PointerId.Empty && mouseDownId.SubId != "")
        {
            nodeEditService.ResizeSelectedNode(e, Zoom, mouseDownId);
            return;
        }

        if (mouseDownId == selectionService.SelectedId && selectionService.IsNodeMovable(Zoom) && mouseDownId.SubId == "")
        {
            nodeEditService.MoveSelectedNode(e, Zoom, mouseDownId);
            var (dx, dy) = (e.MovementX, e.MovementY);
            SelectedNodePosition = new Pos(SelectedNodePosition.X + dx, SelectedNodePosition.Y + dy);
            return;
        }

        panZoomService.Pan(e);
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
