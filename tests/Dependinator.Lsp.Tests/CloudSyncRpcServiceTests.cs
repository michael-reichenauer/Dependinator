using Dependinator.Core.CloudSync;
using Dependinator.Lsp.CloudSync;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Dependinator.Lsp.Tests;

public class CloudSyncRpcServiceTests
{
    const string BaseUrl = "https://api.dependinator.com";
    const string Token = "clerk-token-123";

    static readonly CloudAuthState AuthenticatedState = new(
        IsAvailable: true,
        IsAuthenticated: true,
        User: new CloudUserInfo("user_1", "user@example.com")
    );

    static readonly CloudAuthState UnauthenticatedState = new(IsAvailable: true, IsAuthenticated: false, User: null);

    readonly Mock<ICloudSyncHttpClient> httpClient = new();
    readonly Mock<ILanguageServerFacade> server = new();
    readonly LspCloudSyncContext context = new();

    CloudSyncRpcService CreateSut() => new(httpClient.Object, context, server.Object);

    void SetupSignIn(string token)
    {
        Mock<IResponseRouterReturns> returns = new();
        returns
            .Setup(r => r.Returning<SignInResult>(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResult(token));
        server.Setup(s => s.SendRequest(SignInParams.Method, It.IsAny<SignInParams>())).Returns(returns.Object);
    }

    void SetupClearToken()
    {
        Mock<IResponseRouterReturns> returns = new();
        returns.Setup(r => r.ReturningVoid(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        server.Setup(s => s.SendRequest(ClearTokenParams.Method, It.IsAny<ClearTokenParams>())).Returns(returns.Object);
    }

    void VerifyClearTokenSent() =>
        server.Verify(s => s.SendRequest(ClearTokenParams.Method, It.IsAny<ClearTokenParams>()), Times.Once);

    [Fact]
    public async Task LoginAsync_ShouldReturnError_WhenNotConfigured()
    {
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("not configured", error!.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthState_WhenSignInSucceeds()
    {
        context.SetBaseUrl(BaseUrl);
        SetupSignIn(Token);
        httpClient.Setup(c => c.GetAuthStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthenticatedState);
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.Equal(AuthenticatedState, state);
        Assert.True(context.HasToken);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnError_WhenSignInReturnsEmptyToken()
    {
        context.SetBaseUrl(BaseUrl);
        SetupSignIn("");
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("did not return a token", error!.ErrorMessage);
        Assert.False(context.HasToken);
    }

    [Fact]
    public async Task LoginAsync_ShouldClearToken_WhenApiRejectsToken()
    {
        context.SetBaseUrl(BaseUrl);
        SetupSignIn(Token);
        SetupClearToken();
        httpClient.Setup(c => c.GetAuthStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UnauthenticatedState);
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("did not accept the token", error!.ErrorMessage);
        Assert.False(context.HasToken);
        VerifyClearTokenSent();
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearTokenHereAndInExtension()
    {
        context.SetBaseUrl(BaseUrl);
        context.SetToken(Token);
        SetupClearToken();
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LogoutAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.False(state!.IsAuthenticated);
        Assert.False(context.HasToken);
        VerifyClearTokenSent();
    }

    [Fact]
    public async Task LogoutAsync_ShouldStillClearLocalToken_WhenExtensionRequestFails()
    {
        context.SetBaseUrl(BaseUrl);
        context.SetToken(Token);
        server
            .Setup(s => s.SendRequest(ClearTokenParams.Method, It.IsAny<ClearTokenParams>()))
            .Throws(new InvalidOperationException("extension gone"));
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.LogoutAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.False(state!.IsAuthenticated);
        Assert.False(context.HasToken);
    }

    [Fact]
    public async Task GetAuthStateAsync_ShouldReturnUnavailable_WhenNotConfigured()
    {
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.False(state!.IsAvailable);
    }

    [Fact]
    public async Task GetAuthStateAsync_ShouldReturnSignedOut_WhenNoToken()
    {
        context.SetBaseUrl(BaseUrl);
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.True(state!.IsAvailable);
        Assert.False(state.IsAuthenticated);
        httpClient.Verify(c => c.GetAuthStateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAuthStateAsync_ShouldClearToken_WhenApiRejectsStoredToken()
    {
        context.SetBaseUrl(BaseUrl);
        context.SetToken(Token);
        SetupClearToken();
        httpClient.Setup(c => c.GetAuthStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UnauthenticatedState);
        CloudSyncRpcService sut = CreateSut();

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.True(Try(out CloudAuthState? state, out var error, result), error?.ErrorMessage);
        Assert.False(state!.IsAuthenticated);
        Assert.False(context.HasToken);
        VerifyClearTokenSent();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnError_WhenNotConfigured()
    {
        CloudSyncRpcService sut = CreateSut();

        R<CloudModelList> result = await sut.ListAsync();

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("not configured", error!.ErrorMessage);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnError_WhenNoToken()
    {
        context.SetBaseUrl(BaseUrl);
        CloudSyncRpcService sut = CreateSut();

        R<CloudModelList> result = await sut.ListAsync();

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("requires login", error!.ErrorMessage);
        httpClient.Verify(c => c.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_ShouldDelegateToHttpClient_WhenLoggedIn()
    {
        context.SetBaseUrl(BaseUrl);
        context.SetToken(Token);
        CloudModelList list = new([]);
        httpClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);
        CloudSyncRpcService sut = CreateSut();

        R<CloudModelList> result = await sut.ListAsync();

        Assert.True(Try(out CloudModelList? value, out var error, result), error?.ErrorMessage);
        Assert.Equal(list, value);
    }

    [Fact]
    public async Task PushAsync_ShouldReturnError_WhenNoToken()
    {
        context.SetBaseUrl(BaseUrl);
        CloudSyncRpcService sut = CreateSut();

        R<CloudModelMetadata> result = await sut.PushAsync(CreateDocument());

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("requires login", error!.ErrorMessage);
    }

    [Fact]
    public async Task PullAsync_ShouldReturnError_WhenNoToken()
    {
        context.SetBaseUrl(BaseUrl);
        CloudSyncRpcService sut = CreateSut();

        R<CloudModelDocument> result = await sut.PullAsync("model-key");

        Assert.False(Try(out _, out var error, result));
        Assert.Contains("requires login", error!.ErrorMessage);
    }

    static CloudModelDocument CreateDocument() =>
        new(
            ModelKey: "model-key",
            NormalizedPath: "/repo/app.sln",
            UpdatedUtc: DateTimeOffset.UtcNow,
            ContentHash: "hash",
            CompressedSizeBytes: 1,
            CompressedContentBase64: "AA=="
        );
}
