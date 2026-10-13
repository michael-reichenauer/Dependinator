using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises zones (user-drawn labeled background areas) through the browser: add one via the app
// menu ("Add Zone" arms placing mode → click canvas → dialog) and via the right-click menu, see
// its label on the canvas, survive a reload (persisted with IsZone/IsManual), and delete it.
public class ZoneTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Zone_ShouldBeAddedViaMenu_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();

        ILocator addZone = await App.OpenSubMenuItemAsync("menu-edit", "menu-add-zone");
        await addZone.ClickAsync();
        await Expect(Page.GetByText("Click on the diagram to place the zone's top-left corner.")).ToBeVisibleAsync();

        // Click an empty area (bottom-left) to put the zone's corner there.
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        ILocator labelInput = Page.GetByTestId("zone-label");
        await App.RepeatUntilVisibleAsync(
            () => Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 250),
            labelInput
        );
        await SaveZoneAsync("Edge", "Front Door and API Management");

        await Expect(ZoneLabel("Edge")).ToBeVisibleAsync();
        await Expect(Page.Locator("#svgcanvas text.zoneDescription", new() { HasText = "Front Door" }))
            .ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // A zone is user-drawn (not parsed), so surviving the reload proves it was persisted and
        // kept by the re-parse reconciliation.
        await Expect(ZoneLabel("Edge")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Zone_ShouldBeAddedViaContextMenu_AndDeletedWithTheDeleteKey()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();

        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 250, new() { Button = MouseButton.Right });
        await Expect(Page.GetByTestId("canvas-context-menu")).ToBeVisibleAsync();
        await Page.GetByTestId("context-menu-add-zone").ClickAsync();

        await Expect(Page.GetByTestId("zone-label")).ToBeVisibleAsync();
        await SaveZoneAsync("Data", "");
        await Expect(ZoneLabel("Data")).ToBeVisibleAsync();

        // Clicking the label hits the zone's own rectangle under it (labels take no pointer
        // events), which selects the zone: the toolbar offers the zone editor, no icon picker.
        LocatorBoundingBoxResult label =
            await ZoneLabel("Data").BoundingBoxAsync()
            ?? throw new InvalidOperationException("Zone label not rendered.");
        await App.RepeatUntilVisibleAsync(
            () => Page.Mouse.ClickAsync(label.X + label.Width / 2, label.Y + label.Height / 2),
            Page.GetByTestId("node-edit-zone")
        );
        await Expect(Page.GetByTestId("node-set-icon")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Delete");
        await Expect(Page.GetByText("Zone deleted.")).ToBeVisibleAsync();
        await Expect(ZoneLabel("Data")).ToHaveCountAsync(0);
    }

    ILocator ZoneLabel(string label) => Page.Locator("#svgcanvas text.zoneName", new() { HasText = label });

    async Task SaveZoneAsync(string label, string subtitle)
    {
        await App.FillReliablyAsync(Page.GetByTestId("zone-label"), label);
        if (subtitle.Length > 0)
            await App.FillReliablyAsync(Page.GetByTestId("zone-subtitle"), subtitle);
        await Page.GetByTestId("zone-save").ClickAsync();
    }
}
