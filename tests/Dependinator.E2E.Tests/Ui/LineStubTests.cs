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

        // Clicking one of them selects it: that line stays (the others belonged to the node's
        // selection and are stubs again).
        await App.SelectLineAsync(
            Page.Locator("#svgcanvas g.line:has(polyline[marker-end='url(#arrow-cousin)']) g.hoverable").First
        );
        await Expect(App.CousinLines).ToHaveCountAsync(1);
    }

    [E2EFact]
    public async Task SelectingAStub_ShouldDrawOnlyThatStubsLines()
    {
        // Demo.Core.CloudSync is used from outside Demo.Core and uses things outside it, so it
        // has a stub on each side.
        const string CloudSync = "Demo.Core.CloudSync";
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(CloudSync);
        await App.DeselectAsync();
        await Expect(App.CousinLines).ToHaveCountAsync(0);
        ILocator incoming = App.Line("Demo.Core", CloudSync);
        ILocator outgoing = App.Line(CloudSync, "Demo.Core");
        await Expect(StubRing(incoming)).ToHaveCountAsync(1);
        await Expect(StubRing(outgoing)).ToHaveCountAsync(1);

        // The stub into the node stands for what uses it from outside Demo.Core. Selected, those
        // lines run from their sources to the node, and the stub stays as the selected line.
        await App.SelectLineAsync(incoming);
        ILocator linesIntoNode = Page.Locator(
            $"#svgcanvas g.line:has(polyline[marker-end='url(#arrow-cousin)']) g.hoverable[data-name*='→{CloudSync} (']"
        );
        await Expect(linesIntoNode).Not.ToHaveCountAsync(0);
        await Expect(StubRing(incoming)).ToHaveCountAsync(1);

        // The node's other stub is not part of it: nothing runs out of the node across the border.
        await Expect(App.CousinLinesFrom(CloudSync + "→")).ToHaveCountAsync(0);
        await Expect(StubRing(outgoing)).ToHaveCountAsync(1);

        // The stub's lines are lit, like a selected node's.
        await Expect(linesIntoNode.First.Locator("xpath=..").Locator("g.line-lit")).ToHaveCountAsync(1);

        // Deselected: stubs only.
        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.CousinLines).ToHaveCountAsync(0);
    }

    // The ring of the stub that the line (its hover group) is drawn as.
    static ILocator StubRing(ILocator line) => line.Locator("xpath=..").Locator("circle.line-stub-ring");
}
