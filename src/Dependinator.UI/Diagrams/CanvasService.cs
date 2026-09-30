using System.Globalization;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Dependinator.UI.Shared.VsCode;
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
    IDialogService dialogService,
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
        bool isShowDemoMessage = false;
        using var t = Timing.Start("InitialShow");
        await screenService.CheckResizeAsync();
        // In test mode always load the embedded demo model for a fast, deterministic
        // model, ignoring any persisted recent/local paths.
        var startupPath = Dependinator.Core.Build.IsTestMode ? DemoModel.Path : recentModelsService.StartupPath;
        if (startupPath is null)
        {
            startupPath = DemoModel.Path;
            isShowDemoMessage = true;
        }

        await LoadAsync(startupPath);

        // Signal that the initial model has loaded and rendered (data-app-ready=true on
        // the body), so UI/e2e tests can wait on it instead of arbitrary timeouts.
        await jSInteropService.Call("setAppReady", true);

        // Let the VS Code extension host know the diagram is ready, so it can reveal the
        // node for the editor that was active when the webview was first opened.
        await vsCodeSendService.NotifyDiagramLoadedAsync();

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

    async Task ShowDemoMessageAsync()
    {
        // Where the user's own models come from differs per host: the VS Code extension parses
        // the workspace solution, the web app shows models synced from VS Code (or hand-drawn
        // design models).
        string ownModelsHint = Dependinator.Core.Build.IsVsCodeExtWasm
            ? "Your workspace's solution is parsed and opened automatically when one is found; "
                + "switch between solutions and models under <b>Menu › Models</b>.<br/><br/>"
            : "To map your own code, install the "
                + "<a href=\"https://marketplace.visualstudio.com/items?itemName=michaelreichenauer.dependinator\" "
                + "target=\"_blank\" rel=\"noopener\">Dependinator VS Code extension</a> and enable device sync "
                + "there and here: your models then appear under <b>Menu › Models</b>. You can also sketch an "
                + "architecture by hand with <b>Menu › Models › New Model</b>.<br/><br/>";

        await dialogService.ShowMessageBoxAsync(
            "Welcome to Dependinator",
            (MarkupString)(
                "You don't have a diagram yet, so a <b>demo diagram</b> has been opened for you to explore.<br/><br/>"
                + "<b>Zoom</b> (scroll or pinch) into a node to see what is inside it, <b>drag</b> to pan, "
                + "<b>click</b> a node for its toolbar and <b>double-click</b> it to zoom to it. "
                + "<b>Ctrl+F</b> finds a node by name.<br/><br/>"
                + ownModelsHint
                + "<b>Menu › Help</b> has the full list of controls."
            ),
            yesText: "Got it"
        );
    }
}
