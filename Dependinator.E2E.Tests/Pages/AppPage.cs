using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Dependinator.E2E.Tests.Pages;

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
    public ILocator NodeLabel(string text) => page.Locator("#svgcanvas text.iconName", new() { HasText = text }).First;

    // Navigate to the app and wait until the initial model has loaded and rendered (the
    // app sets data-app-ready=true on the body once CanvasService finishes loading).
    // Prefer this over Page.GotoAsync + ad-hoc waits to avoid timing flakiness.
    public async Task GotoAsync(string path = "/")
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
