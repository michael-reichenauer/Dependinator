using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Export;

enum ExportFormat
{
    Svg,
    Png,
}

// The options chosen in the export dialog; PngScale is the raster scale factor (ignored for SVG).
record ExportOptions(ExportFormat Format, double PngScale, string FileName);

// Exports a diagram area as a downloadable SVG or PNG image, for embedding in wikis/web pages.
interface IExportService
{
    // Arms area selection; when the user has dragged a rectangle, opens the export dialog.
    Task ExportAreaAsync();

    // Exports exactly the currently visible viewport.
    Task ExportCurrentViewAsync();

    // Exports the whole diagram bounds.
    Task ExportEntireDiagramAsync();
}

[Scoped]
class ExportService(
    IModelMgr modelMgr,
    IScreenService screenService,
    ISvgExportService svgExportService,
    ISelectionService selectionService,
    IAreaSelectionService areaSelectionService,
    IDialogService dialogService,
    ISnackbar snackbar,
    IJSInterop jsInterop
) : IExportService
{
    // Margin around the whole-diagram bounds; matches the fit-to-screen margin (PanZoomService).
    const double Margin = 10;

    // Target pixel size of an entire-diagram export: level of detail matches what fit-to-screen
    // would show on a display this size.
    const double TargetPx = 1920;

    // Rasterizing larger canvases risks running out of memory in the browser.
    const double MaxPixels = 8192;

    public async Task ExportAreaAsync()
    {
        var canvasRect = await areaSelectionService.SelectAreaAsync();
        if (canvasRect is null)
            return;

        var zoom = modelMgr.WithModel(m => m.Zoom);
        await OpenExportDialogAsync(canvasRect, zoom);
    }

    public async Task ExportCurrentViewAsync()
    {
        var svgRect = screenService.SvgRect;
        var (zoom, offset) = modelMgr.WithModel(m => (m.Zoom, m.Offset));
        var canvasRect = new Rect(offset.X, offset.Y, svgRect.Width * zoom, svgRect.Height * zoom);
        await OpenExportDialogAsync(canvasRect, zoom);
    }

    public async Task ExportEntireDiagramAsync()
    {
        var bounds = modelMgr.WithModel(m => m.Root.GetTotalBounds());
        if (bounds == Rect.None)
        {
            snackbar.Add("Nothing to export.", Severity.Info);
            return;
        }

        var canvasRect = new Rect(
            bounds.X - Margin,
            bounds.Y - Margin,
            bounds.Width + 2 * Margin,
            bounds.Height + 2 * Margin
        );

        // Zoom ≥ 1 keeps small diagrams at their natural size instead of blowing them up.
        var zoom = Math.Max(1, Math.Max(canvasRect.Width, canvasRect.Height) / TargetPx);
        await OpenExportDialogAsync(canvasRect, zoom);
    }

    async Task OpenExportDialogAsync(Rect canvasRect, double zoom)
    {
        var widthPx = canvasRect.Width / zoom;
        var heightPx = canvasRect.Height / zoom;

        var parameters = new DialogParameters<ExportDialog>
        {
            { d => d.WidthPx, widthPx },
            { d => d.HeightPx, heightPx },
            { d => d.DefaultFileName, DefaultFileName() },
        };
        var dialog = await dialogService.ShowAsync<ExportDialog>("Export Image", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
            return;

        var options = (ExportOptions)result.Data!;
        var scale = options.Format == ExportFormat.Png ? options.PngScale : 1;
        if (widthPx * scale > MaxPixels || heightPx * scale > MaxPixels)
        {
            snackbar.Add(
                $"The image would be too large ({widthPx * scale:0} × {heightPx * scale:0} px); "
                    + "select a smaller area or a lower scale.",
                Severity.Warning
            );
            return;
        }

        // The selection highlight would otherwise be baked into the exported image.
        selectionService.Unselect();
        var svg = svgExportService.GetSvgDocument(canvasRect, zoom);

        try
        {
            if (options.Format == ExportFormat.Svg)
            {
                await jsInterop.Call("downloadFile", $"{options.FileName}.svg", "image/svg+xml", svg);
                snackbar.Add($"Exported {options.FileName}.svg.", Severity.Success);
            }
            else
            {
                await jsInterop.Call(
                    "downloadSvgAsPng",
                    $"{options.FileName}.png",
                    svg,
                    widthPx,
                    heightPx,
                    options.PngScale
                );
                snackbar.Add($"Exported {options.FileName}.png.", Severity.Success);
            }
        }
        catch (Exception e) when (e is Microsoft.JSInterop.JSException)
        {
            Log.Exception(e, "Failed to export image");
            snackbar.Add("Failed to export the image.", Severity.Error);
        }
    }

    string DefaultFileName()
    {
        var name = Path.GetFileNameWithoutExtension(modelMgr.ModelPath);
        return string.IsNullOrWhiteSpace(name) ? "diagram" : name;
    }
}
