using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

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
    IDialogService dialogService
) : ICanvasService
{
    double levelZoom = 1;
    Pos tileOffset = Pos.Zero;
    string content = "";

    public string SvgContent => GetSvgContent();
    public string Cursor => interactionService.Cursor;

    public Rect SvgRect => screenService.SvgRect;
    Pos Offset => modelMgr.WithModel(m => m.Offset);
    double Zoom => modelMgr.WithModel(m => m.Zoom);

    public string SvgViewBox =>
        levelZoom != 0
            ? FormattableString.Invariant(
                $"{Offset.X / levelZoom - tileOffset.X:0.##} {Offset.Y / levelZoom - tileOffset.Y:0.##} {SvgRect.Width * Zoom / levelZoom:0.##} {SvgRect.Height * Zoom / levelZoom:0.##}"
            )
            : "0 0 0 0";

    public async Task InitAsync()
    {
        await interactionService.InitAsync();
    }

    public async Task InitialShowAsync()
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
        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

        if (!Try(out var modelInfo, out var e, await modelService.LoadAsync(modelPath)))
            return;

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

    public async Task RemoveAsync()
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

    public void PanZoomToFit()
    {
        var bound = modelMgr.WithModel(m => m.Root.GetTotalBounds());
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

    async Task ShowDemoMessageAsync()
    {
        await dialogService.ShowMessageBoxAsync(
            "Welcome to Dependinator",
            (MarkupString)(
                "It looks like you don't have a diagram yet, so a <b>demo diagram</b> "
                + "has been opened for you to explore.<br/><br/>"
                + "Pan, zoom and click the nodes to see how Dependinator visualizes "
                + "software dependencies. You can open your own model at any time from the menu.<br/><br/>"
                + "Enable device sync to keep your diagrams in sync across your devices."
            ),
            yesText: "Got it"
        );
    }
}
