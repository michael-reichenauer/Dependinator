using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises "About Node" through the browser: open it from the node menu and the right-click
// menu, see what the model knows of the node, show the prompt with a question of the user's own,
// and get an answer (in test mode the app answers for Claude) to copy.
public class AboutNodeTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task AboutNode_ShouldShowTheNode_AndAnswerAQuestionAboutIt()
    {
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-about");
        await Expect(Page.GetByTestId("about-node-name")).ToContainTextAsync("Demo.sln");
        await Expect(Page.GetByTestId("about-node-facts")).ToContainTextAsync("used by");

        ILocator question = Page.GetByTestId("about-node-question");
        await App.FillReliablyAsync(question, "Where does parsing start?");
        await Page.GetByTestId("about-node-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("Where does parsing start?");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        await Page.GetByTestId("about-node-suggest").ClickAsync();
        ILocator answer = Page.GetByTestId("about-node-answer");
        await Expect(answer).ToContainTextAsync("Where does parsing start?");
        await Page.GetByTestId("about-node-copy-answer").ClickAsync();
        await Expect(Page.GetByTestId("about-node-copied")).ToHaveTextAsync("Copied.");
        string copied = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Test mode", copied);

        await Page.GetByTestId("about-node-close").ClickAsync();
        await Expect(Page.GetByTestId("about-node-name")).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task AboutNode_ShouldOpen_FromTheRightClickMenu()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        LocatorBoundingBoxResult box =
            await App.Node("Externals").BoundingBoxAsync() ?? throw new InvalidOperationException("Node not rendered.");
        await Page.Mouse.ClickAsync(
            box.X + box.Width / 2,
            box.Y + box.Height / 2,
            new() { Button = MouseButton.Right }
        );
        await Page.GetByTestId("context-menu-about").ClickAsync();
        await Expect(Page.GetByTestId("about-node-name")).ToContainTextAsync("Externals");
    }
}
