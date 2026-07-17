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
    // (Names must also not collide with labels in the demo model, e.g. "Api" or "Logging".)
    const string NodeIcon = "Cache";
    const string MenuNodeIcon = "Queue";
    const string LinkSourceIcon = "Database";
    const string LinkTargetIcon = "Storage";
    const string DragSourceIcon = "Messaging";
    const string DragTargetIcon = "Scheduler";
    const string DragCancelIcon = "Analytics";
    const string DragCanvasIcon = "Security";

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

        // Draw the link by dragging the source node's link handle onto the target node.
        LocatorBoundingBoxResult targetBox = await WaitForStableNodeBoxAsync(LinkTargetIcon);
        await HoverLinkHandleAsync(LinkSourceIcon);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(
            targetBox.X + targetBox.Width / 2,
            targetBox.Y + targetBox.Height / 2,
            new() { Steps = 10 }
        );
        await Page.Mouse.UpAsync();

        // The manual link renders as a line between the two nodes. (Not ToBeVisible: a
        // horizontal line's group has a zero-height bounding box, which Playwright
        // considers not visible.)
        ILocator line = LineGroup(LinkSourceIcon, LinkTargetIcon);
        await Expect(line).ToHaveCountAsync(1);

        // Unselect the source node so its toolbar cannot cover the line, then select the line
        // by clicking its midpoint (the line group carries a wide invisible hit polyline).
        await Page.Mouse.ClickAsync(box.X + 60, box.Y + box.Height - 40);
        LocatorBoundingBoxResult lineBox = await WaitForStableLineBoxAsync(line);
        await Page.Mouse.ClickAsync(lineBox.X + lineBox.Width / 2, lineBox.Y + lineBox.Height / 2);

        // A line showing only a manual link exposes Delete; deleting removes the line.
        await Expect(Page.GetByTestId("line-delete")).ToBeVisibleAsync();
        await Page.GetByTestId("line-delete").ClickAsync();
        await Expect(line).ToHaveCountAsync(0);
    }

    [E2EFact]
    public async Task ManualLink_ShouldBeCreatedByDraggingLinkHandle()
    {
        await App.GotoMainPageAsync();

        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        // Two manual nodes on empty canvas (bottom-left, horizontally apart) to link.
        await AddManualNodeAtAsync(DragSourceIcon, box.X + 150, box.Y + box.Height - 130);
        await Expect(App.NodeLabel(DragSourceIcon)).ToBeVisibleAsync();
        await AddManualNodeAtAsync(DragTargetIcon, box.X + 450, box.Y + box.Height - 130);
        await Expect(App.NodeLabel(DragTargetIcon)).ToBeVisibleAsync();

        LocatorBoundingBoxResult targetBox = await WaitForStableNodeBoxAsync(DragTargetIcon);

        // Drag the source node's link handle onto the target: hovering the node reveals the
        // handle, pressing it and moving draws the dotted preview line, releasing over the
        // target creates the manual link.
        await HoverLinkHandleAsync(DragSourceIcon);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(
            targetBox.X + targetBox.Width / 2,
            targetBox.Y + targetBox.Height / 2,
            new() { Steps = 10 }
        );

        // The dotted preview overlay is rendered while the drag is active.
        await Expect(Page.Locator(".link-drag-overlay")).ToBeVisibleAsync();

        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(".link-drag-overlay")).ToBeHiddenAsync();
        await Expect(LineGroup(DragSourceIcon, DragTargetIcon)).ToHaveCountAsync(1);
    }

    [E2EFact]
    public async Task ManualNode_ShouldBeAddedAndLinked_WhenLinkHandleDroppedOnEmptyCanvas()
    {
        await App.GotoMainPageAsync();

        LocatorBoundingBoxResult box =
            await App.Canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("Canvas is not rendered.");

        await AddManualNodeAtAsync(DragCancelIcon, box.X + 150, box.Y + box.Height - 130);
        await Expect(App.NodeLabel(DragCancelIcon)).ToBeVisibleAsync();

        // Dropping the link handle on empty canvas opens the icon selector (a new node will be
        // added at the drop point and linked to). Escape cancels the whole gesture: no node
        // and no link are created.
        (float pressX, float pressY) = await HoverLinkHandleAsync(DragCancelIcon);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(pressX + 200, pressY - 100, new() { Steps = 10 });
        await Expect(Page.Locator(".link-drag-overlay")).ToBeVisibleAsync();
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator(".link-drag-overlay")).ToBeHiddenAsync();

        ILocator search = Page.GetByTestId("icon-dialog-search");
        await Expect(search).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(search).ToBeHiddenAsync();
        ILocator anyLineFromNode = Page.Locator("#svgcanvas g.hoverable")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(DragCancelIcon)}→") });
        await Expect(anyLineFromNode).ToHaveCountAsync(0);

        // Dropping again and picking an icon creates the new node and the link to it.
        (pressX, pressY) = await HoverLinkHandleAsync(DragCancelIcon);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(pressX + 200, pressY - 100, new() { Steps = 10 });
        await Page.Mouse.UpAsync();
        await PickIconAsync(DragCanvasIcon);

        await Expect(App.NodeLabel(DragCanvasIcon)).ToBeVisibleAsync();
        await Expect(LineGroup(DragCancelIcon, DragCanvasIcon)).ToHaveCountAsync(1);
    }

    // Hovers the node to reveal its drag-to-link handle, then moves onto the handle's visible
    // dot and returns the press point. The handle's invisible touch ellipse bridges from the
    // icon edge to the dot, so the handle stays hover-revealed (and pointer-events enabled)
    // while the cursor travels there.
    async Task<(float X, float Y)> HoverLinkHandleAsync(string nodeLabel)
    {
        LocatorBoundingBoxResult nodeBox = await WaitForStableNodeBoxAsync(nodeLabel);
        await Page.Mouse.MoveAsync(nodeBox.X + nodeBox.Width / 2, nodeBox.Y + nodeBox.Height / 2);

        ILocator handleDot = App.Node(nodeLabel).Locator("g.linkhandle circle");
        LocatorBoundingBoxResult handleBox =
            await handleDot.BoundingBoxAsync() ?? throw new InvalidOperationException("Link handle is not rendered.");

        float pressX = handleBox.X + handleBox.Width / 2;
        float pressY = handleBox.Y + handleBox.Height / 2;
        await Page.Mouse.MoveAsync(pressX, pressY, new() { Steps = 3 });
        return (pressX, pressY);
    }

    // Same stability polling as AppPage.WaitForStableNodeBoxAsync (private there): canvas
    // re-renders after adding nodes can momentarily move or detach the group.
    async Task<LocatorBoundingBoxResult> WaitForStableNodeBoxAsync(string label, float timeoutSeconds = 15)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LocatorBoundingBoxResult? previous = null;

        while (stopwatch.Elapsed < timeout)
        {
            LocatorBoundingBoxResult? box = null;
            if (await App.Node(label).CountAsync() > 0)
                box = await App.Node(label).BoundingBoxAsync();

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

        throw new InvalidOperationException($"Node '{label}' did not render/stabilize within {timeout.TotalSeconds}s.");
    }

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
