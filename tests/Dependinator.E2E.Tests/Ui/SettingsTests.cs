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
        await ExpectInvertScrollZoomAsync(isOn: false);
        await ExpectWheelDownToZoomAsync(zoomIn: false);

        // On: the same wheel down now zooms in, so the node grows.
        await ToggleInvertScrollZoomAsync(expectOn: true);
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // The preference is stored in the config, so it is still on after a reload.
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await ExpectInvertScrollZoomAsync(isOn: true);
        await ExpectWheelDownToZoomAsync(zoomIn: true);

        // And back off again.
        await ToggleInvertScrollZoomAsync(expectOn: false);
        await ExpectWheelDownToZoomAsync(zoomIn: false);
    }

    Task<ILocator> OpenInvertScrollZoomAsync() => App.OpenSubMenuItemAsync("menu-settings", "menu-invert-scroll-zoom");

    // Assert the toggle state via the menu, then close the menu again so the canvas is free
    // for wheel gestures (an open menu's overlay would swallow them).
    async Task ExpectInvertScrollZoomAsync(bool isOn)
    {
        await Expect(await OpenInvertScrollZoomAsync()).ToHaveAttributeAsync("data-checked", isOn ? "true" : "false");
        await App.CloseMenuAsync();
    }

    // Click the toggle and verify it really flipped: a click landing while the popover
    // re-renders is swallowed silently, so the state is read back before moving on.
    async Task ToggleInvertScrollZoomAsync(bool expectOn)
    {
        await (await OpenInvertScrollZoomAsync()).ClickAsync();
        await ExpectInvertScrollZoomAsync(expectOn);
    }

    // Scroll the wheel down over empty canvas and assert which way the root node changes size.
    // A wheel landing while the canvas re-renders is swallowed silently, so the gesture is
    // repeated until the node size actually changes.
    async Task ExpectWheelDownToZoomAsync(bool zoomIn)
    {
        LocatorBoundingBoxResult canvas =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        // The demo nodes sit at the canvas center; a quarter in from the top-left is empty
        // canvas, so no node toolbar pops up under the pointer and takes the wheel event.
        await Page.Mouse.MoveAsync(canvas.X + canvas.Width / 4, canvas.Y + canvas.Height / 4);

        // Measure only once the view has settled, so a size change can only come from the wheel.
        float before = await SettledRootNodeWidthAsync();
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

    // The root node's on-screen width once it has stopped changing (e.g. after the initial
    // fit-to-view or a pending zoom re-render), polled up to a few seconds.
    async Task<float> SettledRootNodeWidthAsync()
    {
        float width = await RootNodeWidthAsync();
        for (int poll = 0, stable = 0; poll < 50 && stable < 3; poll++)
        {
            await Page.WaitForTimeoutAsync(100);
            float next = await RootNodeWidthAsync();
            stable = Math.Abs(next - width) < 0.5f ? stable + 1 : 0;
            width = next;
        }
        return width;
    }

    async Task<float> RootNodeWidthAsync()
    {
        LocatorBoundingBoxResult box =
            await App.Node("Demo.sln").First.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Root node is not rendered.");
        return box.Width;
    }

    Task<ILocator> OpenIncludeTestProjectsAsync() =>
        App.OpenSubMenuItemAsync("menu-settings", "menu-include-test-projects");
}
