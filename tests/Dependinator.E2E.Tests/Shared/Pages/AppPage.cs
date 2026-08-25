using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Dependinator.E2E.Tests.Shared.Pages;

// Page object for the main app — the diagram canvas plus the toolbar/menu (AppBar.razor).
// Wraps an IPage with stable data-testid locators and the common flows, so tests read as
// intent rather than selectors. Get one from E2ETestBase.App.
public sealed class AppPage
{
    private readonly IPage page;

    public AppPage(IPage page) => this.page = page;

    // Toolbar / menu hooks (see AppBar.razor data-testid attributes).
    public ILocator Menu => page.GetByTestId("appbar-menu");
    public ILocator SearchButton => page.GetByTestId("toolbar-search");
    public ILocator Canvas => page.Locator("#svgcanvas");

    public ILocator MenuItem(string testId) => page.GetByTestId(testId);

    // A rendered node label on the canvas (e.g. the demo root "Demo.sln"). Targets the
    // visible label element rather than GetByText, which also matches hidden <title>s.
    // (Node group SVG ids are generated, so the label is the stable handle to a node.)
    // The label's class depends on how the node currently renders — icon (iconName),
    // container header (nodeName), or member (memberName) — and zoom level decides which,
    // so match all three (see NodeSvg.cs).
    public ILocator NodeLabel(string text) => page.Locator(NodeLabelSelector, new() { HasText = text }).First;

    const string NodeLabelSelector = "#svgcanvas text.iconName, #svgcanvas text.nodeName, #svgcanvas text.memberName";

