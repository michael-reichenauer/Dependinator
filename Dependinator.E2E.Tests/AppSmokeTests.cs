using Microsoft.Playwright;

namespace Dependinator.E2E.Tests;

public class AppSmokeTests : E2ETestBase
{
    [E2EFact]
    public async Task HomePage_ShouldShowDiagramCanvas()
    {
        await Page.GotoAsync("/");

        await Expect(Page).ToHaveTitleAsync("Dependinator");
        await Expect(Page.Locator("#svgcanvas")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HomePage_ShouldShowAppToolbar()
    {
        await Page.GotoAsync("/");

        // Toolbar buttons and menu items carry stable data-testid hooks (AppBar.razor).
        await Expect(Page.GetByTestId("appbar-menu")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("toolbar-search")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task AppMenu_ShouldOpenSearchDialog_ViaMenuItem()
    {
        await Page.GotoAsync("/");

        await Page.GetByTestId("appbar-menu").ClickAsync();
        await Page.GetByTestId("menu-search").ClickAsync();

        await Expect(Page.GetByPlaceholder("Search nodes…")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task SearchHotkey_ShouldOpenSearchDialog()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator("#svgcanvas")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Control+f");

        ILocator searchField = Page.GetByPlaceholder("Search nodes…");
        await Expect(searchField).ToBeVisibleAsync();

        // A query that matches no nodes shows the empty-result message regardless
        // of which model happens to be loaded.
        await searchField.FillAsync("zzz-no-such-node");
        await Expect(Page.Locator(".search-dialog__empty")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(searchField).ToBeHiddenAsync();
    }
}
