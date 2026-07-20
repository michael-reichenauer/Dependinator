using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the "split" line action through the UI: an aggregated line (here the top-level
// Demo.sln→Externals line) offers a Split button, and splitting shows dashed direct-style
// lines to the target's children while hiding the aggregated line. The split/hide/restore
// bookkeeping and recursion are covered in detail by DependenciesServiceSplitTests; this test
// proves the button and service are wired through the real toolbar and renderer. (Selecting
// an individual split line is not asserted here: at the zoomed-out view all split lines
// converge between the two icons and cannot be clicked apart reliably in headless.)
public class LineSplitTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // Split (direct) lines use a "6,6" dash; the selection highlight uses a different dash.
    const string SplitLineSelector = "#svgcanvas polyline[stroke-dasharray='6,6']";

    [E2EFact]
    public async Task Line_ShouldOfferSplit_AndShowSplitLinesIntoTarget()
    {
        await App.GotoMainPageAsync();
        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();

        // No split lines before any split.
        await Expect(SplitLines).ToHaveCountAsync(0);

        // The aggregated line between the two top-level nodes.
        ILocator line = LineGroup("Demo.sln", "Externals");
        await Expect(line).ToHaveCountAsync(1);
        await SelectLineAsync(line.Locator("polyline").First);

        // A parsed aggregated line whose links go deeper offers Split (and no Delete).
        await Expect(Page.GetByTestId("line-split")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("line-delete")).ToHaveCountAsync(0);

        await Page.GetByTestId("line-split").ClickAsync();

        // Splitting shows dashed direct-style lines (Demo.sln → each used child of Externals)
        // and hides the aggregated line.
        await Expect(SplitLines).Not.ToHaveCountAsync(0);
        await Expect(line).ToHaveCountAsync(0);
    }

    ILocator SplitLines => Page.Locator(SplitLineSelector);

    ILocator LineGroup(string sourceName, string targetName) =>
        Page.Locator("#svgcanvas g.hoverable")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(sourceName)}→{Regex.Escape(targetName)}") });

    // Selects a line by clicking a point that lies ON its polyline (a diagonal line's
    // bounding-box center misses the stroke). Deselects first so a previous toolbar cannot
    // cover the target, and waits for the line's rendered geometry to settle.
    async Task SelectLineAsync(ILocator polyline)
    {
        await Page.Keyboard.PressAsync("Escape");

        Pos point = await WaitForStablePointOnLineAsync(polyline);
        await Page.Mouse.ClickAsync(point.X, point.Y);
    }

    // A stable midpoint of the polyline in screen coordinates, computed from its own points via
    // the element's screen CTM (so a diagonal line gets a point on the stroke, not off it).
    async Task<Pos> WaitForStablePointOnLineAsync(ILocator polyline, float timeoutSeconds = 15)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Pos? previous = null;

        while (stopwatch.Elapsed < timeout)
        {
            Pos? point = null;
            if (await polyline.CountAsync() > 0)
                point = await MidPointOnLineAsync(polyline);

            bool isStable =
                point is not null
                && previous is not null
                && Math.Abs(point.X - previous.X) < 1
                && Math.Abs(point.Y - previous.Y) < 1;
            if (isStable)
                return point!;

            previous = point;
            await Task.Delay(100);
        }

        throw new TimeoutException("Line point did not stabilize.");
    }

    async Task<Pos> MidPointOnLineAsync(ILocator polyline)
    {
        float[] xy = await polyline.EvaluateAsync<float[]>(
            @"el => {
                const p = el.points, ctm = el.getScreenCTM();
                const a = p[0], b = p[p.numberOfItems - 1];
                const m = new DOMPoint((a.x + b.x) / 2, (a.y + b.y) / 2).matrixTransform(ctm);
                return [m.x, m.y];
            }"
        );
        return new Pos(xy[0], xy[1]);
    }

    record Pos(float X, float Y);
}
