using System.Globalization;
using Dependinator.UI.Diagrams.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Dependinator.UI.Diagrams;

// https://css-tricks.com/use-and-reuse-everything-in-svg-even-animations/
partial class Canvas : ComponentBase, IUIComponent, IDisposable
{
    [Inject]
    ICanvasService srv { get; init; } = null!;

    [Inject]
    IJSInterop jSInterop { get; init; } = null!;

    [Inject]
    IApplicationEvents applicationEvents { get; init; } = null!;

    [Inject]
    IInitService initService { get; init; } = null!;

    [Inject]
    Editing.IManualEditService manualEditService { get; init; } = null!;

    [Inject]
    Interaction.IAreaSelectionService areaSelectionService { get; init; } = null!;

    [Inject]
    IIconDefsService iconDefsService { get; init; } = null!;

    [Inject]
    IViewOptions viewOptions { get; init; } = null!;

    [Inject]
    Tooltips.ITooltipService tooltipService { get; init; } = null!;

    DotNetObjectReference<Canvas>? selfReference;

    public ElementReference dropZoneElement { get; private set; }
    public InputFile inputFile { get; private set; } = null!;
    public ElementReference Ref { get; private set; }

    string Content => srv.SvgContent;

    // Formatted invariantly; a raw double attribute value would be rendered with the current
    // culture, which breaks the SVG in comma-decimal locales.
    string Width => string.Create(CultureInfo.InvariantCulture, $"{srv.SvgRect.Width:0.##}");
    string Height => string.Create(CultureInfo.InvariantCulture, $"{srv.SvgRect.Height:0.##}");
    string ViewBox => srv.SvgViewBox;
    string IconDefs => iconDefsService.Defs;

    string Cursor => srv.Cursor;

    // Line strengths: with soft lines, resting lines are faint, while the selection's own lines
    // (line-lit) and the lines that mean something (explorer and direct, cycle, path, rule) stay
    // at full strength; dimmed lines drop further so the selection still stands out. The hover
    // preview (linePreview in jsInterop.js) lights a node's lines and fades the rest to
    // --line-preview-dim, the dimmed strength (or the resting one with Dim Unrelated Lines off).
    const double SoftOpacity = 0.4;
    const double SoftDimOpacity = 0.1;
    const double NormalDimOpacity = 0.18;

    string LineCss
    {
        get
        {
            var soft = viewOptions.IsSoftLines;
            var dim = soft ? SoftDimOpacity : NormalDimOpacity;
            var previewDim =
                viewOptions.DimUnrelatedLines ? dim
                : soft ? SoftOpacity
                : 1;
            var css = soft
                ? $".line-vis {{ opacity: {Px(SoftOpacity)}; }} "
                    + ".line-lit, .line-accent, .line-cycle, .line-path, .line-rule { opacity: 1; } "
                : "";
            // Calls inside a type (LineSvg.IsInnerMemberLine) show only when lit or hover-previewed.
            return css
                + $".line-dim {{ opacity: {Px(dim)}; }} #svgcanvas {{ --line-preview-dim: {Px(previewDim)}; }} "
                + ".line-inner:not(.line-lit) { opacity: 0; }";
        }
    }

    string renderedContent = "";

    static string Px(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.##}");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await initService.InitAsync(this);

            await jSInterop.Call("initializeFileDropZone", dropZoneElement, inputFile.Element);
            await jSInterop.Call("initLinePreview", "svgcanvas");
            selfReference = jSInterop.Reference(this);
            await jSInterop.Call("initTooltips", "svgcanvas", selfReference);

            applicationEvents.UIStateChanged += OnUiStateChanged;
            await InvokeAsync(srv.InitialShowAsync);
        }

        if (!ReferenceEquals(renderedContent, Content))
        {
            renderedContent = Content;
            await jSInterop.Call("refreshSvgPaintServers", "svgcanvas");
        }
    }

    void OnUiStateChanged() => InvokeAsync(StateHasChanged);

    // The hover card's content (initTooltips in jsInterop.js asks when the mouse rests on an item).
    [JSInvokable]
    public string? GetTooltipHtml(string elementId)
    {
        try
        {
            return tooltipService.GetHtml(elementId);
        }
        catch (Exception e)
        {
            // A tooltip is never worth an error in the UI; the card just does not show.
            Log.Warn($"No tooltip for {elementId}: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        applicationEvents.UIStateChanged -= OnUiStateChanged;
        selfReference?.Dispose();
    }

    protected async Task LoadFiles(InputFileChangeEventArgs e)
    {
        var files = e.GetMultipleFiles(1000);
        if (!files.Any())
            return;

        await srv.LoadFilesAsync(files);
    }
}
