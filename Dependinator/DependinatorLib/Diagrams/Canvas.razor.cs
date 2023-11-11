using Dependinator.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/

partial class Canvas : ComponentBase
{
    [Inject] ICanvasService srv { get; init; } = null!;

    public ElementReference Ref { get; private set; }

    string Info => $"Zoom: {srv.Zoom:E2}, Level: {srv.Level}, ViewBox: ({srv.Offset.X:0.##} {srv.Offset.Y:0.##} {srv.SvgRect.Width * srv.Zoom:0.##} {srv.SvgRect.Height * srv.Zoom:0.##})";
    string SvgContent => srv.SvgContent;
    double Width => srv.SvgRect.Width;
    double Height => srv.SvgRect.Height;
    string viewBox => $"{srv.Offset.X} {srv.Offset.Y} {srv.SvgRect.Width * srv.Zoom} {srv.SvgRect.Height * srv.Zoom}";

    static string IconDefs => Icon.IconDefs;

    void OnMouse(MouseEventArgs e) => srv.OnMouse(e);

    public Task TriggerStateHasChangedAsync() => InvokeAsync(StateHasChanged);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await srv.InitAsync(this);
            await InvokeAsync(srv.InitialShow);
        }
    }
}

