using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises line labels through the browser: label the demo's top-level line from the line
// toolbar, see the text on the canvas, survive a reload (persisted with the line), edit it by
// double-clicking the line and remove it again.
public class LineLabelTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task LineLabel_ShouldBeAddedFromTheToolbar_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        await App.SelectLineAsync(App.Line("Demo.sln", "Externals"));
        await Page.GetByTestId("line-label").ClickAsync();
        await SaveLabelAsync("uses", "framework and packages");

        await Expect(LineLabel("uses")).ToBeVisibleAsync();
        await Expect(LineDescription("framework and packages")).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();

        await Expect(LineLabel("uses")).ToBeVisibleAsync();
        await Expect(LineDescription("framework and packages")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task LineLabel_ShouldBeEditableByDoubleClick_AndRemovable()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        ILocator line = App.Line("Demo.sln", "Externals");
        ILocator field = Page.GetByTestId("line-label-text");
        await App.DoubleClickLineAsync(line, field);
        await SaveLabelAsync("calls");
        await Expect(LineLabel("calls")).ToBeVisibleAsync();

        await App.DoubleClickLineAsync(line, field);
        await Page.GetByTestId("line-label-clear").ClickAsync();
        await Expect(LineLabel("calls")).ToHaveCountAsync(0);
    }

    ILocator LineLabel(string text) => Page.Locator("#svgcanvas text.lineLabel", new() { HasText = text });

    ILocator LineDescription(string text) => Page.Locator("#svgcanvas text.lineDescription", new() { HasText = text });

    async Task SaveLabelAsync(string label, string description = "")
    {
        ILocator field = Page.GetByTestId("line-label-text");
        await Expect(field).ToBeVisibleAsync();
        await App.FillReliablyAsync(field, label);
        if (description.Length > 0)
            await App.FillReliablyAsync(Page.GetByTestId("line-label-description"), description);
        await Page.GetByTestId("line-label-save").ClickAsync();
    }
}
