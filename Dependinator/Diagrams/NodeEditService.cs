using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;

interface INodeEditService
{
    string Cursor { get; }

    Task InitAsync();
}

[Scoped]
class NodeEditService : INodeEditService
{
    const double MinCover = 0.5;
    const double MaxCover = 0.8;
    const int MoveDelay = 300;

    readonly Timer moveTimer;
    readonly IMouseEventService mouseEventService;
    readonly IScreenService screenService;
    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;
    readonly ISelectionService selectionService;
    bool moveTimerRunning = false;
    bool isMoving = false;

    Rect SvgRect => screenService.SvgRect;
    double Zoom => panZoomService.Zoom;

    string mouseDownId = "";
    string mouseDownSubId = "";


    public NodeEditService(
        IMouseEventService mouseEventService,
        IScreenService screenService,
        IPanZoomService panZoomService,
        IModelService modelService,
        IApplicationEvents applicationEvents,
        ISelectionService selectionService)
    {
        this.mouseEventService = mouseEventService;
        this.screenService = screenService;
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.selectionService = selectionService;
        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task InitAsync()
    {
        mouseEventService.LeftClick += OnClick;
        mouseEventService.LeftDblClick += OnDblClick;
        mouseEventService.MouseMove += OnMouseMove;
        mouseEventService.MouseDown += OnMouseDown;
        mouseEventService.MouseUp += OnMouseUp;

        return Task.CompletedTask;
    }

    public string Cursor { get; private set; } = "default";


    bool IsNodeEditable(string id)
    {
        if (!modelService.TryGetNode(id, out var node)) return false;

        var v = SvgRect;
        var nodeZoom = (1 / node.GetZoom());
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * Zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * Zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }


    void OnClick(MouseEvent e)
    {
        Log.Info("mouse click", e.TargetId);
        (string nodeId, string subId) = NodeId.ParseString(e.TargetId);

        if (!IsNodeEditable(nodeId))
        {   // No node node at click or node not editable (to larger) 
            selectionService.Unselect();
            return;
        }

        selectionService.Select(nodeId);
    }

    void OnDblClick(MouseEvent e)
    {
        Log.Info($"OnDoubleClick {e.Type}");
    }


    void OnMouseMove(MouseEvent e)
    {
        if (!e.IsLeftButton) return;

        if (!selectionService.IsNodeSelected(mouseDownId)) return;

        if (!IsNodeEditable(mouseDownId))
        {   // No node node at click or node not editable (to larger) 
            selectionService.Unselect();
            return;
        }

        if (mouseDownSubId == "") moveSelectedNode(e);

        if (mouseDownSubId != "") resizeSelectedNode(e);
    }

    void OnMouseDown(MouseEvent e)
    {
        moveTimerRunning = true;
        moveTimer.Change(MoveDelay, Timeout.Infinite);
        (string id, string subId) = NodeId.ParseString(e.TargetId);
        mouseDownId = id;
        mouseDownSubId = subId;
    }

    void OnMouseUp(MouseEvent e)
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


    void moveSelectedNode(MouseEvent e)
    {
        modelService.TryUpdateNode(mouseDownId, node =>
        {
            var zoom = node.GetZoom() * Zoom;
            var (dx, dy) = (e.MovementX * zoom, e.MovementY * zoom);

            node.Boundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };
        });
    }

    void resizeSelectedNode(MouseEvent e)
    {
        modelService.TryUpdateNode(mouseDownId, node =>
        {
            var zoom = node.GetZoom() * Zoom;
            var (dx, dy) = (e.MovementX * zoom, e.MovementY * zoom);

            var oldBoundary = node.Boundary;
            node.Boundary = mouseDownSubId switch
            {
                "tl" => node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy, Width = node.Boundary.Width - dx, Height = node.Boundary.Height - dy },
                "tm" => node.Boundary with { Y = node.Boundary.Y + dy, Height = node.Boundary.Height - dy },
                "tr" => node.Boundary with { X = node.Boundary.X, Y = node.Boundary.Y + dy, Width = node.Boundary.Width + dx, Height = node.Boundary.Height - dy },

                "ml" => node.Boundary with { X = node.Boundary.X + dx, Width = node.Boundary.Width - dx },
                "mr" => node.Boundary with { X = node.Boundary.X, Width = node.Boundary.Width + dx },

                "bl" => node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y, Width = node.Boundary.Width - dx, Height = node.Boundary.Height + dy },
                "bm" => node.Boundary with { Y = node.Boundary.Y, Height = node.Boundary.Height + dy },
                "br" => node.Boundary with { X = node.Boundary.X, Y = node.Boundary.Y, Width = node.Boundary.Width + dx, Height = node.Boundary.Height + dy },

                _ => node.Boundary
            };
            var newBoundary = node.Boundary;

            // Adjust container offest to ensure that children stay in place
            node.ContainerOffset = node.ContainerOffset with
            {
                X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y)
            };
        });
    }
}