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

    // Menu open/hover steps retry a few short attempts instead of one long wait: the flaky
    // failure mode is a swallowed click or hover (MudBlazor opens menus via a server roundtrip
    // behind a popover overlay, and a click landing while a previous popover is still closing
    // can be eaten without opening the menu), which only another attempt can recover from.
    const int MenuAttempts = 3;
    const float MenuAttemptTimeout = 5_000;

    static bool IsRetryable(Exception e) => e is TimeoutException or PlaywrightException;

    // Open the app menu and return a menu-item locator by its data-testid.
    public async Task<ILocator> OpenMenuItemAsync(string testId)
    {
        await OpenAppMenuAsync(MenuItem(testId));
        return MenuItem(testId);
    }

    // Open the app menu, hover the parent submenu to expand it, and return the nested
    // menu-item locator by its data-testid (nested MudMenu flyouts open on hover). The hover
    // retries too: the parent item can be re-rendered (detached) mid-hover, or the flyout can
    // fail to open when the hover races the menu's own opening render.
    public async Task<ILocator> OpenSubMenuItemAsync(string parentTestId, string testId)
    {
        ILocator parent = MenuItem(parentTestId);
        await OpenAppMenuAsync(parent);

        ILocator item = MenuItem(testId);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await parent.HoverAsync(new() { Timeout = MenuAttemptTimeout });
                await item.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return item;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts)
            {
                // Park the pointer off the item so the re-hover fires a fresh enter event.
                await page.Mouse.MoveAsync(0, 0);
            }
        }
    }

    // Click the app-menu button until the expected item shows. A single long wait cannot
    // recover from a swallowed click, so retry with short waits, pressing Escape between
    // attempts to close any half-open popover before clicking again.
    async Task OpenAppMenuAsync(ILocator item)
    {
        for (int attempt = 1; ; attempt++)
        {
            await Menu.ClickAsync();
            try
            {
                await item.WaitForAsync(new() { Timeout = MenuAttemptTimeout });
                return;
            }
            catch (Exception e) when (IsRetryable(e) && attempt < MenuAttempts)
            {
                await page.Keyboard.PressAsync("Escape");
            }
        }
    }

    // Select a diagram node by its exact group label (the *full* node name, e.g.
    // "Demo.Core.RootClass"). We click via the mouse at the computed coordinates rather than
    // Locator.ClickAsync because the SVG canvas re-renders constantly (which fails
    // Playwright's stability check). Selecting a node shows its context toolbar
    // (NodeToolbarMenu). See also SelectNodeByVisibleNameAsync for the short on-screen name.
    public async Task SelectNodeByFullNameAsync(string label)
    {
        LocatorBoundingBoxResult box = await WaitForStableNodeBoxAsync(label);
        await page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    // Select a container node by clicking just inside its top-left corner, where no children
    // render (a container's center often hits child nodes or dependency lines instead).
    public async Task SelectContainerNodeAsync(string label, float timeoutSeconds = 15)
    {
        LocatorBoundingBoxResult box = await WaitForStableNodeBoxAsync(label, timeoutSeconds);
        await page.Mouse.ClickAsync(box.X + 20, box.Y + 20);
    }

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
        float[] point = await WaitForStableNodePointAsync(visibleName);
        await page.Mouse.ClickAsync(point[0], point[1]);
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
    // run against the embedded demo model, see ./e2e).
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
    // the local Functions host validates against the test JWKS (see ./e2e -s). Call this
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
