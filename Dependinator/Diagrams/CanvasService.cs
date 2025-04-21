using Dependinator.Models;
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

    Task InitAsync();
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
    readonly IScreenService screenService;
    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInteropService;
    readonly IFileService fileService;
    readonly IRecentModelsService recentModelsService;
    readonly IInteractionService interactionService;
    private readonly ISelectionService selectionService;

    public CanvasService(
        IScreenService screenService,
        IPanZoomService panZoomService,
        IModelService modelService,
        IApplicationEvents applicationEvents,
        IJSInterop jSInteropService,
        IFileService fileService,
        IRecentModelsService recentModelsService,
        IInteractionService interactionService,
        ISelectionService selectionService
    )
    {
        this.screenService = screenService;
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.jSInteropService = jSInteropService;
        this.fileService = fileService;
        this.recentModelsService = recentModelsService;
        this.interactionService = interactionService;
        this.selectionService = selectionService;
    }

    public IReadOnlyList<string> RecentModelPaths => recentModelsService.ModelPaths;

    public string DiagramName { get; set; } = "Loading ...";
    public string TitleInfo =>
        $"Zoom: {1 / Zoom * 100:#}%, {1 / Zoom:0.#}x, L: {-(int)Math.Ceiling(Math.Log(Zoom) / Math.Log(7)) + 1}";
    public string SvgContent => GetSvgContent();
    public string TileKeyText { get; private set; } = "()";
    public double LevelZoom { get; private set; } = 1;
    public string TileViewBox { get; private set; } = "";
    public Pos TileOffset { get; private set; } = Pos.Zero;
    public string Content { get; private set; } = "";
    public string Cursor => interactionService.Cursor;

    public Rect SvgRect => screenService.SvgRect;
    public Pos Offset => modelService.Offset;
    public double Zoom => modelService.Zoom;
    public double ActualZoom => Zoom / LevelZoom;

    public string SvgViewBox =>
        $"{Offset.X / LevelZoom - TileOffset.X:0.##} {Offset.Y / LevelZoom - TileOffset.Y:0.##} {SvgRect.Width * Zoom / LevelZoom:0.##} {SvgRect.Height * Zoom / LevelZoom:0.##}";

    public async Task InitAsync()
    {
        await interactionService.InitAsync();
    }

    public async void InitialShow()
    {
        await screenService.CheckResizeAsync();
        await LoadAsync(recentModelsService.LastUsedPath);
    }

    public async Task LoadAsync(string modelPath)
    {
        DiagramName = $"Loading {modelPath} ...";
        applicationEvents.TriggerUIStateChanged();

        if (!Try(out var modelInfo, out var e, await modelService.LoadAsync(modelPath)))
            return;

        DiagramName = modelService.ModelName;
        PanZoomModel(modelInfo);

        await recentModelsService.AddModelAsync(modelInfo.Path);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        var paths = await fileService.AddAsync(browserFiles);

        var modelPath = paths.First();
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
            panZoomService.PanZoomToFit(bound, 1, true);
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
        if (!Try(out var paths, out var eee, await fileService.GetFilePathsAsync()))
            return [];

        return paths.Where(p => Path.GetDirectoryName(p) == "/models").ToList();
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

        if (Content == tile.Svg)
            return Content; // No change

        Content = tile.Svg;
        LevelZoom = tile.Zoom;
        var tileViewRect = tile.Key.GetViewRect();
        TileOffset = new Pos(-tile.Offset.X + tileViewRect.X, -tile.Offset.Y + tileViewRect.Y);

        TileKeyText = $"{tile.Key}"; // Log info
        TileViewBox = $"{tileViewRect}"; // Log info

        applicationEvents.TriggerUIStateChanged();
        return Content;
    }
}
