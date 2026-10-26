using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises View › Show Cycles: the panel opens with the cycles found in the model (or says
// there are none) and closes again from its button; a cycle accepted with a reason in the review
// dialog moves to the Accepted section, its reason can be edited, and removing the acceptance
// brings it back; the dialog copies a cycle's AI prompt and reads the answer pasted back, and
// answers (Claude's or pasted) suggest the reason, or a fix prompt to copy and a To fix entry.
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

    // Like the dependencies explorer: the panel folds down to its title bar from its button, and
    // on a narrow screen on a click in the diagram, which it would otherwise cover; a click on
    // the title bar brings the list back.
    [E2EFact]
    public async Task CyclesPanel_ShouldMinimizeOnCanvasClickOnANarrowScreen_AndRestoreOnHeaderClick()
    {
        await Page.SetViewportSizeAsync(800, 600);
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        ILocator cycles = Page.GetByTestId("cycles-item");
        ILocator header = Page.GetByTestId("cycles-header");
        ILocator minimize = Page.GetByTestId("cycles-minimize");
        await Expect(cycles.First).ToBeVisibleAsync();

        // Empty canvas at the bottom left, away from the panel at the top right.
        var canvas = await App.Canvas.BoundingBoxAsync();
        await Page.Mouse.ClickAsync(canvas!.X + 30, canvas.Y + canvas.Height - 30);
        await Expect(cycles).ToHaveCountAsync(0);
        await Expect(header).ToBeVisibleAsync();

        await App.RepeatUntilVisibleAsync(() => header.ClickAsync(), cycles.First);
        await minimize.ClickAsync();
        await Expect(cycles).ToHaveCountAsync(0);
        await App.RepeatUntilVisibleAsync(() => minimize.ClickAsync(), cycles.First);
    }

    // Clicking a cycle frames its nodes inside their opened container and picks it: its lines
    // stay red while the other cycles' lines fade (the demo's Core Utils namespace holds two
    // cycles), and so do the lines that do not touch its nodes, as if they were selected (but
    // without the selection's thicker lines, even with View › Line Widths by Link Count on).
    // Clicking it again shows all cycles alike.
    [E2EFact]
    public async Task ClickCycle_ShouldShowItsNodes_AndDimTheOtherCycles_UntilClickedAgain()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.ToggleSubMenuItemAsync("menu-view", "menu-link-count-widths", true);
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        ILocator row = Page.GetByTestId("cycles-item-show").Filter(new() { HasText = "ExceptionHandling" });
        ILocator dimmedCycleLines = Page.Locator("#svgcanvas .line-cycle.line-dim");
        ILocator brightCycleLines = Page.Locator("#svgcanvas .line-cycle:not(.line-dim)");
        ILocator dimmedOtherLines = Page.Locator("#svgcanvas .line-vis.line-dim:not(.line-cycle)");

        await row.ClickAsync();
        await Expect(row).ToHaveAttributeAsync("data-selected", "true");
        await Expect(Page.GetByTestId("breadcrumb-item").Last).ToHaveTextAsync("Utils");
        await Expect(App.NodeLabel("ExceptionHandling")).ToBeVisibleAsync();
        await Expect(dimmedCycleLines.First).ToBeAttachedAsync();
        await Expect(brightCycleLines.First).ToBeAttachedAsync();
        await Expect(dimmedOtherLines.First).ToBeAttachedAsync();
        // Only dimming: the lines keep their plain width, as with nothing selected.
        await Expect(Page.Locator("#svgcanvas .line-vis > polyline:not([stroke-width='1'])")).ToHaveCountAsync(0);

        await row.ClickAsync();
        await Expect(row).ToHaveAttributeAsync("data-selected", "false");
        await Expect(dimmedCycleLines).ToHaveCountAsync(0);
        await Expect(dimmedOtherLines).ToHaveCountAsync(0);
    }

    // The cycles are visible without opening the panel: the problem indicator in the app bar
    // counts them, and its cycles row opens the panel.
    [E2EFact]
    public async Task ProblemIndicator_ShouldCountTheCycles_AndOpenTheCyclesPanel()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        ILocator panel = Page.GetByTestId("cycles-panel");
        ILocator indicator = Page.GetByTestId("problems-indicator");
        ILocator cycleRow = Page.Locator("[data-testid=problems-item][data-kind=Cycles]");
        await Expect(panel).ToHaveCountAsync(0);

        // The demo model (Dependinator itself) has several cycles.
        await Expect(Page.GetByTestId("problems-warnings")).ToHaveTextAsync(new Regex(@"^[1-9]\d*$"));
        await App.RepeatUntilVisibleAsync(() => indicator.ClickAsync(), cycleRow);
        await Expect(cycleRow).ToContainTextAsync("cycles");
        await cycleRow.ClickAsync();
        await Expect(panel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("cycles-item").First).ToBeVisibleAsync();
    }

    // The diagram marks where the cycles are: the top-level solution node has a badge counting
    // the problems inside it, which the View menu turns off and on again.
    [E2EFact]
    public async Task ProblemBadges_ShouldMarkTheSolution_AndFollowTheViewMenu()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        ILocator badges = Page.Locator(".problemBadge");
        await Expect(Page.Locator(".problemBadge:not([data-warnings='0'])").First).ToBeAttachedAsync();

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-problem-badges")).ClickAsync();
        await Expect(badges).ToHaveCountAsync(0);

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-problem-badges")).ClickAsync();
        await Expect(badges.First).ToBeAttachedAsync();
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

        await cycles.First.GetByTestId("cycles-review").ClickAsync();
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
    public async Task AskClaude_ShouldFillInTheReason_AndLetTheUserPickAmongDifferingAnswers()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        // A pair, as test mode accepts no larger cycle
        ILocator pair = Page.GetByTestId("cycles-item").Filter(new() { HasTextString = "Result<T>" });
        await pair.GetByTestId("cycles-review").ClickAsync();
        ILocator field = Page.GetByTestId("accept-cycle-reason");
        await Expect(field).ToBeVisibleAsync();
        ILocator ask = Page.GetByTestId("accept-cycle-suggest");
        ILocator verdict = Page.GetByTestId("accept-cycle-suggestion");

        // In test mode the app answers for Claude (no key or network): a pair is accepted, and
        // broken when asked again.
        await Expect(ask).ToHaveTextAsync("Ask Claude");
        await ask.ClickAsync();
        // While it asks, the answer's place shows that it does.
        await Expect(Page.GetByTestId("accept-cycle-asking")).ToBeVisibleAsync();
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "accept");
        await Expect(Page.GetByTestId("accept-cycle-asking")).ToHaveCountAsync(0);
        await Expect(field).ToHaveValueAsync("Suggested in test mode");
        await Expect(ask).ToHaveTextAsync("Ask again");

        // The second answer differs: both are kept, and the reason follows the selected one.
        await ask.ClickAsync();
        ILocator answers = Page.GetByTestId("accept-cycle-answer");
        await Expect(answers).ToHaveCountAsync(2);
        await Expect(answers.First).ToHaveTextAsync("Claude 1 · Accept");
        await Expect(Page.GetByTestId("accept-cycle-answers-differ")).ToBeVisibleAsync();
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "break");
        await Expect(field)
            .ToHaveValueAsync("Accepted anyway; suggested fix: Test mode: move one member out of the cycle.");
        await answers.First.ClickAsync();
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "accept");
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
        await cycle.GetByTestId("cycles-review").ClickAsync();
        await Page.GetByTestId("accept-cycle-suggest").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-suggestion")).ToHaveAttributeAsync("data-verdict", "break");
        await Expect(Page.GetByTestId("accept-cycle-save")).ToHaveTextAsync("Accept anyway");
        await Expect(Page.GetByTestId("accept-cycle-reason"))
            .ToHaveValueAsync("Accepted anyway; suggested fix: Test mode: move one member out of the cycle.");

        await Page.GetByTestId("accept-cycle-copy-fix").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-copied")).ToContainTextAsync("fix prompt is copied");
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Please break a circular dependency", prompt);
        Assert.Contains("Test mode: move one member out of the cycle.", prompt);

        // Copying the fix prompt accepts nothing.
        await Page.GetByTestId("accept-cycle-cancel").ClickAsync();
        await Expect(cycle).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task FixLater_ShouldListTheCycleToFix_WithItsFixPrompt_AndBackWhenRemoved()
    {
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-cycles")).ClickAsync();
        ILocator open = Page.GetByTestId("cycles-item");
        await Expect(open.First).ToBeVisibleAsync();
        int openCount = await open.CountAsync();

        // In test mode a cycle of more than two is to be broken (see the test above).
        await open.Filter(new() { HasTextString = "TaskExtensions" }).GetByTestId("cycles-review").ClickAsync();
        await Page.GetByTestId("accept-cycle-suggest").ClickAsync();
        await Expect(Page.GetByTestId("accept-cycle-suggestion")).ToHaveAttributeAsync("data-verdict", "break");
        await Page.GetByTestId("accept-cycle-fix-later").ClickAsync();

        await Expect(open).ToHaveCountAsync(openCount - 1);
        ILocator toFix = Page.GetByTestId("cycles-tofix-item");
        await Expect(toFix).ToHaveCountAsync(1);
        await Expect(toFix.GetByTestId("cycles-tofix-change"))
            .ToHaveTextAsync("Test mode: move one member out of the cycle.");

        await toFix.GetByTestId("cycles-tofix-copy-fix").ClickAsync();
        await Expect(Page.GetByText("The fix prompt is copied")).ToBeVisibleAsync();
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Please break a circular dependency", prompt);
        Assert.Contains("Test mode: move one member out of the cycle.", prompt);

        await toFix.GetByTestId("cycles-tofix-remove").ClickAsync();
        await Expect(toFix).ToHaveCountAsync(0);
        await Expect(open).ToHaveCountAsync(openCount);
    }

    [E2EFact]
    public async Task CopyPrompt_ShouldPutAnAiPromptOnTheClipboard_AndReadTheAnswerPastedBack()
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
        await cycle.GetByTestId("cycles-review").ClickAsync();
        // Both routes are offered side by side before any answer.
        await Expect(Page.GetByTestId("accept-cycle-suggest")).ToHaveTextAsync("Ask Claude");
        // The prompt every route uses can be read first, without copying it.
        await Page.GetByTestId("accept-cycle-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("## The cycle");
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync(member);
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToHaveCountAsync(0);
        ILocator copy = Page.GetByTestId("accept-cycle-copy-prompt");
        await copy.ClickAsync();
        // Copying shows the rest of the AI chat route as steps, the first one done.
        await Expect(Page.GetByTestId("accept-cycle-chat-steps")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("accept-cycle-prompt-copied")).ToContainTextAsync("prompt is copied");

        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("## The cycle", prompt);
        Assert.Contains(member, prompt);
        Assert.Contains(" → ", prompt);
        Assert.Contains("Verdict: Accept or Break", prompt);

        // claude.ai opens in a new tab, to paste the prompt into (window.open is stubbed, so no
        // real tab is opened).
        await Page.EvaluateAsync("() => { window.open = url => { window.__openedUrl = url; return null; }; }");
        await Page.GetByTestId("accept-cycle-open-claude").ClickAsync();
        IJSHandle openedUrl = await Page.WaitForFunctionAsync("() => window.__openedUrl");
        Assert.Equal("https://claude.ai/new", await openedUrl.JsonValueAsync<string>());

        // The AI chat's answer is pasted back and read without a button (a fill is one paste):
        // one without a verdict cannot be read. (A readable one closes the steps, so it is filled
        // once, not reliably.)
        ILocator answerBox = Page.GetByTestId("accept-cycle-paste-text");
        await App.FillReliablyAsync(answerBox, "Looks fine to me.");
        await Expect(Page.GetByTestId("accept-cycle-paste-error")).ToContainTextAsync("could not be read");

        await answerBox.FillAsync("**Verdict:** Break\n**Why:** One misplaced class.\n**Change:** Move it.");
        ILocator verdict = Page.GetByTestId("accept-cycle-suggestion");
        await Expect(verdict).ToHaveAttributeAsync("data-verdict", "break");
        await Expect(verdict).ToContainTextAsync("Your AI chat suggests breaking it");
        await Expect(Page.GetByTestId("accept-cycle-chat-steps")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("accept-cycle-save")).ToHaveTextAsync("Accept anyway");
        await Expect(Page.GetByTestId("accept-cycle-reason"))
            .ToHaveValueAsync("Accepted anyway; suggested fix: Move it.");

        // A second pasted answer joins the first, as with Claude's answers, each named by its source.
        await Expect(copy).ToHaveTextAsync("Use your AI chat");
        await copy.ClickAsync();
        await answerBox.FillAsync("Verdict: Accept\nReason to store: One concept.");
        await Expect(Page.GetByTestId("accept-cycle-answer")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("accept-cycle-answer").Last).ToHaveTextAsync("Chat 2 · Accept");
        await Expect(Page.GetByTestId("accept-cycle-answers-differ")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("accept-cycle-reason")).ToHaveValueAsync("One concept.");
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
