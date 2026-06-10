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
