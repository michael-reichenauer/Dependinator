using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Ask about the model (in test mode the app answers for Claude and Claude Code, after two lookups
// through the model queries): the panel opens from the toolbar beside the diagram, closes the other
// analysis panels, answers a question with node names that go to their node, leaves a name that is
// not a node as plain code, shows what the answer cost and the lookups it made, asks on the route
// picked below the question, and keeps the answers while it is closed.
public class AskModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Ask_ShouldAnswerWithLinksToTheNodes()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
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
        await Page.GetByTestId("ask-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("main parts");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        await Page.GetByTestId("ask-question").FillAsync("Where is the start?");
        await Page.GetByTestId("ask-question").PressAsync("Enter");
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

        // Asked on the Claude Code route, the answer has no cost.
        await Page.GetByTestId("ask-route-code").ClickAsync();
        await Page.GetByTestId("ask-question").FillAsync("What uses the parser?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-answer")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("ask-answer-meta").Nth(1))
            .ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Claude Code\\s*2 lookups$"));

        await Page.GetByTestId("ask-clear").ClickAsync();
        await Expect(Page.GetByTestId("ask-entry")).ToHaveCountAsync(0);
    }
}
