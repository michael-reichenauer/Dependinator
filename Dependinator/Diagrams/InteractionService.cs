﻿using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;

interface IInteractionService
{
    string Cursor { get; }

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
    readonly IModelService modelService;

    const int MoveDelay = 300;

    readonly Timer moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;
    string mouseDownId = "";
    string mouseDownSubId = "";

    double Zoom => modelService.Zoom;


    public InteractionService(
        IPointerEventService mouseEventService,
        IPanZoomService panZoomService,
        INodeEditService nodeEditService,
        IApplicationEvents applicationEvents,
        ISelectionService selectionService,
        IModelService modelService)

    {
        this.mouseEventService = mouseEventService;
        this.panZoomService = panZoomService;
        this.nodeEditService = nodeEditService;
        this.applicationEvents = applicationEvents;
        this.selectionService = selectionService;
        this.modelService = modelService;
        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Cursor { get; private set; } = "default";

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

    void OnClick(PointerEvent e)
    {
        Log.Info("mouse click", e.TargetId);
        (string nodeId, string subId) = NodeId.ParseString(e.TargetId);

        selectionService.Select(nodeId);
    }

    void OnDblClick(PointerEvent e)
    {
        Log.Info($"OnDoubleClick {e.Type}");
    }


    void OnMouseDown(PointerEvent e)
    {
        moveTimerRunning = true;
        moveTimer.Change(MoveDelay, Timeout.Infinite);
        (string id, string subId) = NodeId.ParseString(e.TargetId);
        mouseDownId = id;
        mouseDownSubId = subId;
    }


    void OnMouseMove(PointerEvent e)
    {
        if (!e.IsLeftButton) return;

        if (mouseDownId != "" && mouseDownSubId != "")
        {
            nodeEditService.ResizeSelectedNode(e, Zoom, mouseDownId, mouseDownSubId);
            return;
        }

        if (mouseDownId == selectionService.SelectedId && selectionService.IsNodeMovable(Zoom) && mouseDownSubId == "")
        {
            nodeEditService.MoveSelectedNode(e, Zoom, mouseDownId);
            return;
        }

        panZoomService.Pan(e);
    }


    void OnMouseUp(PointerEvent e)
    {
        mouseDownId = "";
        mouseDownSubId = "";

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
