using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises View › Show Cycles: the panel opens with the cycles found in the model (or says
// there are none) and closes again from its button; a cycle accepted with a reason moves to the
// Accepted section, its reason can be edited, and removing the acceptance brings it back.
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

        await cycles.First.GetByTestId("cycles-accept").ClickAsync();
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
