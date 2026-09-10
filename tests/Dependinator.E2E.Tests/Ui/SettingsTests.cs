using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the Settings submenu in the app menu: the "Include Test Projects" item is a
// checkbox-style toggle whose state is stored per model. The demo model is pre-parsed and
// embedded, so the triggered re-parse short-circuits and the node set does not change --
// this asserts the toggle state itself, which is what the menu owns. "Invert Scroll Zoom"
// is an app-wide toggle stored in the config that flips the mouse wheel zoom direction.
public class SettingsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task IncludeTestProjects_ShouldToggleViaSettingsMenu()
    {
        await App.GotoMainPageAsync();

        // Test projects are excluded by default.
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "false");

        await (await OpenIncludeTestProjectsAsync()).ClickAsync();
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "true");

        // And back off again.
        await (await OpenIncludeTestProjectsAsync()).ClickAsync();
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "false");
    }

    // Only wheel-down (a positive delta) is used to probe the zoom direction: headless
    // Chromium does not reliably deliver negative-delta wheel events to the page.
    [E2EFact]
    public async Task InvertScrollZoom_ShouldFlipWheelZoomDirection_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Off by default: wheel down zooms out, so the root node shrinks on screen.
        await ExpectWheelDownToZoomAsync(zoomIn: false);

        await (await OpenInvertScrollZoomAsync()).ClickAsync();
        // On: the same wheel down now zooms in, so the node grows.
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // The preference is stored in the config, so it is still on after a reload.
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // And back off again.
        ILocator item = await OpenInvertScrollZoomAsync();
        await Expect(item).ToHaveAttributeAsync("data-checked", "true");
        await item.ClickAsync();
        await ExpectWheelDownToZoomAsync(zoomIn: false);
        await Expect(await OpenInvertScrollZoomAsync()).ToHaveAttributeAsync("data-checked", "false");
    }

    Task<ILocator> OpenIncludeTestProjectsAsync() =>
        App.OpenSubMenuItemAsync("menu-settings", "menu-include-test-projects");

    Task<ILocator> OpenInvertScrollZoomAsync() => App.OpenSubMenuItemAsync("menu-settings", "menu-invert-scroll-zoom");

    // Scroll the wheel down over empty canvas and assert which way the root node changes size.
    // A wheel landing while the canvas re-renders (or while a menu popover is still fading out)
    // is swallowed silently, so the gesture is repeated until the node size actually changes.
    async Task ExpectWheelDownToZoomAsync(bool zoomIn)
    {
        LocatorBoundingBoxResult canvas =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        // The demo nodes sit at the canvas center; a quarter in from the top-left is empty
        // canvas, so no node toolbar pops up under the pointer and takes the wheel event.
        await Page.Mouse.MoveAsync(canvas.X + canvas.Width / 4, canvas.Y + canvas.Height / 4);

        float before = await RootNodeWidthAsync();
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await Page.Mouse.WheelAsync(0, 100);

            float after = before;
            for (int poll = 0; poll < 20 && Math.Abs(after - before) < 1; poll++)
            {
                await Page.WaitForTimeoutAsync(100);
                after = await RootNodeWidthAsync();
            }

            if (Math.Abs(after - before) >= 1)
            {
                Assert.True(
                    zoomIn ? after > before : after < before,
                    $"Expected wheel down to zoom {(zoomIn ? "in" : "out")}, but root node width went {before} -> {after}."
                );
                return;
            }
        }

        Assert.Fail("Wheel down never changed the zoom.");
    }

    async Task<float> RootNodeWidthAsync()
    {
        LocatorBoundingBoxResult box =
            await App.Node("Demo.sln").First.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Root node is not rendered.");
        return box.Width;
    }
}
