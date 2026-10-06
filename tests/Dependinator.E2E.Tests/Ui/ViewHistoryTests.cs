using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the view history (ViewHistoryService): a navigation jump records the view it left,
// Back returns there (toolbar button and Alt+Left), Forward re-applies the jump. Pan/zoom is not
// on the undo stack, so undo stays disabled throughout. The view is compared through the root
// node's on-screen size: the viewBox string also depends on which cached tile is drawn.
public class ViewHistoryTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task Back_ShouldReturnToTheViewBeforeASearchJump()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        ILocator back = App.MenuItem("toolbar-back");
        ILocator forward = App.MenuItem("toolbar-forward");
        ILocator undo = App.MenuItem("toolbar-undo");
        await Expect(back).ToBeDisabledAsync();
        await Expect(forward).ToBeDisabledAsync();

        float startWidth = await NodeWidthAsync("Demo.sln");

        // Jump to a node via search. Wait for a result row before Enter (Enter without results is
        // a no-op) and press it on the field itself so the dialog's key handler gets it.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI");
        await Expect(search.SelectedItem).ToBeVisibleAsync();
        await search.Field.PressAsync("Enter");

        await Expect(back).ToBeEnabledAsync();
        await Expect(undo).ToBeDisabledAsync(); // A jump is not an edit

        // Back animates to the recorded view; Forward becomes available.
        await back.ClickAsync();
        await Expect(back).ToBeDisabledAsync();
        await Expect(forward).ToBeEnabledAsync();
        await WaitForNodeWidthAsync("Demo.sln", startWidth);

        // Forward re-applies the jump, and Alt+Left goes back again.
        await forward.ClickAsync();
        await Expect(back).ToBeEnabledAsync();
        await Expect(forward).ToBeDisabledAsync();

        await Page.Keyboard.PressAsync("Alt+ArrowLeft");
        await Expect(back).ToBeDisabledAsync();
        await Expect(forward).ToBeEnabledAsync();
        await WaitForNodeWidthAsync("Demo.sln", startWidth);
    }

    async Task<float> NodeWidthAsync(string label)
    {
        LocatorBoundingBoxResult box =
            await App.Node(label).BoundingBoxAsync()
            ?? throw new InvalidOperationException($"Node '{label}' is not rendered.");
        return box.Width;
    }

    // The animation settles a moment after the buttons flip, so poll the size.
    async Task WaitForNodeWidthAsync(string label, float expectedWidth, float timeoutSeconds = 15)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        float width = -1;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            if (await App.Node(label).CountAsync() > 0 && await App.Node(label).BoundingBoxAsync() is { } box)
            {
                width = box.Width;
                if (Math.Abs(width - expectedWidth) < 2)
                    return;
            }
            await Task.Delay(200);
        }

        throw new InvalidOperationException($"Node '{label}' width {width} did not return to {expectedWidth}.");
    }
}
