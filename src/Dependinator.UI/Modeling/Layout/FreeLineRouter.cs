using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// Routes a line around the nodes between its ends where they stand now, for nodes the user placed:
// the layered layout routes by its columns (LayeredEngine.RouteLines), which a moved node no longer
// keeps to. Starts from the straight line and, where a piece of it passes through a node, bends it
// just above or below that node (beside it, for a piece that runs more up or down than across),
// then checks the new pieces again, a few levels deep, keeping the cheapest result: passing
// through a node costs most, then a bend, then length. Works on rects only, no model access.
static class FreeLineRouter
{
    // How close a line may pass a node, like the layered layout's routing.
    public const double Clearance = 8;

    // How far above or below a node a detour passes when no other node is near it (half the
    // layout's regular gap).
    const double DetourGap = 35;

    // How far beyond the blocker a bend over its middle may go to clear it.
    const double MaxRaise = 3 * DetourGap;

    // The narrowest gap between two nodes a detour goes through (between them, in the middle).
    const double MinPassGap = 2 * Clearance + 4;

    // Levels of bending: each level can split each piece once more (up to 7 bends).
    const int MaxDepth = 3;

    // The pieces one line's search checks at most, so that a line through a crowd of nodes stays
    // quick; past it, the pieces left are taken straight.
    const int MaxPiecesPerLine = 150;

    const double ThroughNodeCost = 100_000;
    const double BendCost = 40;

    // The waypoints of the line from start to end around the obstacles (the nodes the line may
    // not pass through: not its own ends); none when the straight line is clear.
    public static IReadOnlyList<Pos> Route(Pos start, Pos end, IReadOnlyList<Rect> obstacles) =>
        Route(start, end, new Obstacles(obstacles));

    public static IReadOnlyList<Pos> Route(Pos start, Pos end, Obstacles obstacles) =>
        obstacles.Count == 0 ? [] : new Search(obstacles).Route(start, end, MaxDepth).Points;

    // Whether the line over the points (its ends included) passes through or too close to one of
    // the obstacles.
    public static bool IsBlocked(IReadOnlyList<Pos> points, IReadOnlyList<Rect> obstacles) =>
        IsBlocked(points, new Obstacles(obstacles));

    public static bool IsBlocked(IReadOnlyList<Pos> points, Obstacles obstacles)
    {
        for (var index = 0; index < points.Count - 1; index++)
        {
            if (FirstBlocker(points[index], points[index + 1], obstacles) is not null)
                return true;
        }
        return false;
    }

    // The nodes to route around, in a grid of cells, so that a piece is checked only against the
    // nodes near it (a container can hold hundreds). A line's own ends are left out with Without,
    // which shares the grid (and its transposed twin) instead of building another.
    public sealed class Obstacles
    {
        readonly Grid grid;
        readonly Lazy<Grid> transposed;
        readonly int[] excluded;

        public Obstacles(IReadOnlyList<Rect> rects)
            : this(new Grid([.. rects.Where(rect => rect.Width > 0 && rect.Height > 0)]), null, []) { }

        Obstacles(Grid grid, Lazy<Grid>? transposed, int[] excluded)
        {
            this.grid = grid;
            this.transposed = transposed ?? new Lazy<Grid>(() => new Grid([.. grid.Rects.Select(Transpose)]));
            this.excluded = excluded;
            Count = grid.Rects.Count - excluded.Length;
        }

        public int Count { get; }

        // The same nodes without the given ones (the same rect objects: a line's own ends).
        public Obstacles Without(params Rect[] rects) =>
            new(grid, transposed, [.. rects.Select(grid.IndexOf).Where(index => index >= 0).Distinct()]);

        // The same nodes with x and y swapped (to route a piece that runs up or down).
        public Obstacles Transposed() => new(transposed.Value, new Lazy<Grid>(() => grid), excluded);

        // The nodes that may overlap the area, each once.
        public List<Rect> In(double minX, double minY, double maxX, double maxY) =>
            grid.In(minX, minY, maxX, maxY, excluded);
    }

    // The rects by the cells of a grid they overlap.
    sealed class Grid
    {
        const double CellSize = 200;

        readonly Dictionary<(int, int), List<int>> cells = [];
        readonly Dictionary<Rect, int> indexes = new(ReferenceEqualityComparer.Instance);
        readonly int[] seen;
        int stamp;

        public Grid(IReadOnlyList<Rect> rects)
        {
            Rects = rects;
            seen = new int[rects.Count];
            for (var index = 0; index < rects.Count; index++)
            {
                var rect = rects[index];
                indexes.TryAdd(rect, index);
                foreach (var cell in Cells(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height))
                {
                    if (!cells.TryGetValue(cell, out var list))
                        cells[cell] = list = [];
                    list.Add(index);
                }
            }
        }

        public IReadOnlyList<Rect> Rects { get; }

        // The index of this very rect object, or -1.
        public int IndexOf(Rect rect) => indexes.TryGetValue(rect, out var index) ? index : -1;

