using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises View › Show Cycles: the panel opens with the cycles found in the model (or says
// there are none) and closes again from its button.
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
}
