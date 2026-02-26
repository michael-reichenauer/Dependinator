using Dependinator.Models;

namespace Dependinator.Diagrams;

interface ILineEditService
{
    void AddSegmentPoint(LineId lineId);
    void RemoveLastSegmentPoint(LineId lineId);
    void MoveSegmentPoint(PointerEvent e, double zoom, PointerId pointerId);
    void SnapSegmentPointToGrid(PointerId pointerId);
}

[Scoped]
class LineEditService(IModelService modelService) : ILineEditService
{
    static double SnapToGrid(double value) => Math.Round(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;

    public void AddSegmentPoint(LineId lineId)
    {
        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelService.UseModel())
        {
            if (!model.TryGetLine(lineId, out var line))
                return;
            if (!LinePathGeometry.TryGetLocalEndpoints(line, out var endpoints))
                return;

            updatedPoints = InsertPointAtLongestSegment(line, endpoints);
        }

        modelService.Do(new LineEditCommand(lineId) { SegmentPoints = updatedPoints });
    }

    public void RemoveLastSegmentPoint(LineId lineId)
    {
        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelService.UseModel())
        {
            if (!model.TryGetLine(lineId, out var line))
                return;
            if (line.SegmentPoints.Count == 0)
                return;

            updatedPoints = [.. line.SegmentPoints.Take(line.SegmentPoints.Count - 1)];
        }

        modelService.Do(new LineEditCommand(lineId) { SegmentPoints = updatedPoints });
    }

    public void MoveSegmentPoint(PointerEvent e, double zoom, PointerId pointerId)
    {
        if (!pointerId.IsLinePoint)
            return;

        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelService.UseModel())
        {
            if (!model.TryGetLine(LineId.FromId(pointerId.Id), out var line))
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

        modelService.Do(new LineEditCommand(LineId.FromId(pointerId.Id)) { SegmentPoints = updatedPoints });
    }

    public void SnapSegmentPointToGrid(PointerId pointerId)
    {
        if (!pointerId.IsLinePoint)
            return;

        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelService.UseModel())
        {
            if (!model.TryGetLine(LineId.FromId(pointerId.Id), out var line))
                return;
            if (pointerId.LinePointIndex < 0 || pointerId.LinePointIndex >= line.SegmentPoints.Count)
                return;

            var points = line.SegmentPoints.ToList();
            var point = points[pointerId.LinePointIndex];
            var snapped = point with { X = SnapToGrid(point.X), Y = SnapToGrid(point.Y) };
            if (snapped == point)
                return;

            points[pointerId.LinePointIndex] = snapped;
            updatedPoints = points;
        }

        modelService.Do(new LineEditCommand(LineId.FromId(pointerId.Id)) { SegmentPoints = updatedPoints });
    }

    static double GetChildrenLocalZoom(Node owner) => owner.GetZoom() / owner.ContainerZoom;

    static IReadOnlyList<Pos> InsertPointAtLongestSegment(Line line, LinePathGeometry.LineEndpoints endpoints)
    {
        var polyline = new List<Pos>(line.SegmentPoints.Count + 2) { new(endpoints.X1, endpoints.Y1) };
        polyline.AddRange(line.SegmentPoints);
        polyline.Add(new Pos(endpoints.X2, endpoints.Y2));

        var bestSegmentIndex = 0;
        var bestLengthSquared = -1.0;
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            var p1 = polyline[i];
            var p2 = polyline[i + 1];
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len2 = dx * dx + dy * dy;
            if (len2 <= bestLengthSquared)
                continue;

            bestLengthSquared = len2;
            bestSegmentIndex = i;
        }

        var start = polyline[bestSegmentIndex];
        var end = polyline[bestSegmentIndex + 1];
        var midpoint = new Pos((start.X + end.X) / 2, (start.Y + end.Y) / 2);

        var updatedPoints = line.SegmentPoints.ToList();
        var insertIndex = bestSegmentIndex;
        updatedPoints.Insert(insertIndex, midpoint);
        return updatedPoints;
    }
}
