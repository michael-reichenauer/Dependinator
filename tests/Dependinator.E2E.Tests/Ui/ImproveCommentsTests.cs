using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises "Improve Comments" through the browser: the scope (the node only, or everything inside
// it) with its count, a project's description, the prompt to copy where Claude Code is off, and a
// run with Claude Code (in test mode the app reports a changed file without running it) that ends
// with Done, its provider menu offering the prompt to copy and Claude, which cannot change code,
// greyed.
public class ImproveCommentsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ImproveComments_ShouldCountTheScope_AndCopyThePrompt()
    {
        await StubClipboardAsync();
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

    // A project's description is its AssemblyDescription attribute, which counts as a comment.
    [E2EFact]
    public async Task ImproveComments_ShouldOfferAProjectsDescription()
    {
        await StubClipboardAsync();
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.Claude (dll)");
        await search.Result("Demo.Claude (dll)").First.ClickAsync();
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
        await App.ClickNodeMenuItemAsync("node-menu-improve-comments");

        await Page.GetByTestId("improve-comments-only").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-count")).ToHaveTextAsync("1 project in 1 file");
        await Page.GetByTestId("improve-comments-copy").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-copied")).ToContainTextAsync("The prompt is copied");
        string prompt = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("AssemblyDescription", prompt);
        Assert.Contains("Description now: \"Demo.Claude asks Claude", prompt);

        await Page.GetByTestId("improve-comments-below").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-count")).ToContainTextAsync("1 project, ");
    }

    [E2EFact]
    public async Task ImproveComments_ShouldRunClaudeCode_FromTheAboutDialog()
    {
        await StubClipboardAsync();
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.SetAiSettingsAsync(useClaudeCode: true);

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await App.ClickNodeMenuItemAsync("node-menu-about");
        await Page.GetByTestId("about-node-improve-comments").ClickAsync();

        await Page.GetByTestId("improve-comments-menu").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-with-claude")).ToHaveClassAsync(new Regex("mud-disabled"));
        await Page.GetByTestId("improve-comments-copy").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-copied")).ToContainTextAsync("The prompt is copied");

        ILocator run = Page.GetByTestId("improve-comments-run");
        await run.ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-fixing")).ToContainTextAsync("improving the comments");
        await Expect(Page.GetByTestId("improve-comments-fix-report")).ToContainTextAsync("Claude Code changed 1 file");
        await Expect(Page.GetByTestId("improve-comments-fix-files")).ToHaveTextAsync("src/Test.cs");
        await Page.GetByTestId("improve-comments-done").ClickAsync();
        await Expect(Page.GetByTestId("improve-comments-count")).ToBeHiddenAsync();
    }

    Task StubClipboardAsync() =>
        Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
}
