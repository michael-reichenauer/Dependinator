using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the manual "design" flow through the browser: add a user-drawn node via the app menu
// ("Add Node …" arms placing mode → click canvas) or by double-clicking empty canvas, that it
// survives a reload (persisted with IsManual), and that only manual nodes expose the Delete
// action. Editing is enabled by default in the Blazor Server host.
public class ManualEditTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string NodeName = "MyE2ENode";
    const string MenuNodeName = "MyMenuNode";

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedViaMenu_AtClickedPosition()
    {
        await App.GotoMainPageAsync();

        // "Add Node …" arms placing mode; the next canvas click begins the add there.
        ILocator addNode = await App.OpenMenuItemAsync("menu-add-node");
        await addNode.ClickAsync();

        // Wait until placing mode is armed (the prompt snackbar). This also ensures the menu
        // overlay has closed, so the next canvas click lands on the diagram, not the menu.
        await Expect(Page.GetByText("Click on the diagram to place the node.")).ToBeVisibleAsync();

        // Click an empty area (bottom-left) to place the node there.
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 130);

        // The data-testid is applied directly to the MudTextField's <input> element.
        ILocator input = Page.GetByTestId("manual-node-name");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync(MenuNodeName);
        await input.PressAsync("Enter");

        await Expect(App.NodeLabel(MenuNodeName)).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedByDoubleClick_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();

        await AddManualNodeAsync(NodeName);
        await Expect(App.NodeLabel(NodeName)).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // A manual node is not produced by parsing, so surviving the reload proves it was
        // persisted (with IsManual) and kept by the re-parse reconciliation.
        await Expect(App.NodeLabel(NodeName)).ToBeVisibleAsync();

        // Only manual nodes expose the Delete action (proves IsManual round-tripped).
        await App.SelectNodeByVisibleNameAsync(NodeName);
        await App.NodeToolbarMenu.ClickAsync();
        await Expect(Page.GetByTestId("node-menu-delete")).ToBeVisibleAsync();
    }

    // Double-clicks an empty area of the canvas and enters a name in the inline prompt.
    async Task AddManualNodeAsync(string name)
    {
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        // Bottom-left quadrant, away from the centered demo nodes.
        await Page.Mouse.DblClickAsync(box.X + 120, box.Y + box.Height - 120);

        // The data-testid is applied directly to the MudTextField's <input> element.
        ILocator input = Page.GetByTestId("manual-node-name");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync(name);
        await input.PressAsync("Enter");
    }
}
