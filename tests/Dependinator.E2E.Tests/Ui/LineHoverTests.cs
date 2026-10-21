using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// A line's hover highlight is its own polyline, so the wide hit polyline keeps its width while
// hovered (LineSvg, the .hoverable:hover rule in Canvas.razor). Restyled on hover, it shrank to
// 2 px under the pointer, and WebKit tested the press against the thin stroke: clicks on lines
// landed on the canvas. Runs in every browser, so chromium-only branch CI sees a regression too.
public class LineHoverTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task HoveredLine_ShouldKeepItsWideHitTarget()
    {
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        // Let the navigation's own selection land and clear it first: its re-render would
        // replace the hovered line, and WebKit re-applies :hover only on the next pointer move.
        await App.DeselectAsync();
        ILocator line = App.Line("Demo.UI.Main", "Demo.UI.Shared");
        await Expect(line).ToHaveCountAsync(1);

        await App.HoverLineAsync(line);

        // The 2 px highlight shows the hover took effect; the hit target keeps its own (wider)
        // width, whatever the line's weight makes it, and stays transparent.
        await Expect(AppPage.LineHoverHighlight(line)).ToHaveCSSAsync("stroke-width", "2px");
        ILocator hit = AppPage.LineHitTarget(line);
        await Expect(hit).Not.ToHaveCSSAsync("stroke-width", "2px");
        await Expect(hit).ToHaveCSSAsync("stroke-opacity", "0");
    }
}