    // A diagram node's group element, matched by its label. (Node SVG ids are generated, so
    // match on the label text, which comes from the group's <title>.) The title is the node's
    // long name optionally followed by its description ("longName\n\ndescription", see
    // NodeSvg.BuildHoverGroup), so anchor at the start and require a whitespace/end boundary
    // after the label rather than matching to end-of-text. This still gives an exact name match
    // — "Demo.Core" won't match the "Demo.Core.RootClass" group (a '.' follows, not whitespace),
    // and "Demo.sln" won't match the "Demo.sln→Externals (…)" dependency line — while tolerating
    // the description that a node's title may now carry.
    public ILocator Node(string label) =>
        page.Locator("#svgcanvas g.hoverable")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(label)}(\s|$)") });

    // The selected-node context toolbar (NodeToolbar.razor) — its menu activator is
    // present whenever a node is selected.
    public ILocator NodeToolbarMenu => page.GetByTestId("node-menu");

    // The selected-node toolbar's References/Dependencies buttons (NodeToolbar.razor).
    public ILocator NodeReferencesButton => page.GetByTestId("node-references");
    public ILocator NodeDependenciesButton => page.GetByTestId("node-dependencies");

    // The grow/shrink buttons, shown on the node toolbar only while edit mode is enabled.
    public ILocator NodeIncreaseSize => page.GetByTestId("node-increase-size");
    public ILocator NodeDecreaseSize => page.GetByTestId("node-decrease-size");

    // The node toolbar's color swatch dropdown (NodeToolbar.razor): icon tint while the node
    // shows as an icon, container background while it shows as a container. The testid sits on
    // the MudMenu root, so click its inner activator button.
    public ILocator NodeSetColorButton => page.GetByTestId("node-set-color").Locator("button");

    // A swatch row in the icon-tint dropdown, e.g. "Blue" or "Default".
    public ILocator IconColorItem(string color) => page.GetByTestId($"icon-color-item-{color}");

    // A swatch row in the container-color dropdown, e.g. "Blue" or "Default".
    public ILocator ColorItem(string color) => page.GetByTestId($"color-item-{color}");

    // The node toolbar's "Set icon" button (NodeToolbar.razor), opening IconSelectorDialog.
    public ILocator NodeSetIconButton => page.GetByTestId("node-set-icon");

    // A group tab in the icon selector dialog, e.g. "Azure" or "Aws" (IconSelectorDialog.razor).
    public ILocator IconDialogTab(string group) => page.GetByTestId($"icon-dialog-tab-{group}");

    // An icon row in the icon selector dialog, e.g. "Key-Vault".
    public ILocator IconDialogItem(string iconName) => page.GetByTestId($"icon-dialog-item-{iconName}");

    // The pinned "Default (node type icon)" row in the icon selector dialog.
    public ILocator IconDialogDefault => page.GetByTestId("icon-dialog-default");

    // A node icon's <use> reference on the canvas, e.g. "Solution" or "Solution--Blue".
    public ILocator NodeIconUse(string iconId) => page.Locator($"#svgcanvas use[href='#{iconId}']");

    // The toolbar edit-mode toggle (AppBar.razor). Toggles NodeSvg.IsEditingEnabled.
    public ILocator ToolbarEdit => page.GetByTestId("toolbar-edit");

    // The cloud sync/auth button (AppBar.razor). Clicking it while signed out starts login.
    public ILocator CloudButton => page.GetByTestId("toolbar-cloud");

    // A MudBlazor dialog (NodeProperties / MudMessageBox) rendered as role="dialog".
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    // The dependencies/references explorer tree (DependenciesTree.razor popover). The tree
    // renders nested .mud-treeview lists, so take the outermost (first) one.
    public ILocator DependenciesTree => page.Locator(".mud-treeview").First;

    // The explorer popover's header buttons (DependenciesTree.razor).
    public ILocator ExplorerReferencesButton => page.GetByTestId("explorer-references");
    public ILocator ExplorerDependenciesButton => page.GetByTestId("explorer-dependencies");
    public ILocator ExplorerCloseButton => page.GetByTestId("explorer-close");

    // Close the dependencies/references explorer, retrying until the tree is really gone: the
    // close button sits inside the popover, so a click landing while the popover re-renders is
    // swallowed and leaves it open.
    public async Task CloseExplorerAsync()
    {
        for (int attempt = 1; ; attempt++)
        {
            await ExplorerCloseButton.ClickAsync(new() { Timeout = MenuAttemptTimeout });
            try
            {
                await DependenciesTree.WaitForAsync(
                    new() { State = WaitForSelectorState.Hidden, Timeout = MenuAttemptTimeout }
                );
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts) { }
        }
    }

    // Menu open/hover steps retry a few short attempts instead of one long wait: the flaky
    // failure mode is a swallowed click or hover. MudBlazor opens menus via a server roundtrip
    // behind a popover overlay, so a click is eaten when a previous popover is still closing, or
    // when the activator is re-rendered under the pointer (the node toolbar re-renders on every
    // canvas update, and its position follows the node). Only another click can recover from
    // that — a longer wait cannot, because nothing is pending.
    const int MenuAttempts = 3;
    const float MenuAttemptTimeout = 5_000;

    static bool IsRetryable(Exception e) => e is TimeoutException or PlaywrightException;

    // Click a MudBlazor menu activator until its popover really shows the expected item. Use
    // this for every menu/dropdown rather than a bare ClickAsync: Playwright reports a
    // swallowed click as successful, so the failure only surfaces later as the popover content
    // never appearing.
    public Task OpenMenuAsync(ILocator activator, ILocator expectedItem) =>
        OpenMenuAsync(activator, expectedItem, MenuAttempts);

    async Task OpenMenuAsync(ILocator activator, ILocator expectedItem, int attempts)
    {
        for (int attempt = 1; ; attempt++)
        {
            // Already showing — a previous step can leave the menu open, and clicking the
            // activator again would toggle it shut instead of opening it.
            if (await expectedItem.IsVisibleAsync())
                return;

            try
            {
                await activator.ClickAsync(new() { Timeout = MenuAttemptTimeout });
                await expectedItem.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < attempts)
            {
                await ResetPopoversAsync();
            }
        }
    }

    // Dismiss any half-open popover and park the pointer off the toolbar before clicking again:
    // a popover still fading out swallows the next click, and a lingering tooltip popover can
    // cover the activator entirely.
    async Task ResetPopoversAsync()
    {
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.MoveAsync(0, 0);
    }

    // Open the app menu and return a menu-item locator by its data-testid.
    public async Task<ILocator> OpenMenuItemAsync(string testId)
    {
        ILocator item = MenuItem(testId);
        await OpenMenuAsync(Menu, item);
        return item;
    }

    // Open the app menu, hover the parent submenu to expand it, and return the nested
    // menu-item locator by its data-testid (nested MudMenu flyouts open on hover). The hover
    // retries too: the parent item can be re-rendered (detached) mid-hover, or the flyout can
    // fail to open when the hover races the menu's own opening render.
    public async Task<ILocator> OpenSubMenuItemAsync(string parentTestId, string testId)
    {
        ILocator parent = MenuItem(parentTestId);
        ILocator item = MenuItem(testId);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                // Re-open the app menu on every attempt instead of only re-hovering: the menu
                // can close again right after it opened — a re-render, or a click that only
                // looked successful because the item was still visible while the previous
                // popover faded out — and re-hovering a parent that is gone never recovers.
                await OpenMenuAsync(Menu, parent, attempts: 1);
                await parent.HoverAsync(new() { Timeout = MenuAttemptTimeout });
                await item.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return item;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts)
            {
                await ResetPopoversAsync();
            }
        }
    }

    // Repeat a canvas gesture until it produces the expected UI. A pointer gesture landing while
    // the canvas re-renders is swallowed silently — nothing opens, and nothing reports an error
    // — so the only way to tell it actually arrived is to look for its result and gesture again.
    public async Task RepeatUntilVisibleAsync(Func<Task> gesture, ILocator expected)
    {
        for (int attempt = 1; ; attempt++)
        {
            await gesture();
            try
            {
                await expected.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts) { }
        }
    }

    // Open the selected node's context menu (NodeToolbar.razor) and return one of its items.
    public async Task<ILocator> OpenNodeMenuItemAsync(string testId)
    {
        ILocator item = MenuItem(testId);
        await OpenMenuAsync(NodeToolbarMenu, item);
        return item;
    }

    // Open the node toolbar's colour dropdown and return a container-background swatch row.
    public async Task<ILocator> OpenColorItemAsync(string color)
    {
        ILocator item = ColorItem(color);
        await OpenMenuAsync(NodeSetColorButton, item);
        return item;
    }

    // Open the node toolbar's colour dropdown and return an icon-tint swatch row.
    public async Task<ILocator> OpenIconColorItemAsync(string color)
    {
        ILocator item = IconColorItem(color);
        await OpenMenuAsync(NodeSetColorButton, item);
        return item;
    }

    // Pick a swatch from the node toolbar's colour dropdown. A click on a row inside a
    // MudBlazor popover is swallowed just like a click that opens one — observed as the colour
    // simply not being applied — so re-open and click again until the popover closes, which is
    // what a handled pick does.
    public Task PickColorItemAsync(string color) => PickSwatchAsync(color, isIconTint: false);

    // The icon-tint equivalent of PickColorItemAsync (same dropdown, icon-mode contents).
    public Task PickIconColorItemAsync(string color) => PickSwatchAsync(color, isIconTint: true);

    async Task PickSwatchAsync(string color, bool isIconTint)
    {
        for (int attempt = 1; ; attempt++)
        {
            ILocator item = isIconTint ? await OpenIconColorItemAsync(color) : await OpenColorItemAsync(color);
            await item.ClickAsync(new() { Timeout = MenuAttemptTimeout });
            try
            {
                await item.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts) { }
        }
    }

    // Open the icon selector dialog from the node toolbar's "Set icon" button. The dialog's
    // search field is the open marker — the pinned "Default" row is only offered for nodes
    // that can fall back to a type icon.
    public Task OpenIconSelectorAsync() => OpenMenuAsync(NodeSetIconButton, IconDialogSearch);

    // The icon selector dialog's search field (IconSelectorDialog.razor); present exactly
    // while the dialog is open.
    public ILocator IconDialogSearch => page.GetByTestId("icon-dialog-search");

    // Select a diagram node by its exact group label (the *full* node name, e.g.
    // "Demo.Core.RootClass"). We click via the mouse at the computed coordinates rather than
    // Locator.ClickAsync because the SVG canvas re-renders constantly (which fails
    // Playwright's stability check). Selecting a node shows its context toolbar
    // (NodeToolbarMenu). See also SelectNodeByVisibleNameAsync for the short on-screen name.
    public async Task SelectNodeByFullNameAsync(string label)
    {
        await ClickUntilSelectedAsync(async () =>
        {
            LocatorBoundingBoxResult box = await WaitForStableNodeBoxAsync(label);
            return [box.X + box.Width / 2, box.Y + box.Height / 2];
        });
    }

    // Candidate click points inside a container's header strip, as (fraction of width, pixels
    // down from its top edge). The header is the band no child covers, but it also carries the
    // node's icon and name, which are painted over the hover rect and carry no element id — a
    // click on either selects nothing — so the points spread across the strip instead of
    // targeting one spot.
    static readonly (float WidthFraction, float Y)[] ContainerClickPoints =
    [
        (0.6f, 14),
        (0.0f, 20),
        (0.8f, 8),
        (0.4f, 24),
    ];

    // Select a container node by clicking inside it, above its children. The click has to land
    // on the container's own hover rect: a container's area is covered by its children's rects,
    // and the app resolves a click through event.target.id (jsInterop.js), so a point over a
    // child selects that child instead. The toolbar then renders in icon mode, without the
    // container-only affordances, which otherwise only surfaces later as a missing button.
    // How tall the header strip is depends on the zoom, so try a few points and keep the one
    // that actually selected a container — the edit pencil is the container-only marker (this
    // suite runs against the Blazor Server host, where editing is always enabled).
    public async Task SelectContainerNodeAsync(string label, float timeoutSeconds = 15)
    {
        for (int attempt = 0; ; attempt++)
        {
            LocatorBoundingBoxResult box = await WaitForStableNodeBoxAsync(label, timeoutSeconds);
            var (widthFraction, y) = ContainerClickPoints[attempt];

            // A zoomed-in container is wider than the window, so keep the point inside the part
            // of it that is actually on screen — Mouse.ClickAsync outside the viewport hits
            // nothing at all.
            float left = Math.Max(box.X, 0) + 20;
            float right = Math.Min(box.X + box.Width, page.ViewportSize?.Width ?? float.MaxValue) - 20;
            float x = Math.Clamp(left + (right - left) * widthFraction, left, Math.Max(left, right));
            await page.Mouse.ClickAsync(x, Math.Max(box.Y, 0) + y);

            try
            {
                await MenuItem("node-edit").WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt + 1 < ContainerClickPoints.Length)
            {
                // Drop the wrong selection so the next click starts from a clean toolbar.
                await page.Keyboard.PressAsync("Escape");
            }
        }
    }

    // Wait until a node renders as an expanded container rather than as an icon. Which one it
    // is depends on the zoom the view settles at (NodeViewPolicy.IsContainerView), and the
    // container-only toolbar affordances — the edit pencil, the background-colour swatches —
    // exist only in container mode, so tests that need them must wait for it explicitly
    // instead of assuming the navigation zoomed in far enough.
    public Task WaitForContainerNodeAsync(string visibleName) =>
        Expect(ContainerHeader(visibleName)).ToBeVisibleAsync();

    // A container's header label (NodeSvg renders it with class nodeName; an icon uses
    // iconName and a member memberName instead).
    ILocator ContainerHeader(string visibleName) =>
        page.Locator("#svgcanvas text.nodeName").Filter(new() { HasTextString = visibleName }).First;

    // The canvas pans/zooms with animations (initial fit, navigation), so wait until the
    // node's bounds stop moving between two reads before clicking; a single read can compute
    // a click point where the node was a frame ago (same approach as
    // WaitForStableNodePointAsync below).
    async Task<LocatorBoundingBoxResult> WaitForStableNodeBoxAsync(string label, float timeoutSeconds = 15)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LocatorBoundingBoxResult? previous = null;

        while (stopwatch.Elapsed < timeout)
        {
            LocatorBoundingBoxResult? box = null;
            if (await Node(label).CountAsync() > 0)
                box = await Node(label).BoundingBoxAsync();

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

    // Select a diagram node by its visible (short) label — the text shown on the canvas,
    // e.g. "RootClass" rather than the full "Demo.Core.RootClass". The visible labels
    // (text.iconName) are rendered in a separate SVG layer from the interactive node groups
    // (g.hoverable, which carry the full name), so we find the label and click the nearest
    // node group's center (dependency lines, which also use g.hoverable, are excluded).
    //
    // Navigating to a node (NavigationService.ShowNodeAsync) animates pan/zoom over many
    // re-rendered tiles, and mid-animation the label can be momentarily absent (tile culling,
    // icon/container switch) or still moving, so poll until the click point exists and has
    // stopped moving between two reads instead of reading it once. The zoom the navigation
    // settles at decides whether the node renders as icon, container, or member, so the
    // label is matched by any of the three label classes (a CI flake showed "RootClass"
    // ending up as a container header, which the icon-only match never found).
    public async Task SelectNodeByVisibleNameAsync(string visibleName)
    {
        await ClickUntilSelectedAsync(() => WaitForStableNodePointAsync(visibleName));
    }

    // Click a computed canvas point until a node toolbar actually shows. The canvas re-renders
    // continuously, so a click can land where the node was a frame ago and select nothing —
    // which surfaces much later as a missing toolbar button. Recompute the point on each
    // attempt rather than reusing a stale one.
    async Task ClickUntilSelectedAsync(Func<Task<float[]>> getPoint)
    {
        for (int attempt = 1; ; attempt++)
        {
            float[] point = await getPoint();
            await page.Mouse.ClickAsync(point[0], point[1]);
            try
            {
                await NodeToolbarMenu.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts) { }
        }
    }

    async Task<float[]> WaitForStableNodePointAsync(string visibleName, float timeoutSeconds = 15)
    {
        const string FindNodePointScript =
            @"(name) => {
            const selector = '#svgcanvas text.iconName, #svgcanvas text.nodeName, #svgcanvas text.memberName';
            const labels = [...document.querySelectorAll(selector)]
                .filter(t => t.textContent.trim() === name);
            if (labels.length === 0) return null;
            const lr = labels[0].getBoundingClientRect();
            const lx = lr.x + lr.width / 2, ly = lr.y + lr.height / 2;
            const arrow = String.fromCharCode(8594); // dependency lines contain '→'
            let best = null, bestDist = Infinity;
            for (const g of document.querySelectorAll('#svgcanvas g.hoverable')) {
                if (g.textContent.includes(arrow)) continue;
                const r = g.getBoundingClientRect();
                const cx = r.x + r.width / 2, cy = r.y + r.height / 2;
                const d = (cx - lx) ** 2 + (cy - ly) ** 2;
                if (d < bestDist) { bestDist = d; best = [cx, cy]; }
            }
            return best;
        }";

        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        float[]? previous = null;
        bool wasEverFound = false;

        while (stopwatch.Elapsed < timeout)
        {
            float[]? point = await page.EvaluateAsync<float[]?>(FindNodePointScript, visibleName);
            wasEverFound |= point is not null;

            bool isStable =
                point is not null
                && previous is not null
                && Math.Abs(point[0] - previous[0]) < 1
                && Math.Abs(point[1] - previous[1]) < 1;
            if (isStable)
                return point!;

            previous = point;
            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            wasEverFound
                ? $"Node with visible label '{visibleName}' did not stop moving within {timeout.TotalSeconds}s."
                : $"No node with visible label '{visibleName}' is rendered on the canvas."
        );
    }

    // Navigate to the main page app and wait until the initial model has loaded and rendered (the
    // app sets data-app-ready=true on the body once CanvasService finishes loading).
    // Prefer this over Page.GotoAsync + ad-hoc waits to avoid timing flakiness.
    public Task GotoMainPageAsync() => GotoAsync("/");

    // Navigate to the app and wait until the initial model has loaded and rendered (the
    // app sets data-app-ready=true on the body once CanvasService finishes loading).
    // Prefer this over Page.GotoAsync + ad-hoc waits to avoid timing flakiness.
    public async Task GotoAsync(string path)
    {
        await page.GotoAsync(path);
        await WaitForReadyAsync();
    }

    public Task WaitForReadyAsync() =>
        Expect(page.Locator("body")).ToHaveAttributeAsync("data-app-ready", "true", new() { Timeout = 30_000 });

    // Wait for the parsed model to actually render before interacting with model-dependent
    // UI: app-ready fires before parsing completes, and e.g. a search query typed mid-parse
    // can race the model (a CI flake showed the "Parsing" overlay still up when a test
    // filled the search field, and the parse-completion re-render wiped the fill). The
    // demo root label "Demo.sln" rendering on the canvas marks the parse as done (tests
    // run against the embedded demo model, see ./scripts/e2e).
    public Task WaitForModelRenderedAsync() => Expect(NodeLabel("Demo.sln")).ToBeVisibleAsync();

    // Open the node search dialog via the app menu; returns its page object.
    // Waits for the parsed model first — see WaitForModelRenderedAsync.
    public async Task<SearchDialog> OpenSearchViaMenuAsync()
    {
        await WaitForModelRenderedAsync();
        await (await OpenMenuItemAsync("menu-search")).ClickAsync();
        return new SearchDialog(this, page);
    }

    // Fill a server-bound (MudTextField) input and verify the value stuck. A Blazor render
    // landing just after the fill can echo a stale value back into the input, truncating the
    // text (observed flake: a note description "Guiding note" saved as "Gui"). Give any
    // in-flight render a moment to land, then refill if it overwrote the value.
    public async Task FillReliablyAsync(ILocator input, string value)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            await input.FillAsync(value);
            await page.WaitForTimeoutAsync(250);
            if (await input.InputValueAsync() == value)
                return;
        }

        // All attempts were overwritten — fail with Playwright's descriptive assertion.
        await Expect(input).ToHaveValueAsync(value);
    }

    // Open the node search dialog via the Ctrl+F hotkey; returns its page object.
    // Waits for the parsed model first — see WaitForModelRenderedAsync.
    public async Task<SearchDialog> OpenSearchViaHotkeyAsync()
    {
        await WaitForModelRenderedAsync();
        await page.Keyboard.PressAsync("Control+f");
        return new SearchDialog(this, page);
    }

    // Stub Clerk sign-in without the real Clerk: block the Clerk CDN and stub window.Clerk
    // so that once signed in, clerkGetToken() returns a JWT minted by TestAuthToken, which
    // the local Functions host validates against the test JWKS (see ./scripts/e2e -s). Call this
    // BEFORE GotoAsync. Used by signed-in [SyncFact] UI flows.
    //
    // The stub starts signed OUT and only signs in when the app calls Clerk.openSignIn()
    // (i.e. when a test clicks the cloud button). If the stub reported a user from page
    // load, the app's background sync refresh would authenticate on its own before the
    // click — racing the test, auto-syncing the demo model into the user's cloud storage,
    // and sometimes flagging a sync conflict so the click opened a modal conflict dialog
    // whose overlay then blocked the toolbar (flaky timeout).
    public async Task StubClerkSignInAsync(string sub = "e2e-test-user", string email = "e2e@dependinator.test")
    {
        // The real Clerk CDN script would overwrite our stub, so prevent it from loading.
        await page.RouteAsync("**/*.clerk.accounts.dev/**", route => route.AbortAsync());

        string token = TestAuthToken.Create(sub, email);
        await page.AddInitScriptAsync(
            $$"""
            window.Clerk = {
                loaded: true,
                user: null,
                session: null,
                listeners: [],
                load: async () => {},
                addListener: (fn) => { window.Clerk.listeners.push(fn); return () => {}; },
                openSignIn: () => {
                    window.Clerk.user = { id: {{System.Text.Json.JsonSerializer.Serialize(sub)}} };
                    window.Clerk.session = { getToken: async () => {{System.Text.Json.JsonSerializer.Serialize(
                token
            )}} };
                    // clerkSignIn (jsInterop.js) resolves via this listener (or its 1s poll).
                    window.Clerk.listeners.forEach((fn) => fn({ user: window.Clerk.user, session: window.Clerk.session }));
                },
                closeSignIn: () => {},
                signOut: async () => { window.Clerk.user = null; window.Clerk.session = null; },
            };
            """
        );
    }
}
