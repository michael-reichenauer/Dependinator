using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

public class AppSmokeTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task HomePage_ShouldShowDiagramCanvas()
    {
        await App.GotoMainPageAsync();

        await Expect(Page).ToHaveTitleAsync("Dependinator");
        await Expect(App.Canvas).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HomePage_ShouldShowAppToolbar()
    {
        await App.GotoMainPageAsync();

        await Expect(App.Menu).ToBeVisibleAsync();
        await Expect(App.SearchButton).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task AppMenu_ShouldOpenSearchDialog_ViaMenuItem()
    {
        await App.GotoMainPageAsync();

        SearchDialog search = await App.OpenSearchViaMenuAsync();

        await Expect(search.Field).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task HomePage_ShouldLoadDemoModel_InTestMode()
    {
        await App.GotoMainPageAsync();

        // In test mode the app loads the embedded demo model instead of parsing the
        // working solution; its root node label "Demo.sln" renders on the canvas.
        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();
    }

    // Representative *interaction* test (vs. the smoke tests above): drive the diagram
    // itself and assert the app reacts. Copy this shape for more behavior coverage —
    // act through the App/SearchDialog page objects, then assert with Expect.
    [E2EFact]
    public async Task ClickingNode_ShouldShowItsContextToolbar()
    {
        await App.GotoMainPageAsync();

        // Select the demo model's root node ("Demo.sln") on the diagram.
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Selecting a node shows its context toolbar (NodeToolbar.razor, node-* hooks).
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task SearchHotkey_ShouldOpenSearchDialog()
    {
        await App.GotoMainPageAsync();

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
