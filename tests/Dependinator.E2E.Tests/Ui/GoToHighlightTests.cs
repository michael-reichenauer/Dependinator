using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// A go to (search, a link, a list) shows its node with amber corner brackets instead of selecting it
// (HighlightService): the user's selection stays as it was, and the brackets last until the next
// press or key in the diagram.
public class GoToHighlightTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Search_ShouldHighlightTheNode_WithoutSelectingIt_UntilTheNextClick()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.NavigateToNodeWithoutSelectingAsync(AppPage.InsideMain);

        // Highlighted, not selected: no node toolbar, but the breadcrumb leads to it.
        await Expect(App.NodeHighlight).ToHaveCountAsync(1);
        await Expect(App.NodeToolbarMenu).ToBeHiddenAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToContainTextAsync("OnInitialized");

        // The next click selects as usual and the highlight is gone.
        await App.SelectNodeByFullNameAsync(AppPage.InsideMain);
        await Expect(App.NodeHighlight).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Search_ShouldKeepTheSelection_AndHighlightTheOtherNode()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Select a member of Main, then go to a node elsewhere.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.NavigateToNodeWithoutSelectingAsync("Demo.Core.Utils", isFromInitialView: false);

        // The breadcrumb follows the node just asked for.
        await Expect(App.NodeHighlight).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToHaveTextAsync("Utils");

        // Back to the earlier view ends the highlight: the member is still selected, its toolbar
        // and its chain show again.
        await App.MenuItem("toolbar-back").ClickAsync();
        await Expect(App.NodeHighlight).ToHaveCountAsync(0);
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToContainTextAsync("OnInitialized");
    }
}
