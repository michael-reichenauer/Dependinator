using System.Globalization;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
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

interface ICanvasService
{
    string SvgContent { get; }
    Rect SvgRect { get; }
    string SvgViewBox { get; }
    string Cursor { get; }

    Task InitAsync();
    Task RemoveAsync();
    Task RefreshAsync();
    void PanZoomToFit();
    Task InitialShowAsync();
    Task LoadAsync(string modelPath);
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
    IViewHistoryService viewHistory
) : ICanvasService
{
    double levelZoom = 1;
    Pos tileOffset = Pos.None;
    string content = "";

    public string SvgContent => GetSvgContent();
    public string Cursor => interactionService.Cursor;

    public Rect SvgRect => screenService.SvgRect;
    Pos Offset => modelMgr.WithModel(m => m.Offset);
    double Zoom => modelMgr.WithModel(m => m.Zoom);

    public string SvgViewBox =>
        levelZoom != 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{Offset.X / levelZoom - tileOffset.X:0.##} {Offset.Y / levelZoom - tileOffset.Y:0.##} {SvgRect.Width * Zoom / levelZoom:0.##} {SvgRect.Height * Zoom / levelZoom:0.##}"
            )
            : "0 0 0 0";

    public async Task InitAsync()
    {
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
        if (link?.ModelKey is { } modelKey)
        {
            switch (await shareLinkService.ResolveModelAsync(modelKey))
            {
                case LinkedModel { LocalPath: { } localPath }:
                    startupPath = localPath;
                    break;
                case LinkedModel { CloudModel: { } cloudModel }:
                    isLoaded =
                        await appCloudSyncServiceLazy.Value.LoadCloudModelAsync(cloudModel) is CloudModelMetadata;
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

        if (link is not null)
            await shareLinkService.ApplyAsync(link);

        // New users get the short tour (once); it says why a demo diagram is open when it is.
        await coachService.StartIfFirstRunAsync(isDemoModel: startupPath == DemoModel.Path);
    }

    public async Task LoadAsync(string modelPath)
    {
        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

        // Load failures (e.g. a failed parse) are reported to the user by the model service.
        if (await modelService.LoadAsync(modelPath) is not ModelInfo modelInfo)
            return;

        viewHistory.Clear(); // Views of the previous model mean nothing here
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
        await modelService.RefreshAsync();
        applicationEvents.TriggerUIStateChanged();
    }

    string GetSvgContent()
    {
        if (SvgRect.Width == 0 || SvgRect.Height == 0 || Zoom == 0)
            return "";

        var viewRect = new Rect(Offset.X, Offset.Y, SvgRect.Width, SvgRect.Height);
        var tile = svgService.GetTile(viewRect, Zoom);

        if (content == tile.Svg)
            return content; // No change

        content = tile.Svg;
        levelZoom = tile.Zoom;
        var tileViewRect = tile.Key.GetViewRect();
        tileOffset = new Pos(-tile.Offset.X + tileViewRect.X, -tile.Offset.Y + tileViewRect.Y);

        applicationEvents.TriggerUIStateChanged();
        return content;
    }
}
