using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises Models › Manage Models: the dialog lists the models in sections with the open one
// marked as current, and closes again.
public class ModelsDialogTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ManageModels_ShouldListTheCurrentModel_AndClose()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-models", "menu-manage-models")).ClickAsync();

        ILocator dialog = Page.GetByTestId("models-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        ILocator current = Page.GetByTestId("models-item").Filter(new() { HasText = "current" });
        await Expect(current).ToContainTextAsync("Demo.sln");

        await Page.GetByTestId("models-close").ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
    }
}
