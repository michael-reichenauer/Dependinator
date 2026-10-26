using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit;
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

    // Help is the last app bar button, after the problem indicator (the demo model has cycles),
    // so it is easy to find (#115).
    [E2EFact]
    public async Task HelpButton_ShouldBeLastInTheAppBar()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        ILocator help = App.MenuItem("toolbar-help");
        ILocator problems = App.MenuItem("problems-indicator");
        await Expect(help).ToBeVisibleAsync();
        await Expect(problems).ToBeVisibleAsync();

        LocatorBoundingBoxResult helpBox = (await help.BoundingBoxAsync())!;
        LocatorBoundingBoxResult problemsBox = (await problems.BoundingBoxAsync())!;
        Assert.True(helpBox.X > problemsBox.X, $"Help at x={helpBox.X}, problems at x={problemsBox.X}");
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
    public async Task AppMenu_ShouldOpenAboutDialog()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-help-group", "menu-about")).ClickAsync();

        // The About message box shows the build version.
        await Expect(App.Dialog).ToBeVisibleAsync();
        await Expect(App.Dialog).ToContainTextAsync("Version:");
    }

    [E2EFact]
    public async Task AppMenu_ShouldOpenHelpPageInNewTab()
    {
        await App.GotoMainPageAsync();

        ILocator helpItem = await App.OpenSubMenuItemAsync("menu-help-group", "menu-help");

        // Help opens the static quick-start page in a new browser tab.
        IPage helpPage = await Page.RunAndWaitForPopupAsync(async () => await helpItem.ClickAsync());

        await Expect(helpPage).ToHaveURLAsync(new Regex("_content/Dependinator\\.UI/help\\.html"));
        await Expect(helpPage).ToHaveTitleAsync(new Regex("Quick Start"));
    }

    [E2EFact]
    public async Task AppMenu_ShouldOpenPrefilledBugReport()
    {
        // Stub GitHub, so the test needs no network and no sign-in redirect changes the url.
        await Context.RouteAsync(
            "https://github.com/**",
            route => route.FulfillAsync(new() { Body = "<title>New issue</title>", ContentType = "text/html" })
        );
        await App.GotoMainPageAsync();

        ILocator bugItem = await App.OpenSubMenuItemAsync("menu-help-group", "menu-report-bug");

        IPage issuePage = await Page.RunAndWaitForPopupAsync(async () => await bugItem.ClickAsync());

        Uri url = new(issuePage.Url);
        Assert.Equal("/michael-reichenauer/Dependinator/issues/new", url.AbsolutePath);
        string query = Uri.UnescapeDataString(url.Query);
        Assert.Contains("template=bug_report.yml", query);
        Assert.Matches(@"version=\d+\.\d+", query);
        Assert.Contains("Build: ", query);
        Assert.Contains("Host: Web app", query);
        Assert.Contains("Browser: ", query);
    }

    [E2EFact]
    public async Task AppMenu_ShouldConfirmBeforeDeletingModel()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-models", "menu-delete-model")).ClickAsync();

        // Delete is destructive, so it must ask for confirmation first.
        await Expect(App.Dialog).ToBeVisibleAsync();
        await Expect(App.Dialog).ToContainTextAsync("delete the current model");

        // Cancel so the loaded demo model is left intact.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(App.Dialog).ToBeHiddenAsync();
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
