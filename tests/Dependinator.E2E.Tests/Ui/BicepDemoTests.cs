using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The built-in Bicep demo ("/Demo.bicep", the pre-parsed webshop example) is listed next to the
// C# demo wherever nothing else can be opened (the browser app, and this test host) and loads
// without the compiler.
public class BicepDemoTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task BicepDemo_ShouldBeListed_AndOpenWithoutParsing()
    {
        await OpenBicepDemoAsync();

        // Navigate into the deployment: a module sits under the resource group it is deployed
        // into, next to the other modules of that group (see BicepModelTests for why the hotkey
        // is used here).
        await NavigateToAsync("rgPlatform.networking");

        // (Node locators match long names, which drop the root segment.)
        await Expect(App.Node("rgPlatform.networking")).ToBeVisibleAsync();
        await Expect(App.Node("rgPlatform.keyVault")).ToBeVisibleAsync();
        await Expect(App.Node("rgPlatform.monitoring")).ToBeVisibleAsync();
    }

    // Lines that only carry a value into a parameter or variable row are left out by default;
    // View › Lines › Configuration Lines brings them back, and the choice survives a reload.
    [E2EFact]
    public async Task ConfigurationLines_ShouldBeHiddenByDefault_AndToggle()
    {
        await OpenBicepDemoAsync();
        await NavigateToAsync("platformBudget");
        await Expect(App.Node("platformBudget")).ToBeVisibleAsync();

        // The budget's contact e-mail is a root parameter: a configuration line.
        ILocator budgetToEmail = App.LineTitle("platformBudget→alertEmail");
        await Expect(budgetToEmail).ToHaveCountAsync(0);

        await ToggleConfigurationAsync(expectHidden: false);
        await Expect(budgetToEmail).ToHaveCountAsync(1);

        // The test host reopens the C# demo on a reload, so open the Bicep demo again; the
        // filter lives in the config and must still be off.
        await OpenBicepDemoAsync();
        await NavigateToAsync("platformBudget");
        await Expect(App.Node("platformBudget")).ToBeVisibleAsync();
        await Expect(budgetToEmail).ToHaveCountAsync(1);

        await ToggleConfigurationAsync(expectHidden: true);
        await Expect(budgetToEmail).ToHaveCountAsync(0);
    }

    async Task OpenBicepDemoAsync()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.MenuItem("menu-model-item").Filter(new() { HasText = "Demo.bicep" }).ClickAsync();

        await Expect(App.Node("Demo.bicep")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-model"))
            .ToHaveAttributeAsync("title", new Regex(@"Demo\.bicep \(Bicep deployment\)"));
    }

    async Task NavigateToAsync(string fullName)
    {
        await Page.Mouse.MoveAsync(640, 400);
        SearchDialog search = new(App, Page);
        await App.RepeatUntilVisibleAsync(() => Page.Keyboard.PressAsync("Control+f"), search.Field);
        await search.NavigateToAsync(fullName);
    }

    // The Lines submenu is nested under View: open View, hover Lines, then the item.
    Task<ILocator> OpenConfigurationItemAsync() =>
        App.OpenSubMenuItemAsync("menu-view", "menu-lines", "menu-lines-configuration");

    async Task ToggleConfigurationAsync(bool expectHidden)
    {
        await (await OpenConfigurationItemAsync()).ClickAsync();
        await Expect(await OpenConfigurationItemAsync())
            .ToHaveAttributeAsync("data-checked", expectHidden ? "true" : "false");
        await App.CloseMenuAsync();
    }
}
