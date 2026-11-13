using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the stubs (LineStub, LineSvg) and the selection's lines (RepLineService's
// LineReveal): the lines between a container's edge and its children are short arrows with a
// ring at the child, and selecting a node draws its lines end to end across the containers.
public class LineStubTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string Parsing = "Demo.Core.Parsing";

    [E2EFact]
    public async Task SelectingAChild_ShouldDrawItsLinesEndToEnd_AndStubsAtRest()
    {
        await App.GotoMainPageAsync();

        // Demo.Core.Parsing is used from other assemblies and uses its sibling Utils, so its
        // container Demo.Core is open with edge lines to its children. The navigation selects
        // the node it lands on: its lines run to the nodes in the other containers.
        await App.NavigateToNodeAsync(Parsing);
        await Expect(App.CousinLines).Not.ToHaveCountAsync(0);

        // At rest: stubs (each with its ring), and no line crosses a container's border.
        await App.DeselectAsync();
        ILocator rings = Page.Locator("#svgcanvas circle.line-stub-ring");
        await Expect(App.CousinLines).ToHaveCountAsync(0);
        await Expect(rings).Not.ToHaveCountAsync(0);

        // A stub is its line drawn short: the line between the container and the child is still
        // there, with the hit target a click needs.
        ILocator incoming = App.Line("Demo.Core", Parsing);
        await Expect(incoming.Locator("xpath=..").Locator("circle.line-stub-ring")).ToHaveCountAsync(1);
        await Expect(AppPage.LineHitTarget(incoming)).ToHaveCountAsync(1);

        // Selected again: the lines are back.
        await App.SelectNodeByFullNameAsync(Parsing);
        await Expect(App.CousinLines).Not.ToHaveCountAsync(0);
    }
}
