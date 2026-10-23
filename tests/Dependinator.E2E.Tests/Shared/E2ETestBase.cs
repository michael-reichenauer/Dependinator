using System.Reflection;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Shared;

// Base class for UI tests against a running app (started via ./scripts/watch
// or auto-started by the ./scripts/e2e script). Browser is selected with the BROWSER
// environment variable (chromium [default], firefox or webkit); the target app
// with E2E_BASE_URL (default Blazor Server at http://127.0.0.1:5000).
public class E2ETestBase : PageTest
{
    // 127.0.0.1 (not localhost): the app binds IPv4 loopback only (Program.cs
    // Listen(IPAddress.Loopback, 5000)). With "localhost" this client can resolve to ::1
    // and get "connection refused" even though the app is up. The ./scripts/e2e script overrides
    // this via E2E_BASE_URL, but keep the default consistent for manual ./scripts/watch runs.
    protected static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://127.0.0.1:5000";

    // Playwright's default Expect timeout is 5s, which flakes on slow CI runners: navigation
    // after clicking a search result animates pan/zoom over re-rendered tiles and (on the
    // 2-core runner, webkit especially) can take just over 5s before the target label exists.
    // Align with the 15s the polling helpers in AppPage use.
    static E2ETestBase() => Assertions.SetDefaultExpectTimeout(15_000);

    private static bool isAppVerified;

    // When E2E_TRACE=1 (set by `./scripts/e2e -t` and in CI), each test records a Playwright
    // trace to Dependinator.E2E.Tests/traces/. CI uploads that folder as an artifact on
    // failure; open a .zip at https://trace.playwright.dev to debug.
    private static readonly bool tracingEnabled = Environment.GetEnvironmentVariable("E2E_TRACE") == "1";
    private static int traceCounter;
    private bool tracingStarted;
    private readonly string testName;

    private static string TraceDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "traces"));

    // xUnit injects ITestOutputHelper into the test constructor; it holds the running
    // ITest, from which we read the test method name to label the trace file. (xUnit v2
    // has no public test-name API, hence the reflection.)
    protected E2ETestBase(ITestOutputHelper output) => testName = GetTestName(output);

    private static string GetTestName(ITestOutputHelper output)
    {
        try
        {
            ITest? test = output
                .GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(f => f.GetValue(output))
                .OfType<ITest>()
                .FirstOrDefault();
            return test?.TestCase.TestMethod.Method.Name ?? "test";
        }
        catch
        {
            return "test";
        }
    }

    private AppPage? app;

    // Page object for the main app (canvas + toolbar/menu). See Pages/AppPage.cs.
    protected AppPage App => app ??= new AppPage(Page);

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
        await KeepOffTheInternetAsync(Context);
        await WarmUpAppAsync();

        if (tracingEnabled)
        {
            await Context.Tracing.StartAsync(
                new()
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true,
                }
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
            string path = Path.Combine(TraceDir, $"{ordinal:D3}-{testName}-{browser}.zip");
            await Context.Tracing.StopAsync(new() { Path = path });
        }

        await base.DisposeAsync();
    }

    // The page loads Clerk (clerk-js@latest) and Google Fonts from the internet, and the app
    // waits for Clerk before it signals ready. That cost 1-3 s of every page load and let a slow
    // or changed CDN fail any test (a 30 s app-ready timeout). Tests never use the real Clerk,
    // so every context gets a signed-out stub instead (AppPage.StubClerkSignInAsync replaces it
    // with one that signs in) and the fonts fall back to the browser's.
    private static async Task KeepOffTheInternetAsync(IBrowserContext context)
    {
        await context.RouteAsync("**/*.clerk.accounts.dev/**", route => route.AbortAsync());
        await context.RouteAsync(
            "https://fonts.googleapis.com/**",
            route => route.FulfillAsync(new() { ContentType = "text/css", Body = "" })
        );
        await context.RouteAsync("https://fonts.gstatic.com/**", route => route.AbortAsync());
        await context.AddInitScriptAsync(
            """
            window.Clerk = {
                loaded: true,
                user: null,
                session: null,
                load: async () => {},
                addListener: () => () => {},
                openSignIn: () => {},
                closeSignIn: () => {},
                signOut: async () => {},
            };
            """
        );
    }

    private static readonly SemaphoreSlim warmUpLock = new(1, 1);
    private static bool isAppWarm;

    // The first page load after the app starts compiles and loads everything (8 s on two cores,
    // over 30 s on one with two tests loading at once), so the first tests of a run failed on
    // their app-ready wait. Load the app once, with time to spare, before any test does.
    private async Task WarmUpAppAsync()
    {
        if (isAppWarm)
            return;

        await warmUpLock.WaitAsync();
        try
        {
            if (isAppWarm)
                return;

            await using IBrowserContext context = await Browser.NewContextAsync(new() { BaseURL = BaseUrl });
            await KeepOffTheInternetAsync(context);
            IPage page = await context.NewPageAsync();
            await page.GotoAsync("/");
            await Expect(page.Locator("body"))
                .ToHaveAttributeAsync("data-app-ready", "true", new() { Timeout = 120_000 });
            isAppWarm = true;
        }
        finally
        {
            warmUpLock.Release();
        }
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
                    + "Start it with ./scripts/watch, or run tests via ./scripts/e2e "
                    + "which starts the app automatically.",
                e
            );
        }
    }
}
