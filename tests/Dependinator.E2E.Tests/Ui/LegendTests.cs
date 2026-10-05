using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the legend card: View › Show Legend opens it, its close button hides it again.
public class LegendTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Legend_ShouldToggleFromViewMenu_AndClose()
    {
        await App.GotoMainPageAsync();

        ILocator legend = Page.GetByTestId("legend");
        await Expect(legend).ToHaveCountAsync(0);

        await (await App.OpenSubMenuItemAsync("menu-view", "menu-legend")).ClickAsync();
        await Expect(legend).ToBeVisibleAsync();
        await Expect(legend).ToContainTextAsync("Inherits / implements");
        await Expect(legend).ToContainTextAsync("Interface");

        await Page.GetByTestId("legend-close").ClickAsync();
        await Expect(legend).ToHaveCountAsync(0);
    }
}
