using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Editing;

interface ILineEditService
{
    Task AddSegmentPoint(LineId lineId, Pos screenPos);
    Task RemoveSegmentPoint(LineId lineId, Pos screenPos);
    void MoveSegmentPoint(PointerEvent e, double zoom, PointerId pointerId);
    void SnapSegmentPointToGrid(PointerId pointerId);

    // Slides a line's label along the line under an edit-mode drag (pointerId.IsLineLabel), and
    // ends the drag.
    void MoveLabel(PointerEvent e, double zoom, PointerId pointerId);
    void EndLabelMove();
}

[Scoped]
class LineEditService(IModelMgr modelMgr, ICommandService commandService, IScreenService screenService)
    : ILineEditService
{
    public async Task AddSegmentPoint(LineId lineId, Pos screenPos)
    {
        if (screenPos == Pos.None)
            return;

        IReadOnlyList<Pos> updatedPoints;
        var worldHint = await TryScreenToWorldPosAsync(screenPos);
        if (worldHint is null)
            return;

        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            if (!LinePathGeometry.TryGetLocalEndpoints(line, out var endpoints))
                return;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
                return;

            var localHint = ToOwnerChildrenLocal(owner, worldHint);

            updatedPoints = InsertPointAtClosestSegment(line, endpoints, localHint);
        }

        commandService.Do(new LineEditCommand(lineId) { SegmentPoints = updatedPoints });
    }

    public async Task RemoveSegmentPoint(LineId lineId, Pos screenPos)
    {
        if (screenPos == Pos.None)
            return;

        IReadOnlyList<Pos> updatedPoints;
        var worldHint = await TryScreenToWorldPosAsync(screenPos);
        if (worldHint is null)
            return;

        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            if (line.SegmentPoints.Count == 0)
                return;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
                return;

            var localHint = ToOwnerChildrenLocal(owner, worldHint);
            var removeIndex = GetClosestControlPointIndex(line.SegmentPoints, localHint);
            if (removeIndex < 0)
                return;

            updatedPoints = line.SegmentPoints.Where((_, index) => index != removeIndex).ToList();
        }

        commandService.Do(new LineEditCommand(lineId) { SegmentPoints = updatedPoints });
    }

    public void MoveSegmentPoint(PointerEvent e, double zoom, PointerId pointerId)
    {
        if (!pointerId.IsLinePoint)
            return;

        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(LineId.FromId(pointerId.Id), out var line))
                return;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
                return;
            if (pointerId.LinePointIndex < 0 || pointerId.LinePointIndex >= line.SegmentPoints.Count)
                return;

            var localZoom = GetChildrenLocalZoom(owner) * zoom;
            var dx = e.MovementX * localZoom;
            var dy = e.MovementY * localZoom;
            var points = line.SegmentPoints.ToList();
            var point = points[pointerId.LinePointIndex];
            points[pointerId.LinePointIndex] = point with { X = point.X + dx, Y = point.Y + dy };
            updatedPoints = points;
        }

        commandService.Do(new LineEditCommand(LineId.FromId(pointerId.Id)) { SegmentPoints = updatedPoints });
    }

    public void SnapSegmentPointToGrid(PointerId pointerId)
    {
        if (!pointerId.IsLinePoint)
            return;

        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(LineId.FromId(pointerId.Id), out var line))
                return;
            if (pointerId.LinePointIndex < 0 || pointerId.LinePointIndex >= line.SegmentPoints.Count)
                return;

            var points = line.SegmentPoints.ToList();
            var point = points[pointerId.LinePointIndex];
            var snapped = point with { X = NodeGrid.Snap(point.X), Y = NodeGrid.Snap(point.Y) };
            if (snapped == point)
                return;

            points[pointerId.LinePointIndex] = snapped;
            updatedPoints = points;
        }

        commandService.Do(new LineEditCommand(LineId.FromId(pointerId.Id)) { SegmentPoints = updatedPoints });
    }

    // Where the pointer is during a label drag, in the line owner's children-local space. It
    // follows the pointer freely (the label is put at the closest point of the line), so the
    // label keeps up with the pointer however far it strays from the line.
    Pos labelDragPos = Pos.None;

    public void MoveLabel(PointerEvent e, double zoom, PointerId pointerId)
    {
        if (!pointerId.IsLineLabel)
            return;

        double position;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(pointerId.LineId, out var line) || !line.HasLabel)
                return;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
                return;
            var points = LinePathGeometry.GetLocalPolylinePoints(line);
            if (points.Count < 2)
                return;

            if (labelDragPos == Pos.None)
                labelDragPos = LinePathGeometry.PointAt(points, line.LabelPosition);
            var localZoom = GetChildrenLocalZoom(owner) * zoom;
            labelDragPos = new Pos(labelDragPos.X + e.MovementX * localZoom, labelDragPos.Y + e.MovementY * localZoom);
            position = LinePathGeometry.FractionAt(points, labelDragPos);
            if (Math.Abs(position - line.LabelPosition) < 0.001)
                return;
        }

        commandService.Do(new LineLabelPositionCommand(pointerId.LineId) { Position = position });
    }

    public void EndLabelMove() => labelDragPos = Pos.None;

    static double GetChildrenLocalZoom(Node owner) => owner.GetZoom() / owner.ContainerZoom;

    async Task<Pos?> TryScreenToWorldPosAsync(Pos screenPos)
    {
        if (await screenService.GetBoundingRectangle("svgcanvas") is not ElementBoundingRectangle svgBound)
            return null;

        var localX = screenPos.X - svgBound.X;
        var localY = screenPos.Y - svgBound.Y;
        var (zoom, offset) = modelMgr.WithModel(m => (m.Zoom, m.Offset));
        return new Pos(offset.X + localX * zoom, offset.Y + localY * zoom);
    }

    static Pos ToOwnerChildrenLocal(Node owner, Pos worldPos)
    {
        var (ownerPos, ownerZoom) = owner.GetPosAndZoom();
        var childrenOriginWorld = new Pos(
            ownerPos.X + owner.ContainerOffset.X * ownerZoom,
            ownerPos.Y + owner.ContainerOffset.Y * ownerZoom
        );
        var childrenZoom = ownerZoom * owner.ContainerZoom;
        if (childrenZoom == 0)
            return Pos.None;

        return new Pos(
            (worldPos.X - childrenOriginWorld.X) / childrenZoom,
            (worldPos.Y - childrenOriginWorld.Y) / childrenZoom
        );
    }

    static int GetClosestControlPointIndex(IReadOnlyList<Pos> points, Pos hint)
    {
        if (points.Count == 0)
            return -1;

        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var d = DistanceSquared(points[i], hint);
            if (d >= bestDistance)
                continue;

            bestDistance = d;
            bestIndex = i;
        }

        return bestIndex;
    }

    static IReadOnlyList<Pos> InsertPointAtClosestSegment(Line line, LinePathGeometry.LineEndpoints endpoints, Pos hint)
    {
        var polyline = new List<Pos>(line.SegmentPoints.Count + 2) { new(endpoints.X1, endpoints.Y1) };
        polyline.AddRange(line.SegmentPoints);
        polyline.Add(new Pos(endpoints.X2, endpoints.Y2));

        var bestSegmentIndex = 0;
        var bestDistanceSquared = double.MaxValue;
        Pos projectedPoint = polyline[0];

        for (var i = 0; i < polyline.Count - 1; i++)
        {
            var p1 = polyline[i];
            var p2 = polyline[i + 1];
            var projected = LinePathGeometry.ProjectPointOnSegment(hint, p1, p2);
            var dist2 = DistanceSquared(hint, projected);
            if (dist2 >= bestDistanceSquared)
                continue;

            bestDistanceSquared = dist2;
            bestSegmentIndex = i;
            projectedPoint = projected;
        }

        var updatedPoints = line.SegmentPoints.ToList();
        var insertIndex = bestSegmentIndex;
        updatedPoints.Insert(insertIndex, projectedPoint);
        return updatedPoints;
    }

    static double DistanceSquared(Pos a, Pos b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
