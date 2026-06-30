using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Dependinator.UI.Diagrams;

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

    Task InitAsync();
    void OpenFiles();
    void ToggleTheme();
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
    readonly IModelMgr modelMgr;
    readonly ISvgService svgService;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInteropService;
    readonly IFileService fileService;
    readonly IBrowserFileService browserFileService;
    readonly IModelListService recentModelsService;
    readonly IInteractionService interactionService;
    readonly IDialogService dialogService;

    public CanvasService(
        IScreenService screenService,
        IPanZoomService panZoomService,
        IModelService modelService,
        IModelMgr modelMgr,
        ISvgService svgService,
        IApplicationEvents applicationEvents,
        IJSInterop jSInteropService,
        IFileService fileService,
        IBrowserFileService browserFileService,
        IModelListService recentModelsService,
        IInteractionService interactionService,
        IDialogService dialogService
    )
    {
        this.screenService = screenService;
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.modelMgr = modelMgr;
        this.svgService = svgService;
        this.applicationEvents = applicationEvents;
        this.jSInteropService = jSInteropService;
        this.fileService = fileService;
        this.browserFileService = browserFileService;
        this.recentModelsService = recentModelsService;
        this.interactionService = interactionService;
        this.dialogService = dialogService;
    }

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
    public Pos Offset => modelMgr.WithModel(m => m.Offset);
    public double Zoom => modelMgr.WithModel(m => m.Zoom);
    public double ActualZoom => LevelZoom != 0 ? Zoom / LevelZoom : 0;

    public string SvgViewBox =>
        LevelZoom != 0
            ? $"{Offset.X / LevelZoom - TileOffset.X:0.##} {Offset.Y / LevelZoom - TileOffset.Y:0.##} {SvgRect.Width * Zoom / LevelZoom:0.##} {SvgRect.Height * Zoom / LevelZoom:0.##}"
            : "0 0 0 0";

    public async Task InitAsync()
    {
        await interactionService.InitAsync();
    }

    public async void InitialShow()
    {
        bool isShowDemoMessage = false;
        using var t = Timing.Start("InitialShow");
        await screenService.CheckResizeAsync();
        // In test mode always load the embedded demo model for a fast, deterministic
        // model, ignoring any persisted recent/local paths.
        var lastUsedPath = Dependinator.Core.Build.IsTestMode ? DemoModel.Path : recentModelsService.LastUsedPath;
        if (lastUsedPath is null)
        {
            lastUsedPath = DemoModel.Path;
            isShowDemoMessage = true;
        }

        await LoadAsync(lastUsedPath);

        // Signal that the initial model has loaded and rendered (data-app-ready=true on
        // the body), so UI/e2e tests can wait on it instead of arbitrary timeouts.
        await jSInteropService.Call("setAppReady", true);

        // First-time users (or users who reset their last diagram) have no previous
        // model, so a demo diagram is shown. Let them know why, and invite them to
        // explore the application with it.
        if (isShowDemoMessage)
        {
            await ShowDemoMessageAsync();
        }
    }

    public async Task LoadAsync(string modelPath)
    {
        DiagramName = $"Loading {modelPath} ...";
        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

        if (!Try(out var modelInfo, out var e, await modelService.LoadAsync(modelPath)))
            return;

        DiagramName = modelMgr.WithModel(m => Path.GetFileNameWithoutExtension(m.Path));
        PanZoomModel(modelInfo);

        await recentModelsService.AddModelAsync(modelInfo.Path);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        var paths = await browserFileService.AddAsync(browserFiles);

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
            var bound = modelMgr.WithModel(m => m.Root.GetTotalBounds());
            panZoomService.PanZoomToFit(bound, 1, true);
        }
    }

    public async void Remove()
    {
        var lastUsedPath = recentModelsService.LastUsedPath;
        if (lastUsedPath is not null)
        {
            await fileService.DeleteAsync(lastUsedPath);
            await recentModelsService.RemoveModelAsync(lastUsedPath);
        }
        else
        {
            lastUsedPath = DemoModel.Path;
        }
        await LoadAsync(lastUsedPath);
    }

    public async void OpenFiles()
    {
        await jSInteropService.Call("clickElement", "inputfile");
    }

    public void ToggleTheme()
    {
        DColors.IsDark = !DColors.IsDark;

        modelService.ClearCache();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task<IReadOnlyList<string>> GetModelPaths()
    {
        if (!Try(out var paths, out var eee, await fileService.GetFilePathsAsync()))
            return [];

        return paths.Where(p => Path.GetDirectoryName(p) == "/models").ToList();
    }

    public void PanZoomToFit()
    {
        var bound = modelMgr.WithModel(m => m.Root.GetTotalBounds());
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
        if (SvgRect.Width == 0 || SvgRect.Height == 0 || Zoom == 0)
            return "";

        var viewRect = new Rect(Offset.X, Offset.Y, SvgRect.Width, SvgRect.Height);
        var tile = svgService.GetTile(viewRect, Zoom);

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

    async Task ShowDemoMessageAsync()
    {
        await dialogService.ShowMessageBoxAsync(
            "Welcome to Dependinator",
            (MarkupString)(
                "It looks like you don't have a diagram yet, so a <b>demo diagram</b> "
                + "has been opened for you to explore.<br/><br/>"
                + "Pan, zoom and click the nodes to see how Dependinator visualizes "
                + "software dependencies. You can open your own model at any time from the menu.<br/><br/>"
                + "Login in to sync your diagrams across your devices."
            ),
            yesText: "Got it"
        );
    }
}
