using Dependinator.E2E.Tests.Shared;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Drives the selected-node context toolbar (NodeToolbar.razor) and verifies the MudBlazor
// dialogs/popovers it triggers actually open — wiring that unit tests can't reach.
public class NodeToolbarTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task NodeMenu_ShouldOpenPropertiesDialog()
    {
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Open the node context menu and choose "Properties …".
        await App.NodeToolbarMenu.ClickAsync();
        await App.MenuItem("node-menu-properties").ClickAsync();

        // The NodeProperties dialog shows the build version line.
        await Expect(App.Dialog).ToBeVisibleAsync();
        await Expect(App.Dialog).ToContainTextAsync("Version:");
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldOpenDependenciesPanel()
    {
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        await App.NodeDependenciesButton.ClickAsync();

        // The dependencies explorer popover renders a tree view.
        await Expect(App.DependenciesTree).ToBeVisibleAsync();
    }
}
