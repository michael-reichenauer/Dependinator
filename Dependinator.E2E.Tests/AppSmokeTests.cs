using Dependinator.E2E.Tests.Pages;

namespace Dependinator.E2E.Tests;

public class AppSmokeTests : E2ETestBase
{
    [E2EFact]
    public async Task HomePage_ShouldShowDiagramCanvas()
    {
        await App.GotoAsync();

        await Expect(Page).ToHaveTitleAsync("Dependinator");
        await Expect(App.Canvas).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HomePage_ShouldShowAppToolbar()
    {
        await App.GotoAsync();

        await Expect(App.Menu).ToBeVisibleAsync();
        await Expect(App.SearchButton).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task AppMenu_ShouldOpenSearchDialog_ViaMenuItem()
    {
        await App.GotoAsync();

        SearchDialog search = await App.OpenSearchViaMenuAsync();

        await Expect(search.Field).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HomePage_ShouldLoadDemoModel_InTestMode()
    {
        await App.GotoAsync();

        // In test mode the app loads the embedded demo model instead of parsing the
        // working solution; its root node label "Demo.sln" renders on the canvas.
        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task SearchHotkey_ShouldOpenSearchDialog()
    {
        await App.GotoAsync();

        SearchDialog search = await App.OpenSearchViaHotkeyAsync();
        await Expect(search.Field).ToBeVisibleAsync();

        // A query that matches no nodes shows the empty-result message regardless
        // of which model happens to be loaded.
        await search.FillAsync("zzz-no-such-node");
        await Expect(search.EmptyResult).ToBeVisibleAsync();

        await search.CloseAsync();
        await Expect(search.Field).ToBeHiddenAsync();
    }
}
