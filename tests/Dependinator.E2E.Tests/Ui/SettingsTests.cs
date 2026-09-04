using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the Settings submenu in the app menu: the "Include Test Projects" item is a
// checkbox-style toggle whose state is stored per model. The demo model is pre-parsed and
// embedded, so the triggered re-parse short-circuits and the node set does not change --
// this asserts the toggle state itself, which is what the menu owns.
public class SettingsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task IncludeTestProjects_ShouldToggleViaSettingsMenu()
    {
        await App.GotoMainPageAsync();

        // Test projects are excluded by default.
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "false");

        await (await OpenIncludeTestProjectsAsync()).ClickAsync();
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "true");

        // And back off again.
        await (await OpenIncludeTestProjectsAsync()).ClickAsync();
        await Expect(await OpenIncludeTestProjectsAsync()).ToHaveAttributeAsync("data-checked", "false");
    }

    Task<ILocator> OpenIncludeTestProjectsAsync() =>
        App.OpenSubMenuItemAsync("menu-settings", "menu-include-test-projects");
}
