using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the full edit -> command -> undo/redo pipeline through the browser:
// InteractionService -> NodeEditService -> CommandService -> re-render. A unit test can
// cover the command stack, but only an e2e test proves the UI is wired to it.
// (Edit mode is off by default and per user, so the test turns it on; the resize buttons
// only show in edit mode.)
public class EditTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // MudMenuItem renders Disabled as a roleless <div aria-disabled> with a mud-disabled
    // class (Playwright's ToBeDisabledAsync doesn't recognize the div), so assert the class.
    static readonly Regex Disabled = new("mud-disabled");

    [E2EFact]
    public async Task NodeResize_ShouldBeUndoneAndRedone()
    {
        await App.GotoMainPageAsync();
        await App.EnableEditModeAsync();

        // Nothing has been done yet, so undo/redo start disabled.
        await Expect(await App.OpenSubMenuItemAsync("menu-edit", "menu-undo")).ToHaveClassAsync(Disabled);
        await Expect(App.MenuItem("menu-redo")).ToHaveClassAsync(Disabled);
        await Page.Keyboard.PressAsync("Escape");

        // Select the node; its context toolbar exposes the grow/shrink buttons.
        await App.SelectNodeByFullNameAsync("Demo.sln");
        await Expect(App.NodeIncreaseSize).ToBeVisibleAsync();

        // Resizing the node pushes a NodeEditCommand onto the undo stack.
        await App.NodeIncreaseSize.ClickAsync();
        await Expect(await App.OpenSubMenuItemAsync("menu-edit", "menu-undo")).Not.ToHaveClassAsync(Disabled);
        await Expect(App.MenuItem("menu-redo")).ToHaveClassAsync(Disabled);

        // Undo reverts it: undo becomes unavailable, redo becomes available.
        await App.MenuItem("menu-undo").ClickAsync();
        await Expect(await App.OpenSubMenuItemAsync("menu-edit", "menu-undo")).ToHaveClassAsync(Disabled);
        await Expect(App.MenuItem("menu-redo")).Not.ToHaveClassAsync(Disabled);
    }
}
