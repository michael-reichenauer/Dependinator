using Dependinator.Icons;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components;

namespace Dependinator.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/
partial class Canvas : ComponentBase, IUIComponent
{
    [Inject] ICanvasService srv { get; init; } = null!;
    [Inject] IMouseEventService mouseEventService { get; init; } = null!;
    [Inject] IJSInteropService jSInteropService { get; init; } = null!;
    [Inject] IUIService uiService { get; init; } = null!;

    public ElementReference Ref { get; private set; }

    // string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#######}), Level: {srv.LevelNbr}, " +
    //     $"LevelZoom: {srv.LevelZoom:E2}, LevelViewBox: {srv.LevelViewBox}, SvgZoom: {srv.Zoom / srv.LevelZoom:0.###} " +
    //     $"SvgOffset: {srv.Offset}, SvgRect:{srv.SvgRect} SvgViewBox: {srv.SvgViewBox},";


    //string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#######}), SvgKey: {srv.TileKeyText}, Offset: {srv.Offset}, TileViewBox: {srv.TileViewBox}, SvgViewBox: {srv.SvgViewBox}, Svg: {srv.SvgRect}";

    // string Info => $"Zoom: {1 / srv.Zoom * 100:#}% ({srv.Zoom:0.#########}), SvgKey: {srv.TileKeyText}";
    string Info => $"Zoom: {1 / srv.Zoom * 100:#}%, {1 / srv.Zoom:0.#}x, L: {-(int)Math.Ceiling(Math.Log(srv.Zoom) / Math.Log(7)) + 1}";

    string Content => srv.SvgContent;
    double Width => srv.SvgRect.Width;
    double Height => srv.SvgRect.Height;
    string ViewBox => srv.SvgViewBox;
    static string IconDefs => Icon.IconDefs;

    string Cursor => srv.Cursor;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            uiService.OnUIStateChange += () => InvokeAsync(StateHasChanged);
            await srv.InitAsync(this);
            await this.jSInteropService.InitializeAsync(); // must be after srv.InitAsync, since triggered events need Ref
            await mouseEventService.InitAsync();
            await InvokeAsync(srv.InitialShow);
        }
    }
}

