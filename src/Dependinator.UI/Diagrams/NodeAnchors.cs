using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

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
}

// Where lines attach to a node, in the node's local (unzoomed) coordinates. Member nodes anchor
// on their icon (sources leave at the bottom, targets enter at a side); other nodes anchor at the
// vertical middle of their left/right edge.
static class NodeAnchors
{
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
                return (metrics.CenterX, metrics.Bottom + 0.5);

            return preference switch
            {
                AnchorPreference.Right => (metrics.Right, metrics.CenterY),
                _ => (metrics.Left - 0.5, metrics.CenterY),
            };
        }

        var boundary = node.Boundary;
        var centerY = boundary.Y + boundary.Height / 2.0;

        if (role == LineAnchorRole.Source)
        {
            return preference switch
            {
                AnchorPreference.Left => (boundary.X, centerY),
                _ => (boundary.X + boundary.Width, centerY),
            };
        }

        return preference switch
        {
            AnchorPreference.Right => (boundary.X + boundary.Width, centerY),
            _ => (boundary.X, centerY),
        };
    }

    static MemberAnchorMetrics GetMemberAnchorMetrics(Node node)
    {
        var boundary = node.Boundary;
        // The member icon is drawn at the left edge with size equal to the base font size
        // (see NodeSvg.CalculateMemberNodeLayout).
        var iconSize = (double)NodeSvg.FontSize;
        var left = boundary.X;
        var right = boundary.X + iconSize;
        var centerX = boundary.X + iconSize / 2.0;
        var centerY = boundary.Y + boundary.Height / 2.0;
        var bottom = centerY + iconSize / 2.0;
        return new MemberAnchorMetrics(left, right, centerX, centerY, bottom);
    }

    readonly record struct MemberAnchorMetrics(
        double Left,
        double Right,
        double CenterX,
        double CenterY,
        double Bottom
    );
}
