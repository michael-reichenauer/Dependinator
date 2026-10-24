using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the line width rule (SvgService.IsLineWeighted + LineSvg): every aggregated line is
// drawn at the same thin width, also while a node is selected, until View › Line Widths by Link
// Count is on; then selecting a node draws the lines that touch it at their link-count width
// while the dimmed, unrelated lines stay thin.
public class LineWidthTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string PlainWidth = "1";

    [E2EFact]
    public async Task SelectingANode_ShouldWidenItsLinesByLinkCount_OnlyWhenTurnedOn()
    {
        await App.GotoMainPageAsync();

        // Inside Demo.UI several sibling lines are visible; the navigation selects the member it
        // landed on, so deselect first to see the unselected diagram.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.DeselectAsync();

        ILocator allLines = Page.Locator("#svgcanvas g.line-vis > polyline");
        ILocator wideLines = Page.Locator($"#svgcanvas g.line-vis > polyline:not([stroke-width='{PlainWidth}'])");
        ILocator wideDimmedLines = Page.Locator(
            $"#svgcanvas g.line-vis.line-dim > polyline:not([stroke-width='{PlainWidth}'])"
        );
        ILocator wideBrightLines = Page.Locator(
            $"#svgcanvas g.line-vis:not(.line-dim) > polyline:not([stroke-width='{PlainWidth}'])"
        );
        await Expect(allLines).Not.ToHaveCountAsync(0);
        await Expect(wideLines).ToHaveCountAsync(0);

        // Off by default: a selected node's lines keep the plain width too.
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(LinePolyline("Demo.UI.Main→Demo.UI.Shared (7)")).ToHaveAttributeAsync("stroke-width", PlainWidth);
        await Expect(wideLines).ToHaveCountAsync(0);

        // Turned on (closing the menu presses Escape, which also clears the selection), Main's
        // lines show their link counts (demo-model facts: Main → Shared carries 7 links, Main →
        // Diagrams one); the dimmed lines of the other nodes keep the plain width.
        await ToggleWidthsAsync(expectOn: true);
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(LinePolyline("Demo.UI.Main→Demo.UI.Shared (7)")).ToHaveAttributeAsync("stroke-width", "2.4");
        await Expect(LinePolyline("Demo.UI.Main→Demo.UI.Diagrams (1)"))
            .ToHaveAttributeAsync("stroke-width", PlainWidth);
        await Expect(wideBrightLines).Not.ToHaveCountAsync(0);
        await Expect(wideDimmedLines).ToHaveCountAsync(0);

        // Deselected: back to one width for all.
        await App.DeselectAsync();
        await Expect(wideLines).ToHaveCountAsync(0);

        // Saved in the config: still on after a reload, and it turns off again.
        await App.GotoMainPageAsync();
        await App.ExpectSubMenuItemCheckedAsync("menu-view", "menu-link-count-widths", true);
        await ToggleWidthsAsync(expectOn: false);
    }

    // Click the toggle and verify it really flipped (which also closes the menu).
    Task ToggleWidthsAsync(bool expectOn) =>
        App.ToggleSubMenuItemAsync("menu-view", "menu-link-count-widths", expectOn);

    // The visible polyline of the line with the given hover title ("Source→Target (n)", see
    // AppPage.LineTitle). The Has filter is resolved relative to each line group, so the title
    // locator must be relative too.
    ILocator LinePolyline(string title) =>
        Page.Locator("#svgcanvas g.line", new() { Has = Page.Locator("title", new() { HasTextString = title }) })
            .Locator("g.line-vis > polyline");
}
