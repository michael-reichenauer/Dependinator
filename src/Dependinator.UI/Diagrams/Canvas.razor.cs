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

    [Inject]
    Interaction.IHighlightService highlightService { get; init; } = null!;

    // A go to's brackets pulse for a moment when the view lands (dep-highlight in the style below).
    string? SvgClass => highlightService.IsPulsing ? "dep-highlight-pulsing" : null;

    DotNetObjectReference<Canvas>? selfReference;
    bool isDisposed;

    // Names this canvas's hover card listeners in JS, so a late dispose leaves a newer canvas's alone.
    static int lastTooltipToken;
    readonly int tooltipToken = Interlocked.Increment(ref lastTooltipToken);

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
    const double SoftLabelOpacity = 0.75;
    const double SoftLabelDimOpacity = 0.3;

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
                    // A label is text to read, so it rests stronger than its faint line and is
                    // still readable when dimmed.
                    + $".line-vis.line-text {{ opacity: {Px(SoftLabelOpacity)}; }} .line-text.line-lit {{ opacity: 1; }} "
                    + $".line-text.line-dim {{ opacity: {Px(SoftLabelDimOpacity)}; }} "
                : "";
            // In edit mode a line's label can be dragged along its line (LineSvg.LabelHitSvg).
            if (viewOptions.IsEditingEnabled)
                css += $".{Svg.LineSvg.LabelHitClass} {{ cursor: move; }} ";
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
            // Disposed while initializing: a newer canvas owns the hover card's listeners.
            if (isDisposed)
                return;
            selfReference = jSInterop.Reference(this);
            await jSInterop.Call("initTooltips", "svgcanvas", selfReference, tooltipToken);
            if (isDisposed)
                return;

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
        isDisposed = true;
        applicationEvents.UIStateChanged -= OnUiStateChanged;
        if (selfReference is not null)
        {
            // Drops the hover card's listeners if they are still this canvas's (a newer canvas
            // may have replaced them); fire and forget, a gone circuit has none left.
            _ = jSInterop.Call("disposeTooltips", tooltipToken).AsTask();
            selfReference.Dispose();
        }
    }

    protected async Task LoadFiles(InputFileChangeEventArgs e)
    {
        var files = e.GetMultipleFiles(1000);
        if (!files.Any())
            return;

        await srv.LoadFilesAsync(files);
    }
}
