using System.Net.Http.Headers;
using System.Text.Json;
using Shared;
using Xunit;

namespace Dependinator.E2E.Tests;

// Seeds a known cloud model into Azurite (through the API's own PUT) before sync tests
// run, so read tests have a deterministic model regardless of test order. The seed user
// is dedicated, so write tests using other users can't disturb it.
//
// Only seeds when the sync stack is up (E2E_SYNC=1, set by ./e2e -s); otherwise it is a
// no-op and the dependent [SyncFact] tests are skipped anyway. Wired in via IClassFixture.
public sealed class SeededSyncModel : IAsyncLifetime
{
    public const string ApiBaseUrl = "http://127.0.0.1:7071";
    public const string UserSub = "e2e-seed-user";
    public const string ModelPath = "/e2e/seed-model.sln";
    public const string ContentHash = "e2e-seed-hash-v1";

    // Deterministic, non-empty payload; the API stores and echoes it verbatim.
    public static readonly byte[] Content = "e2e-seed-model-content"u8.ToArray();
    public static string ContentBase64 => Convert.ToBase64String(Content);
    public static string ModelKey => CloudModelPath.CreateKey(ModelPath);

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("E2E_SYNC") != "1")
            return;

        using HttpClient client = new() { BaseAddress = new Uri(ApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestAuthToken.Create(sub: UserSub)
        );

        CloudModelDocument document = new(
            ModelKey,
            ModelPath,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ContentHash,
            Content.LongLength,
            ContentBase64
        );

        // Send the body with an explicit Content-Length (ByteArrayContent). PutAsJsonAsync
        // streams chunked, which the isolated Functions host reads as an empty body.
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(document);
        using ByteArrayContent content = new(jsonBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using HttpResponseMessage response = await client.PutAsync($"/api/models/{ModelKey}", content);
        response.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
