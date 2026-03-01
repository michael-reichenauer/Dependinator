using Dependinator.Models;

namespace Dependinator.Diagrams;

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
}

[Scoped]
class NodeEditService(IModelService modelService) : INodeEditService
{
    const double MaxZoom = 1.0;
    const double MinZoom = 1.0 / 10.0;
    const double Margin = 50;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;
    const double sizeDiff = 10.0;

    static double SnapToGrid(double value) => Math.Round(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;

    static double SnapToGridUp(double value) => Math.Ceiling(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;

    static double SnapToGridDown(double value) => Math.Floor(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;

    public void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
                var newBoundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
            }
        );
    }

    public void SnapSelectedNodeToGrid(PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var snappedX = SnapToGrid(node.Boundary.X);
                var snappedY = SnapToGrid(node.Boundary.Y);
                if (snappedX == node.Boundary.X && snappedY == node.Boundary.Y)
                    return;

                var newBoundary = node.Boundary with { X = snappedX, Y = snappedY };
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
            }
        );
    }

    public void SnapResizedSelectedNodeToGrid(PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var oldBoundary = node.Boundary;
                var newBoundary = SnapResizeBoundaryToGrid(oldBoundary, pointerId.NodeResizeType);
                if (newBoundary == oldBoundary)
                    return;

                var newContainerOffset = node.ContainerOffset with
                {
                    X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                    Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y),
                };

                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(
                    new NodeEditCommand(node.Id) { Boundary = newBoundary, ContainerOffset = newContainerOffset }
                );
            }
        );
    }

    public void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
                var newContainerOffset = node.ContainerOffset with
                {
                    X = node.ContainerOffset.X + dx,
                    Y = node.ContainerOffset.Y + dy,
                };

                modelService.Do(new NodeEditCommand(node.Id) { ContainerOffset = newContainerOffset });
            }
        );
    }

    public void IncreaseNodeSize(NodeId nodeId)
    {
        modelService.UseNodeN(
            nodeId,
            node =>
            {
                var newBoundary = node.Boundary with
                {
                    Width = SnapToGridUp(node.Boundary.Width + sizeDiff),
                    Height = SnapToGridUp(node.Boundary.Height + sizeDiff),
                };
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
            }
        );
    }

    public void DecreaseNodeSize(NodeId nodeId)
    {
        modelService.UseNodeN(
            nodeId,
            node =>
            {
                var newBoundary = node.Boundary with
                {
                    Width = SnapToGridDown(node.Boundary.Width - sizeDiff),
                    Height = SnapToGridDown(node.Boundary.Height - sizeDiff),
                };
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
            }
        );
    }

    public void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

                var oldBoundary = node.Boundary;
                var newBoundary = pointerId.NodeResizeType switch
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
                        X = node.Boundary.X,
                        Y = node.Boundary.Y + dy,
                        Width = node.Boundary.Width + dx,
                        Height = node.Boundary.Height - dy,
                    },

                    NodeResizeType.MiddleLeft => node.Boundary with
                    {
                        X = node.Boundary.X + dx,
                        Width = node.Boundary.Width - dx,
                    },
                    NodeResizeType.MiddleRight => node.Boundary with
                    {
                        X = node.Boundary.X,
                        Width = node.Boundary.Width + dx,
                    },

                    NodeResizeType.BottomLeft => node.Boundary with
                    {
                        X = node.Boundary.X + dx,
                        Y = node.Boundary.Y,
                        Width = node.Boundary.Width - dx,
                        Height = node.Boundary.Height + dy,
                    },
                    NodeResizeType.BottomMiddle => node.Boundary with
                    {
                        Y = node.Boundary.Y,
                        Height = node.Boundary.Height + dy,
                    },
                    NodeResizeType.BottomRight => node.Boundary with
                    {
                        X = node.Boundary.X,
                        Y = node.Boundary.Y,
                        Width = node.Boundary.Width + dx,
                        Height = node.Boundary.Height + dy,
                    },

                    _ => node.Boundary,
                };

                // Adjust container offset to ensure that children stay in place
                var newContainerOffset = node.ContainerOffset with
                {
                    X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                    Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y),
                };
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;

                modelService.Do(
                    new NodeEditCommand(node.Id) { Boundary = newBoundary, ContainerOffset = newContainerOffset }
                );
            }
        );
    }

    public void ZoomSelectedNode(PointerEvent e, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                if (e.DeltaY == 0)
                    return;
                //var (mx, my) = (e.OffsetX - node.ContainerOffset.X, e.OffsetY - node.ContainerOffset.Y);
                var (mx, my) = (node.Boundary.Width, node.Boundary.Height);
                // var (mx, my) = (node.Boundary.Width / 2 - node.ContainerOffset.X, node.Boundary.Height / 2 - node.ContainerOffset.Y);

                var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
                double newZoom = (e.DeltaY < 0) ? node.ContainerZoom * speed : node.ContainerZoom * (1 / speed);
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

                var newContainerOffset = new Pos(x, y);

                modelService.Do(
                    new NodeEditCommand(node.Id) { ContainerOffset = newContainerOffset, ContainerZoom = newZoom }
                );
            }
        );
    }

    public void PanZoomToFit(PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                using var model = modelService.UseModel();

                Rect b = node.GetTotalBounds();

                // Determine the X or y zoom that best fits the bounds (including margin)
                var zx = (b.Width + 2 * Margin) / node.Boundary.Width;
                var zy = (b.Height + 2 * Margin) / node.Boundary.Height;
                var newZoom = 1 / Math.Max(zx, zy);

                var wx = node.Boundary.Width * newZoom;
                var wy = node.Boundary.Height / newZoom;

                // Zoom width and height to fit the bounds
                var nw = (b.Width + 2 * Margin) * newZoom;
                var nh = (b.Height + 2 * Margin) * newZoom;
                var w = node.Boundary.Width * newZoom;
                var h = node.Boundary.Height * newZoom;

                // Pan to center the bounds
                var mx = Margin;
                var my = Margin;
                var x = -(b.X - mx) * newZoom;
                var y = -(b.Y - my) * newZoom;

                var newOffset = new Pos(x, y);

                modelService.Do(new NodeEditCommand(node.Id) { ContainerOffset = newOffset, ContainerZoom = newZoom });
            }
        );
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
                left = SnapToGrid(left);
                top = SnapToGrid(top);
                break;
            case NodeResizeType.TopMiddle:
                top = SnapToGrid(top);
                break;
            case NodeResizeType.TopRight:
                right = SnapToGrid(right);
                top = SnapToGrid(top);
                break;
            case NodeResizeType.MiddleLeft:
                left = SnapToGrid(left);
                break;
            case NodeResizeType.MiddleRight:
                right = SnapToGrid(right);
                break;
            case NodeResizeType.BottomLeft:
                left = SnapToGrid(left);
                bottom = SnapToGrid(bottom);
                break;
            case NodeResizeType.BottomMiddle:
                bottom = SnapToGrid(bottom);
                break;
            case NodeResizeType.BottomRight:
                right = SnapToGrid(right);
                bottom = SnapToGrid(bottom);
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
