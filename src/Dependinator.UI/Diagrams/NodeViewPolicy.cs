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

    // Beyond this zoom a node's nested svg viewport coordinates (± zoom * node extent) get so
    // large that browsers' single-precision transform math displaces the whole subtree by
    // millions of pixels, leaving the view blank at deep zoom. Such nodes are far larger than
    // the screen, so clipping no longer matters and they render flattened into the parent
    // viewport instead.
    const double MaxNestedViewportZoom = 1000;

    // Up to this zoom a container's background is drawn at full strength; beyond it the fill
    // fades out (see ContainerBackgroundOpacity).
    const double BackgroundFadeStartZoom = 2 * MinContainerZoom;

    public static bool IsTooLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    // Fill opacity of a container's background at the node's own zoom. The tint reads as "where
    // you are" when the box has just opened, then gives way to the children's own backgrounds as
    // the view zooms further in. Zoom is multiplicative, so the fade runs in log space (each
    // doubling removes the same share), and it reaches zero exactly at MaxNodeZoom, where the
    // chrome is dropped altogether, so that cut is invisible.
    public static double ContainerBackgroundOpacity(double zoom)
    {
        if (zoom <= BackgroundFadeStartZoom)
            return 1;
        if (zoom >= MaxNodeZoom)
            return 0;
        return 1 - Math.Log(zoom / BackgroundFadeStartZoom) / Math.Log(MaxNodeZoom / BackgroundFadeStartZoom);
    }

    public static bool IsRenderedFlat(double zoom) => zoom > MaxNestedViewportZoom;

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
