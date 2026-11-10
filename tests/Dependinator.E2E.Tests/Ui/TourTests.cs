using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises Help › Tour This Codebase through the browser: the panel shows the prompt, gets a
// tour (in test mode the app answers for Claude, with the first top-level nodes and one that is
// not in the model, which is left out), and Start tour plays it stop by stop over the diagram.
public class TourTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Tour_ShouldPlanAndPlayATour_StopByStop()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await (await App.OpenSubMenuItemAsync("menu-help-group", "menu-tour")).ClickAsync();
        ILocator panel = Page.GetByTestId("tour-panel");
        await Expect(panel).ToBeVisibleAsync();

        await Expect(await App.ShowPromptAsync("tour")).ToContainTextAsync("A guided tour of a codebase");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        await Page.GetByTestId("tour-suggest").ClickAsync();
        await Expect(Page.GetByTestId("tour-answer")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("tour-left-out")).ToContainTextAsync("1 stop");
        ILocator stops = Page.GetByTestId("tour-stop");
        int count = await stops.CountAsync();
        Assert.True(count >= 2, $"Expected at least two stops, got {count}.");

        // TextContent, since Firefox's InnerText breaks the name at a wrapped <wbr>.
        var first = await stops.Nth(0).GetByTestId("tour-stop-node").TextContentAsync() ?? "";
        var second = await stops.Nth(1).GetByTestId("tour-stop-node").TextContentAsync() ?? "";

        await Page.GetByTestId("tour-start").ClickAsync();
        ILocator callout = Page.GetByTestId("tour-callout");
        await Expect(callout).ToBeVisibleAsync();
        await Expect(panel).ToBeHiddenAsync();
        await Expect(callout).ToHaveAttributeAsync("data-stop", "1");
        await Expect(Page.GetByTestId("tour-callout-count")).ToHaveTextAsync($"1 of {count}");
        await Expect(Page.GetByTestId("tour-callout-intro")).ToContainTextAsync("Test mode");
        await Expect(Page.GetByTestId("tour-back")).ToBeDisabledAsync();
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync(first.Split('.')[^1]);

        await Page.GetByTestId("tour-next").ClickAsync();
        await Expect(callout).ToHaveAttributeAsync("data-stop", "2");
        await Expect(Page.GetByTestId("tour-callout-node")).ToHaveTextAsync(second);
        await Expect(Page.GetByTestId("breadcrumb")).ToContainTextAsync(second.Split('.')[^1]);

        await Page.GetByTestId("tour-back").ClickAsync();
        await Expect(callout).ToHaveAttributeAsync("data-stop", "1");

        // Ending shows the panel again, with the tour still there to replay from any stop.
        await Page.GetByTestId("tour-end").ClickAsync();
        await Expect(callout).ToHaveCountAsync(0);
        await Expect(panel).ToBeVisibleAsync();
        await stops.Nth(count - 1).GetByTestId("tour-stop-start").ClickAsync();
        await Expect(callout).ToHaveAttributeAsync("data-stop", $"{count}");
        await Page.GetByTestId("tour-done").ClickAsync();
        await Expect(panel).ToBeVisibleAsync();

        await Page.GetByTestId("tour-close").ClickAsync();
        await Expect(panel).ToBeHiddenAsync();
    }
}
