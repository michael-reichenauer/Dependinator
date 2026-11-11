using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Ask about the model (in test mode the app answers for Claude and Claude Code, after two lookups
// through the model queries): the panel opens from the toolbar beside the diagram, closes the other
// analysis panels, answers a question with node names that go to their node, leaves a name that is
// not a node as plain code, shows what the answer cost and the lookups it made, asks the provider
// picked from the chip below the question, and keeps the answers while it is closed. With nodes
// selected the question is about them, and the examples fit what is asked about.
public class AskModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Ask_ShouldAnswerWithLinksToTheNodes()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.SetAiSettingsAsync(useClaudeCode: true);
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-rules")).ClickAsync();
        await Expect(Page.GetByTestId("rules-panel")).ToBeVisibleAsync();

        await Page.GetByTestId("toolbar-ask").ClickAsync();
        await Expect(Page.GetByTestId("ask-panel")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("rules-panel")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("ask-intro")).ToBeVisibleAsync();

        // An example fills in the question; the prompt holds it.
        await Page.GetByTestId("ask-example").First.ClickAsync();
        await Expect(Page.GetByTestId("ask-question"))
            .ToHaveValueAsync(new System.Text.RegularExpressions.Regex("main parts"));
        await Page.GetByTestId("ask-menu").ClickAsync();
        await Page.GetByTestId("ask-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("main parts");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        // Enter asks and empties the field, so a second Enter after a dropped one is safe.
        await Page.GetByTestId("ask-question").FillAsync("Where is the start?");
        await App.PressUntilVisibleAsync(
            Page.GetByTestId("ask-question"),
            "Enter",
            Page.GetByTestId("ask-entry-question")
        );
        await Expect(Page.GetByTestId("ask-entry-question")).ToHaveTextAsync("Where is the start?");
        await Expect(Page.GetByTestId("ask-answer-text")).ToContainTextAsync("Test mode");
        await Expect(Page.GetByTestId("ask-answer-node")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("ask-answer-text").Locator("code")).ToHaveTextAsync("No.Such.Node");

        // Claude with the API key is the first route: the answer says what it cost and lists the
        // lookups on demand.
        await Expect(Page.GetByTestId("ask-answer-meta")).ToContainTextAsync("Claude · 1.2¢");
        await Page.GetByTestId("ask-steps-toggle").ClickAsync();
        await Expect(Page.GetByTestId("ask-step")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("ask-step").Nth(1)).ToHaveTextAsync("Searched for \"Where is the start?\"");
        await Page.GetByTestId("ask-steps-toggle").ClickAsync();
        await Expect(Page.GetByTestId("ask-step")).ToHaveCountAsync(0);

        // A node name in the answer goes to its node, with the panel still open beside the diagram.
        ILocator link = Page.GetByTestId("ask-node-link").First;
        var name = await link.InnerTextAsync();
        await link.ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync(name);
        await Expect(Page.GetByTestId("ask-panel")).ToBeVisibleAsync();

        // Closed and opened again, the answer is still there.
        await Page.GetByTestId("ask-close").ClickAsync();
        await Expect(Page.GetByTestId("ask-panel")).ToBeHiddenAsync();
        await Page.GetByTestId("toolbar-ask").ClickAsync();
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(1);

        // Asked on the Claude Code route, the answer has no cost; the question follows up on the
        // first one.
        ILocator chip = Page.GetByTestId("ask-menu");
        await Expect(chip).ToHaveAttributeAsync("data-provider", "claude");
        await chip.ClickAsync();
        await Page.GetByTestId("ask-with-code").ClickAsync();
        await Expect(chip).ToHaveAttributeAsync("data-provider", "code");
        await Page.GetByTestId("ask-question").FillAsync("What uses the parser?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("ask-answer-text").Nth(1)).ToContainTextAsync("A follow-up, after 1 answered.");
        await Expect(Page.GetByTestId("ask-answer-text").First).Not.ToContainTextAsync("follow-up");
        await Expect(Page.GetByTestId("ask-answer-meta").Nth(1))
            .ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Claude Code\\s*2 lookups$"));

        await Page.GetByTestId("ask-clear").ClickAsync();
        await Expect(Page.GetByTestId("ask-entry")).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Ask_ShouldBeAboutTheSelectedNodes_WithExamplesThatFit()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Nothing selected: the question is about the model. "More examples" shows the rest.
        await Page.GetByTestId("toolbar-ask").ClickAsync();
        await Expect(Page.GetByTestId("ask-context")).ToHaveCountAsync(0);
        int count = await Page.GetByTestId("ask-example").CountAsync();
        await Page.GetByTestId("ask-examples-more").ClickAsync();
        await Expect(Page.GetByTestId("ask-example")).Not.ToHaveCountAsync(count);
        await Page.GetByTestId("ask-close").ClickAsync();

        // A selected node is what the question is about, from its menu; the examples fit it.
        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-ask");
        await Expect(Page.GetByTestId("ask-panel")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("ask-context-node"))
            .ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Demo.sln"));
        await Expect(Page.GetByTestId("ask-example").First).ToHaveTextAsync("What is this and why is it here?");
        await Page.GetByTestId("ask-example").First.ClickAsync();
        await Page.GetByTestId("ask-menu").ClickAsync();
        await Page.GetByTestId("ask-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text"))
            .ToContainTextAsync("asks about it, with everything inside it");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        // Without what is inside, the answer is about the node alone.
        await Page.GetByTestId("ask-context-below").UncheckAsync();
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-entry-about")).ToHaveTextAsync("About Demo.sln");
        await Expect(Page.GetByTestId("ask-answer-text")).ToContainTextAsync("About Demo.sln.");

        // The lightbulb shows the examples again, for a follow-up.
        await Expect(Page.GetByTestId("ask-example")).ToHaveCountAsync(0);
        await Page.GetByTestId("ask-examples-toggle").ClickAsync();
        await Expect(Page.GetByTestId("ask-example").First).ToBeVisibleAsync();
        await Page.GetByTestId("ask-examples-toggle").ClickAsync();
        await Expect(Page.GetByTestId("ask-example")).ToHaveCountAsync(0);

        // Removing the chip asks about the whole model again.
        await Page.GetByTestId("ask-context-remove").ClickAsync();
        await Expect(Page.GetByTestId("ask-context")).ToHaveCountAsync(0);
        await Page.GetByTestId("ask-question").FillAsync("And the model?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("ask-entry-about")).ToHaveCountAsync(1);
    }

    [E2EFact]
    public async Task Ask_ShouldCopyThePromptForAnotherAi_ThatUsesTheMcpServer()
    {
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await Page.GetByTestId("toolbar-ask").ClickAsync();

        await Page.GetByTestId("ask-question").FillAsync("Where is the start?");
        await Page.GetByTestId("ask-menu").ClickAsync();
        // Copilot Chat is offered in VS Code only.
        await Expect(Page.GetByTestId("ask-in-copilot-chat")).ToHaveCountAsync(0);
        await Page.GetByTestId("ask-copy-prompt").ClickAsync();

        await Expect(Page.GetByTestId("ask-other-note")).ToContainTextAsync("The prompt is copied");
        string copied = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("Where is the start?", copied);
        Assert.Contains("MCP server named dependinator", copied);
        // The question stays, to ask it in the panel too; asking clears the note.
        await Expect(Page.GetByTestId("ask-question")).ToHaveValueAsync("Where is the start?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-other-note")).ToHaveCountAsync(0);

        // With no question there is nothing to copy.
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(1);
        await Page.GetByTestId("ask-menu").ClickAsync();
        await Expect(Page.GetByTestId("ask-copy-prompt")).ToBeDisabledAsync();
    }

    [E2EFact]
    public async Task Ask_ShouldOfferToMakeTheChangeAnAnswerProposes()
    {
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.SetAiSettingsAsync(useClaudeCode: true, defaultProvider: "code");
        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-ask");

        // An answer that only explains offers no change.
        await Page.GetByTestId("ask-question").FillAsync("What is this?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId("ask-change")).ToHaveCountAsync(0);

        // One that proposes a change (in test mode: a question about a split) offers to make it.
        await Page.GetByTestId("ask-question").FillAsync("Should it be split?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-change-text")).ToHaveTextAsync("Test mode: split it.");
        ILocator doIt = Page.GetByTestId("ask-fix-claude-code");
        await Expect(doIt).ToHaveTextAsync("Do it with Claude Code");

        // The prompt for another AI holds the change, the question and what it was about.
        await Page.GetByTestId("ask-fix-menu").ClickAsync();
        await Page.GetByTestId("ask-copy-fix").ClickAsync();
        await Expect(Page.GetByTestId("ask-change-note")).ToContainTextAsync("The prompt is copied");
        string copied = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("Test mode: split it.", copied);
        Assert.Contains("Should it be split?", copied);
        Assert.Contains("Demo.sln", copied);

        // Claude Code makes the change and reports the files it changed.
        await doIt.ClickAsync();
        await Expect(Page.GetByTestId("ask-fix-report")).ToContainTextAsync("Claude Code changed 1 file");
        await Expect(Page.GetByTestId("ask-fix-files")).ToHaveTextAsync("src/Test.cs");

        // A node name in Claude Code's summary is a link to its node, as in an answer.
        ILocator link = Page.GetByTestId("ask-fix-summary").GetByTestId("ask-node-link");
        await Expect(link).ToHaveCountAsync(1);
        var name = await link.InnerTextAsync();
        await link.ClickAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync(name);
        await Expect(doIt).ToBeEnabledAsync();
    }
}
