using Dependinator.E2E.Tests.Shared;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the stubs (LineStub, LineSvg) and a selected stub's lines (RepLineService's
// LineReveal): the lines between a container's edge and its children are short arrows with a
// ring at the child, and selecting a stub draws its lines end to end across the containers.
// Selecting the node does not.
public class LineStubTests(ITestOutputHelper output) : E2ETestBase(output)
{
    const string Parsing = "Demo.Core.Parsing";

    [E2EFact]
    public async Task SelectingANode_ShouldLeaveItsStubs_AndSelectingAStubDrawItsLines()
    {
        await App.GotoMainPageAsync();

        // Demo.Core.Parsing is used from other assemblies and uses its sibling Utils, so its
        // container Demo.Core is open with edge lines to its children. The navigation selects
        // the node it lands on, which draws no line across a container's border: the stubs
        // (each with its ring) stay.
        await App.NavigateToNodeAsync(Parsing);
        ILocator rings = Page.Locator("#svgcanvas circle.line-stub-ring");
        ILocator incoming = App.Line("Demo.Core", Parsing);
        ILocator litIncoming = incoming.Locator("xpath=..").Locator("g.line-lit");
        await Expect(litIncoming).ToHaveCountAsync(1); // Drawn for the selection: its stub is lit
        await Expect(rings).Not.ToHaveCountAsync(0);
        await Expect(App.CousinLines).ToHaveCountAsync(0);

        // A stub is its line drawn short: the line between the container and the child is still
        // there, with the hit target a click needs.
        await Expect(StubRing(incoming)).ToHaveCountAsync(1);
        await Expect(AppPage.LineHitTarget(incoming)).ToHaveCountAsync(1);

        // The same with nothing selected, and with the node selected by a click.
        await App.DeselectAsync();
        await Expect(litIncoming).ToHaveCountAsync(0);
        await Expect(App.CousinLines).ToHaveCountAsync(0);
        await App.SelectNodeByFullNameAsync(Parsing);
        await Expect(litIncoming).ToHaveCountAsync(1);
        await Expect(StubRing(incoming)).ToHaveCountAsync(1);
        await Expect(App.CousinLines).ToHaveCountAsync(0);

        // The stub selected: its lines run from the nodes in the other containers.
        await App.SelectLineAsync(incoming);
        await Expect(App.CousinLines).Not.ToHaveCountAsync(0);

        // Clicking one of them selects it: that line stays (the others belonged to the stub's
        // selection and are gone again).
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

        // The stub's lines are lit, as the selection's own.
        await Expect(linesIntoNode.First.Locator("xpath=..").Locator("g.line-lit")).ToHaveCountAsync(1);

        // Deselected: stubs only.
        await Page.Keyboard.PressAsync("Escape");
        await Expect(App.CousinLines).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task SelectingAHubsStub_ShouldDrawOneLinePerFarContainerOutOfView()
    {
        // Demo.Core.Utils is used all over the demo. Framed, the other assemblies are far out of
        // the view: its stub's lines end at each of them (Demo.UI's 553 links on one line),
        // instead of running to every node inside them.
        const string Utils = "Demo.Core.Utils";
        await App.GotoMainPageAsync();
        await App.NavigateToNodeAsync(Utils);
        await App.DeselectAsync();

        await App.SelectLineAsync(App.Line("Demo.Core", Utils));
        await Expect(App.Line("Demo.UI (dll)", Utils)).ToHaveCountAsync(1);
        await Expect(App.CousinLinesFrom("Demo.UI.")).ToHaveCountAsync(0);
    }

    // The ring of the stub that the line (its hover group) is drawn as.
    static ILocator StubRing(ILocator line) => line.Locator("xpath=..").Locator("circle.line-stub-ring");
}
