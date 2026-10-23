using Dependinator.E2E.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The canvas follows the window size, also when the browser reports the new size late, as iOS
// Safari does when an iPad is rotated (#100).
public class ScreenResizeTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const int LandscapeWidth = 1180;
    const int LandscapeHeight = 820;

    [E2EFact]
    public async Task Rotate_ShouldFitCanvasToPortraitWindow()
    {
        await Page.SetViewportSizeAsync(LandscapeWidth, LandscapeHeight);
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await WaitForCanvasRightAsync(LandscapeWidth);

        await Page.SetViewportSizeAsync(LandscapeHeight, LandscapeWidth);

        await WaitForCanvasRightAsync(LandscapeHeight);
    }

    [E2EFact]
    public async Task Rotate_ShouldFitCanvasToPortraitWindow_WhenTheNewSizeIsReportedLate()
    {
        await Page.SetViewportSizeAsync(LandscapeWidth, LandscapeHeight);
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await WaitForCanvasRightAsync(LandscapeWidth);

        // Like iOS Safari on a rotation: the resize event comes while innerWidth/innerHeight
        // still report the landscape size, and no event follows once they are right.
        await Page.EvaluateAsync(
            @"([width, height]) => {
                const until = performance.now() + 300;
                for (const [name, stale] of [['innerWidth', width], ['innerHeight', height]]) {
                    const real = Object.getOwnPropertyDescriptor(window, name)
                        ?? Object.getOwnPropertyDescriptor(Window.prototype, name);
                    Object.defineProperty(window, name, {
                        configurable: true,
                        get() { return performance.now() < until ? stale : real.get.call(window); },
                    });
                }
            }",
            new[] { LandscapeWidth, LandscapeHeight }
        );
        await Page.SetViewportSizeAsync(LandscapeHeight, LandscapeWidth);

        await WaitForCanvasRightAsync(LandscapeHeight);
    }

    // Waits until the canvas ends just inside the window's right edge (it keeps a small margin).
    async Task WaitForCanvasRightAsync(int windowWidth)
    {
        double right = 0;
        for (var i = 0; i < 50; i++)
        {
            var box = await App.Canvas.BoundingBoxAsync();
            right = box is null ? 0 : box.X + box.Width;
            if (right <= windowWidth && right > windowWidth - 20)
                return;
            await Page.WaitForTimeoutAsync(100);
        }
        Assert.Fail($"The canvas should end just inside the {windowWidth} px window, but ended at {right}.");
    }
}
