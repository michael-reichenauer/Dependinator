using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// The hover card of a node or line (TooltipService, initTooltips in jsInterop.js): the tiles carry
// no tooltip, and the card is built only when the mouse rests on an item; a key hides it.
public class TooltipTests(ITestOutputHelper output) : E2ETestBase(output)
{
    ILocator Card => Page.GetByTestId("diagram-tooltip");

    [E2EFact]
    public async Task RestingOnALineOrNode_ShouldShowItsCard()
    {
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(AppPage.InsideMain);
        await App.WaitForContainerNodeAsync("Main");
        await App.DeselectAsync();
        await Expect(Page.Locator("#svgcanvas g.hoverable > title")).ToHaveCountAsync(0);

        ILocator line = App.Line("Demo.UI.Main", "Demo.UI.Shared");
        await App.HoverLineAsync(line);
        await Expect(Card).ToBeVisibleAsync();
        await Expect(Card).ToContainTextAsync("Main→Shared");
        await Expect(Card).ToContainTextAsync("link");

        // A key hides it (a press too, but that would select the line and open its links).
        await Page.Keyboard.PressAsync("Shift");
        await Expect(Card).ToBeHiddenAsync();

        LocatorBoundingBoxResult box = (await App.Node(AppPage.InsideMain).BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 - 2, box.Y + box.Height / 2);
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Expect(Card).ToBeVisibleAsync();
        await Expect(Card).ToContainTextAsync("Method in Demo.UI.Main");
    }
}