        public List<Rect> In(double minX, double minY, double maxX, double maxY, int[] excluded)
        {
            stamp++; // One stamp per query, so a rect in several cells is listed once
            var found = new List<Rect>();
            foreach (var cell in Cells(minX, minY, maxX, maxY))
            {
                if (!cells.TryGetValue(cell, out var list))
                    continue;
                foreach (var index in list)
                {
                    if (seen[index] == stamp)
                        continue;
                    seen[index] = stamp;
                    if (Array.IndexOf(excluded, index) < 0)
                        found.Add(Rects[index]);
                }
            }
            return found;
        }

        static IEnumerable<(int, int)> Cells(double minX, double minY, double maxX, double maxY)
        {
            var x1 = (int)Math.Floor(minX / CellSize);
            var x2 = (int)Math.Floor(maxX / CellSize);
            var y1 = (int)Math.Floor(minY / CellSize);
            var y2 = (int)Math.Floor(maxY / CellSize);
            for (var x = x1; x <= x2; x++)
            for (var y = y1; y <= y2; y++)
                yield return (x, y);
        }
    }

    // One line's search: the pieces already routed are kept (the same corners come up in many
    // branches), and the work is capped.
    sealed class Search(Obstacles obstacles)
    {
        readonly Lazy<Obstacles> transposed = new(obstacles.Transposed);
        readonly Dictionary<(Pos, Pos, int), (List<Pos> Points, double Cost)> routed = [];
        int workLeft = MaxPiecesPerLine;

        public (List<Pos> Points, double Cost) Route(Pos start, Pos end, int depth)
        {
            if (routed.TryGetValue((start, end, depth), out var known))
                return known;
            var result = RouteNew(start, end, depth);
            routed[(start, end, depth)] = result;
            return result;
        }

        (List<Pos> Points, double Cost) RouteNew(Pos start, Pos end, int depth)
        {
            workLeft--;
            var length = Distance(start, end);
            if (FirstBlocker(start, end, obstacles) is not { } blocker)
                return ([], length);

            (List<Pos> Points, double Cost) best = (
                [],
                length + ThroughNodeCost * CountBlockers(start, end, obstacles)
            );
            if (depth == 0 || workLeft <= 0)
                return best;

            foreach (var detour in DetourPoints(start, end, blocker))
            {
                var before = Route(start, detour, depth - 1);
                if (before.Cost + BendCost >= best.Cost)
                    continue;
                var after = Route(detour, end, depth - 1);
                var cost = before.Cost + after.Cost + BendCost;
                if (cost < best.Cost)
                    best = ([.. before.Points, detour, .. after.Points], cost);
            }
            return best;
        }

        // The bend points that take the piece around the blocker: above and below its middle for
        // a piece that runs more across than up or down, otherwise left and right of it (the same,
        // transposed). A point inside a node is no way around.
        List<Pos> DetourPoints(Pos start, Pos end, Rect blocker)
        {
            var isAcross = Math.Abs(end.X - start.X) >= Math.Abs(end.Y - start.Y);
            var points = isAcross
                ? AcrossDetours(start, end, blocker, obstacles)
                : AcrossDetours(Transpose(start), Transpose(end), Transpose(blocker), transposed.Value)
                    .Select(Transpose)
                    .ToList();
            return points.Where(point => !IsInAny(point)).ToList();
        }

        bool IsInAny(Pos point) =>
            obstacles
                .In(point.X - Clearance, point.Y - Clearance, point.X + Clearance, point.Y + Clearance)
                .Any(rect => IsWithin(rect, Clearance, point));
    }

    // Above and below the blocker's middle, high enough that both pieces to the bend clear the
    // blocker; where that would be far off (an end close beside the blocker), its two corners on
    // that side instead (the next level bends around the other one).
    static List<Pos> AcrossDetours(Pos start, Pos end, Rect blocker, Obstacles obstacles)
    {
        var points = new List<Pos>();
        var x = blocker.X + blocker.Width / 2;
        var clear = Inflate(blocker, Clearance + 1);
        foreach (var isUp in new[] { true, false })
        {
            var y = DetourOffset(blocker, x, obstacles, isUp);
            var limit = isUp ? clear.Y - MaxRaise : clear.Y + clear.Height + MaxRaise;
            y = Clamp(y, ClearHeight(start, x, clear, isUp), isUp);
            y = Clamp(y, ClearHeight(end, x, clear, isUp), isUp);
            if (isUp ? y >= limit : y <= limit)
            {
                points.Add(new Pos(x, y));
                continue;
            }

            var cornerY = isUp ? clear.Y : clear.Y + clear.Height;
            points.Add(new Pos(clear.X, cornerY));
            points.Add(new Pos(clear.X + clear.Width, cornerY));
        }
        return points;
    }

