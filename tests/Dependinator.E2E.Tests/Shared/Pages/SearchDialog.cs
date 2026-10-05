using System.Text.RegularExpressions;
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

    // The result row of exactly this node (its full name, e.g. "Demo.UI.Main"). Result(name) matches
    // by substring, so with members in the list "Main" also matches "Demo.UI.Main.OnInitialized()"
    // (and the fuzzy ranking may list such a member first).
    public ILocator ResultByFullName(string fullName) =>
        Results.Filter(
            new()
            {
                Has = page.Locator(
                    ".search-dialog__full",
                    new() { HasTextRegex = new Regex($"^{Regex.Escape(fullName)}$") }
                ),
            }
        );

    // Navigate to a node by its exact full name. An exact full-name query puts that node at the
    // top of the fuzzy ranking, which is asserted before Enter so a ranking change fails here
    // with a clear message rather than later as "node X did not render". Enter is pressed on
    // the field itself: the dialog's key handler is bound to the field, and a globally-pressed
    // Enter is lost if the field momentarily lost focus (a CI flake showed Enter changing
    // nothing, leaving the dialog open).
    public async Task NavigateToAsync(string fullName)
    {
        await FillAsync(fullName);
        await Assertions.Expect(SelectedItem.Locator(".search-dialog__full")).ToHaveTextAsync(fullName);
        await Field.PressAsync("Enter");
    }

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
