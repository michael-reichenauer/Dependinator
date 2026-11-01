using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Soft lines (Canvas.razor.cs LineCss, on by default): resting lines are drawn faint and the
// selected node's lines at full strength; Settings › Soft lines turns it off. Resting the mouse
// on a node icon lights its lines (the hover preview in jsInterop.js).
public class SoftLinesTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string RestingLine =
        "#svgcanvas g.line-vis:not(.line-lit):not(.line-accent):not(.line-cycle):not(.line-path):not(.line-rule)";

    [E2EFact]
    public async Task RestingLines_ShouldBeSoft_UnlessTurnedOff()
    {
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.DeselectAsync();

        await Expect(Page.Locator(RestingLine).First).ToHaveCSSAsync("opacity", "0.4");

        // Selected: the node's own lines at full strength.
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(Page.Locator("#svgcanvas g.line-lit").First).ToHaveCSSAsync("opacity", "1");

        // Off: every resting line at full strength.
        await App.DeselectAsync();
        await App.ToggleSettingAsync("settings-soft-lines", expectChecked: false);
        await Expect(Page.Locator(RestingLine).First).ToHaveCSSAsync("opacity", "1");

        await App.ToggleSettingAsync("settings-soft-lines", expectChecked: true);
        await Expect(Page.Locator(RestingLine).First).ToHaveCSSAsync("opacity", "0.4");
    }

    [E2EFact]
    public async Task HoveringANodeIcon_ShouldLightItsLines()
    {
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.DeselectAsync();

        // An icon with lines: the end node id of a drawn line that has a preview target.
        string id = await Page.EvaluateAsync<string>(
            """
            () => {
              const ids = new Set([...document.querySelectorAll('#svgcanvas [data-line-preview]')]
                .map(g => g.id.split('.')[0]));
              for (const line of document.querySelectorAll('#svgcanvas g.line-vis[data-n]'))
                for (const n of line.dataset.n.split(' '))
                  if (ids.has(n)) return n;
              return '';
            }
            """
        );
        Assert.NotEmpty(id);

        // The mouse on the icon's hover rect (a forced locator hover lands on whatever is on top).
        LocatorBoundingBoxResult? box = await Page.Locator($"#svgcanvas [id='{id}.n'][data-line-preview] rect")
            .First.BoundingBoxAsync();
        Assert.NotNull(box);
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.WaitForFunctionAsync(
            "id => document.getElementById('line-preview-style').textContent.includes(id)",
            id
        );
        ILocator lit = Page.Locator($"#svgcanvas g.line-vis[data-n~='{id}']").First;
        await Expect(lit).ToHaveCSSAsync("opacity", "1");

        // Leaving the canvas ends the preview.
        await Page.Mouse.MoveAsync(0, 0);
        await Page.WaitForFunctionAsync("() => document.getElementById('line-preview-style').textContent === ''");
    }
}
