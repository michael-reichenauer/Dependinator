using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the manual "design" flow through the browser: add a user-drawn node via the app menu
// ("Add Node …" arms placing mode → click canvas) or by double-clicking empty canvas — both open
// the icon selector, and the node is created with the picked icon, named after it — that it
// survives a reload (persisted with IsManual), and that only manual nodes expose the Delete
// action. Editing is enabled by default in the Blazor Server host.
public class ManualEditTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // Each test picks a distinct icon; the added node is named after the icon's display name.
    const string NodeIcon = "Cache";
    const string MenuNodeIcon = "Queue";

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedViaMenu_AtClickedPosition()
    {
        await App.GotoMainPageAsync();

        // "Add Node …" arms placing mode; the next canvas click begins the add there.
        ILocator addNode = await App.OpenSubMenuItemAsync("menu-edit", "menu-add-node");
        await addNode.ClickAsync();

        // Wait until placing mode is armed (the prompt snackbar). This also ensures the menu
        // overlay has closed, so the next canvas click lands on the diagram, not the menu.
        await Expect(Page.GetByText("Click on the diagram to place the node.")).ToBeVisibleAsync();

        // Click an empty area (bottom-left) to place the node there.
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 130);

        await PickIconAsync(MenuNodeIcon);

        await Expect(App.NodeLabel(MenuNodeIcon)).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedByDoubleClick_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();

        await AddManualNodeAsync(NodeIcon);
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // A manual node is not produced by parsing, so surviving the reload proves it was
        // persisted (with IsManual) and kept by the re-parse reconciliation.
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();

        // Only manual nodes expose the Delete action (proves IsManual round-tripped).
        await App.SelectNodeByVisibleNameAsync(NodeIcon);
        await App.NodeToolbarMenu.ClickAsync();
        await Expect(Page.GetByTestId("node-menu-delete")).ToBeVisibleAsync();
    }

    // Double-clicks an empty area of the canvas and picks an icon in the selector dialog.
    async Task AddManualNodeAsync(string iconName)
    {
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        // Bottom-left quadrant, away from the centered demo nodes.
        await Page.Mouse.DblClickAsync(box.X + 120, box.Y + box.Height - 120);

        await PickIconAsync(iconName);
    }

    // Picks an icon from the General tab of the opened icon selector dialog.
    async Task PickIconAsync(string iconName)
    {
        await Expect(Page.GetByTestId("icon-dialog-search")).ToBeVisibleAsync();
        await App.IconDialogTab("General").ClickAsync();
        await App.IconDialogItem(iconName).ClickAsync();
    }
}
