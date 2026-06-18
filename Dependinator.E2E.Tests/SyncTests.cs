using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shared;
using Xunit;

namespace Dependinator.E2E.Tests;

// Cloud-sync tests. These only run under `./e2e -s`, which starts Azurite, the Azure
// Functions API (7071) and the test JWKS server, and points the API at the test issuer.
// Auth uses a self-minted token (TestAuthToken) instead of real Clerk — see TestAuth/.
// SeededSyncModel (a class fixture) seeds a known model so read tests are deterministic.
public class SyncTests : E2ETestBase, IClassFixture<SeededSyncModel>
{
    // The Functions API the Blazor app talks to in dev (Dependinator.Web app settings).
    const string ApiBaseUrl = SeededSyncModel.ApiBaseUrl;

    // SeededSyncModel is a class fixture: xUnit creates it once and runs its seeding
    // (IAsyncLifetime) before these tests. Consuming it here satisfies that wiring.
    public SyncTests(SeededSyncModel seededModel) => _ = seededModel;

    static HttpClient CreateClient(string sub) =>
        new()
        {
            BaseAddress = new Uri(ApiBaseUrl),
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", TestAuthToken.Create(sub)),
            },
        };

    // Proves the whole offline-auth chain end to end against the live host: a token we
    // mint is accepted (signature verified via the test JWKS, issuer + iat checked) and
    // its claims flow back through /api/auth/me.
    [SyncFact]
    public async Task AuthMe_ShouldReturnAuthenticatedUser_WhenTokenIsValid()
    {
        using HttpClient client = CreateClient("e2e-test-user");

        CloudAuthState? state = await client.GetFromJsonAsync<CloudAuthState>("/api/auth/me");

        Assert.NotNull(state);
        Assert.True(state.IsAuthenticated);
        Assert.Equal("e2e-test-user", state.User?.UserId);
    }

    // Without a token the same endpoint reports unauthenticated (sanity check that auth
    // is actually enforced, not bypassed).
    [SyncFact]
    public async Task AuthMe_ShouldReturnUnauthenticated_WhenNoToken()
    {
        using HttpClient client = new() { BaseAddress = new Uri(ApiBaseUrl) };

        CloudAuthState? state = await client.GetFromJsonAsync<CloudAuthState>("/api/auth/me");

        Assert.NotNull(state);
        Assert.False(state.IsAuthenticated);
    }

    // Authenticated calls reach blob storage (Azurite): a fresh user's model list is empty.
    [SyncFact]
    public async Task ListModels_ShouldSucceed_ForAuthenticatedUser()
    {
        using HttpClient client = CreateClient($"e2e-list-{Guid.NewGuid():N}");

        using HttpResponseMessage response = await client.GetAsync("/api/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The seeded model (SeededSyncModel) is listed for its user — deterministic, no PUT
    // in this test, independent of other tests' data.
    [SyncFact]
    public async Task ListModels_ShouldContainSeededModel()
    {
        using HttpClient client = CreateClient(SeededSyncModel.UserSub);

        CloudModelList? list = await client.GetFromJsonAsync<CloudModelList>("/api/models");

        Assert.NotNull(list);
        Assert.Contains(
            list.Models,
            m =>
                m.ModelKey == SeededSyncModel.ModelKey
                && m.NormalizedPath == SeededSyncModel.ModelPath
                && m.ContentHash == SeededSyncModel.ContentHash
        );
    }

    // Fetching the seeded model returns its exact content and metadata round-tripped
    // through Azurite.
    [SyncFact]
    public async Task GetModel_ShouldReturnSeededDocument()
    {
        using HttpClient client = CreateClient(SeededSyncModel.UserSub);

        CloudModelDocument? doc = await client.GetFromJsonAsync<CloudModelDocument>(
            $"/api/models/{SeededSyncModel.ModelKey}"
        );

        Assert.NotNull(doc);
        Assert.Equal(SeededSyncModel.ModelPath, doc.NormalizedPath);
        Assert.Equal(SeededSyncModel.ContentHash, doc.ContentHash);
        Assert.Equal(SeededSyncModel.ContentBase64, doc.CompressedContentBase64);
    }
}
