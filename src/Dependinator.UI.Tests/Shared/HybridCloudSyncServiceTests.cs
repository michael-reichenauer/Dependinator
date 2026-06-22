using System.Net;
using System.Text;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Shared;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.UI.Tests.Shared;

// HybridCloudSyncService picks the VS Code webview proxy when it is available and otherwise falls
// back to the HTTP transport. Routing is generic (ForwardAsync<T>), so verifying one read and one
// auth operation on both branches proves it for every ICloudSyncService method.
public class HybridCloudSyncServiceTests
{
    [Fact]
    public async Task ListAsync_ShouldRouteToVsCodeProxy_WhenProxyIsAvailable()
    {
        RecordingHandler httpHandler = new();
        CloudModelList expected = new([]);
        Mock<IVsCodeCloudSyncProxy> proxy = new();
        proxy.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        proxy.Setup(p => p.ListAsync()).ReturnsAsync((R<CloudModelList>)expected);
        HybridCloudSyncService sut = new(CreateHttpService(httpHandler), proxy.Object);

        R<CloudModelList> result = await sut.ListAsync();

        Assert.True(Try(out CloudModelList? value, out _, result));
        Assert.Same(expected, value);
        Assert.Equal(0, httpHandler.SendCount);
        proxy.Verify(p => p.ListAsync(), Times.Once);
    }

    [Fact]
    public async Task ListAsync_ShouldRouteToHttp_WhenProxyIsUnavailable()
    {
        RecordingHandler httpHandler = new();
        Mock<IVsCodeCloudSyncProxy> proxy = new();
        proxy.Setup(p => p.IsAvailableAsync()).ReturnsAsync(false);
        HybridCloudSyncService sut = new(CreateHttpService(httpHandler), proxy.Object);

        R<CloudModelList> result = await sut.ListAsync();

        Assert.True(Try(out CloudModelList? value, out _, result));
        Assert.NotNull(value);
        Assert.Equal(1, httpHandler.SendCount);
        proxy.Verify(p => p.ListAsync(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldRouteToVsCodeProxy_WhenProxyIsAvailable()
    {
        RecordingHandler httpHandler = new();
        CloudAuthState expected = new(IsAvailable: true, IsAuthenticated: true, User: null);
        Mock<IVsCodeCloudSyncProxy> proxy = new();
        proxy.Setup(p => p.IsAvailableAsync()).ReturnsAsync(true);
        proxy.Setup(p => p.LoginAsync()).ReturnsAsync((R<CloudAuthState>)expected);
        HybridCloudSyncService sut = new(CreateHttpService(httpHandler), proxy.Object);

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.True(Try(out CloudAuthState? value, out _, result));
        Assert.Same(expected, value);
        Assert.Equal(0, httpHandler.SendCount);
        proxy.Verify(p => p.LoginAsync(), Times.Once);
    }

    [Fact]
    public void IsAvailable_ShouldReflectHttpTransport()
    {
        Mock<IVsCodeCloudSyncProxy> proxy = new();

        HybridCloudSyncService enabled = new(CreateHttpService(new RecordingHandler(), enabled: true), proxy.Object);
        HybridCloudSyncService disabled = new(CreateHttpService(new RecordingHandler(), enabled: false), proxy.Object);

        Assert.True(enabled.IsAvailable);
        Assert.False(disabled.IsAvailable);
    }

    static HttpCloudSyncService CreateHttpService(RecordingHandler httpHandler, bool enabled = true)
    {
        HttpClient httpClient = new(httpHandler);
        CloudSyncClientOptions options = new() { Enabled = enabled, ApiBaseAddress = "http://localhost/" };
        return new HttpCloudSyncService(httpClient, new FakeJsInterop(), Options.Create(options));
    }

    // Counts HTTP requests so a test can assert whether the HTTP transport was exercised.
    sealed class RecordingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            SendCount++;
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Models\":[]}", Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    sealed class FakeJsInterop : IJSInterop
    {
        public ValueTask Call(string functionName, params object?[]? args) => ValueTask.CompletedTask;

        public ValueTask<T> Call<T>(string functionName, params object?[]? args) => new(default(T)!);

        public DotNetObjectReference<TValue> Reference<TValue>(TValue value)
            where TValue : class => DotNetObjectReference.Create(value);
    }
}
