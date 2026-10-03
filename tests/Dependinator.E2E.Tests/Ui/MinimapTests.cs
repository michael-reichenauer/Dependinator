using System.Globalization;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the minimap: View › Show Minimap shows it with a frame for the view, clicking in it
// moves the view (the frame follows), the choice survives a reload, and its close button hides it.
public class MinimapTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ShowMinimap_ShouldPanOnClick_AndPersist()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator minimap = Page.GetByTestId("minimap");
        ILocator frame = Page.GetByTestId("minimap-viewport");
        await Expect(minimap).ToHaveCountAsync(0);

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-minimap")).ClickAsync();
        await Expect(minimap).ToBeVisibleAsync();
        await Expect(frame).ToBeAttachedAsync();

        // At the initial fit view the frame spans the whole map, so zoom into Demo.UI first; the
        // node gets selected once the navigation has settled, and the frame then sits inside the map.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI (dll)");
        await search.Result("Demo.UI (dll)").First.ClickAsync();
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
        double before = await FrameXAsync(frame);
        Assert.True(before > 0, $"The frame should sit inside the map after zooming in, but x was {before}.");

        // Clicking near the left edge of the minimap centers the view there, so the frame moves left.
        LocatorBoundingBoxResult box =
            await minimap.Locator("svg.dep-minimap__svg").BoundingBoxAsync()
            ?? throw new InvalidOperationException("No minimap svg");
        await Page.Mouse.ClickAsync(box.X + 12, box.Y + box.Height / 2);
        double after = before;
        for (int attempt = 0; attempt < 50 && !(after < before); attempt++)
        {
            await Page.WaitForTimeoutAsync(100);
            after = await FrameXAsync(frame);
        }
        Assert.True(after < before, $"The frame should have moved left: x {after} was {before}.");

        // Persisted: still shown after a reload.
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await Expect(minimap).ToBeVisibleAsync();

        await Page.GetByTestId("minimap-close").ClickAsync();
        await Expect(minimap).ToHaveCountAsync(0);
    }

    static async Task<double> FrameXAsync(ILocator frame) =>
        double.Parse(await frame.GetAttributeAsync("x") ?? "0", CultureInfo.InvariantCulture);
}
