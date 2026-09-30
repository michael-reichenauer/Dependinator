using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
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
        await (await App.OpenNodeMenuItemAsync("node-menu-properties")).ClickAsync();

        // The NodeProperties dialog shows the build version line.
        await Expect(App.Dialog).ToBeVisibleAsync();
        await Expect(App.Dialog).ToContainTextAsync("Version:");
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldSetAndClearIconColor()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Pick Blue from the color swatch dropdown (icon tint while the node shows as an
        // icon); the node's icon <use> switches to the generated "--Blue" color variant def.
        await App.PickIconColorItemAsync("Blue");
        await Expect(App.NodeIconUse("Solution--Blue")).ToBeVisibleAsync();

        // Picking Default restores the base violet icon.
        await App.PickIconColorItemAsync("Default");
        await Expect(App.NodeIconUse("Solution")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldSetCloudIconViaDialogTab()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        // Open the icon selector dialog and switch to the Azure tab; the list swaps from the
        // Default group's icons to the Azure ones.
        await App.OpenIconSelectorAsync();
        await Expect(App.IconDialogTab("Azure")).ToBeVisibleAsync();
        await App.IconDialogTab("Azure").ClickAsync();
        await Expect(App.IconDialogItem("Key-Vault")).ToBeVisibleAsync();

        // Selecting an icon closes the dialog and the node's <use> switches to it.
        await App.IconDialogItem("Key-Vault").ClickAsync();
        await Expect(App.NodeIconUse("Key-Vault")).ToBeVisibleAsync();

        // The pinned Default row restores the node-type icon.
        await App.OpenIconSelectorAsync();
        await App.IconDialogDefault.ClickAsync();
        await Expect(App.NodeIconUse("Solution")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldSetContainerBackgroundColor()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();

        // Navigate into Demo.UI so its child class "Main" renders as a container. Wait for
        // a selected result row before pressing Enter — Enter without results is a no-op —
        // and press Enter on the field itself: the dialog's key handler is bound to the
        // field, and a globally-pressed Enter is lost if the field momentarily lost focus
        // (a CI flake showed Enter changing nothing, leaving the dialog open).
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");

        // Only wait for "Main" to actually render as a container before selecting it: whether
        // a node draws as a container or as an icon depends on the zoom the navigation settles
        // at, and the buttons asserted below exist only in container mode.
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");

        // Container mode: the edit pencil is offered and the palette dropdown shows the
        // container swatches (color-item-*), not the icon tints.
        await Expect(App.MenuItem("node-edit")).ToBeVisibleAsync();
        await App.PickColorItemAsync("Teal");

        // Reopening marks Teal as the current (bold) selection; Default clears it again.
        // (Move the mouse off the button so its tooltip closes — the "Set background color"
        // tooltip popover otherwise overlays the top menu row and intercepts the click.)
        ILocator teal = await App.OpenColorItemAsync("Teal");
        await Page.Mouse.MoveAsync(0, 0);
        await Expect(teal.Locator("span").Last).ToHaveCSSAsync("font-weight", "600");
        await App.PickColorItemAsync("Default");
    }

    [E2EFact]
    public async Task NodeToolbar_ShouldToggleDirectLines()
    {
        await App.GotoMainPageAsync();

        // Same navigation as the background-color test: "Main" must render as a container so
        // its members are visible endpoints for the crossing lines.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");

        // Aggregated default: no crossing lines anywhere, and nothing to merge yet.
        await Expect(App.CousinLines).ToHaveCountAsync(0);
        await Expect(App.NodeLinesShallowerDisabled).ToBeVisibleAsync();

        // One level deeper: Main's members get their own lines to the sibling containers they
        // use (e.g. BuildRenderTree -> Diagrams). Clicks on the re-rendering toolbar can be
        // swallowed, so repeat until a crossing line shows up.
        await App.RepeatUntilVisibleAsync(() => App.NodeLinesDeeper.ClickAsync(), App.CousinLines.First);

        // One level up again restores the bundle; the button disables itself at depth zero.
        await App.RepeatUntilVisibleAsync(() => App.NodeLinesShallower.ClickAsync(), App.NodeLinesShallowerDisabled);
        await Expect(App.CousinLines).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task DependenciesPanel_ShouldDrawFocusLines_AndSplitOnExpand()
    {
        await App.GotoMainPageAsync();

        // Same navigation as the background-color test: "Main" as a container.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");
        await App.WaitForContainerNodeAsync("Main");
        await App.SelectContainerNodeAsync("Demo.UI.Main");
        await Expect(App.FocusLines).ToHaveCountAsync(0);

        // Opening the explorer draws Main's own lines in the accent style: its links leave from
        // Main itself instead of merging into the Demo.UI bundle, ending at the far top
        // containers (the collapsed tree rows).
        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        await Expect(App.FocusLines).Not.ToHaveCountAsync(0);
        await Expect(App.LineTitle("Demo.UI.Main→Externals")).ToHaveCountAsync(1);

        // Expanding the first row (Externals) splits its line one level, into the row's child
        // MudBlazor; collapsing it merges the line back.
        await App.ExplorerExpandButtons.First.ClickAsync();
        await Expect(App.LineTitle("Demo.UI.Main→MudBlazor (dll)")).ToHaveCountAsync(1);
        await Expect(App.LineTitle("Demo.UI.Main→Externals")).ToHaveCountAsync(0);
        await App.ExplorerExpandButtons.First.ClickAsync();
        await Expect(App.LineTitle("Demo.UI.Main→Externals")).ToHaveCountAsync(1);

        // Closing the explorer keeps its lines (they are what the user opened it for); hiding
        // the lines with the header toggle first removes them.
        await App.ExplorerShowLinesButton.ClickAsync();
        await Expect(App.FocusLines).ToHaveCountAsync(0);
        await App.CloseExplorerAsync();
        await Expect(App.DependenciesTree).Not.ToBeVisibleAsync();
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

    // The explorer folds down on a canvas click only on a narrow viewport (below MudBlazor's md
    // breakpoint), where it would otherwise cover the diagram; on a wide screen it stays open.
    [E2EFact]
    public async Task DependenciesPanel_ShouldMinimizeOnCanvasClick_AndRestoreOnHeaderClick()
    {
        await Page.SetViewportSizeAsync(800, 600);
        await App.GotoMainPageAsync();
        await App.SelectNodeByFullNameAsync("Demo.sln");

        await App.RepeatUntilVisibleAsync(() => App.NodeDependenciesButton.ClickAsync(), App.DependenciesTree);
        await Expect(App.FocusLines).Not.ToHaveCountAsync(0);

        // A click elsewhere in the diagram (empty canvas, bottom-right corner) folds the explorer
        // down to its title bar; its lines stay in the diagram.
        var canvas = await App.Canvas.BoundingBoxAsync();
        await Page.Mouse.ClickAsync(canvas!.X + canvas.Width - 30, canvas.Y + canvas.Height - 30);
        await Expect(App.DependenciesTree).Not.ToBeVisibleAsync();
        await Expect(App.ExplorerHeader).ToBeVisibleAsync();
        await Expect(App.FocusLines).Not.ToHaveCountAsync(0);

        // Clicking the title bar brings the tree back; the header button folds it again.
        await App.RepeatUntilVisibleAsync(() => App.ExplorerHeader.ClickAsync(), App.DependenciesTree);
        await App.ExplorerMinimizeButton.ClickAsync();
        await Expect(App.DependenciesTree).Not.ToBeVisibleAsync();
        await App.RepeatUntilVisibleAsync(() => App.ExplorerMinimizeButton.ClickAsync(), App.DependenciesTree);

        // Closing keeps the lines until something else is selected.
        await App.CloseExplorerAsync();
        await Expect(App.FocusLines).Not.ToHaveCountAsync(0);
        await App.SelectNodeByFullNameAsync("Externals");
        await Expect(App.FocusLines).ToHaveCountAsync(0);
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

        // The header close button dismisses the explorer; on a wide viewport a canvas click
        // does not fold it (it stays open beside the diagram).
        await App.CloseExplorerAsync();
        await Expect(App.DependenciesTree).Not.ToBeVisibleAsync();
    }
}
