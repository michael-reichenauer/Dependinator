using System.Text.RegularExpressions;
using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exercises the manual "design" flow through the browser: add a user-drawn node via the app menu
// ("Add Node …" arms placing mode → click canvas) or by double-clicking empty canvas — both open
// the icon selector, and the node is created with the picked icon, named after it — that it
// survives a reload (persisted with IsManual), and that only manual nodes expose the Delete
// action. Editing is enabled by default in the Blazor Server host.
public class ManualEditTests(ITestOutputHelper output) : E2ETestBase(output)
{
    // Each test picks a distinct icon; the added node is named after the icon's display name.
    const string NodeIcon = "Cache";
    const string MenuNodeIcon = "Queue";
    const string LinkSourceIcon = "Database";
    const string LinkTargetIcon = "Storage";

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedViaMenu_AtClickedPosition()
    {
        await App.GotoMainPageAsync();

        // "Add Node …" arms placing mode; the next canvas click begins the add there.
        ILocator addNode = await App.OpenSubMenuItemAsync("menu-edit", "menu-add-node");
        await addNode.ClickAsync();

        // Wait until placing mode is armed (the prompt snackbar). This also ensures the menu
        // overlay has closed, so the next canvas click lands on the diagram, not the menu.
        await Expect(Page.GetByText("Click on the diagram to place the node.")).ToBeVisibleAsync();

        // Click an empty area (bottom-left) to place the node there.
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");
        await Page.Mouse.ClickAsync(box.X + 150, box.Y + box.Height - 130);

        await PickIconAsync(MenuNodeIcon);

        await Expect(App.NodeLabel(MenuNodeIcon)).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedByDoubleClick_AndPersistAcrossReload()
    {
        await App.GotoMainPageAsync();

        await AddManualNodeAsync(NodeIcon);
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();

        // The debounced save writes to IndexedDB; give it a moment, then reload.
        await Page.WaitForTimeoutAsync(1500);
        await App.GotoMainPageAsync();

        // A manual node is not produced by parsing, so surviving the reload proves it was
        // persisted (with IsManual) and kept by the re-parse reconciliation.
        await Expect(App.NodeLabel(NodeIcon)).ToBeVisibleAsync();

        // Only manual nodes expose the Delete action (proves IsManual round-tripped).
        await App.SelectNodeByVisibleNameAsync(NodeIcon);
        await App.NodeToolbarMenu.ClickAsync();
        await Expect(Page.GetByTestId("node-menu-delete")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ManualLink_ShouldBeDeletableFromLineToolbar()
    {
        await App.GotoMainPageAsync();

        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        // Two manual nodes on empty canvas (bottom-left, horizontally apart) to link.
        await AddManualNodeAtAsync(LinkSourceIcon, box.X + 150, box.Y + box.Height - 130);
        await Expect(App.NodeLabel(LinkSourceIcon)).ToBeVisibleAsync();
        await AddManualNodeAtAsync(LinkTargetIcon, box.X + 450, box.Y + box.Height - 130);
        await Expect(App.NodeLabel(LinkTargetIcon)).ToBeVisibleAsync();

        // Draw the link: select the source, arm add-link mode, click the target. The app
        // treats any two clicks within 300ms as a double-click (PointerEventService), which
        // would start an add-node and cancel add-link mode — so pace the clicks apart.
        await App.SelectNodeByVisibleNameAsync(LinkSourceIcon);
        await PaceClicksAsync();
        await Page.GetByTestId("node-add-link").ClickAsync();
        await Expect(Page.GetByText("Click a target node to link to")).ToBeVisibleAsync();
        await PaceClicksAsync();
        await App.SelectNodeByVisibleNameAsync(LinkTargetIcon);

        // The manual link renders as a line between the two nodes. (Not ToBeVisible: a
        // horizontal line's group has a zero-height bounding box, which Playwright
        // considers not visible.)
        ILocator line = LineGroup(LinkSourceIcon, LinkTargetIcon);
        await Expect(line).ToHaveCountAsync(1);

        // Unselect the source node so its toolbar cannot cover the line, then select the line
        // by clicking its midpoint (the line group carries a wide invisible hit polyline).
        await PaceClicksAsync();
        await Page.Mouse.ClickAsync(box.X + 60, box.Y + box.Height - 40);
        LocatorBoundingBoxResult lineBox = await WaitForStableLineBoxAsync(line);
        await PaceClicksAsync();
        await Page.Mouse.ClickAsync(lineBox.X + lineBox.Width / 2, lineBox.Y + lineBox.Height / 2);

        // A line showing only a manual link exposes Delete; deleting removes the line.
        await Expect(Page.GetByTestId("line-delete")).ToBeVisibleAsync();
        await Page.GetByTestId("line-delete").ClickAsync();
        await Expect(line).ToHaveCountAsync(0);
    }

    // Waits out the app's 300ms double-click window (PointerEventService) so the next click
    // is not merged with the previous one into a double-click.
    Task PaceClicksAsync() => Page.WaitForTimeoutAsync(350);

    // Canvas re-renders (e.g. after an unselect click) can momentarily detach the line's SVG
    // group, so poll until its bounds exist and stop moving between two reads (same approach
    // as AppPage.WaitForStableNodeBoxAsync).
    async Task<LocatorBoundingBoxResult> WaitForStableLineBoxAsync(ILocator line, float timeoutSeconds = 15)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LocatorBoundingBoxResult? previous = null;

        while (stopwatch.Elapsed < timeout)
        {
            LocatorBoundingBoxResult? box = null;
            if (await line.CountAsync() > 0)
                box = await line.BoundingBoxAsync();

            bool isStable =
                box is not null
                && previous is not null
                && Math.Abs(box.X - previous.X) < 1
                && Math.Abs(box.Y - previous.Y) < 1;
            if (isStable)
                return box!;

            previous = box;
            await Task.Delay(100);
        }

        throw new InvalidOperationException($"Line did not render/stabilize within {timeout.TotalSeconds}s.");
    }

    // The SVG group of the dependency line between two nodes, matched by its
    // "source→target (n)" title. Anchored at the start so an ancestor group whose text
    // merely contains the line's title does not match too (same approach as AppPage.Node).
    ILocator LineGroup(string sourceName, string targetName) =>
        Page.Locator("#svgcanvas g.hoverable")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(sourceName)}→{Regex.Escape(targetName)}") });

    // Double-clicks an empty area of the canvas and picks an icon in the selector dialog.
    async Task AddManualNodeAsync(string iconName)
    {
        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        // Bottom-left quadrant, away from the centered demo nodes.
        await AddManualNodeAtAsync(iconName, box.X + 120, box.Y + box.Height - 120);
    }

    // Double-clicks the canvas at the given page position and picks an icon in the selector dialog.
    async Task AddManualNodeAtAsync(string iconName, float x, float y)
    {
        await Page.Mouse.DblClickAsync(x, y);
        await PickIconAsync(iconName);
    }

    // Picks an icon from the General tab of the opened icon selector dialog.
    async Task PickIconAsync(string iconName)
    {
        ILocator search = Page.GetByTestId("icon-dialog-search");
        await Expect(search).ToBeVisibleAsync();

        // Renders landing just after the dialog opens (e.g. the canvas still drawing a
        // previously added node) can detach the tab or row mid-click; retry with short
        // timeouts (same pattern as the AppPage menu helpers). The dialog closing is the
        // success signal — a pick that landed despite a click exception counts too.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await App.IconDialogTab("General").ClickAsync(new() { Timeout = 5_000 });
                await App.IconDialogItem(iconName).ClickAsync(new() { Timeout = 5_000 });
                await Expect(search).ToBeHiddenAsync(new() { Timeout = 5_000 });
                return;
            }
            catch (Exception e) when (e is TimeoutException or PlaywrightException && attempt < 3)
            {
                if (await search.IsHiddenAsync())
                    return;
            }
        }
    }
}
