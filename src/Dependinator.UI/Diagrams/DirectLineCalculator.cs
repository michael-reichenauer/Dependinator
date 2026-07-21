using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

static class DirectLineCalculator
{
    public static (Pos Source, Pos Target) GetAnchorsRelativeToAncestor(
        Node ancestor,
        Node source,
        Node target,
        AnchorPreference? sourcePreferenceOverride = null,
        AnchorPreference? targetPreferenceOverride = null
    )
    {
        var (sourcePreference, targetPreference) = ResolveAnchorSides(source, target);

        var sourceAnchorGlobal = GetAnchorGlobal(
            source,
            LineAnchorRole.Source,
            sourcePreferenceOverride ?? sourcePreference
        );
        var targetAnchorGlobal = GetAnchorGlobal(
            target,
            LineAnchorRole.Target,
            targetPreferenceOverride ?? targetPreference
        );

        var sourceLocal = ToAncestorLocal(ancestor, sourceAnchorGlobal);
        var targetLocal = ToAncestorLocal(ancestor, targetAnchorGlobal);

        return (sourceLocal, targetLocal);
    }

    // Which side of each node the line attaches to. When one node contains the other
    // (an ancestor→descendant line, e.g. a line split into a container's own child), the line
    // enters/leaves the descendant from the same side as the ordinary parent→child lines —
    // left when flowing down into a container, right when flowing up out of it — so it lines up
    // with the other lines at that node. Otherwise the left/right choice follows the two nodes'
    // horizontal order (their centers), the usual left-to-right dependency flow.
    static (AnchorPreference Source, AnchorPreference Target) ResolveAnchorSides(Node source, Node target)
    {
        if (IsAncestorOf(source, target))
            return (AnchorPreference.Left, AnchorPreference.Default); // Both left: down into the container
        if (IsAncestorOf(target, source))
            return (AnchorPreference.Default, AnchorPreference.Right); // Both right: up out of the container

        var (sourceCenter, _) = source.GetCenterPosAndZoom();
        var (targetCenter, _) = target.GetCenterPosAndZoom();
        return sourceCenter.X <= targetCenter.X
            ? (AnchorPreference.Right, AnchorPreference.Left)
            : (AnchorPreference.Left, AnchorPreference.Right);
    }

    static bool IsAncestorOf(Node maybeAncestor, Node node)
    {
        for (Node? current = node.Parent; current is not null; current = current.Parent)
        {
            if (current == maybeAncestor)
                return true;
        }
        return false;
    }

    static Pos GetAnchorGlobal(Node node, LineAnchorRole role, AnchorPreference preference)
    {
        var (pos, zoom) = node.GetPosAndZoom();
        var (anchorX, anchorY) = NodeAnchors.GetLineAnchor(node, role, preference);
        var localX = anchorX - node.Boundary.X;
        var localY = anchorY - node.Boundary.Y;
        var x = pos.X + localX * zoom;
        var y = pos.Y + localY * zoom;
        return new Pos(x, y);
    }

    static Pos ToAncestorLocal(Node ancestor, Pos globalPoint)
    {
        var (ancestorPos, ancestorZoom) = ancestor.GetPosAndZoom();
        var offset = new Pos(
            ancestorPos.X + ancestor.ContainerOffset.X * ancestorZoom,
            ancestorPos.Y + ancestor.ContainerOffset.Y * ancestorZoom
        );

        var scale = ancestorZoom * ancestor.ContainerZoom;
        if (Math.Abs(scale) < double.Epsilon)
        {
            scale = 1;
        }

        var localX = (globalPoint.X - offset.X) / scale;
        var localY = (globalPoint.Y - offset.Y) / scale;
        return new Pos(localX, localY);
    }
}
