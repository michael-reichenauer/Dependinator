using Api;

namespace Api.Tests;

public class ModelFunctionsTests
{
    [Fact]
    public async Task GetModelAsync_ShouldReturnUnauthorized_WhenNoPrincipalHeaderExists()
    {
        Mock<ICloudModelStore> store = new(MockBehavior.Strict);
        ModelFunctions sut = new(new StaticWebAppsPrincipalParser(), store.Object);
        TestHttpRequestData request = new(new TestFunctionContext());

        HttpResponseData response = await sut.GetModelAsync(request, "model-key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetModelAsync_ShouldReturnNotFound_WhenModelDoesNotExist()
    {
        CloudUserInfo user = new("user-123", "user@example.com");
        Mock<ICloudModelStore> store = new(MockBehavior.Strict);
        store.Setup(s => s.GetAsync(user, "model-key", CancellationToken.None)).ReturnsAsync((CloudModelDocument?)null);

        ModelFunctions sut = new(new StubPrincipalParser(user), store.Object);
        TestHttpRequestData request = new(new TestFunctionContext());

        HttpResponseData response = await sut.GetModelAsync(request, "model-key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutModelAsync_ShouldReturnBadRequest_WhenRouteKeyDoesNotMatchPayload()
    {
        CloudUserInfo user = new("user-123", "user@example.com");
        Mock<ICloudModelStore> store = new(MockBehavior.Strict);
        ModelFunctions sut = new(new StubPrincipalParser(user), store.Object);

        CloudModelDocument payload = new(
            ModelKey: "other-key",
            NormalizedPath: "/models/test.model",
            UpdatedUtc: DateTimeOffset.UtcNow,
            ContentHash: "hash",
            CompressedSizeBytes: 4,
            CompressedContentBase64: Convert.ToBase64String([1, 2, 3, 4])
        );
        TestHttpRequestData request = new(new TestFunctionContext(), method: "PUT", bodyText: JsonSerializer.Serialize(payload));

        HttpResponseData response = await sut.PutModelAsync(request, "model-key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    sealed class StubPrincipalParser(CloudUserInfo user) : IStaticWebAppsPrincipalParser
    {
        public CloudUserInfo? TryGetCurrentUser(Microsoft.Azure.Functions.Worker.Http.HttpRequestData request) => user;
    }
}
