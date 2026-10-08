using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises View › Lines: hiding lines to Externals removes the demo's only top-level line
// (Demo.sln → Externals); turning the filter off brings it back. The filter is stored in the
// config so it survives a reload.
public class LineFilterTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task HideExternalLines_ShouldRemoveTheLineToExternals_AndPersist()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator toExternals = App.LineTitle("Demo.sln→Externals");
        await Expect(toExternals).ToHaveCountAsync(1);

        await ToggleExternalAsync(expectOn: true);
        await Expect(toExternals).ToHaveCountAsync(0);

        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await Expect(toExternals).ToHaveCountAsync(0);

        await ToggleExternalAsync(expectOn: false);
        await Expect(toExternals).ToHaveCountAsync(1);
    }

    // The Lines submenu is nested under View: open View, hover Lines, then the item.
    Task<ILocator> OpenExternalItemAsync() =>
        App.OpenSubMenuItemAsync("menu-view", "menu-lines", "menu-lines-external");

    async Task ToggleExternalAsync(bool expectOn)
    {
        await (await OpenExternalItemAsync()).ClickAsync();
        await Expect(await OpenExternalItemAsync()).ToHaveAttributeAsync("data-checked", expectOn ? "true" : "false");
        await App.CloseMenuAsync();
    }
}
