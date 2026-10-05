using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the light/dark theme: View › Theme sets it, the page marks the choice on the root
// element (data-theme, set by applyTheme in jsInterop.js), and the choice is stored in the
// config so it survives a reload. The SVG palette itself is process-wide in this host, so only
// the per-page signal is asserted here.
public class ThemeTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Theme_ShouldSwitchToDark_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        ILocator html = Page.Locator("html");

        // Headless browsers report a light system scheme, so System (the default) is light.
        await Expect(html).ToHaveAttributeAsync("data-theme", "light");

        // The dark palette is process-wide in this host, so it is switched back whatever happens
        // here; a test left dark would change what every later test renders.
        try
        {
            await SetThemeAsync("menu-theme-dark");
            await Expect(html).ToHaveAttributeAsync("data-theme", "dark");

            await App.GotoMainPageAsync();
            await Expect(html).ToHaveAttributeAsync("data-theme", "dark");
        }
        finally
        {
            await SetThemeAsync("menu-theme-light");
        }
        await Expect(html).ToHaveAttributeAsync("data-theme", "light");
    }

    // Click the theme item and verify it really took: a click landing while the popover
    // re-renders is swallowed silently, so the state is read back before moving on.
    async Task SetThemeAsync(string itemTestId)
    {
        await (await App.OpenSubMenuItemAsync("menu-view", itemTestId)).ClickAsync();
        await Expect(await App.OpenSubMenuItemAsync("menu-view", itemTestId))
            .ToHaveAttributeAsync("data-checked", "true");
        await App.CloseMenuAsync();
    }
}
