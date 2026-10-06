using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises group selection: Shift+click adds a node to the selection (the toolbar shows the
// count), hiding applies to every selected node, and Escape clears the whole selection.
public class MultiSelectTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ShiftDrag_ShouldSelectTheNodesInsideTheBand()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Go inside Main (deep, at one of its members), then zoom out until several of Main's
        // members are on screen while Main is still open.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.DeselectAsync();
        LocatorBoundingBoxResult canvas =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("No canvas box");
        await Page.Mouse.MoveAsync(canvas.X + canvas.Width / 2, canvas.Y + canvas.Height / 2);
        for (int notch = 0; notch < 14 && await MembersOnScreenAsync() < 2; notch++)
        {
            await Page.Mouse.WheelAsync(0, 240);
            await Page.WaitForTimeoutAsync(400);
        }
        int membersOnScreen = await MembersOnScreenAsync();
        Assert.True(membersOnScreen >= 2, $"Expected members of Main well inside the band, saw {membersOnScreen}.");
        await App.WaitForContainerNodeAsync("Main");
        await Page.WaitForTimeoutAsync(500); // Let the last zoom step's tiles settle before dragging

        // A band over the visible part of Main's inside (so Main itself is only partly covered
        // and is looked into) picks the members inside it.
        float x1 = canvas.X + 20;
        float y1 = canvas.Y + 110;
        float x2 = canvas.X + canvas.Width - 20;
        float y2 = canvas.Y + canvas.Height - 30;
        await Page.Keyboard.DownAsync("Shift");
        try
        {
            await Page.Mouse.MoveAsync(x1, y1);
            await Page.Mouse.DownAsync();
            await Page.Mouse.MoveAsync(x2, y2, new() { Steps = 8 });
            await Page.Mouse.UpAsync();
        }
        finally
        {
            await Page.Keyboard.UpAsync("Shift");
        }

        await Expect(App.NodeSelectionCount).ToBeVisibleAsync();
        await Expect(App.NodeSelectionCount).ToContainTextAsync("selected");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.NodeSelectionCount).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task ShiftClick_ShouldSelectSeveral_HideThemTogether_AndEscapeClearsAll()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Inside Demo.UI.Main, select the container and then add one of its members
        // (the DemoTheme field) with Shift+click.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(App.NodeSelectionCount).ToHaveCountAsync(0);

        await App.RepeatUntilVisibleAsync(
            () => App.ShiftClickNodeByVisibleNameAsync("DemoTheme"),
            App.NodeSelectionCount
        );
        await Expect(App.NodeSelectionCount).ToContainTextAsync("2 selected");

        // Hiding applies to both: hidden nodes render faded (opacity attributes), so at least
        // two more faded groups appear.
        ILocator faded = Page.Locator("#svgcanvas [opacity='0.3']");
        int before = await faded.CountAsync();
        await (await App.OpenNodeMenuItemAsync("node-menu-toggle-hide")).ClickAsync();
        int after = before;
        for (int attempt = 0; attempt < 50 && after < before + 2; attempt++)
        {
            await Page.WaitForTimeoutAsync(100);
            after = await faded.CountAsync();
        }
        Assert.True(after >= before + 2, $"Expected at least two more faded nodes, had {before}, now {after}.");

        // Show them again (the primary node decides the direction), then clear the selection.
        // The menu popover takes the first Escape while it is still closing, so wait for it to
        // be gone and press again if the selection is still there.
        await (await App.OpenNodeMenuItemAsync("node-menu-toggle-hide")).ClickAsync();
        await Expect(App.MenuItem("node-menu-toggle-hide")).ToHaveCountAsync(0);
        for (int attempt = 0; attempt < 5 && await App.NodeSelectionCount.CountAsync() > 0; attempt++)
        {
            await Page.Keyboard.PressAsync("Escape");
            await Page.WaitForTimeoutAsync(300);
        }
        await Expect(App.NodeSelectionCount).ToHaveCountAsync(0);
        await Expect(App.NodeToolbarMenu).ToHaveCountAsync(0);
    }

    // Member labels (text.memberName) lying well inside the band area, so that the whole member
    // box (icon above the label) is inside the band too.
    Task<int> MembersOnScreenAsync() =>
        Page.EvaluateAsync<int>(
            @"() => {
                const svg = document.querySelector('#svgcanvas').getBoundingClientRect();
                return [...document.querySelectorAll('#svgcanvas text.memberName')].filter(t => {
                    const r = t.getBoundingClientRect();
                    return r.width > 0 && r.left >= svg.left + 90 && r.right <= svg.right - 90
                        && r.top >= svg.top + 190 && r.bottom <= svg.bottom - 60;
                }).length;
            }"
        );
}
