using System.Reflection;
using Dependinator.E2E.Tests.Shared.Pages;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Shared;

// Base class for UI tests against a running app (started via ./watch
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
                    + "Start it with ./watch, or run tests via ./e2e "
                    + "which starts the app automatically.",
                e
            );
        }
    }
}
