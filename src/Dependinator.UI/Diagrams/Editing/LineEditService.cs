using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Editing;

interface ILineEditService
{
    // Adds a bend point at a selected line's segment handle (pointerId.IsLineMidPoint), moved by
    // the drag so far (in screen pixels), and returns the new point's pointer id for the rest of
    // the drag; null when the line has no such segment.
    PointerId? InsertSegmentPoint(PointerId midPointId, double screenDx, double screenDy, double zoom);

    // Moves a bend point under a drag by the pointer's movement in screen pixels.
    void MoveSegmentPoint(double movementX, double movementY, double zoom, PointerId pointerId);

    // Ends a bend-point drag: a point dragged back onto the straight line between its neighbours
    // is removed, any other is snapped to the grid.
    void EndSegmentPointMove(PointerId pointerId, double zoom);
    void RemoveSegmentPoint(PointerId pointerId);

    // Removes all bend points, so the line runs straight between its ends.
    void StraightenLine(LineId lineId);

    // Slides a line's label along the line under an edit-mode drag (pointerId.IsLineLabel) by the
    // pointer's movement in screen pixels, and ends the drag.
    void MoveLabel(double movementX, double movementY, double zoom, PointerId pointerId);
    void EndLabelMove();
}

[Scoped]
class LineEditService(IModelMgr modelMgr, ICommandService commandService) : ILineEditService
{
    // A bend point released closer than this (screen pixels) to the straight line between its
    // neighbours is removed: dragging a bend back is how it is undone without a toolbar button.
    internal const double StraightTolerance = 6;

    public PointerId? InsertSegmentPoint(PointerId midPointId, double screenDx, double screenDy, double zoom)
    {
        if (!midPointId.IsLineMidPoint)
            return null;

        var segmentIndex = midPointId.LineMidPointIndex;
        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(midPointId.LineId, out var line))
                return null;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
                return null;
            var polyline = LinePathGeometry.GetLocalPolylinePoints(line);
            if (polyline.Count != line.SegmentPoints.Count + 2 || segmentIndex >= polyline.Count - 1)
                return null;

            var (a, b) = (polyline[segmentIndex], polyline[segmentIndex + 1]);
            var fraction = midPointId.LineMidPointFraction;
            var localZoom = GetChildrenLocalZoom(owner) * zoom;
            var point = new Pos(
                a.X + (b.X - a.X) * fraction + screenDx * localZoom,
                a.Y + (b.Y - a.Y) * fraction + screenDy * localZoom
            );
            var points = line.SegmentPoints.ToList();
            points.Insert(segmentIndex, point);
            updatedPoints = points;
        }

        commandService.Do(new LineEditCommand(midPointId.LineId) { SegmentPoints = updatedPoints });
        return PointerId.FromLinePoint(midPointId.LineId, segmentIndex);
    }

    public void MoveSegmentPoint(double movementX, double movementY, double zoom, PointerId pointerId)
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
            var dx = movementX * localZoom;
            var dy = movementY * localZoom;
            var points = line.SegmentPoints.ToList();
            var point = points[pointerId.LinePointIndex];
            points[pointerId.LinePointIndex] = point with { X = point.X + dx, Y = point.Y + dy };
            updatedPoints = points;
        }

        commandService.Do(new LineEditCommand(LineId.FromId(pointerId.Id)) { SegmentPoints = updatedPoints });
    }

    public void EndSegmentPointMove(PointerId pointerId, double zoom)
    {
        if (!pointerId.IsLinePoint)
            return;

        var index = pointerId.LinePointIndex;
        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(pointerId.LineId, out var line))
                return;
            if (index < 0 || index >= line.SegmentPoints.Count)
                return;

            var points = line.SegmentPoints.ToList();
            if (IsOnStraightLine(line, index, zoom))
            {
                points.RemoveAt(index);
            }
            else
            {
                var point = points[index];
                var snapped = point with { X = NodeGrid.Snap(point.X), Y = NodeGrid.Snap(point.Y) };
                if (snapped == point)
                    return;
                points[index] = snapped;
            }
            updatedPoints = points;
        }

        commandService.Do(new LineEditCommand(pointerId.LineId) { SegmentPoints = updatedPoints });
    }

    public void RemoveSegmentPoint(PointerId pointerId)
    {
        if (!pointerId.IsLinePoint)
            return;

        IReadOnlyList<Pos> updatedPoints;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(pointerId.LineId, out var line))
                return;
            if (pointerId.LinePointIndex >= line.SegmentPoints.Count)
                return;
            updatedPoints = line.SegmentPoints.Where((_, i) => i != pointerId.LinePointIndex).ToList();
        }

        commandService.Do(
            new LineEditCommand(pointerId.LineId) { SegmentPoints = updatedPoints, IsOwnUndoStep = true }
        );
    }

    public void StraightenLine(LineId lineId)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line) || line.SegmentPoints.Count == 0)
                return;
        }

        commandService.Do(new LineEditCommand(lineId) { SegmentPoints = [], IsOwnUndoStep = true });
    }

    // Whether the bend point lies within StraightTolerance screen pixels of the straight line
    // between its neighbours (the line's ends or the bend points beside it).
    static bool IsOnStraightLine(Line line, int index, double zoom)
    {
        if (!LinePathGeometry.TryGetOwnerNode(line, out var owner))
            return false;
        var polyline = LinePathGeometry.GetLocalPolylinePoints(line);
        if (polyline.Count != line.SegmentPoints.Count + 2)
            return false;

        var (before, point, after) = (polyline[index], polyline[index + 1], polyline[index + 2]);
        var projected = LinePathGeometry.ProjectPointOnSegment(point, before, after);
        var tolerance = StraightTolerance * GetChildrenLocalZoom(owner) * zoom;
        return DistanceSquared(point, projected) <= tolerance * tolerance;
    }

    // Where the pointer is during a label drag, in the line owner's children-local space. It
    // follows the pointer freely (the label is put at the closest point of the line), so the
    // label keeps up with the pointer however far it strays from the line.
    Pos? labelDragPos;

    public void MoveLabel(double movementX, double movementY, double zoom, PointerId pointerId)
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

            var from = labelDragPos ?? LinePathGeometry.PointAt(points, line.LabelPosition);
            var localZoom = GetChildrenLocalZoom(owner) * zoom;
            var to = new Pos(from.X + movementX * localZoom, from.Y + movementY * localZoom);
            labelDragPos = to;
            // Compared as it would be stored, so a pointer past a clamped end sends nothing.
            position = Line.ToLabelPosition(LinePathGeometry.FractionAt(points, to));
            if (position == line.LabelPosition)
                return;
        }

        commandService.Do(new LineLabelPositionCommand(pointerId.LineId) { Position = position });
    }

    public void EndLabelMove() => labelDragPos = null;

    static double GetChildrenLocalZoom(Node owner) => owner.GetZoom() / owner.ContainerZoom;

    static double DistanceSquared(Pos a, Pos b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
