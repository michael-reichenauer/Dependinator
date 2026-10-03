using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises group selection: Shift+click adds a node to the selection (the toolbar shows the
// count), hiding applies to every selected node, and Escape clears the whole selection.
public class MultiSelectTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ShiftClick_ShouldSelectSeveral_HideThemTogether_AndEscapeClearsAll()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Inside Demo.UI.Main (the "Demo.UI" query lands on its _isDarkMode field), select the
        // container and then add one of its members with Shift+click.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(App.NodeSelectionCount).ToHaveCountAsync(0);

        await App.RepeatUntilVisibleAsync(
            () => App.ShiftClickNodeByVisibleNameAsync("_isDarkMode"),
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
}
