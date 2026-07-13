using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

interface INodeEditService
{
    void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void SnapSelectedNodeToGrid(PointerId pointerId);
    void SnapResizedSelectedNodeToGrid(PointerId pointerId);
    void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ZoomSelectedNode(PointerEvent e, PointerId pointerId);
    void PanZoomToFit(PointerId pointerId);
    void IncreaseNodeSize(NodeId nodeId);
    void DecreaseNodeSize(NodeId nodeId);
    void SetNodeIcon(NodeId nodeId, string? iconName);
    void SetNodeIconColor(NodeId nodeId, string? colorName);
    void SetNodeColor(NodeId nodeId, string? colorName);
}

[Scoped]
class NodeEditService(IModelMgr modelMgr, ICommandService commandService) : INodeEditService
{
    const double MaxZoom = 1.0;
    const double MinZoom = 1.0 / 10.0;
    const double Margin = 50;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;
    const double SizeDiff = 10.0;

    // Pan/zoom on a node whose sole child is a pass-through container would be visually inert
    // (the pass-through boundary always re-covers the parent's viewport), so such gestures are
    // redirected to the deepest pass-through node, whose container transform moves the actual
    // visible content.
    static Node ResolveContainerTarget(Node node)
    {
        while (node.Children.Count == 1 && node.Children[0].IsPassThrough)
            node = node.Children[0];
        return node;
    }

