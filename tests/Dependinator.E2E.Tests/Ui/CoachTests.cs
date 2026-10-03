using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the first-run tour (CoachCallout): Help › Show Tips opens it (test mode never
// starts it by itself), Next walks the steps, doing the thing a step asks for moves it on, and
// Done closes it.
public class CoachTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ShowTips_ShouldWalkTheTour_AndAdvanceOnTheUsersActions()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator coach = Page.GetByTestId("coach");
        await Expect(coach).ToHaveCountAsync(0);

        await (await App.OpenSubMenuItemAsync("menu-help-group", "menu-tips")).ClickAsync();
        await Expect(coach).ToBeVisibleAsync();
        await Expect(coach).ToHaveAttributeAsync("data-step", "welcome");
        await Expect(coach).ToContainTextAsync("Welcome to Dependinator");

        await Page.GetByTestId("coach-next").ClickAsync();
        await Expect(coach).ToHaveAttributeAsync("data-step", "zoom");
        await Expect(Page.GetByTestId("coach-count")).ToContainTextAsync("1 of 4");

        await Page.GetByTestId("coach-next").ClickAsync();
        await Expect(coach).ToHaveAttributeAsync("data-step", "select");

        // Doing what the step asks moves the tour on: selecting a node, opening the explorer,
        // opening the search.
        await App.SelectNodeByFullNameAsync("Demo.sln");
        await Expect(coach).ToHaveAttributeAsync("data-step", "explore");

        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        await Expect(coach).ToHaveAttributeAsync("data-step", "search");

        var search = await App.OpenSearchViaHotkeyAsync();
        await Expect(coach).ToHaveAttributeAsync("data-step", "done");
        await search.CloseAsync();

        await Page.GetByTestId("coach-next").ClickAsync();
        await Expect(coach).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task Skip_ShouldCloseTheTour()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await (await App.OpenSubMenuItemAsync("menu-help-group", "menu-tips")).ClickAsync();
        ILocator coach = Page.GetByTestId("coach");
        await Expect(coach).ToBeVisibleAsync();

        await Page.GetByTestId("coach-skip").ClickAsync();
        await Expect(coach).ToHaveCountAsync(0);
    }
}
