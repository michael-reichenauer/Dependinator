using Dependinator.Models;
using Dependinator.Shared;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Diagrams;


interface ICanvasService
{
    string SvgContent { get; }
    string TileKeyText { get; }
    Rect SvgRect { get; }
    string TileViewBox { get; }
    Pos Offset { get; }
    double Zoom { get; }
    string SvgViewBox { get; }
    string Cursor { get; }
    string TitleInfo { get; }
    string DiagramName { get; }
    IReadOnlyList<string> RecentModelPaths { get; }

    void OpenFiles();
    public void Remove();
    void Refresh();
    void Clear();
    void PanZoomToFit();
    void InitialShow();
    Task LoadAsync(string modelPath);
    Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles);
    Task<IReadOnlyList<string>> GetModelPaths();
}


[Scoped]
class CanvasService : ICanvasService
{
    const double MinCover = 0.5;
    const double MaxCover = 0.8;
    const int MoveDelay = 300;

    readonly IScreenService screenService;
    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInteropService;
    readonly IFileService fileService;
    readonly IRecentModelsService recentModelsService;
    readonly Timer moveTimer;
    bool moveTimerRunning = false;
    bool isMoving = false;

    public CanvasService(
        IMouseEventService mouseEventService,
        IScreenService screenService,
        IPanZoomService panZoomService,
        IModelService modelService,
        IApplicationEvents applicationEvents,
        IJSInterop jSInteropService,
        IFileService fileService,
        IRecentModelsService recentModelsService)
    {
        this.screenService = screenService;
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.jSInteropService = jSInteropService;
        this.fileService = fileService;
        this.recentModelsService = recentModelsService;
        mouseEventService.LeftClick += OnClick;
        mouseEventService.LeftDblClick += OnDblClick;
        mouseEventService.MouseWheel += OnMouseWheel;
        mouseEventService.MouseMove += OnMouseMove;
        mouseEventService.MouseDown += OnMouseDown;
        mouseEventService.MouseUp += OnMouseUp;

        moveTimer = new Timer(OnMoveTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<string> RecentModelPaths => recentModelsService.ModelPaths;

    public string DiagramName { get; set; } = "Loading ...";
    public string TitleInfo => $"Zoom: {1 / Zoom * 100:#}%, {1 / Zoom:0.#}x, L: {-(int)Math.Ceiling(Math.Log(Zoom) / Math.Log(7)) + 1}";
    public string SvgContent => GetSvgContent();
    public string TileKeyText { get; private set; } = "()";
    public double LevelZoom { get; private set; } = 1;
    public string TileViewBox { get; private set; } = "";
    public Pos TileOffset { get; private set; } = Pos.Zero;
    public string Content { get; private set; } = "";
    public string Cursor { get; private set; } = "default";

    public Rect SvgRect => screenService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public double ActualZoom => Zoom / LevelZoom;

    string selectedId = "";
    string mouseDownId = "";
    string mouseDownSubId = "";


    public string SvgViewBox => $"{Offset.X / LevelZoom - TileOffset.X:0.##} {Offset.Y / LevelZoom - TileOffset.Y:0.##} {SvgRect.Width * Zoom / LevelZoom:0.##} {SvgRect.Height * Zoom / LevelZoom:0.##}";


    public async void InitialShow()
    {
        await screenService.CheckResizeAsync();
        await LoadAsync(recentModelsService.LastUsedPath);
    }


    public async Task LoadAsync(string modelPath)
    {
        DiagramName = $"Loading {modelPath} ...";
        applicationEvents.TriggerUIStateChanged();

        if (!Try(out var modelInfo, out var e, await modelService.LoadAsync(modelPath))) return;

        DiagramName = modelService.ModelName;
        PanZoomModel(modelInfo);

        await recentModelsService.AddModelAsync(modelInfo.Path);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        await fileService.AddAsync(browserFiles);
        var modelPath = browserFiles.First().Name;
        await LoadAsync(modelPath);
    }


    void PanZoomModel(ModelInfo modelInfo)
    {
        if (modelInfo.ViewRect != Rect.None)
        {
            panZoomService.PanZoom(modelInfo.ViewRect, modelInfo.Zoom);
        }
        else
        {
            var bound = modelService.GetBounds();
            panZoomService.PanZoomToFit(bound);
        }
    }

    public async void Remove()
    {
        var path = recentModelsService.LastUsedPath;
        await fileService.DeleteAsync(path);
        await recentModelsService.RemoveModelAsync(path);
        await LoadAsync(recentModelsService.LastUsedPath);
    }

    public async void OpenFiles()
    {
        await jSInteropService.Call("clickElement", "inputfile");
    }

    public async Task<IReadOnlyList<string>> GetModelPaths()
    {
        if (!Try(out var paths, out var eee, await fileService.GetFilePathsAsync())) return [];

        return paths.Where(p => Path.GetDirectoryName(p) == "/models").ToList();
    }


    bool IsNodeAdjustable(string id)
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

        if (!IsNodeAdjustable(nodeId))
        {   // Node node at click or node not adjustable 
            if (selectedId != "")
            {
                modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
                selectedId = "";
                applicationEvents.TriggerUIStateChanged();
            }
            return;
        }

        if (selectedId != nodeId && selectedId != "")
        {   // click on other node
            modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
            selectedId = "";
            applicationEvents.TriggerUIStateChanged();
        }

        if (modelService.TryUpdateNode(e.TargetId, node =>
        {
            node.IsSelected = true;
        }))
        {
            Log.Info($"Node clicked: {e.TargetId}");
            selectedId = nodeId;
            applicationEvents.TriggerUIStateChanged();
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
        applicationEvents.TriggerUIStateChanged();
    }


    void moveSelectedNode(MouseEvent e)
    {
        if (!IsNodeAdjustable(mouseDownId))
        {
            if (selectedId != "")
            {
                modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
                selectedId = "";
                applicationEvents.TriggerUIStateChanged();
            }

            return;
        }
        modelService.TryUpdateNode(mouseDownId, node =>
        {
            var zoom = node.GetZoom() * Zoom;
            var (dx, dy) = (e.MovementX * zoom, e.MovementY * zoom);

            node.Boundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };
        });
    }

    void resizeSelectedNode(MouseEvent e)
    {
        if (!IsNodeAdjustable(mouseDownId))
        {
            if (selectedId != "")
            {
                modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
                selectedId = "";
                applicationEvents.TriggerUIStateChanged();
            }

            return;
        }

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



    public void PanZoomToFit()
    {
        var bound = modelService.GetBounds();
        panZoomService.PanZoomToFit(bound, Math.Min(1, Zoom));
        applicationEvents.TriggerUIStateChanged();
    }

    public async void Refresh()
    {
        await modelService.RefreshAsync();
        applicationEvents.TriggerUIStateChanged();
    }


    public void Clear()
    {
        modelService.Clear();

        applicationEvents.TriggerUIStateChanged();
    }



    string GetSvgContent()
    {
        // Log.Info($"GetSvgContent:", Offset, Zoom);
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

        applicationEvents.TriggerUIStateChanged();
        return Content;
    }
}