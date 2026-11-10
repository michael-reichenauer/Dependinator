using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Suggest Line Labels through the browser: an AI (the app answers for Claude in test mode) labels
// the demo's unlabeled lines in one answer, the user unticks one, and the rest are added as one
// undoable step.
public class LineLabelsTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task SuggestLineLabels_ShouldListLabelsToTick_AndAddTheTickedOnesAsOneStep()
    {
        await App.GotoMainPageAsync();
        await App.WaitForModelRenderedAsync();
        await App.EnableEditModeAsync();

        var total = await OpenAsync();
        ILocator add = Page.GetByTestId("line-labels-add");
        await Expect(add).ToHaveCountAsync(0);

        await Page.GetByTestId("line-labels-show-prompt").ClickAsync();
        await Expect(Page.GetByTestId("prompt-dialog-text")).ToContainTextAsync("## The lines");
        await Page.GetByTestId("prompt-dialog-close").ClickAsync();

        // In test mode the app answers for Claude: a label per line, and one for no line.
        await Page.GetByTestId("line-labels-suggest").ClickAsync();
        await Expect(Page.GetByTestId("line-labels-answer")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("line-labels-left-out")).ToContainTextAsync("1 label");
        ILocator items = Page.GetByTestId("line-labels-item");
        var count = await items.CountAsync();
        Xunit.Assert.True(count >= 3, $"Expected several labels, got {count}");
        await Expect(items.Nth(0).GetByTestId("line-labels-label")).ToHaveTextAsync("test label 1");
        await Expect(add).ToHaveTextAsync($"Add {count} labels");

        // None, then the first one again; then all but the second, which are added.
        await Page.GetByTestId("line-labels-none").ClickAsync();
        await Expect(add).ToBeDisabledAsync();
        await items.Nth(0).GetByTestId("line-labels-tick").CheckAsync();
        await Expect(add).ToHaveTextAsync("Add 1 label");
        // Wait for All to land: until then the second box is still unticked, and unticking it
        // would do nothing.
        await Page.GetByTestId("line-labels-all").ClickAsync();
        await Expect(add).ToHaveTextAsync($"Add {count} labels");
        await items.Nth(1).GetByTestId("line-labels-tick").UncheckAsync();
        await Expect(add).ToHaveTextAsync($"Add {count - 1} labels");
        await Page.GetByTestId("line-labels-descriptions").CheckAsync();
        await add.ClickAsync();
        await Expect(add).ToHaveCountAsync(0);

        // The labeled lines are no longer asked about.
        Xunit.Assert.Equal(total - (count - 1), await OpenAsync());
        await Page.GetByTestId("line-labels-cancel").ClickAsync();
        await Expect(add).ToHaveCountAsync(0);

        // One undo removes them all.
        await (await App.OpenSubMenuItemAsync("menu-edit", "menu-undo")).ClickAsync();
        Xunit.Assert.Equal(total, await OpenAsync());
    }

    // Opens the dialog from the Edit menu; the number of lines without a label in the model.
    async Task<int> OpenAsync()
    {
        await (await App.OpenSubMenuItemAsync("menu-edit", "menu-suggest-line-labels")).ClickAsync();
        ILocator intro = Page.GetByTestId("line-labels-intro");
        await Expect(intro).ToContainTextAsync("without a label in the model");
        return int.Parse(await intro.GetAttributeAsync("data-total") ?? "");
    }
}
