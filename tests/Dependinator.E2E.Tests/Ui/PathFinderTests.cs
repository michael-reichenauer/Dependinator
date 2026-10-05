using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the path finder and the explorer's indirect rows: including indirect dependencies
// lists nodes reached through other nodes with hop counts, and their chain button opens the path
// panel with the chain listed and its lines drawn highlighted. View › Find Path opens the panel
// with two pickers, and the close button removes it again.
public class PathFinderTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ExplorerIndirectRow_ShouldShowTheChain_AndHighlightItsLines()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("path-panel");
        ILocator pathLines = Page.Locator("#svgcanvas .line-path");
        await Expect(panel).ToHaveCountAsync(0);

        // Navigate inside Main so it renders as a container, and open its dependencies.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        ILocator rows = Page.Locator(".mud-treeview-item-content");
        await Expect(rows.First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("explorer-hops")).ToHaveCountAsync(0);

        // Main has no direct dependency on Demo.Core, so that row only appears with indirect
        // ones included; fully expanded, ModelPaths shows up three hops away (Main → Canvas →
        // AppBar → ModelPaths in the demo model).
        await Page.GetByTestId("explorer-indirect").ClickAsync();
        ILocator coreRow = rows.Filter(new() { HasTextString = "Demo.Core" }).First;
        await Expect(coreRow).ToBeVisibleAsync();
        await App.ClickRowHoverButtonAsync(coreRow, "explorer-expand-all");
        ILocator modelPathsRow = rows.Filter(new() { HasTextString = "ModelPaths" }).First;
        await Expect(modelPathsRow).ToBeVisibleAsync();
        await Expect(modelPathsRow.GetByTestId("explorer-hops")).ToContainTextAsync("3 hops");

        await App.ClickRowHoverButtonAsync(modelPathsRow, "explorer-show-chain");
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("path-from")).ToContainTextAsync("Main");
        await Expect(Page.GetByTestId("path-to")).ToContainTextAsync("ModelPaths");
        ILocator items = Page.GetByTestId("path-item");
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
    public async Task FindPathMenu_ShouldOpenThePanel_AndPickBothEnds()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("path-panel");
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-find-path")).ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("path-from")).ToContainTextAsync("Choose a node");
        await Expect(Page.GetByTestId("path-to")).ToContainTextAsync("Choose a node");

        // Each picker is the node search in picker mode: the chosen node fills that end. Pick
        // the row of the class itself (the ranking may list a member such as OnInitialized()
        // above it, and "Main" also matches those rows by substring).
        var search = new Shared.Pages.SearchDialog(App, Page);
        await Page.GetByTestId("path-from").ClickAsync();
        await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();
        await search.FillAsync("Demo.UI.Main");
        await search.ResultByFullName("Demo.UI.Main").ClickAsync();
        await Expect(Page.GetByTestId("path-from")).ToContainTextAsync("Main");

        await Page.GetByTestId("path-to").ClickAsync();
        await search.FillAsync("ModelPaths");
        await search.Result("ModelPaths").First.ClickAsync();
        await Expect(Page.GetByTestId("path-to")).ToContainTextAsync("ModelPaths");
        await Expect(Page.GetByTestId("path-item").First).ToContainTextAsync("ModelPaths");

        await Page.GetByTestId("path-close").ClickAsync();
        await Expect(panel).ToHaveCountAsync(0);
    }
}
