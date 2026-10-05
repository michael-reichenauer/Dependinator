using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises share links: opening the app with ?m=demo&n=<node> shows that node, and "Copy Link
// to Node" puts such a link on the clipboard.
public class ShareLinkTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task OpeningANodeLink_ShouldShowThatNode()
    {
        await App.GotoAsync("/?m=demo&n=" + Uri.EscapeDataString("Demo*UI*dll.Demo.UI.Main"));

        // The link navigates to Main (not shown at the initial root view) and selects it, so its
        // label renders and its toolbar shows.
        await Expect(App.NodeLabel("Main")).ToBeVisibleAsync();
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Filter(new() { HasTextString = "Main" })).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task CopyLinkToNode_ShouldPutTheLinkOnTheClipboard()
    {
        // Playwright can grant clipboard permissions on Chromium only (Firefox and WebKit throw
        // "Unknown permission"), so the clipboard is stubbed and the written text is read back.
        await Page.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {"
                + " writeText: text => { window.__copiedText = text; return Promise.resolve(); } } });"
        );
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.SelectNodeByFullNameAsync("Demo.sln");
        await (await App.OpenNodeMenuItemAsync("node-menu-copy-link")).ClickAsync();
        await Expect(Page.GetByText("Link copied")).ToBeVisibleAsync();

        string link = await Page.EvaluateAsync<string>("() => window.__copiedText");
        Assert.Contains("?m=demo&n=", link);
        Assert.Equal("Demo*sln", Uri.UnescapeDataString(link.Split("&n=")[1]));
    }
}
