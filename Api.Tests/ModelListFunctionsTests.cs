namespace Api.Tests;

public class ModelListFunctionsTests
{
    [Fact]
    public async Task ListModelsAsync_ShouldReturnUnauthorized_WhenNoPrincipalHeaderExists()
    {
        Mock<ICloudModelStore> store = new(MockBehavior.Strict);
        ModelFunctions sut = new(new StubUserProvider(null), store.Object);
        TestHttpRequestData request = new(new TestFunctionContext());

        HttpResponseData response = await sut.ListModelsAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListModelsAsync_ShouldReturnModels_WhenAuthenticated()
    {
        CloudUserInfo user = new("user-123", "user@example.com");
        IReadOnlyList<CloudModelMetadata> models =
        [
            new(
                ModelKey: "b",
                NormalizedPath: "/models/b.model",
                UpdatedUtc: DateTimeOffset.Parse("2026-03-01T11:00:00Z"),
                ContentHash: "hash-b",
                CompressedSizeBytes: 12
            ),
            new(
                ModelKey: "a",
                NormalizedPath: "/models/a.model",
                UpdatedUtc: DateTimeOffset.Parse("2026-03-01T10:00:00Z"),
                ContentHash: "hash-a",
                CompressedSizeBytes: 8
            ),
        ];

        Mock<ICloudModelStore> store = new(MockBehavior.Strict);
        store.Setup(s => s.ListAsync(user, CancellationToken.None)).ReturnsAsync(models);

        ModelFunctions sut = new(new StubUserProvider(user), store.Object);
        TestHttpRequestData request = new(new TestFunctionContext());

        HttpResponseData response = await sut.ListModelsAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TestHttpResponseData typedResponse = Assert.IsType<TestHttpResponseData>(response);
        CloudModelList? body = JsonSerializer.Deserialize<CloudModelList>(
            typedResponse.ReadBodyText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(body);
        Assert.Equal(2, body.Models.Count);
        Assert.Equal("/models/b.model", body.Models[0].NormalizedPath);
        Assert.Equal("/models/a.model", body.Models[1].NormalizedPath);
    }

    sealed class StubUserProvider(CloudUserInfo? user) : ICloudSyncUserProvider
    {
        public Task<CloudUserInfo?> TryGetCurrentUserAsync(
            Microsoft.Azure.Functions.Worker.Http.HttpRequestData request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(user);
        }
    }
}
