using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Editing;

interface INodeEditService
{
    void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void SnapSelectedNodeToGrid(PointerId pointerId);

    // The same for a group of selected nodes, as one undo step: each node moves by the pointer
    // delta in its own container's scale. A node whose ancestor is in the group moves with it.
    // The lead (the node under the pointer, or the group node that holds it) is the one aligned
    // with its siblings, and the whole group shifts with it.
    void MoveSelectedNodes(PointerEvent e, double zoom, IReadOnlyCollection<NodeId> nodeIds, NodeId? leadId = null);
    void SnapSelectedNodesToGrid(IReadOnlyCollection<NodeId> nodeIds);

    // A move of one or more nodes starts: forgets a move whose end was missed (a release outside
    // the window).
    void BeginMove();

    // Ends a move of one or more nodes (after the snap to the grid): the lines in the moved nodes'
    // containers follow them (MovedNodeLines), in the move's undo step.
    void EndMove();
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

    // Shows or folds the private rows of the node's member card and lays the card out again.
    // Null when nothing changed; otherwise where the toggle row moved to (canvas positions) and
    // the factor its rows were scaled by, so the view can keep the toggle in place.
    CardToggleMove? TogglePrivateMembers(NodeId nodeId);

    // Group variants: one undo step for all the nodes.
    void IncreaseNodeSize(IReadOnlyCollection<NodeId> nodeIds);
    void DecreaseNodeSize(IReadOnlyCollection<NodeId> nodeIds);
    void SetNodeIconColor(IReadOnlyCollection<NodeId> nodeIds, string? colorName);
    void SetNodeColor(IReadOnlyCollection<NodeId> nodeIds, string? colorName);
}

