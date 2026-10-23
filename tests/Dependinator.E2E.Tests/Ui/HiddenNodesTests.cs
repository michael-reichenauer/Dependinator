using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Hide Node leaves a node out of the diagram; View › Reveal Hidden Nodes draws it faintly again
// to find it, and Show Node brings it back for good.
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

    async Task ToggleRevealAsync(bool expectChecked)
    {
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-reveal-hidden-nodes")).ClickAsync();
        await Expect(await App.OpenSubMenuItemAsync("menu-view", "menu-reveal-hidden-nodes"))
            .ToHaveAttributeAsync("data-checked", expectChecked ? "true" : "false");
        await App.CloseMenuAsync();
    }
}