    public void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            nodeId = node.Id;
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
            newBoundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    public void SnapSelectedNodeToGrid(PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;
            nodeId = node.Id;
            var snappedX = NodeGrid.Snap(node.Boundary.X);
            var snappedY = NodeGrid.Snap(node.Boundary.Y);
            if (snappedX == node.Boundary.X && snappedY == node.Boundary.Y)
                return;

            newBoundary = node.Boundary with { X = snappedX, Y = snappedY };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }
        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    public void SnapResizedSelectedNodeToGrid(PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        Pos newContainerOffset;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            nodeId = node.Id;
            var oldBoundary = node.Boundary;
            newBoundary = SnapResizeBoundaryToGrid(oldBoundary, pointerId.NodeResizeType);
            if (newBoundary == oldBoundary)
                return;

            newContainerOffset = node.ContainerOffset with
            {
                X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y),
            };

            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary, ContainerOffset = newContainerOffset });
    }

    public void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        NodeId nodeId;
        Pos newContainerOffset;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            node = ResolveContainerTarget(node);
            nodeId = node.Id;
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
            newContainerOffset = node.ContainerOffset with
            {
                X = node.ContainerOffset.X + dx,
                Y = node.ContainerOffset.Y + dy,
            };
        }

        commandService.Do(new NodeEditCommand(nodeId) { ContainerOffset = newContainerOffset });
    }

    public void IncreaseNodeSize(NodeId nodeId)
    {
        Rect newBoundary;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            newBoundary = node.Boundary with
            {
                Width = NodeGrid.SnapUp(node.Boundary.Width + SizeDiff),
                Height = NodeGrid.SnapUp(node.Boundary.Height + SizeDiff),
            };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    public void DecreaseNodeSize(NodeId nodeId)
    {
        Rect newBoundary;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            newBoundary = node.Boundary with
            {
                Width = NodeGrid.SnapDown(node.Boundary.Width - SizeDiff),
                Height = NodeGrid.SnapDown(node.Boundary.Height - SizeDiff),
            };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    // Sets the node's custom icon; null clears it back to the node-type default icon.
    public void SetNodeIcon(NodeId nodeId, string? iconName)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            if (node.CustomIconName == iconName)
                return;
        }

        commandService.Do(new NodeEditCommand(nodeId) { IconName = iconName ?? "" });
    }

    // Sets the node's icon tint; null clears it back to the default violet.
    public void SetNodeIconColor(NodeId nodeId, string? colorName)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            if (node.CustomIconColor == colorName)
                return;
        }

        commandService.Do(new NodeEditCommand(nodeId) { IconColor = colorName ?? "" });
    }

    // Sets the node's container color; null clears it back to the auto-assigned color.
    public void SetNodeColor(NodeId nodeId, string? colorName)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            if (node.CustomColor == colorName)
                return;
        }

        commandService.Do(new NodeEditCommand(nodeId) { CustomColor = colorName ?? "" });
    }

    public void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        Pos newContainerOffset;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            nodeId = node.Id;
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

            var oldBoundary = node.Boundary;
            newBoundary = pointerId.NodeResizeType switch
            {
                NodeResizeType.TopLeft => node.Boundary with
                {
                    X = node.Boundary.X + dx,
                    Y = node.Boundary.Y + dy,
                    Width = node.Boundary.Width - dx,
                    Height = node.Boundary.Height - dy,
                },
                NodeResizeType.TopMiddle => node.Boundary with
                {
                    Y = node.Boundary.Y + dy,
                    Height = node.Boundary.Height - dy,
                },
                NodeResizeType.TopRight => node.Boundary with
                {
                    Y = node.Boundary.Y + dy,
                    Width = node.Boundary.Width + dx,
                    Height = node.Boundary.Height - dy,
                },

                NodeResizeType.MiddleLeft => node.Boundary with
                {
                    X = node.Boundary.X + dx,
                    Width = node.Boundary.Width - dx,
                },
                NodeResizeType.MiddleRight => node.Boundary with { Width = node.Boundary.Width + dx },

                NodeResizeType.BottomLeft => node.Boundary with
                {
                    X = node.Boundary.X + dx,
                    Width = node.Boundary.Width - dx,
                    Height = node.Boundary.Height + dy,
                },
                NodeResizeType.BottomMiddle => node.Boundary with { Height = node.Boundary.Height + dy },
                NodeResizeType.BottomRight => node.Boundary with
                {
                    Width = node.Boundary.Width + dx,
                    Height = node.Boundary.Height + dy,
                },

                _ => node.Boundary,
            };

            // Children are positioned relative to the node's top-left corner, so when resizing
            // moves that corner (left/top handles), shift the container offset by the opposite
            // amount to keep the children visually in place.
            newContainerOffset = node.ContainerOffset with
            {
                X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y),
            };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(new NodeEditCommand(nodeId) { Boundary = newBoundary, ContainerOffset = newContainerOffset });
    }

    public void ZoomSelectedNode(PointerEvent e, PointerId pointerId)
    {
        NodeId nodeId;
        double newZoom;
        Pos newContainerOffset;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            node = ResolveContainerTarget(node);
            nodeId = node.Id;
            if (e.DeltaY == 0)
                return;
            var (mx, my) = (node.Boundary.Width, node.Boundary.Height);

            var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
            newZoom = (e.DeltaY < 0) ? node.ContainerZoom * speed : node.ContainerZoom * (1 / speed);
            if (newZoom < MinZoom)
                newZoom = MinZoom;
            if (newZoom > MaxZoom)
                newZoom = MaxZoom;

            double svgX = mx * node.ContainerZoom + node.ContainerOffset.X;
            double svgY = my * node.ContainerZoom + node.ContainerOffset.Y;

            var w = node.Boundary.Width * newZoom;
            var h = node.Boundary.Height * newZoom;

            var x = svgX - mx / node.Boundary.Width * w;
            var y = svgY - my / node.Boundary.Height * h;

            newContainerOffset = new Pos(x, y);
        }

        commandService.Do(
            new NodeEditCommand(nodeId) { ContainerOffset = newContainerOffset, ContainerZoom = newZoom }
        );
    }

    public void PanZoomToFit(PointerId pointerId)
    {
        NodeId nodeId;
        double newZoom;
        Pos newOffset;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node))
                return;

            node = ResolveContainerTarget(node);
            nodeId = node.Id;
            Rect b = node.GetTotalBounds();

            // Determine the X or y zoom that best fits the bounds (including margin)
            var zx = (b.Width + 2 * Margin) / node.Boundary.Width;
            var zy = (b.Height + 2 * Margin) / node.Boundary.Height;
            newZoom = 1 / Math.Max(zx, zy);

            // Pan so the bounds (with margin) start at the container's top-left corner
            var x = -(b.X - Margin) * newZoom;
            var y = -(b.Y - Margin) * newZoom;

            newOffset = new Pos(x, y);
        }

        commandService.Do(new NodeEditCommand(nodeId) { ContainerOffset = newOffset, ContainerZoom = newZoom });
    }

    static Rect SnapResizeBoundaryToGrid(Rect boundary, NodeResizeType resizeType)
    {
        var left = boundary.X;
        var top = boundary.Y;
        var right = boundary.X + boundary.Width;
        var bottom = boundary.Y + boundary.Height;

        switch (resizeType)
        {
            case NodeResizeType.TopLeft:
                left = NodeGrid.Snap(left);
                top = NodeGrid.Snap(top);
                break;
            case NodeResizeType.TopMiddle:
                top = NodeGrid.Snap(top);
                break;
            case NodeResizeType.TopRight:
                right = NodeGrid.Snap(right);
                top = NodeGrid.Snap(top);
                break;
            case NodeResizeType.MiddleLeft:
                left = NodeGrid.Snap(left);
                break;
            case NodeResizeType.MiddleRight:
                right = NodeGrid.Snap(right);
                break;
            case NodeResizeType.BottomLeft:
                left = NodeGrid.Snap(left);
                bottom = NodeGrid.Snap(bottom);
                break;
            case NodeResizeType.BottomMiddle:
                bottom = NodeGrid.Snap(bottom);
                break;
            case NodeResizeType.BottomRight:
                right = NodeGrid.Snap(right);
                bottom = NodeGrid.Snap(bottom);
                break;
            default:
                return boundary;
        }

        return boundary with
        {
            X = left,
            Y = top,
            Width = right - left,
            Height = bottom - top,
        };
    }
}
