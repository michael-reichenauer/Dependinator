using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Dependinator.E2E.Tests;

// Base class for UI tests against a running app (started via ./watch, ./watch-sync
// or auto-started by the ./e2e script). Browser is selected with the BROWSER
// environment variable (chromium [default], firefox or webkit); the target app
// with E2E_BASE_URL (default Blazor Server at http://localhost:5000).
public class E2ETestBase : PageTest
{
    protected static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

    private static bool isAppVerified;

    // When E2E_TRACE=1 (set by `./e2e -t` and in CI), each test records a Playwright
    // trace to Dependinator.E2E.Tests/traces/. CI uploads that folder as an artifact on
    // failure; open a .zip at https://trace.playwright.dev to debug.
    private static readonly bool tracingEnabled = Environment.GetEnvironmentVariable("E2E_TRACE") == "1";
    private static int traceCounter;
    private bool tracingStarted;

    private static string TraceDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "traces"));

    public override BrowserNewContextOptions ContextOptions()
    {
        BrowserNewContextOptions options = base.ContextOptions() ?? new BrowserNewContextOptions();
        options.BaseURL = BaseUrl;
        return options;
    }

    public override async Task InitializeAsync()
    {
        await EnsureAppIsRunningAsync();
        await base.InitializeAsync();

        if (tracingEnabled)
        {
            await Context.Tracing.StartAsync(
                new() { Screenshots = true, Snapshots = true, Sources = true }
            );
            tracingStarted = true;
        }
    }

    public override async Task DisposeAsync()
    {
        if (tracingStarted)
        {
            Directory.CreateDirectory(TraceDir);
            string browser = Environment.GetEnvironmentVariable("BROWSER") ?? "chromium";
            int ordinal = Interlocked.Increment(ref traceCounter);
            string path = Path.Combine(TraceDir, $"{ordinal:D3}-{browser}.zip");
            await Context.Tracing.StopAsync(new() { Path = path });
        }

        await base.DisposeAsync();
    }

    // Navigates and waits until the app has finished loading and rendering the initial
    // model (the app sets data-app-ready=true on the body via jsInterop's setAppReady).
    // Prefer this over GotoAsync + arbitrary waits to avoid timing flakiness.
    protected async Task GotoReadyAsync(string path = "/")
    {
        await Page.GotoAsync(path);
        await WaitForAppReadyAsync();
    }

    protected Task WaitForAppReadyAsync() =>
        Expect(Page.Locator("body")).ToHaveAttributeAsync("data-app-ready", "true", new() { Timeout = 30_000 });

    // Makes the page appear signed in to cloud sync without real Clerk: blocks the Clerk
    // CDN and stubs window.Clerk so clerkGetToken() returns a JWT minted by TestAuthToken,
    // which the local Functions host validates against the test JWKS (see ./e2e -s).
    // Call this BEFORE navigating (Page.GotoAsync). Used by [SyncFact] tests.
    protected async Task SignInAsTestUserAsync(string sub = "e2e-test-user", string email = "e2e@dependinator.test")
    {
        // The real Clerk CDN script would overwrite our stub, so prevent it from loading.
        await Page.RouteAsync("**/*.clerk.accounts.dev/**", route => route.AbortAsync());

        string token = TestAuthToken.Create(sub, email);
        await Page.AddInitScriptAsync(
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

    private static async Task EnsureAppIsRunningAsync()
    {
        if (isAppVerified)
        {
            return;
        }

        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using HttpResponseMessage response = await client.GetAsync(BaseUrl);
            response.EnsureSuccessStatusCode();
            isAppVerified = true;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"The app is not reachable at {BaseUrl}. "
                    + "Start it with ./watch (or ./watch-sync), or run tests via ./e2e "
                    + "which starts the app automatically.",
                e
            );
        }
    }
}
