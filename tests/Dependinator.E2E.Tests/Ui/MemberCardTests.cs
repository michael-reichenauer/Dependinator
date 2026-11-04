using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The member card of an open type: rows with signatures, and a large type's private rows folded
// into a "+ N private" row that a click shows and hides.
public class MemberCardTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task PrivateToggle_ShouldShowAndFoldThePrivateRows()
    {
        await App.GotoMainPageAsync();

        // AskPanel (demo model) has a few public methods and many private members.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI.Diagrams.Ask.AskPanel.Dispose()");
        await search.Results.First.ClickAsync();

        var toggle = Page.Locator("#svgcanvas [data-testid='private-toggle'][data-node='AskPanel']").First;
        await Expect(toggle).ToHaveAttributeAsync("data-folded", "true", new() { Timeout = 20000 });
        await Expect(toggle).ToHaveTextAsync(new Regex(@"^\+ \d+ private"));

        // A public row shows its signature after the name.
        await Expect(Page.Locator("#svgcanvas text.memberName[data-name='Dispose']").First).ToContainTextAsync("()");

        // A private row is folded: not drawn until the toggle is clicked.
        var privateRow = Page.Locator("#svgcanvas text.memberName[data-name='ScrollToEndAsync']");
        await Expect(privateRow).ToHaveCountAsync(0);

        await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("data-folded", "false");
        await Expect(toggle).ToHaveTextAsync(new Regex(@"^hide \d+ private"));
        await Expect(privateRow.First).ToBeVisibleAsync();

        // The view keeps the toggle in place, so a second click folds the rows again (after the
        // double-click window, or it is a double-click, which does not toggle).
        await Page.WaitForTimeoutAsync(400);
        await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("data-folded", "true");
        await Expect(privateRow).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task ShowNode_ShouldOpenTheFoldedPrivateRows_OfAPrivateMember()
    {
        await App.GotoMainPageAsync();

        // ScrollToEndAsync is private, folded into AskPanel's "+ N private" row.
        var search = await App.OpenSearchViaHotkeyAsync();
        await search.FillAsync("Demo.UI.Diagrams.Ask.AskPanel.ScrollToEndAsync()");
        await search.Results.First.ClickAsync();

        await Expect(Page.Locator("#svgcanvas text.memberName[data-name='ScrollToEndAsync']").First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        var toggle = Page.Locator("#svgcanvas [data-testid='private-toggle'][data-node='AskPanel']").First;
        await Expect(toggle).ToHaveAttributeAsync("data-folded", "false");
    }
}
