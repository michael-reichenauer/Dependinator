using Dependinator.Icons;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Dependinator.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/



partial class Canvas : ComponentBase, IUIComponent
{
    [Inject] ICanvasService srv { get; init; } = null!;
    [Inject] IMouseEventService mouseEventService { get; init; } = null!;
    [Inject] IJSInteropService jSInteropService { get; init; } = null!;

    public ElementReference Ref { get; private set; }

    string Info => $"Zoom: {srv.Zoom:0.#######}, SvgZoom: {srv.SvgZoom:E2}, ViewZoom: {srv.ActualZoom:0.###} Level: {srv.Level}";
    string SvgContent => srv.SvgContent;
    double Width => srv.SvgRect.Width;
    double Height => srv.SvgRect.Height;
    string ViewBox => srv.ViewBox;
    DotNetObjectReference<Canvas> objRef = null!;

    static string IconDefs => Icon.IconDefs;

    protected override void OnInitialized()
    {
        objRef = DotNetObjectReference.Create(this);
    }

    // void OnMouse(MouseEventArgs e) => mouseEventService.OnMouse(e);

    public Task TriggerStateHasChangedAsync() => InvokeAsync(StateHasChanged);


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await srv.InitAsync(this);
            await this.jSInteropService.InitializeAsync();
            await mouseEventService.InitAsync(this);
            await InvokeAsync(srv.InitialShow);
        }
    }
}

