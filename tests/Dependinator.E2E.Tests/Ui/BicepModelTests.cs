using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Opens the sample Bicep deployment (tests/Dependinator.Bicep.Tests/Samples/main.bicep, listed
// because the Blazor Server host's workspace folder is the repo) from the Models menu and checks
// that it is parsed in-process into a diagram of resources and modules.
public class BicepModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task BicepModel_ShouldOpenFromTheModelsMenu_AndShowItsResources()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        // Open the Models submenu (anchored on the always-present New Model item, since
        // menu-model-item matches several entries), then click the sample deployment's entry.
        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.MenuItem("menu-model-item").Filter(new() { HasText = "main.bicep" }).ClickAsync();

        // The first Bicep compile also loads the Azure resource type index, so allow a little longer.
        // Node groups are matched by their long name, which drops the root segment and is exact
        // ("web" is not "webHost"), unlike the substring match of a label.
        await Expect(App.Node("main.bicep")).ToBeVisibleAsync(new() { Timeout = 60_000 });

        // A deployment is a parseable model: it has the Parse button and names its kind.
        await Expect(Page.GetByTestId("toolbar-parse")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-model"))
            .ToHaveAttributeAsync("title", new Regex(@"main\.bicep \(Bicep deployment\)"));

        // At the initial zoom the deployment is one icon; navigate into it through the search
        // dialog. The menu button's tooltip tends to stay open after the model item click (the
        // pointer never left the button), which makes the app menu unreliable here, so the dialog
        // is opened with its hotkey instead of the search helpers (which also wait for the demo
        // model's root, no longer the open model).
        await Page.Mouse.MoveAsync(640, 400);
        SearchDialog search = new(App, Page);
        await App.RepeatUntilVisibleAsync(() => Page.Keyboard.PressAsync("Control+f"), search.Field);
        await search.NavigateToAsync("storage");

        await Expect(App.Node("storage")).ToBeVisibleAsync();
        await Expect(App.Node("web")).ToBeVisibleAsync();
        await Expect(App.Node("plan")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Filter(new() { HasText = "storage" })).ToBeVisibleAsync();
    }
}
