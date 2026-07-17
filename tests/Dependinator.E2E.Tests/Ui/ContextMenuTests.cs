using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the right-click context menu on the diagram canvas: it offers "Add Note Here" and
// "Add Node Here", each placing the note/manual node at the clicked position (the same flows also
// reachable via the app menu and double-click, but anchored where the user right-clicked).
public class ContextMenuTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string NoteId = "Z9";

    [E2EFact]
    public async Task ContextMenu_AddNodeHere_ShouldAddManualNodeAtClick()
    {
        await App.GotoMainPageAsync();

        await OpenContextMenuAsync();
        await Page.GetByTestId("context-menu-add-node").ClickAsync();

        // The icon selector opens in picker mode: no pinned "Default" row (there is no node to
        // revert yet); picking an icon adds a node named after it at the clicked position.
        await Expect(Page.GetByTestId("icon-dialog-search")).ToBeVisibleAsync();
        await Expect(App.IconDialogDefault).Not.ToBeVisibleAsync();
        await App.IconDialogTab("General").ClickAsync();
        await App.IconDialogItem("Database").ClickAsync();

        await Expect(App.NodeLabel("Database")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ContextMenu_AddNoteHere_ShouldAddNoteAtClick()
    {
        await App.GotoMainPageAsync();

        await OpenContextMenuAsync();
        await Page.GetByTestId("context-menu-add-note").ClickAsync();

        ILocator idInput = Page.GetByTestId("note-id");
        await Expect(idInput).ToBeVisibleAsync();
        await App.FillReliablyAsync(idInput, NoteId);
        await App.FillReliablyAsync(Page.GetByTestId("note-description"), "Context note");
        await Page.GetByTestId("note-save").ClickAsync();

        // The note's id is drawn as plain SVG <text> (not the text.iconName used by parsed nodes).
        await Expect(Page.Locator("#svgcanvas text", new() { HasTextString = NoteId }).First).ToBeVisibleAsync();
    }

    // Right-click an empty area (bottom-left quadrant, away from the centered demo nodes) and wait
    // for the context menu to appear.
    async Task OpenContextMenuAsync()
    {
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 130, new() { Button = MouseButton.Right });
        await Expect(Page.GetByTestId("canvas-context-menu")).ToBeVisibleAsync();
    }
}
