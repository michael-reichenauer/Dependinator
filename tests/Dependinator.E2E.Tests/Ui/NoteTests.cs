using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the note annotation flow through the browser: add a note via the app menu
// ("Add note" arms placing mode → click canvas → dialog), that it renders as an SVG circle
// showing the id, and that it survives a reload (persisted with IsNote/IsManual).
public class NoteTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // A distinctive id that won't collide with any demo-model label text.
    const string NoteId = "Z1";

    [E2EFact]
    public async Task Note_ShouldBeAddedViaMenu_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();

        await AddNoteAsync(NoteId, "Guiding note");
        await Expect(NoteText(NoteId)).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // A note is user-drawn (not parsed), so surviving the reload proves it was persisted
        // (with IsNote/IsManual) and kept by the re-parse reconciliation.
        await Expect(NoteText(NoteId)).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NotesSidebar_ShouldToggle_AndListNotes()
    {
        await App.GotoMainPageAsync();
        await AddNoteAsync(NoteId, "Guiding note");

        // Toggle the sidebar on from the app menu; it lists the note (id + description).
        await (await App.OpenSubMenuItemAsync("menu-view", "menu-notes-sidebar")).ClickAsync();
        ILocator item = Page.GetByTestId("notes-sidebar-item");
        await Expect(item).ToHaveCountAsync(1);
        await Expect(item.First).ToContainTextAsync("Guiding note");

        // Toggle it off again via the sidebar's close button.
        await Page.GetByTestId("notes-sidebar-close").ClickAsync();
        await Expect(Page.GetByTestId("notes-sidebar-list")).ToHaveCountAsync(0);
    }

    // The note's id is drawn as plain SVG <text> (not the text.iconName used by parsed nodes).
    ILocator NoteText(string id) => Page.Locator("#svgcanvas text", new() { HasTextString = id }).First;

    async Task AddNoteAsync(string id, string description)
    {
        ILocator addNote = await App.OpenSubMenuItemAsync("menu-edit", "menu-add-note");
        await addNote.ClickAsync();

        // Wait until placing mode is armed (the prompt snackbar). This also ensures the menu
        // overlay has closed, so the next canvas click lands on the diagram, not the menu.
        await Expect(Page.GetByText("Click on the diagram to place the note.")).ToBeVisibleAsync();

        // Click an empty area (bottom-left) to drop the note there.
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 130);

        ILocator idInput = Page.GetByTestId("note-id");
        await Expect(idInput).ToBeVisibleAsync();
        await idInput.FillAsync(id);
        await Page.GetByTestId("note-description").FillAsync(description);
        await Page.GetByTestId("note-save").ClickAsync();
    }
}
