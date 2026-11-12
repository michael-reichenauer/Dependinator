using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

// How a node is presented at a given zoom level: as an icon, as an expanded container with
// visible children, or not at all because the view has zoomed in past it. The zoom thresholds
// are for a node of ReferenceNodeSize.
// Siblings open together: a node stops being an icon at the zoom where a node of the reference
// size does (MinContainerZoom), whatever its own size, so a container's children are all icons
// or all open and a bigger (more important, see NodeSizing) node is a bigger icon until then.
// Opening each node when it was large enough on screen (Oct 2026) left siblings mixed, icons
// beside open containers, over a 3x zoom range, with nothing that showed why. Only a node far
// outside the layout's sizes (resized by hand) opens by its own size on screen (OpenSize).
// What concerns the node's own chrome (its background fade, being zoomed past) still follows
// its size on screen (EffectiveZoom).
static class NodeViewPolicy
{
    // The default node size (NodeLayout.DefaultSize) the zoom thresholds below are tuned for.
    const double ReferenceNodeSize = 100;

    // "Show node" frames a node at most this large on screen, which keeps it below the size it
    // opens at (OpenSize), so a framed node shows as an icon, also in a tile rendered up to
    // TileKey.ZoomFactor (1.3) larger than the view's zoom.
    const double MaxFramedNodeSize = 150;

    // Beyond this zoom a node's own chrome (border, name) is too large to be seen; only its
    // children remain meaningful.
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom;
    const double MinContainerZoom = 2.0;

    // The sizes on screen (the node's shorter side in pixels) between which a node opens: the
    // layout's sizes (NodeSizing.SideSteps, 60 to 180) open at 120 to 360 pixels, all at
    // MinContainerZoom. A node enlarged by hand opens at the latest at twice the size a default
    // node opens at, instead of staying an ever larger icon, and one shrunk by hand not before
    // half that size, where its children can be told apart.
    const double MinOpenSize = MinContainerZoom * ReferenceNodeSize / 2;
    const double MaxOpenSize = MinContainerZoom * ReferenceNodeSize * 2;

    // Beyond this zoom a node's nested svg viewport coordinates (± zoom * node extent) get so
    // large that browsers' single-precision transform math displaces the whole subtree by
    // millions of pixels, leaving the view blank at deep zoom. Such nodes are far larger than
    // the screen, so clipping no longer matters and they render flattened into the parent
    // viewport instead.
    const double MaxNestedViewportZoom = 1000;

    // Up to this zoom a container's background is drawn at full strength; beyond it the fill
    // fades out (see ContainerBackgroundOpacity). A node that opens larger on screen than a
    // default-size one starts to fade later (BackgroundFadeStart).
    const double BackgroundFadeStartZoom = 2 * MinContainerZoom;

    // The zoom at which the node's own size is drawn, scaled by its size relative to the
    // reference size. The root (a fixed frame), member rows (never containers) and nodes not
    // laid out yet (Rect.None, sized by their parent's coming layout) keep the raw zoom.
    public static double EffectiveZoom(Node node, double zoom) => zoom * Side(node) / ReferenceNodeSize;

    // The node's shorter side, the reference size where the node has no size of its own.
    static double Side(Node node)
    {
        var boundary = node.Boundary;
        if (node.IsRoot || node.Type.IsMember || boundary == Rect.None)
            return ReferenceNodeSize;
        return Math.Min(boundary.Width, boundary.Height);
    }

    // The size on screen (the shorter side in pixels) above which the node is no longer an icon:
    // its size at MinContainerZoom, the zoom all siblings share, within the two limits.
    internal static double OpenSize(Node node) => Math.Clamp(MinContainerZoom * Side(node), MinOpenSize, MaxOpenSize);

    static bool IsPastOpenSize(Node node, double zoom) => zoom * Side(node) > OpenSize(node);

    // Multiplier for the model zoom that "show node" pans to (the node at 1 pixel per unit), so
    // that a node larger than MaxFramedNodeSize is framed smaller, as an icon.
    // A member row is framed the other way, closer: its card's rows must be readable (not the
    // summary card) and its parent open as a container (not an icon), also in a tile rendered
    // TileKey.ZoomFactor (1.3) apart from the view's zoom.
    public static double FramingZoomFactor(Node node)
    {
        if (node.Type.IsMember && node.Parent is { IsRoot: false } parent && parent.Boundary != Rect.None)
        {
            // The parent's size on screen with the member at 1 pixel per unit.
            var parentSize = Math.Min(parent.Boundary.Width, parent.Boundary.Height) / parent.ContainerZoom;
            return Math.Min(1 / TileZoomMargin, parentSize / (FramedParentMargin * OpenSize(parent)));
        }
        var factor = Math.Max(1, Math.Max(node.Boundary.Width, node.Boundary.Height) / MaxFramedNodeSize);
        // Inside a member card node the rows must be readable too, or the parent shows its summary
        // card instead of its children.
        if (node.Parent is { IsRoot: false } cardParent && IsMemberCardNode(cardParent))
            factor = Math.Min(factor, 1 / TileZoomMargin);
        return Math.Min(factor, OpenAncestorsFactor(node));
    }

