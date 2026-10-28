using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises architecture rules: the panel opens empty, a rule picked from two nodes lists the
// dependencies that break it and draws them highlighted, a rule nobody breaks is marked kept, and
// removing a rule takes it away again. An AI can suggest rules, which Dependinator checks.
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

    // A broken rule shows in the problem indicator in the app bar, whose row opens the panel again.
    [E2EFact]
    public async Task BrokenRule_ShouldShowInTheProblemIndicator_AndOpenTheRulesPanel()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        ILocator indicator = Page.GetByTestId("problems-indicator");
        ILocator ruleRow = Page.Locator("[data-testid=problems-item][data-kind=RuleViolations]");

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await AddRuleAsync("Demo.UI (dll)", "Demo.Core (dll)");
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(1);
        await Page.GetByTestId("rules-close").ClickAsync();
        await Expect(Page.GetByTestId("rules-panel")).ToHaveCountAsync(0);

        await App.RepeatUntilVisibleAsync(() => indicator.ClickAsync(), ruleRow);
        await Expect(ruleRow).ToContainTextAsync("1 broken architecture rule");
        await ruleRow.ClickAsync();
        await Expect(Page.GetByTestId("rules-panel")).ToBeVisibleAsync();
    }

    // A dependency that breaks a rule is reviewed like a cycle: the prompt can be read first, an AI
    // suggests fixing it (with a fix prompt) or allowing it (with a reason), and an allowed one moves
    // to the rule's Allowed list until the user stops allowing it.
    [E2EFact]
    public async Task ReviewViolation_ShouldOfferAFixPrompt_AndAllowItWithAReason()
    {
        // Playwright can grant clipboard permissions on Chromium only, so the clipboard is stubbed.
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await AddRuleAsync("Demo.UI (dll)", "Demo.Core (dll)");
        ILocator violations = Page.GetByTestId("rules-violation-item");
        await Expect(violations.First).ToBeVisibleAsync();
        int count = await violations.CountAsync();

        await violations.First.GetByTestId("rules-review").ClickAsync();
        await Expect(Page.GetByTestId("rule-review-text")).ToContainTextAsync("must not use");
        await Page.GetByTestId("rule-review-expand").ClickAsync();
        await Page.GetByTestId("rule-review-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("## The dependency that breaks it");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        // In test mode the app answers for Claude: first fix it, then allow it.
        await Page.GetByTestId("rule-review-suggest").ClickAsync();
        ILocator verdict = Page.GetByTestId("rule-review-suggestion");
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "fix");
        await Expect(Page.GetByTestId("rule-review-save")).ToHaveTextAsync("Allow anyway");
        await Page.GetByTestId("rule-review-copy-fix").ClickAsync();
        await Expect(Page.GetByTestId("rule-review-copied")).ToContainTextAsync("fix prompt is copied");
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Please remove a dependency that breaks an architecture rule", prompt);

        await Page.GetByTestId("rule-review-suggest").ClickAsync();
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "allow");
        await Expect(Page.GetByTestId("rule-review-answers-differ")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("rule-review-reason")).ToHaveValueAsync("Allowed in test mode");
        await Page.GetByTestId("rule-review-save").ClickAsync();

        await Expect(violations).ToHaveCountAsync(count - 1);
        ILocator allowed = Page.GetByTestId("rules-allowed-item");
        await Expect(allowed).ToHaveCountAsync(1);
        await Expect(allowed.GetByTestId("rules-allowed-reason")).ToHaveTextAsync("Allowed in test mode");

        // Collapsed, the list keeps its count; stopping to allow it reports it again.
        await Page.GetByTestId("rules-allowed-toggle").ClickAsync();
        await Expect(allowed).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("rules-allowed-toggle")).ToContainTextAsync("Allowed (1)");
        await Page.GetByTestId("rules-allowed-toggle").ClickAsync();
        await allowed.GetByTestId("rules-allowed-remove").ClickAsync();
        await Expect(violations).ToHaveCountAsync(count);
        await Expect(Page.GetByTestId("rules-allowed-toggle")).ToHaveCountAsync(0);
    }

    // An AI suggests rules (in test mode the app answers for Claude): the prompt holds the model's
    // structure and dependencies, Dependinator checks each rule, one on a node that is not in the
    // model cannot be added, and the ticked ones are added.
    [E2EFact]
    public async Task SuggestRules_ShouldCheckEachRule_AndAddTheChosenOnes()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await Page.GetByTestId("rules-suggest").ClickAsync();

        await Page.GetByTestId("suggest-rules-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("Dependencies between them");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        await Page.GetByTestId("suggest-rules-suggest").ClickAsync();
        ILocator items = Page.GetByTestId("suggest-rules-item");
        await Expect(items).ToHaveCountAsync(3);
        await Expect(items.Nth(2)).ToHaveAttributeAsync("data-can-add", "no");
        await Expect(items.Nth(2).GetByTestId("suggest-rules-error")).ToContainTextAsync("No node is named");
        ILocator add = Page.GetByTestId("suggest-rules-add");
        await Expect(add).ToHaveTextAsync("Add 2 rules");

        await items.Nth(0).GetByTestId("suggest-rules-choose").ClickAsync();
        await Expect(add).ToHaveTextAsync("Add 1 rule");
        await add.ClickAsync();
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(1);
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
