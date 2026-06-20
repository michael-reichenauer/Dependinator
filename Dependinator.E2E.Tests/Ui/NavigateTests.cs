using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

public class NavigateTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Search_ShouldNavigateToNode_WhenSelectingResult()
    {
        await App.GotoAsync();

        // Search for "RoCla" and select the first match (the "RootClass" node).
        SearchDialog search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("RoCla");
        await search.Results.First.ClickAsync();

        // Selecting a result navigates the diagram to that node, so its label "RootClass"
        // renders on the canvas (it is not shown at the initial root-level view).
        await Expect(App.NodeLabel("RootClass")).ToBeVisibleAsync();

        // Select that node on the canvas by its visible (short) label; its context toolbar
        // then appears.
        await App.SelectNodeByVisibleNameAsync("RootClass");
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
    }
}
