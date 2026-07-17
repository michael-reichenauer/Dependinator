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

        // Pick Blue from the color swatch dropdown (icon tint while the node shows as an
        // icon); the node's icon <use> switches to the generated "--Blue" color variant def.
        await App.NodeSetColorButton.ClickAsync();
        await App.IconColorItem("Blue").ClickAsync();
        await Expect(App.NodeIconUse("Solution--Blue")).ToBeVisibleAsync();

        // Picking Default restores the base violet icon.
        await App.NodeSetColorButton.ClickAsync();
        await App.IconColorItem("Default").ClickAsync();
        await Expect(App.NodeIconUse("Solution")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldSetCloudIconViaDialogTab()
    {
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Open the icon selector dialog and switch to the Azure tab; the list swaps from the
        // Default group's icons to the Azure ones.
        await App.NodeSetIconButton.ClickAsync();
        await Expect(App.IconDialogTab("Azure")).ToBeVisibleAsync();
        await App.IconDialogTab("Azure").ClickAsync();
        await Expect(App.IconDialogItem("Key-Vault")).ToBeVisibleAsync();

        // Selecting an icon closes the dialog and the node's <use> switches to it.
        await App.IconDialogItem("Key-Vault").ClickAsync();
        await Expect(App.NodeIconUse("Key-Vault")).ToBeVisibleAsync();

        // The pinned Default row restores the node-type icon.
        await App.NodeSetIconButton.ClickAsync();
        await App.IconDialogDefault.ClickAsync();
        await Expect(App.NodeIconUse("Solution")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldSetContainerBackgroundColor()
    {
        await App.GotoMainPageAsync();

        // Navigate into Demo.UI so its child class "Main" renders as a container.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Page.Keyboard.PressAsync("Enter");

        await App.SelectContainerNodeAsync("Demo.UI.Main");

        // Container mode: the edit pencil is offered and the palette dropdown shows the
        // container swatches (color-item-*), not the icon tints.
        await Expect(App.MenuItem("node-edit")).ToBeVisibleAsync();
        await App.NodeSetColorButton.ClickAsync();
        await App.ColorItem("Teal").ClickAsync();

        // Reopening marks Teal as the current (bold) selection; Default clears it again.
        // (Move the mouse off the button so its tooltip closes — the "Set background color"
        // tooltip popover otherwise overlays the top menu row and intercepts the click.)
        await App.NodeSetColorButton.ClickAsync();
        await Page.Mouse.MoveAsync(0, 0);
        await Expect(App.ColorItem("Teal").Locator("span").Last).ToHaveCSSAsync("font-weight", "600");
        await App.ColorItem("Default").ClickAsync();
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
