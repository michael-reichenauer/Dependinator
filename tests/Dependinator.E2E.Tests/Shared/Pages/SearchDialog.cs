using Microsoft.Playwright;

namespace Dependinator.E2E.Tests.Shared.Pages;

// Page object for the node search dialog (Dependinator.UI/Diagrams/SearchDialog.razor).
// Opened via AppPage.OpenSearchViaMenuAsync / OpenSearchViaHotkeyAsync.
public sealed class SearchDialog
{
    private readonly AppPage app;
    private readonly IPage page;

    public SearchDialog(AppPage app, IPage page)
    {
        this.app = app;
        this.page = page;
    }

    public ILocator Field => page.GetByPlaceholder("Search nodes…");
    public ILocator Results => page.Locator(".search-dialog__item");
    public ILocator SelectedItem => page.Locator(".search-dialog__item--selected");
    public ILocator EmptyResult => page.Locator(".search-dialog__empty");

    // The search field is a server-bound MudTextField, so a Blazor render landing just
    // after the fill can echo a stale value back and wipe the text (see
    // AppPage.FillReliablyAsync) — fill via the retrying helper.
    public Task FillAsync(string query) => app.FillReliablyAsync(Field, query);

    // A result row by its (short) node name.
    public ILocator Result(string name) => Results.Filter(new() { HasTextString = name });

    // Dismiss the dialog with Escape. A keystroke landing while the dialog is still animating in
    // is swallowed, so Escape is pressed until the dialog is really gone.
    public async Task CloseAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await page.Keyboard.PressAsync("Escape");
            try
            {
                await Assertions.Expect(Field).ToBeHiddenAsync(new() { Timeout = 1500 });
                return;
            }
            catch (PlaywrightException)
            {
                // Swallowed: press again
            }
        }
        await Assertions.Expect(Field).ToBeHiddenAsync();
    }
}
