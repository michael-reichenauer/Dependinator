using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises architecture rules: the panel opens empty, a rule picked from two nodes lists the
// dependencies that break it and draws them highlighted, a rule nobody breaks is marked kept, and
// removing a rule takes it away again.
public class RulesTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Rules_ShouldListViolations_AndHighlightTheirLines()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("rules-panel");
        ILocator ruleLines = Page.Locator("#svgcanvas .line-rule");
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("rules-empty")).ToBeVisibleAsync();

        // "Demo.UI must not use Demo.Core": the demo's UI project does use Core, so it is broken.
        await AddRuleAsync("Demo.UI (dll)", "Demo.Core (dll)");
        ILocator rule = Page.GetByTestId("rules-item").First;
        await Expect(rule).ToBeVisibleAsync();
        await Expect(rule.GetByTestId("rules-item-count")).ToContainTextAsync("broken");
        ILocator violation = Page.GetByTestId("rules-violation").First;
        await Expect(violation).ToBeVisibleAsync();

        // At the initial view only the solution's own lines are drawn; a violation row navigates
        // to the offending class, where its lines show up in the rule color.
        await violation.ClickAsync();
        await Expect(ruleLines.First).ToBeAttachedAsync();

        // The other way round nobody breaks it.
        await AddRuleAsync("Demo.Core (dll)", "Demo.UI (dll)");
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("rules-item").Nth(1).GetByTestId("rules-item-count")).ToContainTextAsync("kept");

        // Removing the broken rule takes its lines with it.
        await rule.GetByTestId("rules-remove").ClickAsync();
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(1);
        await Expect(ruleLines).ToHaveCountAsync(0);

        await Page.GetByTestId("rules-close").ClickAsync();
        await Expect(panel).ToHaveCountAsync(0);
    }

    // Picks both nodes through the search dialog in picker mode and adds the rule.
    async Task AddRuleAsync(string from, string to)
    {
        var search = new Shared.Pages.SearchDialog(App, Page);
        await Page.GetByTestId("rules-add-from").ClickAsync();
        await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();
        await search.FillAsync(from);
        await search.Result(from).First.ClickAsync();
        await Expect(Page.GetByTestId("rules-add-from")).ToContainTextAsync(from);

        await Page.GetByTestId("rules-add-to").ClickAsync();
        await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();
        await search.FillAsync(to);
        await search.Result(to).First.ClickAsync();
        await Expect(Page.GetByTestId("rules-add-to")).ToContainTextAsync(to);

        await Page.GetByTestId("rules-add").ClickAsync();
    }
}