    // The highest (isUp) or lowest bend height over x that a straight piece from the end clears
    // the rect with; null when any height does (the end is beyond the rect on that side).
    static double? ClearHeight(Pos from, double x, Rect clear, bool isUp)
    {
        var isLeft = from.X < clear.X;
        var isRight = from.X > clear.X + clear.Width;
        if (!isLeft && !isRight)
        {
            var isBeyond = isUp ? from.Y <= clear.Y : from.Y >= clear.Y + clear.Height;
            return isBeyond ? null : (isUp ? double.NegativeInfinity : double.PositiveInfinity);
        }

        // The piece must pass the rect's near corner on the detour's side.
        var cornerX = isLeft ? clear.X : clear.X + clear.Width;
        var cornerY = isUp ? clear.Y : clear.Y + clear.Height;
        var fraction = (cornerX - from.X) / (x - from.X);
        return from.Y + (cornerY - from.Y) / fraction;
    }

    static double Clamp(double y, double? bound, bool isUp) =>
        bound is not { } value ? y
        : isUp ? Math.Min(y, value)
        : Math.Max(y, value);

    // The height above or below the blocker where a line at x passes it: the middle of the gap to
    // the next node in the way when that gap is wide enough, otherwise past that node too.
    static double DetourOffset(Rect blocker, double x, Obstacles obstacles, bool isUp)
    {
        var edge = isUp ? blocker.Y : blocker.Y + blocker.Height;
        for (var step = 0; step <= obstacles.Count; step++)
        {
            var y = isUp ? edge - DetourGap : edge + DetourGap;
            // The nearest node at x beyond the edge that the detour would come too close to.
            Rect? next = null;
            var nextDistance = double.MaxValue;
            var (fromY, toY) = isUp ? (y - Clearance, edge) : (edge, y + Clearance);
            foreach (var rect in obstacles.In(x - Clearance, fromY, x + Clearance, toY))
            {
                if (x <= rect.X - Clearance || x >= rect.X + rect.Width + Clearance)
                    continue;
                var isInTheWay = isUp
                    ? rect.Y + rect.Height <= edge && rect.Y + rect.Height + Clearance > y
                    : rect.Y >= edge && rect.Y - Clearance < y;
                var distance = isUp ? edge - (rect.Y + rect.Height) : rect.Y - edge;
                if (isInTheWay && distance < nextDistance)
                    (next, nextDistance) = (rect, distance);
            }
            if (next is null)
                return y;

            var gap = isUp ? edge - (next.Y + next.Height) : next.Y - edge;
            if (gap >= MinPassGap)
                return isUp ? edge - gap / 2 : edge + gap / 2;
            edge = isUp ? next.Y : next.Y + next.Height;
        }
        return isUp ? edge - DetourGap : edge + DetourGap;
    }

    // The obstacle the piece enters first, if any (passing within the clearance counts).
    static Rect? FirstBlocker(Pos start, Pos end, Obstacles obstacles)
    {
        Rect? first = null;
        var firstEntry = double.MaxValue;
        foreach (var rect in Near(start, end, obstacles))
        {
            if (EntryFactor(start, end, rect, Clearance) is { } entry && entry < firstEntry)
                (first, firstEntry) = (rect, entry);
        }
        return first;
    }

    static int CountBlockers(Pos start, Pos end, Obstacles obstacles) =>
        Near(start, end, obstacles).Count(rect => EntryFactor(start, end, rect, Clearance) is not null);

    static List<Rect> Near(Pos start, Pos end, Obstacles obstacles) =>
        obstacles.In(
            Math.Min(start.X, end.X) - Clearance,
            Math.Min(start.Y, end.Y) - Clearance,
            Math.Max(start.X, end.X) + Clearance,
            Math.Max(start.Y, end.Y) + Clearance
        );

    // Where along the piece (0 to 1) it enters the inside of the rect grown by the margin, or
    // null when it does not (Liang-Barsky clipping; running along an edge does not count).
    static double? EntryFactor(Pos start, Pos end, Rect rect, double margin)
    {
        var (x1, y1) = (rect.X - margin, rect.Y - margin);
        var (x2, y2) = (rect.X + rect.Width + margin, rect.Y + rect.Height + margin);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var t0 = 0.0;
        var t1 = 1.0;
        if (
            !Clip(-dx, start.X - x1, ref t0, ref t1)
            || !Clip(dx, x2 - start.X, ref t0, ref t1)
            || !Clip(-dy, start.Y - y1, ref t0, ref t1)
            || !Clip(dy, y2 - start.Y, ref t0, ref t1)
        )
            return null;
        return t0;
    }

    static bool Clip(double p, double q, ref double t0, ref double t1)
    {
        if (p == 0)
            return q > 0;
        var t = q / p;
        if (p < 0)
            t0 = Math.Max(t0, t);
        else
            t1 = Math.Min(t1, t);
        return t0 < t1;
    }

    static Rect Inflate(Rect rect, double margin) =>
        new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);

    // Whether the point is inside the rect grown by the margin.
    static bool IsWithin(Rect rect, double margin, Pos point) =>
        point.X > rect.X - margin
        && point.X < rect.X + rect.Width + margin
        && point.Y > rect.Y - margin
        && point.Y < rect.Y + rect.Height + margin;

    static Rect Transpose(Rect rect) => new(rect.Y, rect.X, rect.Height, rect.Width);

    static Pos Transpose(Pos pos) => new(pos.Y, pos.X);

    static double Distance(Pos a, Pos b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
