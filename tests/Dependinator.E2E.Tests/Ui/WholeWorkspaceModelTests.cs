using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The model of the whole workspace: in a section of its own at the top of the Models menu in a
// host with a workspace (this test host's workspace is the repository). Opened for the first time it asks which of the
// sources found in the workspace it is made of, and shows the chosen ones in one diagram, each
// parsed by its own parser; the choice can be changed later (Models › Choose Sources …).
public class WholeWorkspaceModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task WorkspaceModel_ShouldShowTheChosenSourcesInOneDiagram()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        ILocator workspaceModel = App.MenuItem("menu-workspace-model");
        await Expect(workspaceModel).ToHaveCountAsync(1);
        // Apart from the models of its single sources
        await Expect(Page.GetByTestId("menu-models-header")).ToHaveTextAsync(["Whole workspace", "Single sources"]);
        // Not offered for another model than the workspace's
        await Expect(App.MenuItem("menu-choose-sources")).ToHaveCountAsync(0);
        await App.ClickUntilVisibleAsync(workspaceModel, Page.GetByTestId("sources-dialog"));

        // The proposal: the solution and the projects under src, not the test samples or examples
        await Expect(Source("Dependinator.sln")).ToHaveAttributeAsync("data-checked", "true");
        await Expect(Source("Dependinator.VsCodeExt")).ToHaveAttributeAsync("data-checked", "true");
        await Expect(Source("webshop.bicep")).ToHaveAttributeAsync("data-checked", "false");
        await Expect(Source("shop")).ToHaveAttributeAsync("data-checked", "false");

        // Two small test samples in place of the proposal, which takes long to parse: a Bicep
        // deployment and a TypeScript project
        foreach (ILocator source in await Page.GetByTestId("sources-check").AllAsync())
            await SetTickedAsync(source, false);
        await Expect(Page.GetByTestId("sources-ok")).ToBeDisabledAsync(); // A model of nothing
        await SetTickedAsync(Source("main.bicep"), true);
        await SetTickedAsync(Source("shop"), true);
        await Expect(Page.GetByTestId("sources-count")).ToContainTextAsync("2 of");
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));

        // Both sources, side by side with the names their own models have
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(App.Node("shop")).ToBeVisibleAsync();
        await Expect(App.Node("Externals")).ToBeVisibleAsync();

        await App.ExpectSubMenuAsync("menu-models", "menu-choose-sources", async timeout =>
        {
            await Expect(App.MenuItem("menu-workspace-model"))
                .ToHaveAttributeAsync("data-checked", "true", new() { Timeout = timeout });
            await Expect(App.MenuItem("menu-visited-model")).ToHaveCountAsync(0, new() { Timeout = timeout });
        });

        // The choice is saved with the model: one source is taken out, and the other stays
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-choose-sources"), Page.GetByTestId("sources-dialog"));
        await Expect(Source("main.bicep")).ToHaveAttributeAsync("data-checked", "true");
        await Expect(Source("Dependinator.sln")).ToHaveAttributeAsync("data-checked", "false");
        await SetTickedAsync(Source("shop"), false);
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));

        await Expect(App.Node("shop")).ToHaveCountAsync(0, new() { Timeout = 60_000 });
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync();

        // Put back, only the script project is parsed: the deployment stays as it is
        await App.OpenSubMenuItemAsync("menu-models", "menu-choose-sources");
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-choose-sources"), Page.GetByTestId("sources-dialog"));
        await SetTickedAsync(Source("shop"), true);
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));

        await Expect(App.Node("shop")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync();
    }

    // What the user did in a source's own model comes along when the source is taken into the
    // workspace model: here a display name.
    [E2EFact]
    public async Task WorkspaceModel_ShouldBringWhatWasDoneInASourcesOwnModel()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.MenuItem("menu-model-item")
            .Filter(new() { Has = Page.GetByText("shop", new() { Exact = true }) })
            .ClickAsync();
        await Expect(App.Node("shop")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await App.EnableEditModeAsync();
        await App.SelectNodeByFullNameAsync("shop");
        await App.ClickNodeMenuItemAsync("node-menu-alias");
        ILocator field = Page.GetByTestId("node-alias-text");
        await Expect(field).ToBeVisibleAsync();
        await App.FillReliablyAsync(field, "The shop");
        await Page.GetByTestId("node-alias-save").ClickAsync();
        await Expect(App.NodeLabel("The shop")).ToBeVisibleAsync();
        await App.WaitForSavedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-workspace-model"), Page.GetByTestId("sources-dialog"));
        foreach (ILocator source in await Page.GetByTestId("sources-check").AllAsync())
            await SetTickedAsync(source, false);
        await SetTickedAsync(Source("main.bicep"), true);
        await SetTickedAsync(Source("shop"), true);
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));

        await Expect(App.NodeLabel("The shop")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync();
    }

    // A source is shown where the user chooses ("Shown in" in the sources dialog, saved with the
    // model), and its root has "Show Inside" in its node menu for the same choice. The two
    // samples are in folders apart, so both start at the top level.
    [E2EFact]
    public async Task WorkspaceModel_ShouldShowASourceWhereTheUserChooses()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-workspace-model"), Page.GetByTestId("sources-dialog"));
        foreach (ILocator source in await Page.GetByTestId("sources-check").AllAsync())
            await SetTickedAsync(source, false);
        await SetTickedAsync(Source("main.bicep"), true);
        await SetTickedAsync(Source("shop"), true);
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));
        await Expect(App.Node("shop")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync();

        // Show Inside the deployment, from the shop's node menu: the view goes to the shop there
        // (it stays selected, so it gets no go to brackets)
        await App.SelectNodeByFullNameAsync("shop");
        ILocator showInside = await App.OpenNodeMenuItemAsync("node-menu-show-inside");
        ILocator inDeployment = Page.Locator("[data-testid=show-inside-item][data-value^='node:']");
        await App.ClickUntilVisibleAsync(showInside, inDeployment);
        await App.ClickUntilGoneAsync(inDeployment);
        await Expect(App.Node("shop")).ToBeVisibleAsync();

        // The choice is saved with the model, and the sources dialog shows it; there the shop is
        // put back at the top level
        await App.OpenSubMenuItemAsync("menu-models", "menu-choose-sources");
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-choose-sources"), Page.GetByTestId("sources-dialog"));
        await Expect(Place("shop")).ToHaveAttributeAsync("data-value", new Regex("^node:"));
        ILocator topLevel = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasTextRegex = new Regex("^Top level$") });
        await App.ClickUntilVisibleAsync(Place("shop").Locator(".mud-select").First, topLevel);
        await App.ClickUntilGoneAsync(topLevel);
        await Expect(Place("shop")).ToHaveAttributeAsync("data-value", "top");
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-ok"));

        await App.OpenSubMenuItemAsync("menu-models", "menu-choose-sources");
        await App.ClickUntilVisibleAsync(App.MenuItem("menu-choose-sources"), Page.GetByTestId("sources-dialog"));
        await Expect(Place("shop")).ToHaveAttributeAsync("data-value", "top");
        await App.ClickUntilGoneAsync(Page.GetByTestId("sources-cancel"));
    }

    // The "Shown in" list of a source in the sources dialog
    ILocator Place(string name) =>
        Page.GetByTestId("sources-item")
            .Filter(new() { Has = Page.GetByText(name, new() { Exact = true }) })
            .GetByTestId("sources-place");

    // The checkbox of a source in the sources dialog, by the source's whole name ("webshop.bicep"
    // has "shop" in it too).
    ILocator Source(string name) =>
        Page.GetByTestId("sources-item")
            .Filter(new() { Has = Page.GetByText(name, new() { Exact = true }) })
            .GetByTestId("sources-check");

    // Not retried: a second click would flip the checkbox back.
    async Task SetTickedAsync(ILocator source, bool isTicked)
    {
        string expected = isTicked ? "true" : "false";
        if (await source.GetAttributeAsync("data-checked") != expected)
            await source.ClickAsync();
        await Expect(source).ToHaveAttributeAsync("data-checked", expected);
    }
}
