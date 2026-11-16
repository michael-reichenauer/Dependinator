using System.Globalization;
using System.Text;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Editing;

// Smart guides for a node drag: when the dragged node's edge or centre comes close to the same
// edge or centre of a nearby sibling, the node snaps to it and a thin guide line shows the
// alignment. Calm on busy diagrams: only siblings within reach count, and there is at most one
// guide per axis (the closest match).
static class AlignmentSnap
{
    // A guide in the container's children-local units: a vertical guide lies at x = At and runs
    // from y = From to y = To, a horizontal guide the other way round.
    public readonly record struct Guide(double At, double From, double To);

    // How far to shift the box to align it (0 for an axis without a match) and the guide of
    // each aligned axis.
    public readonly record struct Result(double Dx, double Dy, Guide? Vertical, Guide? Horizontal)
    {
        public static readonly Result None = new(0, 0, null, null);
        public bool IsAlignedX => Vertical is not null;
        public bool IsAlignedY => Horizontal is not null;
    }

    // How close, in screen pixels, an edge or centre must come to snap (a finger is less exact).
    public const double MouseSnapPixels = 7;
    public const double TouchSnapPixels = 12;

    // How far away, in screen pixels, a sibling may be (the gap between the boxes on either
    // axis) to be aligned with.
    public const double ReachPixels = 600;

    // How far a guide runs past the boxes it joins, in screen pixels.
    public const double OvershootPixels = 10;

    const double Epsilon = 1e-6;

    // Where the box should go to align with the others: snap and reach in the same units as the
    // boxes, overshoot too.
    public static Result Find(Rect moving, IReadOnlyList<Rect> others, double snap, double reach, double overshoot)
    {
        var near = others
            .Where(other => Gap(moving, other, isX: true) <= reach && Gap(moving, other, isX: false) <= reach)
            .ToList();
        if (near.Count == 0)
            return Result.None;

        var dx = FindShift(moving, near, snap, isX: true);
        var dy = FindShift(moving, near, snap, isX: false);
        var aligned = new Rect(moving.X + (dx ?? 0), moving.Y + (dy ?? 0), moving.Width, moving.Height);
        var vertical = dx is null ? null : GuideFor(aligned, near, isX: true, overshoot);
        var horizontal = dy is null ? null : GuideFor(aligned, near, isX: false, overshoot);

        // A shift without a guide (rounding at far coordinates) is no alignment: the drop would
        // snap that axis to the grid anyway, so the node does not jump to it.
        return new Result(vertical is null ? 0 : dx ?? 0, horizontal is null ? 0 : dy ?? 0, vertical, horizontal);
    }

    // The smallest shift on one axis that lines up an edge or the centre of the box with the same
    // kind of line of another box (edges also with the other box's opposite edge, for boxes side by
    // side or stacked), within snap; on a tie the box nearer across the axis wins. Null when none.
    static double? FindShift(Rect moving, IReadOnlyList<Rect> near, double snap, bool isX)
    {
        var (start, center, end) = Lines(moving, isX);
        double? best = null;
        var bestGap = double.MaxValue;
        foreach (var other in near)
        {
            var (oStart, oCenter, oEnd) = Lines(other, isX);
            var gap = Gap(moving, other, !isX);
            Consider(oStart - start);
            Consider(oEnd - start);
            Consider(oStart - end);
            Consider(oEnd - end);
            Consider(oCenter - center);

            void Consider(double shift)
            {
                var size = Math.Abs(shift);
                if (size > snap)
                    return;
                var bestSize = best is { } b ? Math.Abs(b) : double.MaxValue;
                if (size < bestSize - Epsilon || (size <= bestSize + Epsilon && gap < bestGap))
                {
                    best = shift;
                    bestGap = gap;
                }
            }
        }
        return best;
    }

    // The guide through the aligned box and every near box that has an edge or centre on the
    // same line: one line from the first box to the last, a little past both. The centre goes
    // first: with boxes of one size all three lines match, and a centre guide reads as a row
    // (it is also where the lines between the boxes run straight).
    static Guide? GuideFor(Rect aligned, IReadOnlyList<Rect> near, bool isX, double overshoot)
    {
        var (start, center, end) = Lines(aligned, isX);
        foreach (var at in new[] { center, start, end })
        {
            var joined = near.Where(other => HasLineAt(other, at, isX)).ToList();
            if (joined.Count == 0)
                continue;
            var from = joined.Append(aligned).Min(box => isX ? box.Y : box.X);
            var to = joined.Append(aligned).Max(box => isX ? box.Y + box.Height : box.X + box.Width);
            return new Guide(at, from - overshoot, to + overshoot);
        }
        return null;
    }

    static bool HasLineAt(Rect box, double at, bool isX)
    {
        var (start, center, end) = Lines(box, isX);
        var tolerance = Epsilon * Math.Max(1, Math.Abs(at));
        return Math.Abs(start - at) <= tolerance
            || Math.Abs(center - at) <= tolerance
            || Math.Abs(end - at) <= tolerance;
    }

    static (double Start, double Center, double End) Lines(Rect box, bool isX) =>
        isX ? (box.X, box.X + box.Width / 2, box.X + box.Width) : (box.Y, box.Y + box.Height / 2, box.Y + box.Height);

    // The empty space between two boxes on one axis; 0 when they overlap on it.
    static double Gap(Rect a, Rect b, bool isX) =>
        isX
            ? Math.Max(0, Math.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width)))
            : Math.Max(0, Math.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height)));
}

// A guide line to draw, in canvas coordinates.
readonly record struct GuideLine(Pos From, Pos To);

// The guide lines of the drag in progress, for the canvas to draw over the diagram.
interface IAlignmentGuideService
{
    IReadOnlyList<GuideLine> Lines { get; }

    void Set(IReadOnlyList<GuideLine> lines);

    // Clears the guides (the drag ended) and repaints when there were any.
    void Clear();

    // The guides as svg for the shown tile: canvas point p is drawn at p / levelZoom - tileOffset.
    string GetSvg(double levelZoom, Pos tileOffset);
}

[Scoped]
class AlignmentGuideService(IApplicationEvents applicationEvents) : IAlignmentGuideService
{
    public IReadOnlyList<GuideLine> Lines { get; private set; } = [];

    // Set during a drag: the move's own command repaints the canvas.
    public void Set(IReadOnlyList<GuideLine> lines) => Lines = lines;

    public void Clear()
    {
        if (Lines.Count == 0)
            return;
        Lines = [];
        applicationEvents.TriggerUIStateChanged();
    }

    public string GetSvg(double levelZoom, Pos tileOffset)
    {
        if (Lines.Count == 0 || levelZoom <= 0)
            return "";
        var svg = new StringBuilder("<g class=\"dep-alignment-guides\" pointer-events=\"none\">");
        foreach (var line in Lines)
        {
            var (x1, y1) = (line.From.X / levelZoom - tileOffset.X, line.From.Y / levelZoom - tileOffset.Y);
            var (x2, y2) = (line.To.X / levelZoom - tileOffset.X, line.To.Y / levelZoom - tileOffset.Y);
            svg.Append(
                CultureInfo.InvariantCulture,
                $"<line x1=\"{x1:0.###}\" y1=\"{y1:0.###}\" x2=\"{x2:0.###}\" y2=\"{y2:0.###}\" stroke=\"{DColors.AlignmentGuide}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"/>"
            );
        }
        return svg.Append("</g>").ToString();
    }
}
