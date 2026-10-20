using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises View › Show Cycles: the panel opens with the cycles found in the model (or says
// there are none) and closes again from its button; a cycle accepted with a reason moves to the
// Accepted section, its reason can be edited, and removing the acceptance brings it back; a
// cycle's AI prompt can be copied, and Claude can suggest the reason, or a fix prompt to copy.
public class CyclesTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ShowCycles_ShouldOpenThePanel_AndClose()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator panel = Page.GetByTestId("cycles-panel");
        await Expect(panel).ToHaveCountAsync(0);

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel).ToContainTextAsync("Cycles");

        await Page.GetByTestId("cycles-close").ClickAsync();
        await Expect(panel).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task AcceptCycle_ShouldMoveItToAccepted_WithItsReason_AndBackWhenRemoved()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();

        // The demo model (Dependinator itself) has several cycles.
        ILocator cycles = Page.GetByTestId("cycles-item");
        await Expect(cycles.First).ToBeVisibleAsync();
        int open = await cycles.CountAsync();

        await cycles.First.GetByTestId("cycles-accept").ClickAsync();
        await AcceptReasonAsync("Intended: one concept");
        await Expect(cycles).ToHaveCountAsync(open - 1);

        // The Accepted section starts collapsed; opened it shows the reason.
        await Page.GetByTestId("cycles-accepted-toggle").ClickAsync();
        ILocator accepted = Page.GetByTestId("cycles-accepted-item");
        await Expect(accepted).ToHaveCountAsync(1);
        await Expect(accepted.GetByTestId("cycles-accepted-reason")).ToHaveTextAsync("Intended: one concept");

        await accepted.GetByTestId("cycles-accepted-edit").ClickAsync();
        await AcceptReasonAsync("Edited reason");
        await Expect(accepted.GetByTestId("cycles-accepted-reason")).ToHaveTextAsync("Edited reason");

        await accepted.GetByTestId("cycles-accepted-remove").ClickAsync();
        await Expect(accepted).ToHaveCountAsync(0);
        await Expect(cycles).ToHaveCountAsync(open);
    }

    [E2EFact]
    public async Task AcceptCycle_ShouldFillInTheReasonClaudeSuggests()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        await Page.GetByTestId("cycles-item").First.GetByTestId("cycles-accept").ClickAsync();
        ILocator field = Page.GetByTestId("accept-cycle-reason");
        await Expect(field).ToBeVisibleAsync();

        // In test mode the app answers for Claude (no key or network), so this covers the dialog.
        await Page.GetByTestId("accept-cycle-suggest").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-suggestion")).ToHaveAttributeAsync("data-verdict", "accept");
        await Expect(field).ToHaveValueAsync("Suggested in test mode");

        await Page.GetByTestId("accept-cycle-save").ClickAsync();
        await Expect(field).ToHaveCountAsync(0);
        await Page.GetByTestId("cycles-accepted-toggle").ClickAsync();
        await Expect(Page.GetByTestId("cycles-accepted-reason")).ToHaveTextAsync("Suggested in test mode");
    }

    [E2EFact]
    public async Task AcceptCycle_ShouldOfferAFixPromptFirst_WhenClaudeSuggestsBreakingIt()
    {
        // The clipboard is stubbed and read back (see CopyPrompt_ShouldPutAnAiPromptForTheCycleOnTheClipboard).
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();

        // In test mode the app answers for Claude: a cycle of more than two is to be broken.
        ILocator cycle = Page.GetByTestId("cycles-item").Filter(new() { HasTextString = "TaskExtensions" });
        await cycle.GetByTestId("cycles-accept").ClickAsync();
        await Page.GetByTestId("accept-cycle-suggest").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-suggestion")).ToHaveAttributeAsync("data-verdict", "break");
        await Expect(Page.GetByTestId("accept-cycle-save")).ToHaveTextAsync("Accept anyway");

        await Page.GetByTestId("accept-cycle-copy-fix").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-fix-copied")).ToBeVisibleAsync();
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Please break a circular dependency", prompt);
        Assert.Contains("Test mode: move one member out of the cycle.", prompt);

        // Copying the fix prompt accepts nothing.
        await Page.GetByTestId("accept-cycle-cancel").ClickAsync();
        await Expect(cycle).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task CopyPrompt_ShouldPutAnAiPromptForTheCycleOnTheClipboard()
    {
        // Playwright can grant clipboard permissions on Chromium only, so the clipboard is
        // stubbed and the written text is read back (as in ShareLinkTests).
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();

        ILocator cycle = Page.GetByTestId("cycles-item").First;
        string member = (await cycle.Locator(".dep-cycles__members").InnerTextAsync()).Split(" ↔ ")[0];
        await cycle.GetByTestId("cycles-copy-prompt").ClickAsync();
        await Expect(Page.GetByText("AI prompt copied")).ToBeVisibleAsync();

        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("## The cycle", prompt);
        Assert.Contains(member, prompt);
        Assert.Contains(" → ", prompt);
        Assert.Contains("Verdict: Accept or Break", prompt);
    }

    // Fills the reason in the accept dialog and saves it.
    async Task AcceptReasonAsync(string reason)
    {
        ILocator field = Page.GetByTestId("accept-cycle-reason");
        await Expect(field).ToBeVisibleAsync();
        await App.FillReliablyAsync(field, reason);
        await Page.GetByTestId("accept-cycle-save").ClickAsync();
        await Expect(field).ToHaveCountAsync(0);
    }
}
