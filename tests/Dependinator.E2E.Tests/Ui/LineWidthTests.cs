using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the line width rule (LineEmphasis.IsLineWeighted + LineSvg): every aggregated line is
// drawn at the same thin width, also while a node is selected, until View › Line Widths by Link
// Count is on; then selecting a node draws the lines that touch it at their link-count width
// while the dimmed, unrelated lines stay thin.
public class LineWidthTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string PlainWidth = "1";

    // Main's lines while it is selected (they end at the nodes inside the open containers).
    const string MainToThemeService = "Demo.UI.Main→Demo.UI.Shared.IThemeService (7)";
    const string MainToCanvas = "Demo.UI.Main→Demo.UI.Diagrams.Canvas (1)";

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

        // Off by default: a selected node's lines keep the plain width too. (The selection's
        // lines run end to end, so Main's line to Shared now ends at the type it uses there.)
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(LinePolyline(MainToThemeService)).ToHaveAttributeAsync("stroke-width", PlainWidth);
        await Expect(wideLines).ToHaveCountAsync(0);

        // Turned on (closing the menu presses Escape, which also clears the selection), Main's
        // lines show their link counts (demo-model facts: Main → Shared.IThemeService carries 7
        // links, Main → Diagrams.Canvas one); the dimmed lines of the other nodes keep the plain
        // width.
        await ToggleWidthsAsync(expectOn: true);
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(LinePolyline(MainToThemeService)).ToHaveAttributeAsync("stroke-width", "2.4");
        await Expect(LinePolyline(MainToCanvas)).ToHaveAttributeAsync("stroke-width", PlainWidth);
        await Expect(wideBrightLines).Not.ToHaveCountAsync(0);
        await Expect(wideDimmedLines).ToHaveCountAsync(0);

        // Deselected: back to one width for all.
        await App.DeselectAsync();
        await Expect(wideLines).ToHaveCountAsync(0);

        // Saved in the config: still on after a reload, and it turns off again.
        await App.GotoMainPageAsync();
        await App.ExpectSettingAsync("settings-link-count-widths", true);
        await ToggleWidthsAsync(expectOn: false);
    }

    // Click the Settings checkbox and verify it really flipped (the dialog is closed again).
    Task ToggleWidthsAsync(bool expectOn) => App.ToggleSettingAsync("settings-link-count-widths", expectOn);

    // The visible polyline of the line whose data-name ("Source→Target (n)", see
    // AppPage.LineTitle) contains the text. The Has filter is resolved relative to each line
    // group, so the inner locator must be relative too.
    ILocator LinePolyline(string title) =>
        Page.Locator("#svgcanvas g.line", new() { Has = Page.Locator($"g.hoverable[data-name*=\"{title}\"]") })
            .Locator("g.line-vis > polyline");
}
