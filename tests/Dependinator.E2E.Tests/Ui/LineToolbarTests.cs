using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the selected-line toolbar (LineToolbar.razor): the lines the dependency explorer
// draws for its subject (Line.IsFocused) offer the "go to source/target node" jumps that direct
// lines have, since their far end can lie deep inside another container, while they keep the
// explore actions of an ordinary line.
public class LineToolbarTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ExplorerLine_ShouldOfferGoToSourceAndTarget()
    {
        await App.GotoMainPageAsync();

        // Same setup as the explorer test in NodeToolbarTests: "Main" as a container, then its
        // dependencies explorer draws Main's own line to the Externals container (a demo-model
        // fact), which exists only while the explorer shows lines.
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        ILocator line = App.Line("Demo.UI.Main", "Externals");
        await Expect(line).ToHaveCountAsync(1);

        // The explorer line is not a direct line (no hide button) and keeps the explore actions,
        // and it adds the jumps to its ends.
        await App.SelectLineAsync(line);
        await Expect(App.LinePanSourceButton).ToBeVisibleAsync();
        await Expect(App.LinePanTargetButton).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("line-references")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("line-close")).ToHaveCountAsync(0);

        // Going to the target navigates to the far end and selects it (the breadcrumb chain
        // ends at the selected node).
        await App.LinePanTargetButton.ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToHaveTextAsync("Externals");
    }
}
