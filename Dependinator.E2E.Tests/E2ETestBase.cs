using Dependinator.E2E.Tests.Pages;
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
            string path = Path.Combine(TraceDir, $"{ordinal:D3}-{browser}.zip");
            await Context.Tracing.StopAsync(new() { Path = path });
        }

        await base.DisposeAsync();
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