    // The largest framing factor at which every ancestor of the node is open, since the view
    // would otherwise show an ancestor's icon instead of the node. It matters where the node is
    // large in its parent (a container zoom near 1, as in a small card): the parent opens at
    // MinContainerZoom, later than the node is framed at. Being visible goes before being an
    // icon, so such a node can be framed past its own icon size. Tiles are only rendered closer
    // than the view's zoom, where the ancestors stay open.
    static double OpenAncestorsFactor(Node node)
    {
        var factor = double.MaxValue;
        var scale = 1.0; // The ancestor's units per unit of the node
        for (var ancestor = node.Parent; ancestor is { IsRoot: false }; ancestor = ancestor.Parent)
        {
            scale *= ancestor.ContainerZoom;
            if (ancestor.IsPassThrough || ancestor.Boundary == Rect.None)
                continue; // Always shows its children, or not laid out yet
            // The ancestor's size on screen with the node at 1 pixel per unit, over the size it must have.
            factor = Math.Min(factor, Side(ancestor) / scale / (OpenAncestorMargin * OpenSize(ancestor)));
        }
        return factor;
    }

    const double OpenAncestorMargin = 1.05;

    const double TileZoomMargin = 1.3;

    // A framed member's parent is this much larger than the size it opens at (OpenSize), so it is
    // clearly open after TileZoomMargin.
    const double FramedParentMargin = 1.4;

    public static bool IsTooLargeToBeSeen(Node node, double zoom) => EffectiveZoom(node, zoom) > MaxNodeZoom;

    // Fill opacity of a container's background at the node's own zoom. The tint reads as "where
    // you are" when the box has just opened, then gives way to the children's own backgrounds as
    // the view zooms further in. Zoom is multiplicative, so the fade runs in log space (each
    // doubling removes the same share), and it reaches zero exactly at MaxNodeZoom, where the
    // chrome is dropped altogether, so that cut is invisible.
    public static double ContainerBackgroundOpacity(Node node, double zoom) =>
        ContainerBackgroundOpacity(EffectiveZoom(node, zoom), BackgroundFadeStart(node));

    // The effective zoom at which the node's background starts to fade: twice the zoom it opens
    // at, so a large node, which opens later, also keeps its full tint for a doubling of the zoom.
    static double BackgroundFadeStart(Node node) =>
        Math.Max(BackgroundFadeStartZoom, 2 * OpenSize(node) / ReferenceNodeSize);

    static double ContainerBackgroundOpacity(double zoom, double fadeStartZoom)
    {
        if (zoom <= fadeStartZoom)
            return 1;
        if (zoom >= MaxNodeZoom)
            return 0;
        return 1 - Math.Log(zoom / fadeStartZoom) / Math.Log(MaxNodeZoom / fadeStartZoom);
    }

    public static bool IsRenderedFlat(double zoom) => zoom > MaxNestedViewportZoom;

    public static bool IsShowIcon(Node node, double zoom) =>
        node.Type.IsMember || !IsPastOpenSize(node, zoom) || IsMemberSummary(node, zoom);

    // Member rows are shown once their names are this large on screen (the 8-unit member names
    // at 8 px, the size the summary card's rows have); until then a member card container shows its summary card instead.
    const double MinMemberRowZoom = 1.0;

    // The node shows its summary card (MemberCardSvg.GetMemberSummarySvg): it is past its icon size, but
    // its member rows would still be too small to read. It acts as an icon (lines attach to it, a
    // click selects it), so it is part of IsShowIcon. Only containers whose rows are the point: a
    // C# type, or a node of only member rows (a Bicep resource with auxiliary rows); a Bicep
    // module, whose resources are the content, opens as a container right away.
    public static bool IsMemberSummary(Node node, double zoom) =>
        IsPastIcon(node, zoom) && zoom * node.ContainerZoom < MinMemberRowZoom && IsMemberCardNode(node);

    // A non-member, non-root node large enough on screen to be more than an icon (a container
    // or a summary card).
    public static bool IsPastIcon(Node node, double zoom) =>
        !node.Type.IsMember && !node.IsRoot && IsPastOpenSize(node, zoom);

    public static bool IsMemberCardNode(Node node) =>
        node.Children.Count > 0 && (node.Type.IsType ? MemberCard.HasMembers(node) : NodeLayout.IsOnlyMembers(node));

    // The node is shown as an expanded box with visible children (not an icon and not zoomed
    // past visibility). Annotations (zones, notes) are leaves at every zoom: nothing is ever
    // placed inside them, dropped onto them or opened from them.
    public static bool IsContainerView(Node node, double modelZoom)
    {
        if (IsAnnotation(node))
            return false;
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !IsTooLargeToBeSeen(node, nodeZoom) && !IsShowIcon(node, nodeZoom);
    }

    // The node's children are rendered at this zoom: pass-through nodes always show children,
    // icons, members and annotations never do, and containers do even when zoomed in past
    // their own chrome.
    public static bool IsChildrenShown(Node node, double modelZoom)
    {
        if (node.IsPassThrough)
            return true;
        if (IsAnnotation(node))
            return false;
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !IsShowIcon(node, nodeZoom);
    }

    // A user-drawn annotation (a zone or a note): never a container, never a link endpoint.
    public static bool IsAnnotation(Node node) => node.IsZone || node.IsNote;
}
