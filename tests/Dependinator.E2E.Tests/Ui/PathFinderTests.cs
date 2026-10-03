using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the path finder: "Find Path from Here" on a node opens the panel with that node as
// "from" and asks for "to" in the search dialog; the chain is listed and its lines are drawn
// highlighted. View › Find Path opens the empty panel, and the close button removes it again.
public class PathFinderTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task FindPathFromNode_ShouldListTheChain_AndHighlightItsLines()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("path-panel");
        ILocator pathLines = Page.Locator("#svgcanvas .line-path");
        await Expect(panel).ToHaveCountAsync(0);

        // Navigate into Demo.UI (its children render as containers there) and start from Main;
        // the picker for "to" opens right away.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await (await App.OpenNodeMenuItemAsync("node-menu-find-path")).ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("path-from")).ToContainTextAsync("Main");
        await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();

        // In the demo model Main reaches Demo.Core's ModelPaths in three hops (via Canvas and AppBar).
        await search.FillAsync("ModelPaths");
        await search.Result("ModelPaths").First.ClickAsync();

        await Expect(Page.GetByTestId("path-to")).ToContainTextAsync("ModelPaths");
        ILocator items = Page.GetByTestId("path-item");
        await Expect(items.First).ToBeVisibleAsync();
        await Expect(items.First).ToContainTextAsync("ModelPaths");
        await Expect(Page.GetByTestId("path-summary")).ToContainTextAsync("3 hops");
        await Expect(pathLines.First).ToBeAttachedAsync();

        // Swapping asks the reverse question, which the demo model answers with "no path".
        await Page.GetByTestId("path-swap").ClickAsync();
        await Expect(Page.GetByTestId("path-none")).ToBeVisibleAsync();
        await Expect(pathLines).ToHaveCountAsync(0);

        await Page.GetByTestId("path-close").ClickAsync();
        await Expect(panel).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task FindPathMenu_ShouldOpenTheEmptyPanel_AndClose()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("path-panel");
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-find-path")).ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("path-from")).ToContainTextAsync("Choose a node");
        await Expect(Page.GetByTestId("path-to")).ToContainTextAsync("Choose a node");

        await Page.GetByTestId("path-close").ClickAsync();
        await Expect(panel).ToHaveCountAsync(0);
    }
}
