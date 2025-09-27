using Dependinator.Diagrams.Icons;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/
partial class Canvas : ComponentBase, IUIComponent
{
    [Inject]
    ICanvasService srv { get; init; } = null!;

    [Inject]
    IJSInterop jSInterop { get; init; } = null!;

    [Inject]
    IApplicationEvents applicationEvents { get; init; } = null!;

    [Inject]
    IInitService initService { get; init; } = null!;

    public ElementReference dropZoneElement { get; private set; }
    public InputFile inputFile { get; private set; } = null!;
    public ElementReference Ref { get; private set; }

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

            await jSInterop.Call("initializeFileDropZone", dropZoneElement, inputFile.Element);

            applicationEvents.UIStateChanged += () => InvokeAsync(StateHasChanged);
            await InvokeAsync(srv.InitialShow);
        }
    }

    protected async void LoadFiles(InputFileChangeEventArgs e)
    {
        var files = e.GetMultipleFiles(1000);
        if (!files.Any())
            return;

        await srv.LoadFilesAsync(files);
    }
}
