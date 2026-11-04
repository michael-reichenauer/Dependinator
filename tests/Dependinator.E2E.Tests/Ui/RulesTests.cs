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

        // At the initial view only the solution's own lines are drawn; a violation row frames
        // both classes, where the lines between them show up in the rule color.
        await violation.ClickAsync();
        await Expect(ruleLines.First).ToBeAttachedAsync();

        // A rule's node names go to that node and select it (the breadcrumb ends with it).
        await rule.GetByTestId("rules-item-to").ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync("Demo.Core");
        await rule.GetByTestId("rules-item-from").ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync("Demo.UI");

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

        // Claude is the default now: one button asks it again.
        await Page.GetByTestId("rule-review-ask").ClickAsync();
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

    // Fix later moves a violation to the rule's To fix list with the AI's suggested change as the
    // plan: no longer a violation, but listed; editing it can report it again.
    [E2EFact]
    public async Task FixLater_ShouldListTheDependencyUnderToFix_WithThePlannedChange()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await AddRuleAsync("Demo.UI (dll)", "Demo.Core (dll)");
        ILocator violations = Page.GetByTestId("rules-violation-item");
        await Expect(violations.First).ToBeVisibleAsync();
        int count = await violations.CountAsync();

        await violations.First.GetByTestId("rules-review").ClickAsync();
        await Page.GetByTestId("rule-review-expand").ClickAsync();
        await Page.GetByTestId("rule-review-suggest").ClickAsync();
        await Expect(Page.GetByTestId("rule-review-suggestion")).ToHaveAttributeAsync("data-verdict", "fix");
        await Page.GetByTestId("rule-review-fix-later").ClickAsync();

        await Expect(violations).ToHaveCountAsync(count - 1);
        ILocator toFix = Page.GetByTestId("rules-tofix-item");
        await Expect(toFix).ToHaveCountAsync(1);
        await Expect(toFix.GetByTestId("rules-tofix-change"))
            .ToHaveTextAsync("Test mode: depend on an interface instead.");

        // Editing it can change the plan, or report it again.
        await toFix.GetByTestId("rules-tofix-edit").ClickAsync();
        await Expect(Page.GetByTestId("rule-review-reason"))
            .ToHaveValueAsync("Test mode: depend on an interface instead.");
        await Page.GetByTestId("rule-review-disallow").ClickAsync();
        await Expect(toFix).ToHaveCountAsync(0);
        await Expect(violations).ToHaveCountAsync(count);
    }

    // An AI suggests rules (in test mode the app answers for Claude) in a view the Rules panel
    // switches to: the prompt holds the model's structure and dependencies, Dependinator checks
    // each rule, one on a node that is not in the model cannot be added, a rule's node name goes to
    // its node, and an added rule shows as one, both here and back in the rules list.
    [E2EFact]
    public async Task SuggestRules_ShouldCheckEachRule_AndAddTheChosenOnes()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await Page.GetByTestId("rules-suggest").ClickAsync();
        await Expect(Page.GetByTestId("suggest-rules-panel")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("rules-panel")).ToHaveCountAsync(0);

        await Page.GetByTestId("suggest-rules-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("Dependencies between them");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        await Page.GetByTestId("suggest-rules-suggest").ClickAsync();
        ILocator items = Page.GetByTestId("suggest-rules-item");
        await Expect(items).ToHaveCountAsync(3);
        await Expect(items.Nth(2)).ToHaveAttributeAsync("data-can-add", "no");
        await Expect(items.Nth(2).GetByTestId("suggest-rules-error")).ToContainTextAsync("No node is named");
        await Expect(Page.GetByTestId("suggest-rules-add-all")).ToHaveTextAsync("Add all 2");

        // The node names go to their node, with the view still open beside the diagram.
        // TextContent, since Firefox's InnerText breaks the name at a wrapped <wbr> ("Demo.\nsln").
        var toName = await items.Nth(0).GetByTestId("suggest-rules-to").TextContentAsync() ?? "";
        await items.Nth(0).GetByTestId("suggest-rules-to").ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync(toName.Split(' ')[0]);
        await Expect(Page.GetByTestId("suggest-rules-panel")).ToBeVisibleAsync();

        await items.Nth(0).GetByTestId("suggest-rules-add").ClickAsync();
        await Expect(items.Nth(0)).ToHaveAttributeAsync("data-can-add", "no");
        await Expect(items.Nth(0).GetByTestId("suggest-rules-status")).ToHaveTextAsync("already a rule");
        await Expect(Page.GetByTestId("suggest-rules-add-all")).ToHaveCountAsync(0);

        // Back to the rules, which list the added one; the answer is still there on return.
        await Page.GetByTestId("suggest-rules-back").ClickAsync();
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(1);
        await Page.GetByTestId("rules-suggest").ClickAsync();
        await Expect(items).ToHaveCountAsync(3);
    }

    // A dismissed suggestion is not suggested again, nor is one an earlier answer suggested; an
    // added rule keeps the AI's why as its reason, and removing it remembers it as dismissed too.
    [E2EFact]
    public async Task SuggestRules_ShouldRememberDismissedRules_AndKeepTheWhyOfAddedOnes()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await Page.GetByTestId("rules-suggest").ClickAsync();
        await Page.GetByTestId("suggest-rules-suggest").ClickAsync();
        ILocator items = Page.GetByTestId("suggest-rules-item");
        await Expect(items).ToHaveCountAsync(3);

        await items.Nth(1).GetByTestId("suggest-rules-dismiss").ClickAsync();
        await Expect(items.Nth(1).GetByTestId("suggest-rules-status")).ToHaveTextAsync("dismissed");
        await Expect(items.Nth(1)).ToHaveAttributeAsync("data-can-add", "no");
        await items.Nth(1).GetByTestId("suggest-rules-undismiss").ClickAsync();
        await Expect(items.Nth(1)).ToHaveAttributeAsync("data-can-add", "yes");
        await items.Nth(1).GetByTestId("suggest-rules-dismiss").ClickAsync();
        await items.Nth(0).GetByTestId("suggest-rules-add").ClickAsync();

        // Asked again, the same answer brings nothing new: one rule added, one dismissed, and
        // the one that cannot be added.
        await Page.GetByTestId("suggest-rules-ask").ClickAsync();
        await Expect(Page.GetByTestId("suggest-rules-left-out")).ToContainTextAsync("2 more were left out");
        await Expect(items).ToHaveCountAsync(1);
        await Expect(items.Nth(0)).ToHaveAttributeAsync("data-can-add", "no");

        // The added rule shows the why, and removing it lists it with the dismissed one.
        await Page.GetByTestId("suggest-rules-back").ClickAsync();
        await Expect(Page.GetByTestId("rules-item-reason")).ToHaveTextAsync("Test mode: Claude was not asked.");
        await Page.GetByTestId("rules-dismissed-toggle").ClickAsync();
        await Expect(Page.GetByTestId("rules-dismissed-item")).ToHaveCountAsync(1);
        await Page.GetByTestId("rules-remove").ClickAsync();
        await Expect(Page.GetByTestId("rules-item")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("rules-dismissed-item")).ToHaveCountAsync(2);
        await Page.GetByTestId("rules-dismissed-remove").First.ClickAsync();
        await Expect(Page.GetByTestId("rules-dismissed-item")).ToHaveCountAsync(1);
    }

    // The other kinds of rule: "only X may use Y" and layers, each picked in the same panel.
    [E2EFact]
    public async Task RuleKinds_ShouldAddOnlyMayUseAndLayers_AndEvaluateEach()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        ILocator rules = Page.GetByTestId("rules-item");

        // Other projects than UI use Core, so "only UI may use Core" is broken.
        await Page.GetByTestId("rules-add-kind").SelectOptionAsync("OnlyMayUse");
        await AddRuleAsync("Demo.UI (dll)", "Demo.Core (dll)");
        await Expect(rules).ToHaveCountAsync(1);
        await Expect(rules.Nth(0)).ToContainTextAsync("may use");
        await Expect(rules.Nth(0).GetByTestId("rules-item-count")).ToContainTextAsync("broken");

        // UI uses Core, so UI above Core is kept, and Core above UI is broken.
        await Page.GetByTestId("rules-add-kind").SelectOptionAsync("Layers");
        await Expect(Page.GetByTestId("rules-add")).ToBeDisabledAsync();
        await AddLayersAsync("Demo.UI (dll)", "Demo.Core (dll)");
        await Expect(rules).ToHaveCountAsync(2);
        await Expect(rules.Nth(1).GetByTestId("rules-item-layer")).ToHaveCountAsync(2);
        await Expect(rules.Nth(1).GetByTestId("rules-item-count")).ToContainTextAsync("kept");
        await Expect(Page.GetByTestId("rules-add-layer")).ToHaveCountAsync(0);

        await AddLayersAsync("Demo.Core (dll)", "Demo.UI (dll)");
        await Expect(rules).ToHaveCountAsync(3);
        await Expect(rules.Nth(2).GetByTestId("rules-item-count")).ToContainTextAsync("broken");
        await Expect(rules.Nth(2).GetByTestId("rules-violation").First).ToBeVisibleAsync();
    }

    // The rules about one node: "X is independent" and "no cycles inside X".
    [E2EFact]
    public async Task OneNodeRules_ShouldReportUsesOutsideTheNode_AndCyclesInsideIt()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        ILocator rules = Page.GetByTestId("rules-item");
        var search = new Shared.Pages.SearchDialog(App, Page);

        // Core uses the shared cloud sync types, so it is not independent; it has cycles inside.
        foreach (var kind in new[] { "Independent", "NoCycles" })
        {
            await Page.GetByTestId("rules-add-kind").SelectOptionAsync(kind);
            await Expect(Page.GetByTestId("rules-add-to")).ToHaveCountAsync(0);
            await Page.GetByTestId("rules-add-from").ClickAsync();
            await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();
            await search.FillAsync("Demo.Core (dll)");
            await search.Result("Demo.Core (dll)").First.ClickAsync();
            await Page.GetByTestId("rules-add").ClickAsync();
        }

        await Expect(rules).ToHaveCountAsync(2);
        await Expect(rules.Nth(0)).ToContainTextAsync("is independent");
        await Expect(rules.Nth(0).GetByTestId("rules-item-count")).ToContainTextAsync("broken");
        await Expect(rules.Nth(1)).ToContainTextAsync("no cycles inside");
        await Expect(rules.Nth(1).GetByTestId("rules-item-count")).ToContainTextAsync("broken");
    }

    // Picks the layers top first through the search dialog in picker mode and adds the rule.
    async Task AddLayersAsync(params string[] layers)
    {
        var search = new Shared.Pages.SearchDialog(App, Page);
        foreach (var layer in layers)
        {
            await Page.GetByTestId("rules-add-layer-pick").ClickAsync();
            await Expect(Page.GetByTestId("search-pick-caption")).ToBeVisibleAsync();
            await search.FillAsync(layer);
            await search.Result(layer).First.ClickAsync();
            await Expect(Page.GetByTestId("rules-add-layer").Last).ToContainTextAsync(layer);
        }
        await Page.GetByTestId("rules-add").ClickAsync();
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
