using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Opens the sample TypeScript project (tests/Dependinator.TypeScript.Tests/Samples/shop, listed
// as its folder name because the Blazor Server host's workspace folder is the repo) from the
// Models menu and checks that the parser's Node.js helper turns it into a diagram of files.
public class TypeScriptModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task TypeScriptProject_ShouldOpenFromTheModelsMenu_AndShowItsFiles()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        // The whole name: "webshop.bicep" has "shop" in it too
        await App.MenuItem("menu-model-item")
            .Filter(new() { Has = Page.GetByText("shop", new() { Exact = true }) })
            .ClickAsync();

        // The project is named after its package. The first parse also unpacks the helper script.
        await Expect(App.Node("shop")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(Page.GetByTestId("toolbar-parse")).ToBeVisibleAsync();

        // Navigate to a file through the search dialog, opened with its hotkey (see BicepModelTests).
        await Page.Mouse.MoveAsync(640, 400);
        SearchDialog search = new(App, Page);
        await App.RepeatUntilVisibleAsync(() => Page.Keyboard.PressAsync("Control+f"), search.Field);
        await search.NavigateToAsync("src.cart.ts");

        // The files of the project's one source folder, which is passed through, and a folder in it
        await Expect(App.Node("src.cart.ts")).ToBeVisibleAsync();
        await Expect(App.Node("src.main.ts")).ToBeVisibleAsync();
        await Expect(App.Node("src.lib")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-item").Filter(new() { HasText = "cart.ts" })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("breadcrumb-model"))
            .ToHaveAttributeAsync("title", new Regex(@"tsconfig\.json \(TypeScript project\)"));
    }
}
