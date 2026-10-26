using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Hide Node leaves a node out of the diagram; View › Reveal Hidden Nodes draws it faintly again
// to find it, and Show Node brings it back for good. The app bar counts dimmed and hidden nodes
// and lists them in the hidden nodes panel.
public class HiddenNodesTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task HideNode_ShouldLeaveItOut_RevealShouldDrawItFaintly_AndShowBringItBack()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");

        await App.SelectNodeByVisibleNameAsync("DemoTheme");
        await App.ClickNodeMenuItemAsync("node-menu-hide");
        await Expect(App.NodeLabel("DemoTheme")).ToHaveCountAsync(0);

        await ToggleRevealAsync(expectChecked: true);
        await Expect(App.NodeLabel("DemoTheme")).ToBeVisibleAsync();

        await App.SelectNodeByVisibleNameAsync("DemoTheme");
        await App.ClickNodeMenuItemAsync("node-menu-show");
        await ToggleRevealAsync(expectChecked: false);
        await Expect(App.NodeLabel("DemoTheme")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HiddenNodesList_ShouldListAHiddenNode_GoToIt_AndShowItAgain()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectNodeByVisibleNameAsync("DemoTheme");
        await App.ClickNodeMenuItemAsync("node-menu-hide");

        // The app bar counts it; a click lists it.
        await Expect(App.MenuItem("hidden-count")).ToHaveTextAsync("1");
        // A reminder, not a problem: drawn in the app bar's own color, not an alarm color (#114).
        Assert.Equal(await IconColorAsync("toolbar-search"), await IconColorAsync("hidden-indicator"));
        await App.MenuItem("hidden-indicator").ClickAsync();
        ILocator item = App.MenuItem("hidden-item");
        await Expect(item).ToHaveCountAsync(1);
        await Expect(item).ToHaveAttributeAsync("data-visibility", "Hidden");
        await Expect(item.GetByTestId("hidden-item-name")).ToContainTextAsync("DemoTheme");

        // Going to a hidden node reveals hidden nodes, so it can be seen.
        await item.GetByTestId("hidden-item-name").ClickAsync();
        await Expect(App.NodeLabel("DemoTheme")).ToBeVisibleAsync();
        await App.ExpectSubMenuItemCheckedAsync("menu-view", "menu-reveal-hidden-nodes", true);

        // Show takes it off the list, and the count goes away.
        await item.GetByTestId("hidden-item-show").ClickAsync();
        await Expect(App.MenuItem("hidden-empty")).ToBeVisibleAsync();
        await Expect(App.MenuItem("hidden-indicator")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task HidingAChild_ShouldBadgeItsParent_WhoseMenuListsTheHiddenChildren()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectNodeByVisibleNameAsync("DemoTheme");
        await App.ClickNodeMenuItemAsync("node-menu-hide");

        // Main, the parent, carries a badge with the count.
        await Expect(Page.Locator("#svgcanvas g.hiddenBadge[data-count='1']")).ToHaveCountAsync(1);

        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await App.ClickNodeMenuItemAsync("node-menu-hidden-children");
        await Expect(App.MenuItem("hidden-parent")).ToContainTextAsync("Main");
        await Expect(App.MenuItem("hidden-item")).ToHaveCountAsync(1);
        await Expect(App.MenuItem("hidden-item-name")).ToContainTextAsync("DemoTheme");

        await App.MenuItem("hidden-item-show").ClickAsync();
        await Expect(Page.Locator("#svgcanvas g.hiddenBadge")).ToHaveCountAsync(0);
        await Expect(App.MenuItem("hidden-empty")).ToContainTextAsync("No hidden children left");
    }

    Task<string> IconColorAsync(string testId) =>
        App.MenuItem(testId).Locator("svg").First.EvaluateAsync<string>("e => getComputedStyle(e).color");

    Task ToggleRevealAsync(bool expectChecked) =>
        App.ToggleSubMenuItemAsync("menu-view", "menu-reveal-hidden-nodes", expectChecked);
}
