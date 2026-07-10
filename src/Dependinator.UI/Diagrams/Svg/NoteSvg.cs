using System.Web;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using static System.FormattableString;

namespace Dependinator.UI.Diagrams.Svg;

// Renders a note annotation: a small filled circle showing the note's short id (e.g. "1", "A")
// with the description exposed as a native SVG <title> tooltip on hover. A note is a Node with
// IsNote set, placed on the root canvas; it reuses the normal node id scheme so selection,
// dragging and deletion flow through the existing interaction pipeline.
static class NoteSvg
{
    // The note stops growing once the render zoom passes this factor, so zooming in keeps the
    // marker a readable, fixed on-screen size instead of an ever-larger circle.
    const double MaxNoteZoom = 1.0;

    public static string GetNoteSvg(Node node, Rect canvasRect, double zoom)
    {
        var cx = canvasRect.X + canvasRect.Width / 2;
        var cy = canvasRect.Y + canvasRect.Height / 2;

        // canvasRect is the note's boundary scaled by the render zoom; cap that growth beyond
        // MaxNoteZoom while keeping the note centred at its (unclamped) position.
        var naturalR = Math.Min(canvasRect.Width, canvasRect.Height) / 2;
        var r = zoom > MaxNoteZoom ? naturalR * MaxNoteZoom / zoom : naturalR;

        var id = node.HtmlShortName;
        var fontSize = FitFontSize(node.ShortName, r);
        var strokeWidth = Math.Max(0.5, r * 0.06);
        var elementId = PointerId.FromNode(node.Id).ElementId;

        // Show the description on hover; fall back to the id so hovering an unlabelled note still
        // gives feedback. HtmlDescription is kept in sync by Node.SetDescription.
        var title = string.IsNullOrWhiteSpace(node.HtmlDescription) ? id : node.HtmlDescription;

        // The visible circle is the hover/hit target (id resolves to the node); the id text is drawn
        // on top with pointer-events="none" so it is not stroked by the .hoverable:hover rule.
        return Invariant(
                $"""
                <g class="hoverable" id="{elementId}">
                  <circle id="{elementId}" cx="{cx:0.##}" cy="{cy:0.##}" r="{r:0.##}" fill="{DColors.NoteFill}" stroke="{DColors.NoteBorder}" stroke-width="{strokeWidth:0.##}" />
                  <title>{title}</title>
                </g>
                <text x="{cx:0.##}" y="{cy:0.##}" text-anchor="middle" dominant-baseline="central" font-family="Verdana, Helvetica, Arial, sans-serif" font-weight="bold" font-size="{fontSize:0.##}px" fill="{DColors.NoteText}" pointer-events="none">{id}</text>
                {SelectedNoteSvg(node, cx, cy, r)}
                """
            )
            .Trim();
    }

    // Largest font size that keeps the id text (about 2-3 chars) inside the circle, capped so a
    // single character does not fill the whole circle.
    static double FitFontSize(string shortName, double radius)
    {
        var len = Math.Max(1, shortName.Length);
        var maxTextWidth = radius * 1.6; // leave a little padding inside the diameter
        var byWidth = maxTextWidth / (len * NodeSvg.AverageCharWidthFactor);
        return Math.Min(radius * 1.1, byWidth);
    }

    static string SelectedNoteSvg(Node node, double cx, double cy, double r)
    {
        if (!node.IsSelected)
            return "";

        var ringRadius = r + Math.Max(3, r * 0.25);
        return Invariant(
            $"""<circle cx="{cx:0.##}" cy="{cy:0.##}" r="{ringRadius:0.##}" fill="none" stroke="{DColors.Selected}" stroke-width="0.5" stroke-dasharray="5,5" pointer-events="none" />"""
        );
    }
}
