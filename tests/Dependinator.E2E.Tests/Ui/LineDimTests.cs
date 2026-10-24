using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
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
        await App.NavigateToNodeAsync(AppPage.InsideMain);
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

    // Click the toggle and verify it really flipped; the menu is left closed, so the canvas is
    // free again.
    Task ToggleDimAsync(bool expectOn) => App.ToggleSubMenuItemAsync("menu-view", "menu-dim-unrelated-lines", expectOn);
}
