using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// How a node is presented at a given zoom level: as an icon, as an expanded container with
// visible children, or not at all because the view has zoomed in past it.
static class NodeViewPolicy
{
    // Beyond this zoom a node's own chrome (border, name) is too large to be seen; only its
    // children remain meaningful.
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom;
    const double MinContainerZoom = 2.0;

    public static bool IsTooLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public static bool IsShowIcon(Parsing.NodeType nodeType, double zoom) =>
        nodeType.IsMember || zoom <= MinContainerZoom;

    // The node is shown as an expanded box with visible children (not an icon and not zoomed
    // past visibility).
    public static bool IsContainerView(Node node, double modelZoom)
    {
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !IsTooLargeToBeSeen(nodeZoom) && !IsShowIcon(node.Type, nodeZoom);
    }

    // The node's children are rendered at this zoom: pass-through nodes always show children,
    // icons and members never do, and containers do even when zoomed in past their own chrome.
    public static bool IsChildrenShown(Node node, double modelZoom)
    {
        if (node.IsPassThrough)
            return true;
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !IsShowIcon(node.Type, nodeZoom);
    }
}
