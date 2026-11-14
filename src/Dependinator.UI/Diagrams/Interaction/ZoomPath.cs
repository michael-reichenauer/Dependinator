using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// The "smooth and efficient" zoom and pan path of van Wijk and Nuij (2003), the one d3's
// interpolateZoom and map apps' fly-to use: the view pans and zooms at the same time, zooming
// out on the way just as far as a long jump needs and back in as it arrives, so the motion
// feels like one flight at an even pace instead of three separate steps.
//
// A view is its center (canvas units) and its width w (canvas units across the screen, so
// the screen size times the model zoom). At(t) gives the view at t in [0, 1].
sealed class ZoomPath
{
    // Curvature: how far a long jump zooms out. sqrt(2) is van Wijk's measured optimum (and
    // d3's default): larger zooms out further.
    const double Rho = 1.41421356237;
    const double Epsilon = 1e-12;

    // A top less than this (log scale) above the wider end is no top: about 1.5 times wider.
    const double MinTopLog = 0.4;

    readonly Pos from;
    readonly double dx;
    readonly double dy;
    readonly double d1;
    readonly double w0;
    readonly double r0;
    readonly double logW;

    public ZoomPath(Pos fromCenter, double fromWidth, Pos toCenter, double toWidth)
    {
        from = fromCenter;
        w0 = fromWidth;
        dx = toCenter.X - fromCenter.X;
        dy = toCenter.Y - fromCenter.Y;
        var d2 = dx * dx + dy * dy;
        d1 = Math.Sqrt(d2);

        if (d2 < Epsilon * w0 * w0)
        {
            // Only a zoom: an even pace in log scale.
            IsZoomOnly = true;
            logW = Math.Log(toWidth / fromWidth);
            Length = Math.Abs(logW) / Rho;
            return;
        }

        const double Rho2 = Rho * Rho;
        const double Rho4 = Rho2 * Rho2;
        var w1 = toWidth;
        var b0 = (w1 * w1 - w0 * w0 + Rho4 * d2) / (2 * w0 * Rho2 * d1);
        var b1 = (w1 * w1 - w0 * w0 - Rho4 * d2) / (2 * w1 * Rho2 * d1);
        r0 = Math.Log(Math.Sqrt(b0 * b0 + 1) - b0);
        var r1 = Math.Log(Math.Sqrt(b1 * b1 + 1) - b1);
        Length = (r1 - r0) / Rho;
    }

    // The path's length S, in van Wijk's units: a zoom by e^sqrt(2) or a pan by about a screen
    // is 1. The animation's duration is taken from it.
    public double Length { get; }

    bool IsZoomOnly { get; }

    public (Pos Center, double Width) At(double t)
    {
        var s = Math.Clamp(t, 0, 1) * Length;
        if (IsZoomOnly)
        {
            var lt = Length > 0 ? s / Length : 1;
            return (new Pos(from.X + lt * dx, from.Y + lt * dy), w0 * Math.Exp(lt * logW));
        }

        const double Rho2 = Rho * Rho;
        var coshR0 = Math.Cosh(r0);
        var u = w0 / (Rho2 * d1) * (coshR0 * Math.Tanh(Rho * s + r0) - Math.Sinh(r0));
        var w = w0 * coshR0 / Math.Cosh(Rho * s + r0);
        return (new Pos(from.X + u * dx, from.Y + u * dy), w);
    }

    // The view at a share of the flight's time (0 to 1): the flight starts and lands softly
    // and slows down near its top, where the view is widest, so the user has a moment to see
    // where the target lies relative to the start. dwell is how much slower the top is (0: an
    // even pace along the path, 1: twice as slow at the top).
    public (Pos Center, double Width) AtTime(double timeShare, double dwell)
    {
        var table = GetTimeTable(dwell);
        var time = EaseInOut(Math.Clamp(timeShare, 0, 1)) * table[^1];
        var i = Array.BinarySearch(table, time);
        if (i < 0)
            i = ~i;
        if (i <= 0)
            return At(0);
        if (i >= table.Length)
            return At(1);
        var segment = table[i] - table[i - 1];
        var part = segment > 0 ? (time - table[i - 1]) / segment : 0;
        return At((i - 1 + part) / (table.Length - 1));
    }

    // The time to reach each of TableSteps even steps along the path, each step slowed down by
    // how near its view is to the widest one, in log scale between the wider end and the top.
    // A flight that does not zoom out beyond its ends (a zoom in or out, a short hop) has no top.
    double[] GetTimeTable(double dwell)
    {
        if (timeTable is not null && timeTableDwell == dwell)
            return timeTable;

        const int TableSteps = 200;
        var widths = Enumerable.Range(0, TableSteps + 1).Select(i => At((double)i / TableSteps).Width).ToArray();
        var topLog = Math.Log(widths.Max());
        var endLog = Math.Log(Math.Max(widths[0], widths[^1]));
        var hasTop = topLog - endLog > MinTopLog;
        var table = new double[TableSteps + 1];
        for (var i = 1; i <= TableSteps; i++)
        {
            var nearTop = hasTop ? Math.Clamp((Math.Log(widths[i]) - endLog) / (topLog - endLog), 0, 1) : 0;
            table[i] = table[i - 1] + 1 + dwell * Math.Pow(nearTop, 3);
        }
        timeTable = table;
        timeTableDwell = dwell;
        return table;
    }

    double[]? timeTable;
    double timeTableDwell;

    // Sine ease in and out: soft start and landing, and only about 1.6 times the average pace
    // in the middle (a cubic ease is 3 times, which rushed past the top of the flight).
    static double EaseInOut(double t) => (1 - Math.Cos(Math.PI * t)) / 2;
}
