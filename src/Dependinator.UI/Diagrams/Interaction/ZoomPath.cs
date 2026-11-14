using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// The "smooth and efficient" zoom and pan path of van Wijk and Nuij (2003), the one d3's
// interpolateZoom and map apps' fly-to use: the view pans and zooms at the same time, zooming
// out on the way just as far as a long jump needs and back in as it arrives, so the motion
// feels like one flight at an even pace instead of three separate steps.
//
// A view is its center (canvas units) and its width w (canvas units across the screen, so
// the screen size times the model zoom). At(s) gives the view at a share s of the path, and
// AtTime(t) at a share t of the flight's time.
sealed class ZoomPath
{
    // Curvature: how far a long jump zooms out. sqrt(2) is van Wijk's measured optimum (and
    // d3's default): larger zooms out further.
    const double Rho = 1.41421356237;
    const double Rho2 = Rho * Rho;
    const double Rho4 = Rho2 * Rho2;

    // A pan shorter than this share of the wider end's width is no pan: only a zoom. The
    // hyperbolic path divides by the pan's length.
    const double MinPanShare = 1e-6;

    // A top less than this (log scale) above the wider end is no top: about 1.5 times wider.
    const double MinTopLog = 0.4;
    const int TableSteps = 200;

    readonly Pos from;
    readonly double dx;
    readonly double dy;
    readonly double d1;
    readonly double w0;
    readonly double r0;
    readonly double logW;
    readonly bool isZoomOnly;
    readonly double[] timeTable;

    // dwell is how much slower the flight is at its top, where the view is widest (0: an even
    // pace along the path, 1: twice as slow at the top).
    public ZoomPath(Pos fromCenter, double fromWidth, Pos toCenter, double toWidth, double dwell = 0)
    {
        from = fromCenter;
        w0 = fromWidth;
        var w1 = toWidth;
        dx = toCenter.X - fromCenter.X;
        dy = toCenter.Y - fromCenter.Y;
        d1 = Math.Sqrt(dx * dx + dy * dy);

        if (d1 < MinPanShare * Math.Max(w0, w1))
        {
            // Only a zoom (and the tiny pan along): an even pace in log scale.
            isZoomOnly = true;
            logW = Math.Log(w1 / w0);
            Length = Math.Abs(logW) / Rho;
        }
        else
        {
            var b0 = (w1 * w1 - w0 * w0 + Rho4 * d1 * d1) / (2 * w0 * Rho2 * d1);
            var b1 = (w1 * w1 - w0 * w0 - Rho4 * d1 * d1) / (2 * w1 * Rho2 * d1);
            // The paper's ln(sqrt(b² + 1) - b), written as -asinh(b), which stays finite for a
            // large b (a big zoom with a small pan) where the subtraction cancels to 0.
            r0 = -Math.Asinh(b0);
            var r1 = -Math.Asinh(b1);
            Length = (r1 - r0) / Rho;
        }

        timeTable = BuildTimeTable(dwell);
    }

    // The path's length S, in van Wijk's units: a zoom by e^sqrt(2) or a pan by about a view's
    // width is 1. The animation's duration is taken from it. Not finite for a broken input
    // (a zero or infinite width).
    public double Length { get; }

    // The view at a share of the path (0 to 1), at an even pace along it.
    public (Pos Center, double Width) At(double share)
    {
        share = Math.Clamp(share, 0, 1);
        if (isZoomOnly)
            return (new Pos(from.X + share * dx, from.Y + share * dy), w0 * Math.Exp(share * logW));

        var s = share * Length;
        var coshR0 = Math.Cosh(r0);
        var u = w0 / (Rho2 * d1) * (coshR0 * Math.Tanh(Rho * s + r0) - Math.Sinh(r0));
        u = Math.Clamp(u, 0, 1); // Rounding at a large r0 can step a hair past the ends
        var w = w0 * coshR0 / Math.Cosh(Rho * s + r0);
        return (new Pos(from.X + u * dx, from.Y + u * dy), w);
    }

    // The view at a share of the flight's time (0 to 1): the flight starts and lands softly
    // and slows down near its top (the dwell), so the user has a moment to see where the
    // target lies relative to the start.
    public (Pos Center, double Width) AtTime(double timeShare)
    {
        var time = EaseInOut(Math.Clamp(timeShare, 0, 1)) * timeTable[^1];
        var i = Array.BinarySearch(timeTable, time);
        if (i < 0)
            i = ~i;
        if (i <= 0)
            return At(0);
        if (i >= timeTable.Length)
            return At(1);
        var segment = timeTable[i] - timeTable[i - 1];
        var part = segment > 0 ? (time - timeTable[i - 1]) / segment : 0;
        return At((i - 1 + part) / TableSteps);
    }

    // The time to reach each of TableSteps even steps along the path, each step slowed down by
    // how near its view is to the widest one, in log scale between the wider end and the top.
    // A flight that does not zoom out beyond its ends (a zoom in or out, a short hop) has no top.
    double[] BuildTimeTable(double dwell)
    {
        var widths = Enumerable.Range(0, TableSteps + 1).Select(i => At((double)i / TableSteps).Width).ToArray();
        var topLog = Math.Log(widths.Max());
        var endLog = Math.Log(Math.Max(widths[0], widths[^1]));
        var hasTop = dwell > 0 && topLog - endLog > MinTopLog;
        var table = new double[TableSteps + 1];
        for (var i = 1; i <= TableSteps; i++)
        {
            var nearTop = hasTop ? Math.Clamp((Math.Log(widths[i]) - endLog) / (topLog - endLog), 0, 1) : 0;
            table[i] = table[i - 1] + 1 + dwell * Math.Pow(nearTop, 3);
        }
        return table;
    }

    // Sine ease in and out: soft start and landing, and only about 1.6 times the average pace
    // in the middle (a cubic ease is 3 times, which rushed past the top of the flight).
    static double EaseInOut(double t) => (1 - Math.Cos(Math.PI * t)) / 2;
}
