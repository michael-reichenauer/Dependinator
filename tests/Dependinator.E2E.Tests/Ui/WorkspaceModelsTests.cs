using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// A host with a workspace (VS Code, and this test host, whose workspace is the repository with
// the demo standing in for the working solution) keeps the workspace's models apart from all
// others: the Models menu lists only the workspace's solutions and Bicep files, the Models
// dialog opens a cloud, design or demo model, and while such a model is open the breadcrumb
// bar names its kind and leads back to the workspace.
public class WorkspaceModelsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ModelsMenu_ShouldListOnlyTheWorkspaceModels()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.ExpectSubMenuAsync("menu-models", "menu-manage-models", async timeout =>
        {
            ILocator models = App.MenuItem("menu-model-item");
            await Expect(models.Filter(new() { HasText = "Demo.sln" }))
                .ToHaveAttributeAsync("data-checked", "true", new() { Timeout = timeout });
            // The sample deployment is a file of the workspace, shown with its folder.
            ILocator sample = models.Filter(new() { HasText = "main.bicep" });
            await Expect(sample).ToHaveAttributeAsync("data-checked", "false", new() { Timeout = timeout });
            await Expect(sample.GetByTestId("menu-model-folder"))
                .ToContainTextAsync("Samples", new() { Timeout = timeout });

            // The other demo is no workspace model: it is opened from the dialog.
            await Expect(models.Filter(new() { HasText = "Demo.bicep" }))
                .ToHaveCountAsync(0, new() { Timeout = timeout });
            await Expect(App.MenuItem("menu-manage-models"))
                .ToContainTextAsync("Other Models", new() { Timeout = timeout });
            await Expect(App.MenuItem("menu-visited-model")).ToHaveCountAsync(0, new() { Timeout = timeout });
        });
    }

    [E2EFact]
    public async Task OtherModel_ShouldBeAVisit_WithAWayBackToTheWorkspace()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // A workspace model is no visit: nothing leads "back".
        ILocator back = Page.GetByTestId("breadcrumb-back");
        await Expect(back).ToHaveCountAsync(0);

        await App.OpenOtherModelAsync("Demo.bicep");
        await Expect(App.Node("Demo.bicep")).ToBeVisibleAsync();

        // The bar is up without a selected node: the model with its kind, and the way back.
        await Expect(Page.GetByTestId("breadcrumb-model")).ToContainTextAsync("Demo.bicep");
        await Expect(Page.GetByTestId("breadcrumb-kind")).ToHaveTextAsync("demo model");
        await Expect(back).ToContainTextAsync("Demo.sln");

        // The menu names the visited model apart from the workspace's, none of which is open.
        await App.ExpectSubMenuAsync("menu-models", "menu-manage-models", async timeout =>
        {
            await Expect(App.MenuItem("menu-visited-model"))
                .ToContainTextAsync("Demo.bicep", new() { Timeout = timeout });
            await Expect(App.MenuItem("menu-model-item").Filter(new() { HasText = "Demo.sln" }))
                .ToHaveAttributeAsync("data-checked", "false", new() { Timeout = timeout });
        });
        await App.CloseMenuAsync();

        // The menu button's tooltip opens over the back button, right below it, while the
        // pointer rests on the menu button: move away first.
        await Page.Mouse.MoveAsync(640, 400);
        await back.ClickAsync();

        await Expect(App.NodeLabel("Demo.sln")).ToBeVisibleAsync();
        await Expect(back).ToHaveCountAsync(0);
    }
}
