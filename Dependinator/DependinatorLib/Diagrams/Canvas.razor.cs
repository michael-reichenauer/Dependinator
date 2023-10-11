using Microsoft.AspNetCore.Components;

namespace Dependinator.Diagrams;

partial class Canvas : ComponentBase
{
    [Inject] ICanvasService srv { get; init; } = null!;
    [Inject] IPanZoomService pzs { get; init; } = null!;

    public ElementReference Ref { get; protected set; }


    public void TriggerStateHasChanged()
    {
        InvokeAsync(() => { StateHasChanged(); });
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Add initialization logic here
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Add parameter set logic here
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await pzs.InitAsync(this);
            await srv.InitAsync(this);
        }
    }
}

