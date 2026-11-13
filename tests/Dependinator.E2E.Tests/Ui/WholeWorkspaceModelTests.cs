using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The model of the whole workspace: the first entry of the Models menu in a host with a workspace
// (this test host's workspace is the repository). Opened for the first time it asks which of the
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
        ILocator workspaceModel = App.MenuItem("menu-model-item").Filter(new() { HasText = "whole workspace" });
        await Expect(workspaceModel).ToHaveCountAsync(1);
        // Not offered for another model than the workspace's
        await Expect(App.MenuItem("menu-choose-sources")).ToHaveCountAsync(0);
        await App.ClickUntilVisibleAsync(workspaceModel, Page.GetByTestId("sources-dialog"));

        // The proposal: the solution and the projects under src, not the test samples or examples
        await Expect(Source("Dependinator.sln")).ToHaveAttributeAsync("data-checked", "true");
        await Expect(Source("DependinatorVsCode")).ToHaveAttributeAsync("data-checked", "true");
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
            await Expect(App.MenuItem("menu-model-item").Filter(new() { HasText = "whole workspace" }))
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
    }

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
