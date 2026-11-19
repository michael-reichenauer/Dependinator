using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the selected-line toolbar (LineToolbar.razor) and the line-end tags
// (LineEndTags.razor): a selected line whose end is out of view gets a tag at the edge of the
// view naming that end's node, which goes there and keeps the line selected.
public class LineToolbarTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ExplorerLine_ShouldGoToItsFarEnd_ByTheEndTag()
    {
        await App.GotoMainPageAsync();

        // "Main" as a container, then its dependencies explorer draws Main's own line to the
        // Externals container (a demo-model fact), which exists only while the explorer shows
        // lines; Externals lies far outside the view.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        ILocator line = App.Line("Demo.UI.Main", "Externals");
        await Expect(line).ToHaveCountAsync(1);

        await App.SelectLineAsync(line);
        await Expect(Page.GetByTestId("line-references")).ToBeVisibleAsync();
        await Expect(App.LineEndTargetTag).ToContainTextAsync("Externals");

        // Going to the target lands on it (the breadcrumb ends at it). The line is not drawn
        // there (its source, deep inside Demo.UI, is not shown at that zoom), so its toolbar goes.
        await App.LineEndTargetTag.ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToHaveTextAsync("Externals");
    }

    [E2EFact]
    public async Task LineEndTag_ShouldGoToTheEndOutOfView_AndKeepTheLineSelected()
    {
        // Demo.UI's line to Shared, two projects of the demo solution (a demo-model fact).
        await App.GotoMainPageAsync();
        await App.NavigateToNodeWithoutSelectingAsync("Demo.UI (dll)");
        ILocator line = App.Line("Demo.UI (dll)", "Shared (dll)");
        await App.SelectLineAsync(line);
        await Expect(App.LineEndTargetTag).ToHaveCountAsync(0);

        // Zoom in at the source end until the target end is out of view.
        (float x, float y) = await App.WaitForStableLinePointAsync(line, 0.1f);
        await Page.Mouse.MoveAsync(x, y);
        for (int i = 0; i < 14; i++)
        {
            await Page.Mouse.WheelAsync(0, -200);
            await Task.Delay(50);
        }
        await Expect(App.LineEndTargetTag).ToContainTextAsync("Shared (dll)");
        await Expect(App.LineEndSourceTag).ToHaveCountAsync(0);

        await App.LineEndTargetTag.ClickAsync();
        await Expect(App.LineToolbar).ToBeVisibleAsync();
        await Expect(App.LineEndTargetTag).ToHaveCountAsync(0);
    }

    // The toolbar's groups come and go (the edit-mode actions), and a divider
    // that no longer has a group on both sides is hidden (the dep-toolbar-sep CSS).
    [E2EFact]
    public async Task OrdinaryLine_ShouldShowNoDivider_WithOnlyTheExploreActions()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.SelectLineAsync(App.Line("Demo.sln", "Externals"));

        await Expect(Page.GetByTestId("line-dependencies")).ToBeVisibleAsync();
        await Expect(App.LineToolbar.Locator(".dep-toolbar-sep:visible")).ToHaveCountAsync(0);
    }
}
