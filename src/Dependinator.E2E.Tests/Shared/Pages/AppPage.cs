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
    public ILocator NodeLabel(string text) => page.Locator("#svgcanvas text.iconName", new() { HasText = text }).First;

    // A diagram node's group element, matched by its exact label. (Node SVG ids are
    // generated, so match on the label text. The whitespace-tolerant anchored regex gives
    // an exact match — so "Demo.sln" doesn't also match the dependency line whose label is
    // "Demo.sln→Externals (…)" — and tolerates the surrounding whitespace in the SVG text.)
    public ILocator Node(string label) =>
        page.Locator("#svgcanvas g.hoverable")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(label)}\s*$") });

    // The selected-node context toolbar (NodeToolbar.razor) — its menu activator is
    // present whenever a node is selected.
    public ILocator NodeToolbarMenu => page.GetByTestId("node-menu");

    // The selected-node toolbar's References/Dependencies buttons (NodeToolbar.razor).
    public ILocator NodeReferencesButton => page.GetByTestId("node-references");
    public ILocator NodeDependenciesButton => page.GetByTestId("node-dependencies");

    // The grow/shrink buttons, shown on the node toolbar only while edit mode is enabled.
    public ILocator NodeIncreaseSize => page.GetByTestId("node-increase-size");
    public ILocator NodeDecreaseSize => page.GetByTestId("node-decrease-size");

    // The toolbar edit-mode toggle (AppBar.razor). Toggles NodeSvg.IsEditingEnabled.
    public ILocator ToolbarEdit => page.GetByTestId("toolbar-edit");

    // The cloud sync/auth button (AppBar.razor). Clicking it while signed out starts login.
    public ILocator CloudButton => page.GetByTestId("toolbar-cloud");

    // A MudBlazor dialog (NodeProperties / MudMessageBox) rendered as role="dialog".
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    // The dependencies/references explorer tree (DependenciesTree.razor popover). The tree
    // renders nested .mud-treeview lists, so take the outermost (first) one.
    public ILocator DependenciesTree => page.Locator(".mud-treeview").First;

    // Open the app menu and return a menu-item locator by its data-testid.
    public async Task<ILocator> OpenMenuItemAsync(string testId)
    {
        await Menu.ClickAsync();
        return MenuItem(testId);
    }

    // Select a diagram node by its exact group label (the *full* node name, e.g.
    // "Demo.Core.RootClass"). We click via the mouse at the computed coordinates rather than
    // Locator.ClickAsync because the SVG canvas re-renders constantly (which fails
    // Playwright's stability check). Selecting a node shows its context toolbar
    // (NodeToolbarMenu). See also SelectNodeByVisibleNameAsync for the short on-screen name.
    public async Task SelectNodeByFullNameAsync(string label)
    {
        LocatorBoundingBoxResult box =
            await Node(label).BoundingBoxAsync()
            ?? throw new InvalidOperationException($"Node '{label}' is not rendered on the canvas.");
        await page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    // Select a diagram node by its visible (short) label — the text shown on the canvas,
    // e.g. "RootClass" rather than the full "Demo.Core.RootClass". The visible labels
    // (text.iconName) are rendered in a separate SVG layer from the interactive node groups
    // (g.hoverable, which carry the full name), so we find the label and click the nearest
    // node group's center (dependency lines, which also use g.hoverable, are excluded).
    public async Task SelectNodeByVisibleNameAsync(string visibleName)
    {
        float[]? point = await page.EvaluateAsync<float[]?>(
            @"(name) => {
                const labels = [...document.querySelectorAll('#svgcanvas text.iconName')]
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
            }",
            visibleName
        );

        if (point is null)
            throw new InvalidOperationException(
                $"No node with visible label '{visibleName}' is rendered on the canvas."
            );

        await page.Mouse.ClickAsync(point[0], point[1]);
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

    // Open the node search dialog via the app menu; returns its page object.
    public async Task<SearchDialog> OpenSearchViaMenuAsync()
    {
        await Menu.ClickAsync();
        await MenuItem("menu-search").ClickAsync();
        return new SearchDialog(page);
    }

    // Open the node search dialog via the Ctrl+F hotkey; returns its page object.
    public async Task<SearchDialog> OpenSearchViaHotkeyAsync()
    {
        await page.Keyboard.PressAsync("Control+f");
        return new SearchDialog(page);
    }

    // Make the page appear signed in to cloud sync without real Clerk: block the Clerk CDN
    // and stub window.Clerk so clerkGetToken() returns a JWT minted by TestAuthToken, which
    // the local Functions host validates against the test JWKS (see ./e2e -s). Call this
    // BEFORE GotoAsync. Used by signed-in [SyncFact] UI flows.
    public async Task SignInAsTestUserAsync(string sub = "e2e-test-user", string email = "e2e@dependinator.test")
    {
        // The real Clerk CDN script would overwrite our stub, so prevent it from loading.
        await page.RouteAsync("**/*.clerk.accounts.dev/**", route => route.AbortAsync());

        string token = TestAuthToken.Create(sub, email);
        await page.AddInitScriptAsync(
            $$"""
            window.Clerk = {
                loaded: true,
                user: { id: {{System.Text.Json.JsonSerializer.Serialize(sub)}} },
                session: { getToken: async () => {{System.Text.Json.JsonSerializer.Serialize(token)}} },
                load: async () => {},
                addListener: () => (() => {}),
                openSignIn: () => {},
                closeSignIn: () => {},
                signOut: async () => { window.Clerk.user = null; window.Clerk.session = null; },
            };
            """
        );
    }
}
