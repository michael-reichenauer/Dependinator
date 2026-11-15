using System.Globalization;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Diagrams.Tours;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.CloudSync;
using Dependinator.UI.Shared.Types;
using Dependinator.UI.Shared.VsCode;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Shared;

// The interactive diagram canvas: rendering the model, pan/zoom, selection, and pointer-driven
// editing of nodes and lines.
namespace Dependinator.UI.Diagrams;

// What the canvas shows: the svg content, its viewBox and the CSS transform that moves it to the
// current view while the view moves (GestureTransform).
record CanvasView(string Content, string ViewBox, string Style);

interface ICanvasService
{
    Rect SvgRect { get; }
    string Cursor { get; }

    // The view to render: while the view moves, the shown svg follows it by a transform where it
    // can (GestureTransform), else the current view is drawn.
    CanvasView GetView();

    // Draws one tile the resting view is likely to need next (SvgService.PrefetchNext); false
    // while the view moves or when nothing is left to draw ahead.
    bool PrefetchNext();

    Task InitAsync();
    Task RemoveAsync();
    Task RefreshAsync();

    // Parses the model again after files of the kinds changed (IModelService.RefreshChangedAsync).
    Task RefreshChangedAsync(IReadOnlyCollection<string> changedKinds);
    void PanZoomToFit();
    Task InitialShowAsync();

    // isRetry: the load is tried again after it failed, with what the user chose for it.
    Task LoadAsync(string modelPath, bool? includeTestProjects = null, bool isRetry = false);
    Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles);
}

