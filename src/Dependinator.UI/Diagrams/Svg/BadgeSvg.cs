using System.Globalization;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// The small marks drawn on a node: its problem count, its hidden children's count, and the
// padlock of a private member. None takes pointer events, so they never get in the way.
namespace Dependinator.UI.Diagrams.Svg;

static class BadgeSvg
{
    // The icon symbols leave a margin inside their box, so a badge on the box's corner would
    // float beside the icon; this is about that margin, as a part of the box.
    const double IconSymbolMargin = 0.12;

    internal static Rect VisibleIconRect(Rect iconRect) =>
        new(
            iconRect.X + iconRect.Width * IconSymbolMargin,
            iconRect.Y + iconRect.Height * IconSymbolMargin,
            iconRect.Width * (1 - 2 * IconSymbolMargin),
            iconRect.Height * (1 - 2 * IconSymbolMargin)
        );

    const double BadgeMaxRadius = 7; // At text zoom 1; follows the text size like the names
    const double BadgeMinRadius = 2.5; // Smaller than this it cannot be read, so it is left out
    const double BadgeWidthFactor = 0.15; // Of the node's width, so it never covers a small node

    // The number of problems in or inside a node, on its top-right corner: red when compile
    // errors are among them, orange otherwise. It lets pointer events through to the node, so it
    // never gets in the way of selecting, dragging or hovering it.
    internal static string Problem(NodeProblems? problems, Rect nodeRect, double textZoom)
    {
        if (problems is not { } p || p.Errors + p.Warnings == 0)
            return "";
        var radius = Math.Min(nodeRect.Width * BadgeWidthFactor, BadgeMaxRadius * textZoom);
        if (radius < BadgeMinRadius)
            return "";

        var count = p.Errors + p.Warnings;
        var text = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        var fontSize = radius * (text.Length > 2 ? 0.85 : 1.15);
        var fill = p.Errors > 0 ? DColors.ProblemError : DColors.ProblemWarning;
        var (cx, cy) = (nodeRect.X + nodeRect.Width, nodeRect.Y);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<g class="problemBadge" data-errors="{p.Errors}" data-warnings="{p.Warnings}" pointer-events="none"><circle cx="{cx:0.##}" cy="{cy:0.##}" r="{radius:0.##}" fill="{fill}" stroke="{DColors.CanvasBackground}" stroke-width="{radius * 0.15:0.##}"/><text x="{cx:0.##}" y="{cy:0.##}" font-size="{fontSize:0.##}px" fill="#FFFFFF" font-weight="bold" text-anchor="middle" dominant-baseline="central">{text}</text></g>"""
        );
    }

    // A private member's mark: a small padlock on the lower right corner of its icon, on a disc of
    // the card's background so it reads over the icon (like private members in an IDE). Drawn
    // right after the icon, under the row's hover and selection.
    internal static string Private(Rect icon, string nodeOpacity)
    {
        var s = icon.Width;
        // Inside the row (the summary card's rows are only a little taller than the icon).
        var (cx, cy, r) = (icon.X + s * 0.88, icon.Y + s * 0.78, s * 0.42);
        var (w, h) = (r, r * 0.8);
        var lockColor = DColors.PrivateLock;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<g class="privateBadge" pointer-events="none" {nodeOpacity}><circle cx="{cx:0.##}" cy="{cy:0.##}" r="{r:0.##}" fill="{DColors.CardBackground}"/><path d="M{cx - w * 0.32:0.##},{cy - h * 0.15:0.##} v{-h * 0.35:0.##} a{w * 0.32:0.##},{w * 0.32:0.##} 0 0 1 {w * 0.64:0.##},0 v{h * 0.35:0.##}" fill="none" stroke="{lockColor}" stroke-width="{s * 0.08:0.##}"/><rect x="{cx - w / 2:0.##}" y="{cy - h * 0.15:0.##}" width="{w:0.##}" height="{h * 0.75:0.##}" rx="{s * 0.04:0.##}" fill="{lockColor}"/></g>"""
        );
    }

    // How many children the user hid, on the node's top-left corner (the problem badge has the
    // top-right): a grey circle with the count, so the hidden nodes are not forgotten. Like the
    // problem badge it lets pointer events through to the node.
    internal static string Hidden(int count, Rect nodeRect, double textZoom)
    {
        if (count == 0)
            return "";
        var radius = Math.Min(nodeRect.Width * BadgeWidthFactor, BadgeMaxRadius * textZoom);
        if (radius < BadgeMinRadius)
            return "";

        var text = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        var fontSize = radius * (text.Length > 2 ? 0.85 : 1.15);
        var (cx, cy) = (nodeRect.X, nodeRect.Y);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<g class="hiddenBadge" data-count="{count}" pointer-events="none"><circle cx="{cx:0.##}" cy="{cy:0.##}" r="{radius:0.##}" fill="{DColors.HiddenBadge}" stroke="{DColors.CanvasBackground}" stroke-width="{radius * 0.15:0.##}"/><text x="{cx:0.##}" y="{cy:0.##}" font-size="{fontSize:0.##}px" fill="#FFFFFF" font-weight="bold" text-anchor="middle" dominant-baseline="central">{text}</text></g>"""
        );
    }
}
