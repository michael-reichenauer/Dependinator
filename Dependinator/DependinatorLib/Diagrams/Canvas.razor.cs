using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

partial class Canvas : ComponentBase
{
    [Inject] ICanvasService srv { get; init; } = null!;

    public ElementReference Ref { get; private set; }

    string Zoom => $"{srv.Zoom}";
    string SvgContent => srv.SvgContent;
    double Width => srv.SvgRect.Width;
    double Height => srv.SvgRect.Height;
    string viewBox => $"{srv.ViewRect.X} {srv.ViewRect.Y} {srv.ViewRect.Width} {srv.ViewRect.Height}";

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

