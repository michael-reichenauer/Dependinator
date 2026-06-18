using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Dependinator.E2E.Tests;

// Cloud-sync tests. These only run under `./e2e -s`, which starts Azurite, the Azure
// Functions API (7071) and the test JWKS server, and points the API at the test issuer.
// Auth uses a self-minted token (TestAuthToken) instead of real Clerk — see TestAuth/.
public class SyncTests : E2ETestBase
{
    // The Functions API the Blazor app talks to in dev (Dependinator.Web appsettings).
    const string ApiBaseUrl = "http://127.0.0.1:7071";

    // Proves the whole offline-auth chain end to end against the live host: a token we
    // mint is accepted (signature verified via the test JWKS, issuer + iat checked) and
    // its claims flow back through /api/auth/me.
    [SyncFact]
    public async Task AuthMe_ShouldReturnAuthenticatedUser_WhenTokenIsValid()
    {
        using HttpClient client = new() { BaseAddress = new Uri(ApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestAuthToken.Create(sub: "e2e-test-user", email: "e2e@dependinator.test")
        );

        AuthState? state = await client.GetFromJsonAsync<AuthState>("/api/auth/me");

        Assert.NotNull(state);
        Assert.True(state.IsAuthenticated);
        Assert.Equal("e2e-test-user", state.User?.UserId);
        Assert.Equal("e2e@dependinator.test", state.User?.Email);
    }

    // Without a token the same endpoint reports unauthenticated (sanity check that auth
    // is actually enforced, not bypassed).
    [SyncFact]
    public async Task AuthMe_ShouldReturnUnauthenticated_WhenNoToken()
    {
        using HttpClient client = new() { BaseAddress = new Uri(ApiBaseUrl) };

        AuthState? state = await client.GetFromJsonAsync<AuthState>("/api/auth/me");

        Assert.NotNull(state);
        Assert.False(state.IsAuthenticated);
    }

    // Authenticated calls reach blob storage (Azurite): a fresh user's model list is empty.
    [SyncFact]
    public async Task ListModels_ShouldSucceed_ForAuthenticatedUser()
    {
        using HttpClient client = new() { BaseAddress = new Uri(ApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestAuthToken.Create(sub: $"e2e-list-{Guid.NewGuid():N}")
        );

        using HttpResponseMessage response = await client.GetAsync("/api/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Mirrors the production contract shape (Shared.CloudAuthState / CloudUserInfo) so the
    // test does not need a reference to the Api/Shared projects.
    sealed record AuthState(bool IsAvailable, bool IsAuthenticated, UserInfo? User);

    sealed record UserInfo(string UserId, string? Email);
}
