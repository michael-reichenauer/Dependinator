using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises selection dimming (SvgService.IsLineDimmed + the line-dim CSS): with a node
// selected, lines that do not touch it get the line-dim class; View › Dim Unrelated Lines turns
// it off, and the setting is stored in the config.
public class LineDimTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task SelectingANode_ShouldDimUnrelatedLines_UnlessTurnedOff()
    {
        await App.GotoMainPageAsync();

        // Inside Demo.UI several sibling lines are visible; selecting Main dims the others.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");

        ILocator dimmed = Page.Locator("#svgcanvas g.line-dim");
        await Expect(dimmed).Not.ToHaveCountAsync(0);

        // Off: nothing is dimmed, whatever is selected. (Closing the menu presses Escape, which
        // also clears the selection, so the node is selected again after each toggle.)
        await ToggleDimAsync(expectOn: false);
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(dimmed).ToHaveCountAsync(0);

        // On again: dimmed while selected, full strength once deselected.
        await ToggleDimAsync(expectOn: true);
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(dimmed).Not.ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(dimmed).ToHaveCountAsync(0);
    }

    Task<ILocator> OpenDimItemAsync() => App.OpenSubMenuItemAsync("menu-view", "menu-dim-unrelated-lines");

    // Click the toggle and verify it really flipped (a click landing while the popover
    // re-renders is swallowed silently), then close the menu so the canvas is free again.
    async Task ToggleDimAsync(bool expectOn)
    {
        await (await OpenDimItemAsync()).ClickAsync();
        await Expect(await OpenDimItemAsync()).ToHaveAttributeAsync("data-checked", expectOn ? "true" : "false");
        await App.CloseMenuAsync();
    }
}
