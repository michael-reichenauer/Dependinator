using Dependinator.E2E.Tests.Shared;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Drives the selected-node context toolbar (NodeToolbar.razor) and verifies the MudBlazor
// dialogs/popovers it triggers actually open — wiring that unit tests can't reach.
public class NodeToolbarTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact(Skip = "The Properties menu item is disabled until actual data exists (see NodeToolbar.razor)")]
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
    public async Task NodeToolbar_ShouldSetAndClearIconColor()
    {
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Pick Blue from the icon-color swatch dropdown; the node's icon <use> switches to
        // the generated "--Blue" color variant def.
        await App.NodeSetIconColorButton.ClickAsync();
        await App.IconColorItem("Blue").ClickAsync();
        await Expect(App.NodeIconUse("Solution--Blue")).ToBeVisibleAsync();

        // Picking Default restores the base violet icon.
        await App.NodeSetIconColorButton.ClickAsync();
        await App.IconColorItem("Default").ClickAsync();
        await Expect(App.NodeIconUse("Solution")).ToBeVisibleAsync();
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

    [E2EFact]
    public async Task DependenciesPanel_ShouldToggleDirectionAndClose()
    {
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        await App.NodeDependenciesButton.ClickAsync();
        await Expect(App.DependenciesTree).ToBeVisibleAsync();

        // The header toggle switches the tree to references without closing the popover.
        await App.ExplorerReferencesButton.ClickAsync();
        await Expect(App.DependenciesTree).ToBeVisibleAsync();

        // The header close button dismisses the explorer.
        await App.ExplorerCloseButton.ClickAsync();
        await Expect(App.DependenciesTree).Not.ToBeVisibleAsync();
    }
}
