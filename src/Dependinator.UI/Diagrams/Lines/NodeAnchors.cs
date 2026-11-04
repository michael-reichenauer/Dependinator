using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Lines;

enum LineAnchorRole
{
    Source,
    Target,
}

enum AnchorPreference
{
    Default,
    Left,
    Right,
    Top,
    Bottom,
}

// Where lines attach to a node, in the node's local (unzoomed) coordinates. Member rows anchor at
// the vertical middle of the row, like the rows of a class box: lines leave at the row's right
// edge (the column's) and enter at its left edge, before the icon; other nodes anchor at the
// vertical middle of their left/right edge.
static class NodeAnchors
{
    // Small gap between a member icon's edge and the line endpoint, so arrow heads and line
    // caps are not drawn directly on top of the icon.
    const double MemberIconGap = 0.5;

    public static (double X, double Y) GetLineAnchor(
        Node node,
        LineAnchorRole role,
        AnchorPreference preference = AnchorPreference.Default
    )
    {
        if (node.Type.IsMember)
        {
            var metrics = GetMemberAnchorMetrics(node);
            if (role == LineAnchorRole.Source)
            {
                return preference switch
                {
                    AnchorPreference.Left => (metrics.Left - MemberIconGap, metrics.CenterY),
                    _ => (metrics.RowRight + MemberIconGap, metrics.CenterY),
                };
            }

            return preference switch
            {
                AnchorPreference.Right => (metrics.RowRight + MemberIconGap, metrics.CenterY),
                _ => (metrics.Left - MemberIconGap, metrics.CenterY),
            };
        }

        var boundary = node.Boundary;
        var centerX = boundary.X + boundary.Width / 2.0;
        var centerY = boundary.Y + boundary.Height / 2.0;

        if (role == LineAnchorRole.Source)
        {
            return preference switch
            {
                AnchorPreference.Left => (boundary.X, centerY),
                AnchorPreference.Top => (centerX, boundary.Y),
                _ => (boundary.X + boundary.Width, centerY),
            };
        }

        return preference switch
        {
            AnchorPreference.Right => (boundary.X + boundary.Width, centerY),
            AnchorPreference.Bottom => (centerX, boundary.Y + boundary.Height),
            _ => (boundary.X, centerY),
        };
    }

    static MemberAnchorMetrics GetMemberAnchorMetrics(Node node)
    {
        // The member icon is drawn at the row's left edge, its name vertically centered in the
        // row (see NodeSvg.CalculateMemberNodeLayout); the row is as wide as its card column.
        var boundary = node.Boundary;
        return new MemberAnchorMetrics(boundary.X, boundary.X + boundary.Width, boundary.Y + boundary.Height / 2.0);
    }

    readonly record struct MemberAnchorMetrics(double Left, double RowRight, double CenterY);
}
