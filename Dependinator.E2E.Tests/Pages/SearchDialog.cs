using Microsoft.Playwright;

namespace Dependinator.E2E.Tests.Pages;

// Page object for the node search dialog (Dependinator.UI/Diagrams/SearchDialog.razor).
// Opened via AppPage.OpenSearchViaMenuAsync / OpenSearchViaHotkeyAsync.
public sealed class SearchDialog
{
    private readonly IPage page;

    public SearchDialog(IPage page) => this.page = page;

    public ILocator Field => page.GetByPlaceholder("Search nodes…");
    public ILocator Results => page.Locator(".search-dialog__item");
    public ILocator EmptyResult => page.Locator(".search-dialog__empty");

    public Task FillAsync(string query) => Field.FillAsync(query);

    // A result row by its (short) node name.
    public ILocator Result(string name) => Results.Filter(new() { HasTextString = name });

    // Dismiss the dialog with Escape.
    public Task CloseAsync() => page.Keyboard.PressAsync("Escape");
}
