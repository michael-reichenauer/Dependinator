using Microsoft.AspNetCore.Components;

namespace Dependinator.Diagrams;

partial class Canvas : ComponentBase
{
    [Inject] ICanvasService srv { get; init; } = null!;
    [Inject] IPanZoomService pzs { get; init; } = null!;

    public ElementReference Ref { get; protected set; }

    public string SvgContent { get; set; } = "";


    public Task TriggerStateHasChangedAsync()
    {
        Log.Info("TriggerStateHasChangedAsync");
        return InvokeAsync(() => { StateHasChanged(); });
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Log.Info("Canvas.OnInitialized");
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            Log.Info("OnAfterRenderAsync: FirstRender");
            await pzs.InitAsync(this);
            await srv.InitAsync(this);
        }
    }
}

