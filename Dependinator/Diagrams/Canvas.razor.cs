using Dependinator.DiagramIcons;
using Dependinator.Shared;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;


namespace Dependinator.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/
partial class Canvas : ComponentBase, IUIComponent
{
    [Inject] ICanvasService srv { get; init; } = null!;
    [Inject] IMouseEventService mouseEventService { get; init; } = null!;
    [Inject] IJSInteropService jSInteropService { get; init; } = null!;
    [Inject] IApplicationEvents applicationEvents { get; init; } = null!;
    [Inject] IDatabase database { get; init; } = null!;
    [Inject] IInitService initService { get; init; } = null!;

    public ElementReference dropZoneElement { get; private set; }
    public InputFile inputFile { get; private set; } = null!;
    public ElementReference Ref { get; private set; }

    // string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#######}), Level: {srv.LevelNbr}, " +
    //     $"LevelZoom: {srv.LevelZoom:E2}, LevelViewBox: {srv.LevelViewBox}, SvgZoom: {srv.Zoom / srv.LevelZoom:0.###} " +
    //     $"SvgOffset: {srv.Offset}, SvgRect:{srv.SvgRect} SvgViewBox: {srv.SvgViewBox},";


    //string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#######}), SvgKey: {srv.TileKeyText}, Offset: {srv.Offset}, TileViewBox: {srv.TileViewBox}, SvgViewBox: {srv.SvgViewBox}, Svg: {srv.SvgRect}";

    // string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#########}), SvgKey: {srv.TileKeyText}";
    string Info => $"{1 / srv.Zoom * 100:#}%, L: {-(int)Math.Ceiling(Math.Log(srv.Zoom) / Math.Log(7)) + 1}";
    string DiagramName => srv.DiagramName;

    string Content => srv.SvgContent;
    double Width => srv.SvgRect.Width;
    double Height => srv.SvgRect.Height;
    string ViewBox => srv.SvgViewBox;
    static string IconDefs => Icon.IconDefs;

    string Cursor => srv.Cursor;
    IReadOnlyList<string> RecentModelPaths => srv.RecentModelPaths;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await initService.InitAsync(this);

            await this.jSInteropService.InitializeFileDropZone(dropZoneElement, inputFile.Element);

            applicationEvents.UIStateChanged += () => InvokeAsync(StateHasChanged);
            await InvokeAsync(srv.InitialShow);
        }
    }


    protected async void LoadFiles(InputFileChangeEventArgs e)
    {
        await srv.LoadFilesAsync(e.GetMultipleFiles(1000));
    }
}

