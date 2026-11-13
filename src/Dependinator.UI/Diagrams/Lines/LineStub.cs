using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Lines;

// A line between a container's edge and one of its children (an "edge line") stands for the
// links that cross the container's border: into the child from outside, or out of the child.
// All such lines of a container used to meet at the middle of its left and right edge, long
// lines over the siblings that could not be followed through that point anyway. They are now
// drawn as a stub at the child: a short arrow into its left side or out of its right side, with
// a ring at the far end for "continues outside the container". Selecting the child shows where
// its lines really go (see RepLineService).
//
// A stub is the same line, only drawn short: its element id, tooltip, selection and styling
// are those of the line. An edge line the user has labelled or routed with line points, and one
// on the dependency path being shown, is still drawn in full.
static class LineStub
{
    // The stub's length in the container's units, within these limits in tile pixels: like the
    // arrow heads, a stub does not grow with the zoom. The layout leaves at least 60 units between
    // two nodes side by side, so the stub out of the left one and the stub into the right one
    // (each with its ring) stay apart.
    const double LengthUnits = 18;

    // A member row's stub is shorter, so the one into the row stays on its card.
    const double MemberLengthUnits = 12;
    const double MinLength = 5;
    const double MaxLength = 22;

    const double RingFactor = 0.13;
    const double MinRingRadius = 1.5;
    const double MaxRingRadius = 2.8;

    // A stub is level (or upright at an inheritance end). When a sibling line leaves or enters
    // the same anchor in nearly that direction, the stub would lie on top of it, so it is turned
    // up or down instead.
    const double TiltDegrees = 35;
    const double MinSeparationDegrees = 25;

    // Start and End are the visible stub in its arrow's direction; Ring is the center of the
    // ring at the end away from the child; HitStart and HitEnd also cover the ring.
    public readonly record struct Geometry(Pos Start, Pos End, Pos Ring, double RingRadius, Pos HitStart, Pos HitEnd);

    // From the container's edge into one of its children.
    public static bool IsIncoming(Line line) => IsChainLine(line) && line.Target.Parent == line.Source;

    // From a child to its container's edge.
    public static bool IsOutgoing(Line line) => IsChainLine(line) && line.Source.Parent == line.Target;

    public static bool IsEdgeLine(Line line) => IsIncoming(line) || IsOutgoing(line);

    public static bool IsStub(Line line, bool isOnPath) =>
        IsEdgeLine(line) && !isOnPath && !line.HasLabel && !line.IsSegmentsUserSet;

    // The child the edge line belongs to (the stub is drawn at it).
    public static Node Child(Line line) => IsIncoming(line) ? line.Target : line.Source;

    // The stub of an edge line whose child end is rendered at anchor. minLength makes room for
    // an arrow head longer than the usual stub (the hollow inheritance head).
    public static Geometry GetGeometry(Line line, Pos anchor, double childrenZoom, double minLength = 0)
    {
        var isIncoming = IsIncoming(line);
        var units = Child(line).Type.IsMember ? MemberLengthUnits : LengthUnits;
        var length = Math.Max(minLength, Math.Clamp(units * childrenZoom, MinLength, MaxLength));
        var radius = Math.Clamp(length * RingFactor, MinRingRadius, MaxRingRadius);
        var (dx, dy) = GetDirection(line);

        if (isIncoming)
        {
            var start = new Pos(anchor.X - dx * length, anchor.Y - dy * length);
            var ring = new Pos(start.X - dx * radius, start.Y - dy * radius);
            var hitStart = new Pos(ring.X - dx * radius, ring.Y - dy * radius);
            return new Geometry(start, anchor, ring, radius, hitStart, anchor);
        }

        var end = new Pos(anchor.X + dx * length, anchor.Y + dy * length);
        var outerRing = new Pos(end.X + dx * radius, end.Y + dy * radius);
        var hitEnd = new Pos(outerRing.X + dx * radius, outerRing.Y + dy * radius);
        return new Geometry(anchor, end, outerRing, radius, anchor, hitEnd);
    }

