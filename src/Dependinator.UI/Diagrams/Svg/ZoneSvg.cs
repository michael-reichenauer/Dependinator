using System.Globalization;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

// Renders a zone: a user-drawn, labeled background area that groups the nodes drawn over it
// ("Edge", "Data", a subnet). A zone is a Node with IsZone set, so selection, dragging, resizing
// and deletion flow through the normal node pipeline, but it is drawn as a translucent rounded
// rectangle with its label in the top-left corner and no node chrome: no icon, no link handle,
// no pencil marker (a zone is always user-drawn). SvgService emits zones before their siblings
// and the sibling lines, and a selected zone's outline and handles after them, so the zone
// reads as a background and its selection chrome stays visible.
static class ZoneSvg
{
    // A constant tint, unlike container fills that fade with zoom: a zone is background at
    // every zoom.
    const double FillOpacity = 0.35;
    const double CornerRadius = 6;
    const double LabelInset = 4;
    const double SubtitleFontFactor = 0.75;

    public static string GetZoneSvg(Node node, Rect canvasRect, double zoom)
    {
        var (border, background) = NodeSvg.NodeColors(node);
        var textZoom = NodeSvg.TextZoom(zoom);
        var fontSize = NodeSvg.FontSize * textZoom;
        var subtitleFontSize = fontSize * SubtitleFontFactor;
        var strokeWidth = Math.Max(0.5, textZoom);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (nodeOpacity, textOpacity) = NodeSvg.HiddenAttributes(node);

        var x = canvasRect.X;
        var y = canvasRect.Y;
        var labelX = x + LabelInset * textZoom;
        var labelY = y + LabelInset * textZoom;
        var subtitleY = labelY + fontSize * 1.35;

        // The label is the zone's display name (its alias; the identity name for zones made
        // before labels became aliases).
        var title = string.IsNullOrWhiteSpace(node.HtmlDescription)
            ? node.HtmlDisplayName
            : $"{node.HtmlDisplayName}\n\n{node.HtmlDescription}";

        // The visible rect is the hover/hit target (its id resolves to the node); the texts sit
        // outside the group with pointer-events="none" so hovering does not stroke them.
        var subtitleSvg = string.IsNullOrWhiteSpace(node.HtmlDescription)
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"""<text class="zoneDescription" x="{labelX:0.##}" y="{subtitleY:0.##}" font-size="{subtitleFontSize:0.##}px" {textOpacity}>{node.HtmlDescription}</text>"""
            );

        return string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <g class="hoverable" id="{elementId}" {nodeOpacity}>
                  <rect id="{elementId}" x="{x:0.##}" y="{y:0.##}" width="{canvasRect.Width:0.##}" height="{canvasRect.Height:0.##}" rx="{CornerRadius
                    * textZoom:0.##}" fill="{background}" fill-opacity="{FillOpacity}" stroke="{border}" stroke-width="{strokeWidth:0.##}" stroke-dasharray="{6
                    * textZoom:0.##},{4 * textZoom:0.##}"/>
                  <title>{title}</title>
                </g>
                <text class="zoneName" x="{labelX:0.##}" y="{labelY:0.##}" font-size="{fontSize:0.##}px" font-weight="bold" {textOpacity}>{node.HtmlDisplayName}</text>
                {subtitleSvg}
                """
            )
            .Trim();
    }
}
