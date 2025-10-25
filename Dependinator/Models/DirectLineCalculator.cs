using System;
using Dependinator.Diagrams.Svg;

namespace Dependinator.Models;

static class DirectLineCalculator
{
    public static (Pos Source, Pos Target) GetAnchorsRelativeToAncestor(Node ancestor, Node source, Node target)
    {
        var (sourceCenter, _) = source.GetCenterPosAndZoom();
        var (targetCenter, _) = target.GetCenterPosAndZoom();

        var useRightSide = sourceCenter.X <= targetCenter.X;

        var sourcePreference = useRightSide ? NodeSvg.AnchorPreference.Right : NodeSvg.AnchorPreference.Left;
        var targetPreference = useRightSide ? NodeSvg.AnchorPreference.Left : NodeSvg.AnchorPreference.Right;

        var sourceAnchorGlobal = GetAnchorGlobal(source, NodeSvg.LineAnchorRole.Source, sourcePreference);
        var targetAnchorGlobal = GetAnchorGlobal(target, NodeSvg.LineAnchorRole.Target, targetPreference);

        var sourceLocal = ToAncestorLocal(ancestor, sourceAnchorGlobal);
        var targetLocal = ToAncestorLocal(ancestor, targetAnchorGlobal);

        return (sourceLocal, targetLocal);
    }

    static Pos GetAnchorGlobal(Node node, NodeSvg.LineAnchorRole role, NodeSvg.AnchorPreference preference)
    {
        var (pos, zoom) = node.GetPosAndZoom();
        var (anchorX, anchorY) = NodeSvg.GetLineAnchor(node, role, preference);
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