[Scoped]
class NodeEditService(IModelMgr modelMgr, ICommandService commandService, IAlignmentGuideService alignmentGuides)
    : INodeEditService
{
    const double MaxZoom = 1.0;
    const double MinZoom = 1.0 / 10.0;
    const double Margin = 50;
    const double WheelZoomSpeed = 1.2;
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
            // Member rows keep their place in the member card (MemberCard), so they do not move.
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node) || node.Type.IsMember)
                return;

            nodeId = node.Id;
            var scale = node.GetZoom() * zoom;
            var raw = MoveRaw(node, e, scale);
            var alignment = Align(node, raw, scale, e, [node]);
            newBoundary = raw with { X = raw.X + alignment.Dx, Y = raw.Y + alignment.Dy };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    public void MoveSelectedNodes(
        PointerEvent e,
        double zoom,
        IReadOnlyCollection<NodeId> nodeIds,
        NodeId? leadId = null
    )
    {
        var commands = new List<Command>();
        using (var model = modelMgr.UseModel())
        {
            var nodes = Resolve(model, nodeIds, isTopLevelOnly: true).Where(node => !node.Type.IsMember).ToList();
            var raws = nodes.ToDictionary(node => node, node => MoveRaw(node, e, node.GetZoom() * zoom));

            // The lead's alignment shift, in screen pixels: each node shifts by it in its own scale.
            var (shiftX, shiftY) = (0.0, 0.0);
            if (Lead(model, nodes, leadId) is { } lead)
            {
                var scale = lead.GetZoom() * zoom;
                var alignment = Align(lead, raws[lead], scale, e, nodes);
                (shiftX, shiftY) = (alignment.Dx / scale, alignment.Dy / scale);
            }
            else
                SetAlignment(AlignmentSnap.Result.None, []);

            foreach (var node in nodes)
            {
                var scale = node.GetZoom() * zoom;
                var raw = raws[node];
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;
                commands.Add(
                    new NodeEditCommand(node.Id)
                    {
                        Boundary = raw with { X = raw.X + shiftX * scale, Y = raw.Y + shiftY * scale },
                    }
                );
            }
        }
        if (commands.Count == 0)
            return;
        Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
    }

    public void BeginMove()
    {
        moveStarts.Clear();
        rawBoundaries.Clear();
        alignment = AlignmentSnap.Result.None;
        alignmentGuides.Clear();
        lastMoveCommand = null;
    }

    // Where the pointer alone puts the node (the drag so far without the alignment snaps, so a
    // snapped node lets go again once the pointer moves on), and records where its move started.
    Rect MoveRaw(Node node, PointerEvent e, double scale)
    {
        moveStarts.TryAdd(node, node.Boundary);
        var origin = rawBoundaries.TryGetValue(node, out var raw) ? raw : node.Boundary;
        var moved = origin with { X = origin.X + e.MovementX * scale, Y = origin.Y + e.MovementY * scale };
        rawBoundaries[node] = moved;
        return moved;
    }

    // The group node that is or holds the node under the pointer; null when there is none.
    static Node? Lead(IModel model, IReadOnlyList<Node> nodes, NodeId? leadId)
    {
        if (leadId is not { } id || !model.Nodes.TryGetValue(id, out var node))
            return null;
        var group = nodes.ToHashSet();
        while (!group.Contains(node) && !node.IsRoot)
            node = node.Parent;
        return group.Contains(node) ? node : null;
    }

    // Aligns the node's raw box with its nearby siblings (not those that move with it) and shows
    // the guides; the result's shift is in the node's container units.
    AlignmentSnap.Result Align(Node node, Rect raw, double scale, PointerEvent e, IReadOnlyCollection<Node> moving)
    {
        if (node.IsRoot || scale <= 0)
        {
            SetAlignment(AlignmentSnap.Result.None, []);
            return AlignmentSnap.Result.None;
        }

        var moved = moving.ToHashSet();
        var others = node
            .Parent.Children.Where(sibling =>
                !moved.Contains(sibling)
                && !sibling.Type.IsMember
                && sibling.Visibility != NodeVisibility.Hidden
                && sibling.Boundary.Width > 0
                && sibling.Boundary.Height > 0
            )
            .Select(sibling => sibling.Boundary)
            .ToList();
        var snapPixels = e.PointerType == "touch" ? AlignmentSnap.TouchSnapPixels : AlignmentSnap.MouseSnapPixels;
        var result = AlignmentSnap.Find(
            raw,
            others,
            snapPixels * scale,
            AlignmentSnap.ReachPixels * scale,
            AlignmentSnap.OvershootPixels * scale
        );
        SetAlignment(result, GuideLines(node.Parent, result));
        return result;
    }

    void SetAlignment(AlignmentSnap.Result result, IReadOnlyList<GuideLine> lines)
    {
        alignment = result;
        alignmentGuides.Set(lines);
    }

    // The guides from the container's children-local units to canvas coordinates.
    static IReadOnlyList<GuideLine> GuideLines(Node parent, AlignmentSnap.Result result)
    {
        var (parentPos, parentZoom) = parent.GetPosAndZoom();
        var originX = parentPos.X + parent.ContainerOffset.X * parentZoom;
        var originY = parentPos.Y + parent.ContainerOffset.Y * parentZoom;
        var zoom = parentZoom * parent.ContainerZoom;
        Pos Canvas(double x, double y) => new(originX + x * zoom, originY + y * zoom);

        var lines = new List<GuideLine>(2);
        if (result.Vertical is { } vertical)
            lines.Add(new GuideLine(Canvas(vertical.At, vertical.From), Canvas(vertical.At, vertical.To)));
        if (result.Horizontal is { } horizontal)
            lines.Add(new GuideLine(Canvas(horizontal.From, horizontal.At), Canvas(horizontal.To, horizontal.At)));
        return lines;
    }

    public void EndMove()
    {
        Command? linesCommand = null;
        if (moveStarts.Count > 0)
        {
            using var model = modelMgr.UseModel();
            var moves = moveStarts
                .Where(pair => model.Nodes.TryGetValue(pair.Key.Id, out var node) && node == pair.Key)
                .ToDictionary(
                    pair => pair.Key,
                    pair => new Pos(pair.Key.Boundary.X - pair.Value.X, pair.Key.Boundary.Y - pair.Value.Y)
                );
            var points = MovedNodeLines.NewPoints(moves);
            if (points.Count > 0)
                linesCommand = new LinePointsCommand(points.ToDictionary(pair => pair.Key.Id, pair => pair.Value));
        }
        var step = lastMoveCommand;
        BeginMove(); // Also clears the guides

        if (linesCommand is null)
            return;
        if (step is not null)
            commandService.DoInStepOf(step, linesCommand);
        else
            commandService.Do(linesCommand);
    }

    // Where each node of the move in progress started, and the move's latest command: the whole
    // move is one undo step (the edits of a drag merge by time only, so a pause would split it,
    // and the lines routed at the end would join only its last part).
    readonly Dictionary<Node, Rect> moveStarts = [];
    Command? lastMoveCommand;

    // Where the pointer alone has put each moved node, and the alignment of the latest step: an
    // aligned axis is not snapped to the grid at the end, or the snap would undo the alignment.
    readonly Dictionary<Node, Rect> rawBoundaries = [];
    AlignmentSnap.Result alignment = AlignmentSnap.Result.None;

    double SnapX(double x) => alignment.IsAlignedX ? x : NodeGrid.Snap(x);

    double SnapY(double y) => alignment.IsAlignedY ? y : NodeGrid.Snap(y);

    void Do(Command command)
    {
        if (moveStarts.Count > 0 && lastMoveCommand is not null)
            commandService.DoInStepOf(lastMoveCommand, command);
        else
            commandService.Do(command);
        lastMoveCommand = moveStarts.Count > 0 ? command : null;
    }

    // Snaps the group after a drag (an axis the drag aligned with a sibling stays). Every node gets a command, also one already on the grid, so
    // the step has the drag composite's merge key and folds into that undo step; with no node
    // off the grid there is no step at all.
    public void SnapSelectedNodesToGrid(IReadOnlyCollection<NodeId> nodeIds)
    {
        bool isAnyOffGrid;
        using (var model = modelMgr.UseModel())
        {
            isAnyOffGrid = Resolve(model, nodeIds, isTopLevelOnly: true)
                .Any(node =>
                    !node.Type.IsMember
                    && (SnapX(node.Boundary.X) != node.Boundary.X || SnapY(node.Boundary.Y) != node.Boundary.Y)
                );
        }
        if (!isAnyOffGrid)
            return;

        Apply(
            nodeIds,
            node =>
            {
                if (node.Type.IsMember)
                    return null;
                if (!node.IsRoot)
                    node.Parent.IsChildrenLayoutCustomized = true;
                return new NodeEditCommand(node.Id)
                {
                    Boundary = node.Boundary with { X = SnapX(node.Boundary.X), Y = SnapY(node.Boundary.Y) },
                };
            },
            isTopLevelOnly: true
        );
    }

    public void IncreaseNodeSize(IReadOnlyCollection<NodeId> nodeIds) =>
        Apply(nodeIds, node => SizeCommand(node, SizeDiff));

    public void DecreaseNodeSize(IReadOnlyCollection<NodeId> nodeIds) =>
        Apply(nodeIds, node => SizeCommand(node, -SizeDiff));

    public void SetNodeIconColor(IReadOnlyCollection<NodeId> nodeIds, string? colorName) =>
        Apply(nodeIds, node =>
            node.CustomIconColor == colorName ? null : new NodeEditCommand(node.Id) { IconColor = colorName ?? "" }
        );

    public void SetNodeColor(IReadOnlyCollection<NodeId> nodeIds, string? colorName) =>
        Apply(nodeIds, node =>
            node.CustomColor == colorName ? null : new NodeEditCommand(node.Id) { CustomColor = colorName ?? "" }
        );

    static NodeEditCommand? SizeCommand(Node node, double diff)
    {
        if (node.Type.IsMember)
            return null; // Rows have the size of their card's column
        if (!node.IsRoot)
            node.Parent.IsChildrenLayoutCustomized = true;
        var width =
            diff > 0 ? NodeGrid.SnapUp(node.Boundary.Width + diff) : NodeGrid.SnapDown(node.Boundary.Width + diff);
        var height =
            diff > 0 ? NodeGrid.SnapUp(node.Boundary.Height + diff) : NodeGrid.SnapDown(node.Boundary.Height + diff);
        return new NodeEditCommand(node.Id) { Boundary = node.Boundary with { Width = width, Height = height } };
    }

    // Builds one edit per node (null skips a node) and runs them as a single undo step. With
    // isTopLevelOnly, a node whose ancestor is also in the group is left out: it moves with that
    // ancestor, and moving it as well would move it twice.
    void Apply(IReadOnlyCollection<NodeId> nodeIds, Func<Node, NodeEditCommand?> build, bool isTopLevelOnly = false)
    {
        var commands = new List<Command>();
        using (var model = modelMgr.UseModel())
        {
            foreach (var node in Resolve(model, nodeIds, isTopLevelOnly))
            {
                if (build(node) is { } command)
                    commands.Add(command);
            }
        }
        if (commands.Count == 0)
            return;
        Do(commands.Count == 1 ? commands[0] : new CompositeCommand([.. commands]));
    }

    static List<Node> Resolve(IModel model, IReadOnlyCollection<NodeId> nodeIds, bool isTopLevelOnly)
    {
        var nodes = nodeIds
            .Select(id => model.Nodes.TryGetValue(id, out var node) ? node : null)
            .OfType<Node>()
            .ToList();
        if (!isTopLevelOnly)
            return nodes;
        var group = nodes.ToHashSet();
        return nodes.Where(node => !node.Ancestors().Any(group.Contains)).ToList();
    }

    public void SnapSelectedNodeToGrid(PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node) || node.Type.IsMember)
                return;
            nodeId = node.Id;
            var snappedX = SnapX(node.Boundary.X);
            var snappedY = SnapY(node.Boundary.Y);
            if (snappedX == node.Boundary.X && snappedY == node.Boundary.Y)
                return;

            newBoundary = node.Boundary with { X = snappedX, Y = snappedY };
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }
        Do(new NodeEditCommand(nodeId) { Boundary = newBoundary });
    }

    // A view of the card, not an edit: no undo step.
    public CardToggleMove? TogglePrivateMembers(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        // The toggle that was clicked is the one drawn: as laid out, whatever a re-parse changed since.
        if (!model.Nodes.TryGetValue(nodeId, out var node) || MemberCard.PrivateToggleRect(node) == Rect.None)
            return null;
        var (from, zoomBefore) = ToggleCanvasPos(node);
        node.IsPrivateMembersShown = !node.IsPrivateMembersShown;
        NodeLayout.AdjustChildren(node);
        model.BumpStructureVersion();
        if (MemberCard.PrivateToggleRect(node) == Rect.None)
            return new CardToggleMove(from, from, 1); // The members changed: no toggle any more
        var (to, zoomAfter) = ToggleCanvasPos(node);
        return new CardToggleMove(from, to, zoomAfter / zoomBefore);
    }

    // The canvas position of the card's toggle row (its left end, mid-height) and the zoom of the
    // card's rows.
    static (Pos Pos, double RowZoom) ToggleCanvasPos(Node node)
    {
        var (pos, zoom) = node.GetPosAndZoom();
        var rowZoom = zoom * node.ContainerZoom;
        var rect = MemberCard.PrivateToggleRect(node);
        var offset = node.ContainerOffset;
        return (
            new Pos(
                pos.X + rect.X * rowZoom + offset.X * zoom,
                pos.Y + (rect.Y + rect.Height / 2) * rowZoom + offset.Y * zoom
            ),
            rowZoom
        );
    }

    public void SnapResizedSelectedNodeToGrid(PointerId pointerId)
    {
        NodeId nodeId;
        Rect newBoundary;
        Pos newContainerOffset;
        double? newContainerZoom = null;
        using (var model = modelMgr.UseModel())
        {
            // Member rows are placed by their card, off the grid on purpose
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node) || node.Type.IsMember)
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
            // A card keeps its rows under its header at the top left: fit them to the new box.
            if (NodeViewPolicy.IsMemberCardNode(node))
                (newContainerZoom, newContainerOffset) = NodeLayout.CardTransform(node, newBoundary);

            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(
            new NodeEditCommand(nodeId)
            {
                Boundary = newBoundary,
                ContainerOffset = newContainerOffset,
                ContainerZoom = newContainerZoom,
            }
        );
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
            if (NodeViewPolicy.IsMemberCardNode(node))
                return; // A card's rows stay under its header, at the card's own scale
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
        double? newContainerZoom = null;
        using (var model = modelMgr.UseModel())
        {
            // Member rows have the size of their member card's column (MemberCard).
            if (!model.Nodes.TryGetValue(NodeId.FromId(pointerId.Id), out var node) || node.Type.IsMember)
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
            // A card keeps its rows under its header at the top left: fit them to the new box.
            if (NodeViewPolicy.IsMemberCardNode(node))
                (newContainerZoom, newContainerOffset) = NodeLayout.CardTransform(node, newBoundary);
            if (!node.IsRoot)
                node.Parent.IsChildrenLayoutCustomized = true;
        }

        commandService.Do(
            new NodeEditCommand(nodeId)
            {
                Boundary = newBoundary,
                ContainerOffset = newContainerOffset,
                ContainerZoom = newContainerZoom,
            }
        );
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
            if (NodeViewPolicy.IsMemberCardNode(node))
                return; // A card's rows stay under its header, at the card's own scale
            if (e.DeltaY == 0 || e.PinchScale == 1)
                return; // No change (a pinch whose fingers kept their distance)
            var (mx, my) = (node.Boundary.Width, node.Boundary.Height);

            // A pinch zooms by how much the fingers spread (PointerEvent.PinchScale), a wheel by
            // steps, a trackpad pinch by its delta (PointerEvent.WheelZoomFactor).
            var factor = e.WheelZoomFactor(WheelZoomSpeed);
            newZoom =
                e.PinchScale > 0 ? node.ContainerZoom * e.PinchScale
                : (e.DeltaY < 0) ? node.ContainerZoom * factor
                : node.ContainerZoom * (1 / factor);
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
            if (NodeViewPolicy.IsMemberCardNode(node))
                return; // A card's rows stay under its header, at the card's own scale
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

// Where a member card's private toggle row moved when the card was laid out again (canvas
// positions), and the factor its rows were scaled by.
record CardToggleMove(Pos From, Pos To, double Scale);
