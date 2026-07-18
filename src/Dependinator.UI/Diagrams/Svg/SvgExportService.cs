using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

// Produces complete standalone SVG documents of a diagram area, for export as an image file
// (or rasterization to PNG).
interface ISvgExportService
{
    // A self-contained SVG document showing canvasRect (canvas coordinates) at zoom (canvas
    // units per output pixel); the document's pixel size is canvasRect size / zoom.
    string GetSvgDocument(Rect canvasRect, double zoom);
}

[Transient]
class SvgExportService(ISvgService svgService) : ISvgExportService
{
    public string GetSvgDocument(Rect canvasRect, double zoom)
    {
        var contentSvg = svgService.GetContentSvg(canvasRect, zoom);
        var widthPx = canvasRect.Width / zoom;
        var heightPx = canvasRect.Height / zoom;
        return SvgExportDocument.Create(contentSvg, widthPx, heightPx, DColors.CanvasBackground);
    }
}
