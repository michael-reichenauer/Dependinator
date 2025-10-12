using System;

namespace Dependinator.Models;

static class DirectLineCalculator
{
    public static (Pos Source, Pos Target) GetAnchorsRelativeToAncestor(Node ancestor, Node source, Node target)
    {
        var (sourceCenter, _) = source.GetCenterPosAndZoom();
        var (targetCenter, _) = target.GetCenterPosAndZoom();

        var useRightSide = sourceCenter.X <= targetCenter.X;

        var sourceAnchorGlobal = GetHorizontalAnchorGlobal(source, useRightSide ? AnchorSide.Right : AnchorSide.Left);
        var targetAnchorGlobal = GetHorizontalAnchorGlobal(target, useRightSide ? AnchorSide.Left : AnchorSide.Right);

        var sourceLocal = ToAncestorLocal(ancestor, sourceAnchorGlobal);
        var targetLocal = ToAncestorLocal(ancestor, targetAnchorGlobal);

        return (sourceLocal, targetLocal);
    }

    static Pos GetHorizontalAnchorGlobal(Node node, AnchorSide side)
    {
        var (pos, zoom) = node.GetPosAndZoom();
        var x = pos.X + (side == AnchorSide.Right ? node.Boundary.Width : 0) * zoom;
        var y = pos.Y + node.Boundary.Height / 2 * zoom;
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

    enum AnchorSide
    {
        Left,
        Right,
    }
}
