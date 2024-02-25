using Dependinator.Models;
using Dependinator.Utils.UI;


namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(IUIComponent component);

    string SvgContent { get; }
    string TileKeyText { get; }
    Rect SvgRect { get; }
    string TileViewBox { get; }
    Pos Offset { get; }
    double Zoom { get; }
    int ZCount { get; }
    string SvgViewBox { get; }
    string Cursor { get; }

    void Refresh();
    void Clear();
    void PanZoomToFit();
    void InitialShow();
}



[Scoped]
class CanvasService : ICanvasService
{
    const int MoveDelay = 300;

    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;
    readonly IUIService uiService;
    readonly Timer moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;

    public CanvasService(
        IMouseEventService mouseEventService,
        IPanZoomService panZoomService,
        IModelService modelService,
        IUIService uiService)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.uiService = uiService;
        mouseEventService.LeftClick += OnClick;
        mouseEventService.LeftDblClick += OnDblClick;
        mouseEventService.MouseWheel += OnMouseWheel;
        mouseEventService.MouseMove += OnMouseMove;
        mouseEventService.MouseDown += OnMouseDown;
        mouseEventService.MouseUp += OnMouseUp;

        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string SvgContent => GetSvgContent();
    public string TileKeyText { get; private set; } = "()";
    public double LevelZoom { get; private set; } = 1;
    public string TileViewBox { get; private set; } = "";
    public Pos TileOffset { get; private set; } = Pos.Zero;
    public string Content { get; private set; } = "";
    public string Cursor { get; private set; } = "default";

    public Rect SvgRect => panZoomService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public double ActualZoom => Zoom / LevelZoom;
    public int ZCount => panZoomService.ZCount;

    string selectedId = "";
    string mouseDownId = "";
    string mouseDownSubId = "";

    public string SvgViewBox => $"{Offset.X / LevelZoom - TileOffset.X:0.##} {Offset.Y / LevelZoom - TileOffset.Y:0.##} {SvgRect.Width * Zoom / LevelZoom:0.##} {SvgRect.Height * Zoom / LevelZoom:0.##}";


    public async Task InitAsync(IUIComponent component)
    {
        await panZoomService.InitAsync(component);
    }


    void OnClick(MouseEvent e)
    {
        Log.Info("mouse click", e.TargetId);
        (string id, string subId) = NodeId.ParseString(e.TargetId);
        if (selectedId == id) return; // Clicked on same node again

        if (modelService.TryUpdateNode(e.TargetId, node => node.IsSelected = true))
        {
            Log.Info($"Node clicked: {e.TargetId}");
            if (selectedId != "")
            {   // Deselect previous node
                modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
            }

            selectedId = id;
            uiService.TriggerUIStateChange();
        }
        else
        {
            Log.Info($"No node found at {e.OffsetX},{e.OffsetY}");
            if (selectedId != "")
            {
                modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
                selectedId = "";
                uiService.TriggerUIStateChange();
            }
        }
    }

    void OnDblClick(MouseEvent e)
    {
        Log.Info($"OnDoubleClick {e.Type}");
    }

    void OnMouseWheel(MouseEvent e)
    {
        panZoomService.OnMouseWheel(e);
    }

    void OnMouseMove(MouseEvent e)
    {
        if (!e.IsLeftButton)
        {   // No left button, just moving mouse
            if (isMoving)
            {
                Cursor = "default";
                isMoving = false;
            }
            return;
        };


        if (!isMoving && selectedId != "" && selectedId == mouseDownId)
        {
            Cursor = "move";
            isMoving = true;
        }

        if (selectedId != "")
        {
            if (selectedId == mouseDownId && mouseDownSubId == "")
            {
                moveSelectedNode(e);
                return;
            }
            if (selectedId == mouseDownId && mouseDownSubId != "")
            {
                Log.Info($"Move {mouseDownId}, {mouseDownSubId}");
                resizeSelectedNode(e);
                return;
            }
        }

        panZoomService.OnMouseMove(e);
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
        uiService.TriggerUIStateChange();
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

            Log.Info("Resize", mouseDownSubId);
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
        });
    }

    public async void InitialShow()
    {
        await panZoomService.CheckResizeAsync();

        await modelService.LoadAsync();

        //panZoomService.PanZoomToFit(bounds);
        uiService.TriggerUIStateChange();
    }

    public void PanZoomToFit()
    {

    }

    public async void Refresh()
    {
        await modelService.RefreshAsync();
        // panZoomService.PanZoomToFit(bounds);
        uiService.TriggerUIStateChange();
    }


    public void Clear()
    {
        modelService.Clear();

        uiService.TriggerUIStateChange();
    }



    string GetSvgContent()
    {
        var viewRect = new Rect(Offset.X, Offset.Y, SvgRect.Width, SvgRect.Height);
        var tile = modelService.GetTile(viewRect, Zoom);

        if (Content == tile.Svg) return Content;  // No change

        Content = tile.Svg;
        LevelZoom = tile.Zoom;
        var tileViewRect = tile.Key.GetViewRect();
        TileOffset = new Pos(-tile.Offset.X + tileViewRect.X, -tile.Offset.Y + tileViewRect.Y);
        panZoomService.SvgZoom = tile.Zoom;

        TileKeyText = $"{tile.Key}"; // Log info
        TileViewBox = $"{tileViewRect}"; // Log info

        uiService.TriggerUIStateChange();
        return Content;
    }
}