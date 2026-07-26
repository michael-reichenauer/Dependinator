using System.Net;
using System.Text;
using Dependinator.Core.CloudSync;
using Shared;

namespace Dependinator.Core.Tests.CloudSync;

public class CloudSyncHttpClientTests
{
    [Fact]
    public async Task GetAuthStateAsync_ShouldSendBothAuthHeaders_WhenTokenIsAvailable()
    {
        RecordingHandler handler = new(OkJson("{\"IsAvailable\":true,\"IsAuthenticated\":true,\"User\":null}"));
        CloudSyncHttpClient sut = CreateClient(handler, token: "the-token");

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.True(state.IsAuthenticated);
        Assert.Equal("Bearer the-token", handler.Request!.Headers.Authorization!.ToString());
        Assert.Equal("Bearer the-token", handler.Request.Headers.GetValues("X-Dependinator-Authorization").Single());
    }

    [Fact]
    public async Task GetAuthStateAsync_ShouldSendNoAuthHeaders_WhenTokenIsNull()
    {
        RecordingHandler handler = new(OkJson("{\"IsAvailable\":true,\"IsAuthenticated\":false,\"User\":null}"));
        CloudSyncHttpClient sut = CreateClient(handler, token: null);

        await sut.GetAuthStateAsync();

        Assert.Null(handler.Request!.Headers.Authorization);
        Assert.False(handler.Request.Headers.Contains("X-Dependinator-Authorization"));
    }

    [Fact]
    public async Task ListAsync_ShouldUseAbsoluteUri_WhenBaseAddressIsConfigured()
    {
        RecordingHandler handler = new(OkJson("{\"Models\":[]}"));
        CloudSyncHttpClient sut = CreateClient(handler, apiBaseAddress: "https://example.com/");

        R<CloudModelList> result = await sut.ListAsync();

        Assert.True(Try(out CloudModelList? _, out var error, result), error?.ErrorMessage);
        Assert.Equal("https://example.com/api/models", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ListAsync_ShouldReturnJsonErrorMessage_WhenApiReturnsErrorBody()
    {
        RecordingHandler handler = new(Json(HttpStatusCode.Conflict, "{\"Message\":\"Device sync quota exceeded.\"}"));
        CloudSyncHttpClient sut = CreateClient(handler);

        R<CloudModelList> result = await sut.ListAsync();

        Assert.False(Try(out CloudModelList? _, out var error, result));
        Assert.Contains("Device sync quota exceeded.", error!.ErrorMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Device sync is not enabled.")]
    [InlineData(HttpStatusCode.NotFound, "Cloud model was not found.")]
    [InlineData(HttpStatusCode.InternalServerError, "Device sync request failed with status code 500.")]
    public async Task ListAsync_ShouldMapStatusCode_WhenErrorBodyIsNotJson(
        HttpStatusCode statusCode,
        string expectedMessage
    )
    {
        RecordingHandler handler = new(
            new HttpResponseMessage(statusCode) { Content = new StringContent("<html>oops</html>") }
        );
        CloudSyncHttpClient sut = CreateClient(handler);

        R<CloudModelList> result = await sut.ListAsync();

        Assert.False(Try(out CloudModelList? _, out var error, result));
        Assert.Contains(expectedMessage, error!.ErrorMessage);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnError_WhenResponseBodyIsEmpty()
    {
        RecordingHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK));
        CloudSyncHttpClient sut = CreateClient(handler);

        R<CloudModelList> result = await sut.ListAsync();

        Assert.False(Try(out CloudModelList? _, out var error, result));
        Assert.NotNull(error);
    }

    [Fact]
    public async Task PushAsync_ShouldPutJsonDocument_ToModelKeyUri()
    {
        CloudModelDocument document = new("model-key", "/m.model", DateTimeOffset.UtcNow, "hash", 3, "abc=");
        RecordingHandler handler = new(
            OkJson(
                "{\"ModelKey\":\"model-key\",\"NormalizedPath\":\"/m.model\",\"UpdatedUtc\":\"2026-01-01T00:00:00Z\",\"ContentHash\":\"hash\",\"CompressedSizeBytes\":3}"
            )
        );
        CloudSyncHttpClient sut = CreateClient(handler);

        R<CloudModelMetadata> result = await sut.PushAsync(document);

        Assert.True(Try(out CloudModelMetadata? metadata, out var error, result), error?.ErrorMessage);
        Assert.Equal("model-key", metadata.ModelKey);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.EndsWith("/api/models/model-key", handler.Request.RequestUri!.ToString());
        Assert.Equal("application/json", handler.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task PullAsync_ShouldReturnNone_WhenApiReturnsNotFound()
    {
        RecordingHandler handler = new(
            Json(HttpStatusCode.NotFound, "{\"Message\":\"No cloud model exists for key 'model-key'.\"}")
        );
        CloudSyncHttpClient sut = CreateClient(handler);

        R<CloudModelDocument> result = await sut.PullAsync("model-key");

        Assert.True(result.IsNone);
    }

    static CloudSyncHttpClient CreateClient(
        RecordingHandler handler,
        string? token = "token",
        string? apiBaseAddress = "http://localhost/"
    )
    {
        return new CloudSyncHttpClient(new HttpClient(handler), new FakeApiContext(token, apiBaseAddress));
    }

    static HttpResponseMessage OkJson(string json) => Json(HttpStatusCode.OK, json);

    static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    sealed class FakeApiContext(string? token, string? apiBaseAddress) : ICloudSyncApiContext
    {
        public bool IsEnabled => true;

        public string? ApiBaseAddress => apiBaseAddress;

        public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult(token);
    }

    sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }
}
