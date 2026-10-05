using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the keyboard shortcuts routed through KeyboardService (listenToKeyboard in
// jsInterop.js): Ctrl+Z undoes an edit, and Escape clears the selection.
public class KeyboardTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Keyboard_ShouldUndoWithCtrlZ_AndDeselectWithEscape()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();

        // Nothing has been done yet, so the toolbar undo button starts disabled (but visible).
        ILocator undo = App.MenuItem("toolbar-undo");
        await Expect(undo).ToBeVisibleAsync();
        await Expect(undo).ToBeDisabledAsync();

        // Resizing the node is an undoable edit. A click on the re-rendering toolbar can be
        // swallowed, so repeat until undo becomes available.
        await App.SelectNodeByFullNameAsync("Demo.sln");
        await Expect(App.NodeIncreaseSize).ToBeVisibleAsync();
        for (int attempt = 0; attempt < 5 && await undo.IsDisabledAsync(); attempt++)
        {
            await App.NodeIncreaseSize.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }
        await Expect(undo).ToBeEnabledAsync();

        await Page.Keyboard.PressAsync("Control+z");
        await Expect(undo).ToBeDisabledAsync();
        await Expect(App.MenuItem("toolbar-redo")).ToBeEnabledAsync();

        // Escape clears the selection, so the node toolbar goes away.
        await Expect(App.NodeToolbarMenu).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.NodeToolbarMenu).ToBeHiddenAsync();
    }
}
