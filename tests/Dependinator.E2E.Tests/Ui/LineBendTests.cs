using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Bends are made on the line itself (LineSvg's segment handles, LineEditService): in edit mode a
// selected line shows a hollow handle on each segment, dragging one adds a bend point, a
// double-click on a bend point removes it, and the toolbar's Straighten removes them all.
public class LineBendTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string SegmentHandles = "#svgcanvas g.segment-handle circle[id]";

    // The selected line's bend-point handles (not their copies above a label).
    const string BendHandles = "#svgcanvas g.selectpoint:not(.segment-handle) circle[id]";

    [E2EFact]
    public async Task SegmentHandle_ShouldBendTheLine_AndADoubleClickOrStraightenRemovesTheBends()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();
        await App.SelectLineAsync(App.Line("Demo.sln", "Externals"));

        // The layout may already have routed the line around something.
        await WaitForCountAsync(SegmentHandles, n => n > 0, "segment handles");
        var bendsBefore = (await HandlesAsync(BendHandles)).Count;

        await DragAsync(SegmentHandles, 30, 60);
        await WaitForCountAsync(BendHandles, n => n == bendsBefore + 1, "a new bend");
        await Expect(Page.GetByTestId("line-straighten")).ToBeVisibleAsync();

        await DragAsync(SegmentHandles, -40, -50);
        await WaitForCountAsync(BendHandles, n => n == bendsBefore + 2, "a second bend");

        var bend = await TopmostHandleAsync(BendHandles);
        await Page.Mouse.DblClickAsync(bend.X, bend.Y);
        await WaitForCountAsync(BendHandles, n => n == bendsBefore + 1, "a bend removed by double-click");

        await Page.GetByTestId("line-straighten").ClickAsync();
        await WaitForCountAsync(BendHandles, n => n == 0, "no bends");
        await Expect(Page.GetByTestId("line-straighten")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task SegmentHandles_ShouldShowOnlyInEditMode()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.SelectLineAsync(App.Line("Demo.sln", "Externals"));
        await Expect(App.LineToolbar).ToBeVisibleAsync();
        Assert.Empty(await HandlesAsync(SegmentHandles));

        await App.EnableEditModeAsync();
        await WaitForCountAsync(SegmentHandles, n => n > 0, "segment handles");
    }

    // A drag in small steps from a handle, as a hand would make it (the handle waits for a few
    // pixels first).
    async Task DragAsync(string handles, float dx, float dy)
    {
        var handle = await TopmostHandleAsync(handles);
        await Page.Mouse.MoveAsync(handle.X, handle.Y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(handle.X + dx, handle.Y + dy, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
    }

    // Read back from the page as JSON.
    sealed class Handle
    {
        public string Id { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public bool IsTopmost { get; set; }
    }

    // The handles on screen, once per id: the canvas draws a line in every tile it crosses, so
    // the same handle can be in the page more than once. IsTopmost tells whether a press at its
    // center reaches it (not a toolbar or another tile's copy).
    async Task<List<Handle>> HandlesAsync(string selector) =>
        System.Text.Json.JsonSerializer.Deserialize<List<Handle>>(
            await Page.EvaluateAsync<string>(
                """
                selector => {
                    const handles = new Map();
                    for (const el of document.querySelectorAll(selector)) {
                        const r = el.getBoundingClientRect();
                        if (r.width === 0 || r.bottom < 0 || r.right < 0 || r.top > innerHeight || r.left > innerWidth)
                            continue;
                        const x = r.x + r.width / 2, y = r.y + r.height / 2;
                        const isTopmost = document.elementFromPoint(x, y) === el;
                        const known = handles.get(el.id);
                        if (!known || (!known.IsTopmost && isTopmost))
                            handles.set(el.id, { Id: el.id, X: x, Y: y, IsTopmost: isTopmost });
                    }
                    return JSON.stringify([...handles.values()]);
                }
                """,
                selector
            )
        )!;

    async Task<Handle> TopmostHandleAsync(string selector)
    {
        for (int i = 0; i < 50; i++)
        {
            if ((await HandlesAsync(selector)).FirstOrDefault(h => h.IsTopmost) is { } handle)
                return handle;
            await Task.Delay(200);
        }
        throw new TimeoutException($"No handle to press for {selector}.");
    }

    // The canvas repaints after an edit, so the count is read until it holds.
    async Task WaitForCountAsync(string selector, Func<int, bool> isExpected, string what)
    {
        var count = -1;
        for (int i = 0; i < 50; i++)
        {
            count = (await HandlesAsync(selector)).Count;
            if (isExpected(count))
                return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Timed out waiting for {what} (count {count}).");
    }
}