[Scoped]
class CanvasService(
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
    ICoachService coachService,
    IShareLinkService shareLinkService,
    Lazy<IAppCloudSyncService> appCloudSyncServiceLazy,
    IVsCodeSendService vsCodeSendService,
    IViewHistoryService viewHistory,
    ICommandService commandService,
    App.IWorkspaceSourcesService workspaceSourcesService
) : ICanvasService
{
    double levelZoom = 1;
    Pos tileOffset = Pos.None;
    string content = "";

    // The view the content and viewBox were drawn for, and the canvas area its tile holds.
    Pos shownOffset = Pos.None;
    double shownZoom = 0;
    Rect shownTileArea = Rect.None;
    volatile bool isShownStale = true;
    bool isSubscribed;

    public string Cursor => interactionService.Cursor;

    public Rect SvgRect => screenService.SvgRect;
    double Zoom => modelMgr.WithModel(m => m.Zoom);

    public async Task InitAsync()
    {
        // A model change clears the tiles: what is shown must then be drawn again, also while
        // the view moves. The service lives as long as its scope, so it never unsubscribes.
        if (!isSubscribed)
        {
            isSubscribed = true;
            applicationEvents.ModelChanged += () => isShownStale = true;
        }
        await interactionService.InitAsync();
    }

    public async Task InitialShowAsync()
    {
        using var t = Timing.Start("InitialShow");
        await screenService.CheckResizeAsync();
        // In test mode always load the embedded demo model for a fast, deterministic
        // model, ignoring any persisted recent/local paths. First-time users (or users who
        // reset their last diagram) have no previous model, so the demo diagram is shown.
        var startupPath = Dependinator.Core.Build.IsTestMode
            ? DemoModel.Path
            : recentModelsService.StartupPath ?? DemoModel.Path;

        // Opened with a share link: its model wins over the remembered one (when it can be found).
        var link = shareLinkService.TakeStartupTarget();
        var isLoaded = false;
        var isLinkedModelOpen = link?.ModelKey is null; // A link without a model applies to whatever opens
        if (link?.ModelKey is { } modelKey)
        {
            switch (await shareLinkService.ResolveModelAsync(modelKey))
            {
                case LinkedModel { LocalPath: { } localPath }:
                    startupPath = localPath;
                    isLinkedModelOpen = true;
                    break;
                case LinkedModel { CloudModel: { } cloudModel }:
                    isLoaded =
                        await appCloudSyncServiceLazy.Value.LoadCloudModelAsync(cloudModel) is CloudModelMetadata;
                    isLinkedModelOpen = isLoaded;
                    break;
                case Error error:
                    applicationEvents.TriggerErrorReported(error.Message);
                    break;
            }
        }

        if (!isLoaded)
            await LoadAsync(startupPath);

        // Signal that the initial model has loaded and rendered (data-app-ready=true on
        // the body), so UI/e2e tests can wait on it instead of arbitrary timeouts.
        await jSInteropService.Call("setAppReady", true);

        // Let the VS Code extension host know the diagram is ready, so it can reveal the
        // node for the editor that was active when the webview was first opened.
        await vsCodeSendService.NotifyDiagramLoadedAsync();

        // The link's node or view only means something in the link's own model.
        if (link is not null && isLinkedModelOpen)
            await shareLinkService.ApplyAsync(link);

        // New users get the short tour (once); it says why a demo diagram is open when it is.
        await coachService.StartIfFirstRunAsync(isDemoModel: !isLoaded && DemoModel.IsDemo(startupPath));
    }

    public async Task LoadAsync(string modelPath, bool? includeTestProjects = null, bool isRetry = false)
    {
        // The workspace's model, opened for the first time: the user chooses what is in it, and
        // cancelling leaves the open model as it is.
        var sources = await workspaceSourcesService.AskBeforeFirstOpenAsync(modelPath, isRetry);
        if (sources.IsCancelled)
            return;

        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

        // Load failures (e.g. a failed parse) are reported to the user by the model service.
        if (await modelService.LoadAsync(modelPath, includeTestProjects, sources.Choices) is not ModelInfo modelInfo)
            return;

        viewHistory.Clear(); // Views of the previous model mean nothing here
        commandService.Clear(); // Nor do its edits: undoing them here would change this model
        PanZoomModel(modelInfo);

        await recentModelsService.AddModelAsync(modelInfo.Path);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task LoadFilesAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        var paths = await browserFileService.AddAsync(browserFiles);
        if (paths.Count == 0)
        {
            applicationEvents.TriggerErrorReported("The dropped file could not be read.");
            return;
        }

        await LoadAsync(paths[0]);
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

    public async Task RemoveAsync()
    {
        var lastUsedPath = recentModelsService.LastUsedPath;
        if (lastUsedPath is not null)
        {
            await fileService.DeleteAsync(lastUsedPath);
            await recentModelsService.RemoveModelAsync(lastUsedPath);
        }

        // Load the next remaining model instead of re-creating the deleted one; re-loading
        // the deleted path would re-parse it and, with device sync on, re-upload it.
        await LoadAsync(recentModelsService.LastUsedPath ?? DemoModel.Path);
    }

    public void PanZoomToFit()
    {
        var bound = modelMgr.WithModel(m => m.Root.GetTotalBounds());
        viewHistory.RecordJump();
        panZoomService.PanZoomToFit(bound, Math.Min(1, Zoom));
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task RefreshAsync()
    {
        // A source can have been added or removed since the workspace was listed: only VS Code
        // tells when that happens, and not of every way it can (a folder that is moved).
        await recentModelsService.RefreshWorkspaceSourcesAsync();
        await modelService.RefreshAsync();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task RefreshChangedAsync(IReadOnlyCollection<string> changedKinds)
    {
        await modelService.RefreshChangedAsync(changedKinds);
        applicationEvents.TriggerUIStateChanged();
    }

    public CanvasView GetView()
    {
        var svgRect = SvgRect;
        var (offset, zoom) = modelMgr.WithModel(m => (m.Offset, m.Zoom));
        if (svgRect.Width == 0 || svgRect.Height == 0 || zoom == 0)
            return new CanvasView("", "0 0 0 0", "");

        // The view rect, as the saved view and the layout refine read it (SvgService.GetTile
        // records it as well, but is not called while the svg is only moved).
        var viewRect = new Rect(offset.X, offset.Y, svgRect.Width, svgRect.Height);
        modelMgr.WithModel(m => m.ViewRect = viewRect);

        // While the view moves the shown svg is moved, not drawn again, until the view rests
        // (PanZoomService.IsViewMoving), unless the model changed since it was drawn.
        if (
            panZoomService.IsViewMoving
            && !isShownStale
            && GestureTransform.Get(shownOffset, shownZoom, offset, zoom, svgRect, shownTileArea) is { } transform
        )
            return new CanvasView(content, ViewBox(shownOffset, shownZoom, svgRect), GestureTransform.Css(transform));

        UpdateContent(viewRect, zoom);
        return new CanvasView(content, ViewBox(offset, zoom, svgRect), "");
    }

    public bool PrefetchNext()
    {
        if (panZoomService.IsViewMoving)
            return false; // Its rest draws the view, and asks again after
        var svgRect = SvgRect;
        var (offset, zoom) = modelMgr.WithModel(m => (m.Offset, m.Zoom));
        return svgService.PrefetchNext(new Rect(offset.X, offset.Y, svgRect.Width, svgRect.Height), zoom);
    }

    string ViewBox(Pos offset, double zoom, Rect svgRect) =>
        levelZoom != 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{offset.X / levelZoom - tileOffset.X:0.##} {offset.Y / levelZoom - tileOffset.Y:0.##} {svgRect.Width * zoom / levelZoom:0.##} {svgRect.Height * zoom / levelZoom:0.##}"
            )
            : "0 0 0 0";

    void UpdateContent(Rect viewRect, double zoom)
    {
        isShownStale = false; // Before the tile: a model change while it is drawn marks it again
        Tile tile;
        try
        {
            tile = svgService.GetTile(viewRect, zoom);
        }
        catch
        {
            isShownStale = true; // What is shown was not replaced: never move it
            throw;
        }
        shownOffset = new Pos(viewRect.X, viewRect.Y);
        shownZoom = zoom;
        shownTileArea =
            tile.Key == TileKey.Empty
                ? Rect.None
                : SvgService.GetCanvasArea(tile.Key.GetTileRect(), tile.Key.GetTileRectWithMargin(), tile.Zoom);

        if (content == tile.Svg)
            return; // No change

        content = tile.Svg;
        levelZoom = tile.Zoom;
        var tileViewRect = tile.Key.GetViewRect();
        tileOffset = new Pos(-tile.Offset.X + tileViewRect.X, -tile.Offset.Y + tileViewRect.Y);

        applicationEvents.TriggerUIStateChanged();
    }
}
