using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises "About Node" through the browser: open it from the node menu and the right-click
// menu, see what the model knows of the node, and take one of its example questions, or the
// user's own, to the Ask panel, which asks it about the node (in test mode the app answers).
public class AboutNodeTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task AboutNode_ShouldShowTheNode_AndAnswerAQuestionAboutIt()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-about");
        await Expect(Page.GetByTestId("about-node-name")).ToContainTextAsync("Demo.sln");
        await Expect(Page.GetByTestId("about-node-facts")).ToContainTextAsync("used by");

        // An example question closes the dialog for the Ask panel, which asks it about the node.
        await Expect(Page.GetByTestId("about-node-example").First).ToHaveTextAsync("What is this and why is it here?");
        await Page.GetByTestId("about-node-example").First.ClickAsync();
        await Expect(Page.GetByTestId("about-node-name")).ToBeHiddenAsync();
        await Expect(Page.GetByTestId("ask-panel")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("ask-context-node")).ToContainTextAsync("Demo.sln");
        await Expect(Page.GetByTestId("ask-question")).ToHaveValueAsync("What is this and why is it here?");
        await Page.GetByTestId("ask-send").ClickAsync();
        await Expect(Page.GetByTestId("ask-entry-about")).ToHaveTextAsync("About Demo.sln");
        await Expect(Page.GetByTestId("ask-answer-text")).ToContainTextAsync("About Demo.sln with everything inside.");

        // "Ask another question" opens the panel for the user's own question.
        await App.ClickNodeMenuItemAsync("node-menu-about");
        await Page.GetByTestId("about-node-ask-other").ClickAsync();
        await Expect(Page.GetByTestId("about-node-name")).ToBeHiddenAsync();
        await Expect(Page.GetByTestId("ask-panel")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("ask-question")).ToHaveValueAsync("");
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
