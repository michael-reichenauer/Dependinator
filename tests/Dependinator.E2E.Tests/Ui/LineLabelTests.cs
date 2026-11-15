using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises line labels through the browser: label the demo's top-level line from the line
// toolbar, see the text on the canvas, survive a reload (persisted with the line), edit it by
// double-clicking the line and remove it again, and have an AI suggest one.
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

        // Wait for the debounced save, then reload.
        await App.WaitForSavedAsync();
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

    [E2EFact]
    public async Task LineLabel_ShouldBeSuggestedByAi_AndFillTheFields()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        await App.SelectLineAsync(App.Line("Demo.sln", "Externals"));
        await Page.GetByTestId("line-label").ClickAsync();
        ILocator field = Page.GetByTestId("line-label-text");
        await Expect(field).ToBeVisibleAsync();

        await Page.GetByTestId("line-label-expand").ClickAsync();
        await Page.GetByTestId("line-label-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("## The line");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        // In test mode the app answers for Claude: empty fields are filled at once.
        await Page.GetByTestId("line-label-suggest").ClickAsync();
        await Expect(Page.GetByTestId("line-label-suggested-label")).ToHaveTextAsync("test label");
        await Expect(field).ToHaveValueAsync("test label");
        await Expect(Page.GetByTestId("line-label-used")).ToBeVisibleAsync();

        // An alternative fills the label only, and the suggestion can be used again.
        await Page.GetByTestId("line-label-alternative").ClickAsync();
        await Expect(field).ToHaveValueAsync("test alternative");
        await Page.GetByTestId("line-label-use").ClickAsync();
        await Expect(field).ToHaveValueAsync("test label");

        await Page.GetByTestId("line-label-save").ClickAsync();
        await Expect(LineLabel("test label")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task LineLabel_ShouldSlideAlongItsLine_WhenDragged_AndGoBackWithTheDialog()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        ILocator line = App.Line("Demo.sln", "Externals");
        ILocator field = Page.GetByTestId("line-label-text");
        await App.DoubleClickLineAsync(line, field);
        await SaveLabelAsync("uses");
        await Expect(LineLabel("uses")).ToBeVisibleAsync();
        // Measured on the label's hit rect, what a press lands on: WebKit reports the bounds of
        // SVG text inside the canvas's nested tile SVGs at the wrong place, so a press at the
        // text's reported center missed the label there.
        ILocator label = Page.Locator("#svgcanvas rect.line-label-hit").First;
        float middleX = await LabelCenterXAsync(label);

        // The label takes the press (not the line under it) and follows the pointer along the line.
        LocatorBoundingBoxResult box = (await label.BoundingBoxAsync())!;
        float x = box.X + box.Width / 2;
        float y = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (int step = 1; step <= 10; step++)
            await Page.Mouse.MoveAsync(x + step * 4, y + 15, new() { Steps = 2 });
        await Page.Mouse.UpAsync();
        await WaitUntilAsync(async () => await LabelCenterXAsync(label) > middleX + 20, "the label moved right");

        // Double-clicking the label edits it, and "To the middle" puts it back.
        await App.RepeatUntilVisibleAsync(
            async () =>
            {
                LocatorBoundingBoxResult moved = (await label.BoundingBoxAsync())!;
                await Page.Mouse.DblClickAsync(moved.X + moved.Width / 2, moved.Y + moved.Height / 2);
            },
            field
        );
        await Page.GetByTestId("line-label-reset-position").ClickAsync();
        await Page.GetByTestId("line-label-save").ClickAsync();
        await WaitUntilAsync(
            async () => Math.Abs(await LabelCenterXAsync(label) - middleX) < 2,
            "the label is back in the middle"
        );
    }

    // The canvas repaints after the edit, so the label's place is read until it holds.
    static async Task WaitUntilAsync(Func<Task<bool>> condition, string what)
    {
        for (int i = 0; i < 50; i++)
        {
            if (await condition())
                return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Timed out waiting until {what}.");
    }

    static async Task<float> LabelCenterXAsync(ILocator label)
    {
        LocatorBoundingBoxResult? box = await label.BoundingBoxAsync();
        return box is null ? float.NaN : box.X + box.Width / 2;
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
