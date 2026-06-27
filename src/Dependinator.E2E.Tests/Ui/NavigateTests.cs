using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

public class NavigateTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Search_ShouldNavigateToNode_WhenSelectingResult()
    {
        await App.GotoMainPageAsync();

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

    [E2EFact]
    public async Task Search_ShouldMoveSelectionWithArrowKeysAndNavigateOnEnter()
    {
        await App.GotoMainPageAsync();

        SearchDialog search = await App.OpenSearchViaHotkeyAsync();

        // "Class" matches several demo nodes (RootClass, ...), so there are >= 2 results.
        await search.FillAsync("Class");
        await Expect(search.Results.Nth(1)).ToBeVisibleAsync();

        // The first result is selected initially; ArrowDown moves selection to the second.
        var selected = new Regex("search-dialog__item--selected");
        await Expect(search.Results.First).ToHaveClassAsync(selected);
        await search.Field.PressAsync("ArrowDown");
        await Expect(search.Results.Nth(1)).ToHaveClassAsync(selected);
        await Expect(search.Results.First).Not.ToHaveClassAsync(selected);

        // Enter activates the selected result, which navigates and closes the dialog.
        await search.Field.PressAsync("Enter");
        await Expect(search.Field).ToBeHiddenAsync();
    }
}
