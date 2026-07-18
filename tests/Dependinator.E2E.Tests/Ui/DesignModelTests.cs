using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises manually designed models: creating a new empty model via the app menu
// ("Models > New Model …" prompts for a name), adding a manual node to it, and re-opening it
// after a reload (in test mode the app always starts with the demo model, so re-opening from
// the models menu proves the design model was persisted and loaded without any parsing).
// Design models are never parsed, so the Parse toolbar button must be hidden while one is open.
public class DesignModelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // The added node is named after the picked icon; distinct from icons used by other tests.
    const string NodeIcon = "Database";

    [E2EFact]
    public async Task DesignModel_ShouldBeCreatedViaMenu_AndPersistAcrossReload()
    {
        // Unique name so reruns against the same browser storage start from a fresh model.
        string modelName = $"Design {Guid.NewGuid().ToString("N")[..8]}";

        await App.GotoMainPageAsync();

        // Parse is offered for the parseable demo/solution model.
        await Expect(Page.GetByTestId("toolbar-parse")).ToBeVisibleAsync();

        ILocator newModelItem = await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await newModelItem.ClickAsync();

        await App.FillReliablyAsync(Page.GetByTestId("new-model-name"), modelName);
        await Page.GetByTestId("new-model-create").ClickAsync();

        // The new empty model is created and loaded; design models cannot be parsed.
        await Expect(Page.GetByText("Empty model created")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("toolbar-parse")).ToHaveCountAsync(0);

        // Add a manual node by double-clicking the empty canvas (edit mode is auto-enabled).
        await AddManualNodeAsync(NodeIcon);
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload. In test mode
        // the app always starts with the demo model, so re-open the design model via the menu.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // Open the Models submenu (anchored on the always-present New Model item, since
        // menu-model-item matches several entries), then click the created model's entry.
        await App.OpenSubMenuItemAsync("menu-models", "menu-new-model");
        await App.MenuItem("menu-model-item").Filter(new() { HasText = modelName }).ClickAsync();

        // The design model was persisted and re-loaded without parsing.
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("toolbar-parse")).ToHaveCountAsync(0);
    }

    // Double-clicks an empty area of the canvas and picks an icon in the selector dialog.
    async Task AddManualNodeAsync(string iconName)
    {
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        await Page.Mouse.DblClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);

        await Expect(Page.GetByTestId("icon-dialog-search")).ToBeVisibleAsync();
        await App.IconDialogTab("General").ClickAsync();
        await App.IconDialogItem(iconName).ClickAsync();
    }
}