    // The unit direction the stub's arrow points in: to the right (into the child's left side,
    // out of its right side), or upward at an inheritance end (into the supertype's bottom, out
    // of the subtype's top), turned aside when a sibling line already runs there.
    internal static (double X, double Y) GetDirection(Line line)
    {
        var isIncoming = IsIncoming(line);
        var isUpright = isIncoming ? line.HasInheritanceTargetEnd : line.HasInheritanceSourceEnd;
        (double X, double Y) level = isUpright ? (0, -1) : (1, 0);

        Span<(double X, double Y)> taken = stackalloc (double, double)[MaxSiblingDirections];
        var count = FillSiblingDirections(line, isIncoming, isUpright, taken);
        if (count == 0)
            return level;
        taken = taken[..count];

        var best = level;
        var bestSeparation = -1.0;
        foreach (var degrees in (ReadOnlySpan<double>)[0, -TiltDegrees, TiltDegrees])
        {
            var candidate = Rotate(level, degrees);
            var separation = MinSeparationDegreesTo(candidate, taken);
            if (separation >= MinSeparationDegrees)
                return candidate;
            if (separation > bestSeparation)
            {
                best = candidate;
                bestSeparation = separation;
            }
        }
        return best;
    }

    const int MaxSiblingDirections = 16;

    // The directions of the drawn sibling lines that share the stub's anchor at the child, each
    // as the direction an arrow travels through the anchor (so it compares with the stub's).
    static int FillSiblingDirections(Line line, bool isIncoming, bool isUpright, Span<(double X, double Y)> directions)
    {
        var child = isIncoming ? line.Target : line.Source;
        var siblings = isIncoming ? child.TargetLines : child.SourceLines;
        var count = 0;
        foreach (var sibling in siblings)
        {
            if (count == directions.Length)
                break;
            if (sibling == line || !sibling.IsActiveRep || sibling.Visibility == NodeVisibility.Hidden)
                continue;
            if (!IsChainLine(sibling) || sibling.Source.Parent != sibling.Target.Parent)
                continue;
            var isSiblingUpright = isIncoming ? sibling.HasInheritanceTargetEnd : sibling.HasInheritanceSourceEnd;
            if (isSiblingUpright != isUpright)
                continue; // Attached at another anchor of the child
            if (!LinePathGeometry.TryGetLocalEndpoints(sibling, out var ends))
                continue;

            var points = sibling.SegmentPoints;
            var (fromX, fromY, toX, toY) = isIncoming
                ? (
                    points.Count > 0 ? points[^1].X : ends.X1,
                    points.Count > 0 ? points[^1].Y : ends.Y1,
                    ends.X2,
                    ends.Y2
                )
                : (
                    ends.X1,
                    ends.Y1,
                    points.Count > 0 ? points[0].X : ends.X2,
                    points.Count > 0 ? points[0].Y : ends.Y2
                );
            var dx = toX - fromX;
            var dy = toY - fromY;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0)
                continue;
            directions[count++] = (dx / length, dy / length);
        }
        return count;
    }

    static double MinSeparationDegreesTo((double X, double Y) direction, ReadOnlySpan<(double X, double Y)> taken)
    {
        var min = 180.0;
        foreach (var other in taken)
        {
            var cos = Math.Clamp(direction.X * other.X + direction.Y * other.Y, -1, 1);
            min = Math.Min(min, Math.Acos(cos) * 180 / Math.PI);
        }
        return min;
    }

    static (double X, double Y) Rotate((double X, double Y) v, double degrees)
    {
        if (degrees == 0)
            return v;
        var radians = degrees * Math.PI / 180;
        var (sin, cos) = Math.SinCos(radians);
        return (v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    // A funnel segment or sibling line of the node tree, not a direct or cousin line (those are
    // drawn in an ancestor's space and cross container borders).
    static bool IsChainLine(Line line) => !line.IsDirect && line.RenderAncestor is null;
}
