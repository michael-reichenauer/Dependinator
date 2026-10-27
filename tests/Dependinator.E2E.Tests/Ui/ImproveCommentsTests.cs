using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises "Improve Comments" through the browser: the scope (the node only, or everything inside
// it) with its count, the prompt to copy where Claude Code is off, and a run with Claude Code (in
// test mode the app reports a changed file without running it) that ends with Done.
public class ImproveCommentsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ImproveComments_ShouldCountTheScope_AndCopyThePrompt()
    {
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-improve-comments");
        ILocator count = Page.GetByTestId("improve-comments-count");
        await Expect(count).ToContainTextAsync("namespaces and types in");
        await Expect(Page.GetByTestId("improve-comments-many")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("improve-comments-run")).ToHaveCountAsync(0);

        // The solution itself has no comment the parser reads.
        await Page.GetByTestId("improve-comments-only").ClickAsync();
        await Expect(count).ToContainTextAsync("Nothing here");
        await Expect(Page.GetByTestId("improve-comments-copy")).ToBeDisabledAsync();

        await Page.GetByTestId("improve-comments-below").ClickAsync();
        await Page.GetByTestId("improve-comments-copy").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-copied")).ToContainTextAsync("The prompt is copied");
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.StartsWith("Please add or improve the code comments", prompt);
    }

    [E2EFact]
    public async Task ImproveComments_ShouldRunClaudeCode_FromTheAboutDialog()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await (await App.OpenSubMenuItemAsync("menu-settings", "menu-use-claude-code")).ClickAsync();
        await Page.GetByTestId("use-claude-code-confirm").ClickAsync();

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-about");
        await Page.GetByTestId("about-node-improve-comments").ClickAsync();

        ILocator run = Page.GetByTestId("improve-comments-run");
        await run.ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-fixing")).ToContainTextAsync("improving the comments");
        await Expect(Page.GetByTestId("improve-comments-fix-report")).ToContainTextAsync("Claude Code changed 1 file");
        await Expect(Page.GetByTestId("improve-comments-fix-files")).ToHaveTextAsync("src/Test.cs");
        await Page.GetByTestId("improve-comments-done").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-count")).ToBeHiddenAsync();
    }
}
