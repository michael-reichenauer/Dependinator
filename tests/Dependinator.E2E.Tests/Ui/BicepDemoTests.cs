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
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.MenuItem("menu-model-item").Filter(new() { HasText = "Demo.bicep" }).ClickAsync();

        await Expect(App.Node("Demo.bicep")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-model"))
            .ToHaveAttributeAsync("title", new Regex(@"Demo\.bicep \(Bicep deployment\)"));

        // Navigate into the deployment (see BicepModelTests for why the hotkey is used here).
        await Page.Mouse.MoveAsync(640, 400);
        SearchDialog search = new(App, Page);
        await App.RepeatUntilVisibleAsync(() => Page.Keyboard.PressAsync("Control+f"), search.Field);
        await search.NavigateToAsync("networking");

        await Expect(App.Node("networking")).ToBeVisibleAsync();
        await Expect(App.Node("rgApps")).ToBeVisibleAsync();
        await Expect(App.Node("frontDoor")).ToBeVisibleAsync();
    }
}
