using System.Globalization;
using System.Text.RegularExpressions;
using Dependinator.UI.Diagrams.Icons;

namespace Dependinator.UI.Diagrams.Svg;

// Assembles a self-contained SVG document around rendered diagram content, for export/download.
// The live canvas keeps styles, arrow markers, and icon defs in the outer #svgcanvas element;
// an exported document must carry its own copies to render standalone (in a browser tab, a wiki
// page, or an <img> rasterized to PNG).
static class SvgExportDocument
{
    // Matches the <use href="#name"> icon references emitted by NodeSvg, to include only the
    // icon defs the content actually uses (the full icon library is far too large to embed).
    static readonly Regex IconReference = new("href=\"#([^\"]+)\"", RegexOptions.Compiled);

    // Creates a complete standalone SVG document with the given content and pixel size.
    // Explicit width/height attributes are required: Firefox refuses to rasterize (drawImage)
    // an SVG image without them.
    public static string Create(string contentSvg, double widthPx, double heightPx, string background)
    {
        var iconDefs = GetUsedIconDefs(contentSvg);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="{widthPx:0.##}" height="{heightPx:0.##}" viewBox="0 0 {widthPx:0.##} {heightPx:0.##}">
              <style>
                {StyleSvg}
              </style>
              <rect width="100%" height="100%" fill="{background}"/>
              <defs>
                {MarkersSvg}
                {iconDefs}
              </defs>
              {contentSvg}
            </svg>
            """
        );
    }

    // The text classes the node/line markup references, copied from the canvas styles in
    // Canvas.razor. The .linkhandle rule is load-bearing: in edit mode the drag-to-link handles
    // are baked into the markup and only hidden by CSS, so without this rule they would show
    // in the exported image. Hover rules are irrelevant in a static image and omitted.
    static string StyleSvg =>
        $$"""
            .linkhandle { display: none; }
            .iconName {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                fill: {{DColors.Text}};
                dominant-baseline: hanging;
                text-anchor: middle;
            }
            .memberName {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                fill: {{DColors.Text}};
                dominant-baseline: middle;
                text-anchor: start;
            }
            .nodeName {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                fill: {{DColors.Text}};
                dominant-baseline: hanging;
                text-anchor: start;
            }
            .iconDescription {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                font-style: italic;
                fill: {{DColors.Text}};
                fill-opacity: 0.7;
                text-anchor: middle;
            }
            .memberDescription {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                font-style: italic;
                fill: {{DColors.Text}};
                fill-opacity: 0.7;
                text-anchor: start;
            }
            .nodeDescription {
                font-family: Verdana, Helvetica, Arial, sans-serif;
                font-style: italic;
                fill: {{DColors.Text}};
                fill-opacity: 0.7;
                text-anchor: start;
            }
            """;

    // The line arrow-head markers, copied from the canvas defs in Canvas.razor.
    static string MarkersSvg =>
        $"""
            <marker id="arrow-line" markerWidth="7" markerHeight="6" refX="7" refY="3" orient="auto">
                <polygon points="0 0, 7 3, 0 6" fill="{DColors.Line}" />
            </marker>
            <marker id="arrow-hidden" markerWidth="7" markerHeight="6" refX="7" refY="3" orient="auto">
                <polygon points="0 0, 7 3, 0 6" fill="{DColors.LineHidden}" />
            </marker>
            <marker id="arrow-direct" markerWidth="7" markerHeight="6" refX="7" refY="3" orient="auto">
                <polygon points="0 0, 7 3, 0 6" fill="{DColors.DirectLine}" />
            </marker>
            <marker id="arrow-cousin" markerWidth="7" markerHeight="6" refX="7" refY="3" orient="auto">
                <polygon points="0 0, 7 3, 0 6" fill="{DColors.CousinLine}" />
            </marker>
            <marker id="arrow-inheritance" markerWidth="15" markerHeight="12" refX="0" refY="6" orient="auto">
                <polygon points="0.5 0.5, 14.5 6, 0.5 11.5" fill="none" stroke="{DColors.Line}" stroke-width="1" />
            </marker>
            """;

    // The icon defs for the icons the content references. Each icon svg carries its own gradient
    // defs (tint variants have '--Color'-suffixed ids), so including the whole icon svg brings
    // its paint servers along. Unknown names are skipped rather than resolved via IconLibrary.Get,
    // whose Module fallback has an id that would not match the reference anyway.
    static string GetUsedIconDefs(string contentSvg)
    {
        var names = IconReference
            .Matches(contentSvg)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Where(IconLibrary.Contains);
        return names.Select(IconLibrary.Get).Join("\n");
    }
}
