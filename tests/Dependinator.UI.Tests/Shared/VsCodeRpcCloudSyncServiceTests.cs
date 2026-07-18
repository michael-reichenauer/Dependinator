using Dependinator.Core.CloudSync;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Microsoft.JSInterop;
using Shared;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.UI.Tests.Shared;

// VsCodeRpcCloudSyncService forwards cloud sync calls to the LSP-hosted ICloudSyncRpcService
// with a per-call timeout, and compresses/decompresses model documents locally.
public class VsCodeRpcCloudSyncServiceTests
{
    [Fact]
    public async Task GetAuthStateAsync_ShouldReturnError_WhenRpcCallTimesOut()
    {
        Mock<ICloudSyncRpcService> rpc = new();
        rpc.Setup(r => r.GetAuthStateAsync()).Returns(new TaskCompletionSource<R<CloudAuthState>>().Task);
        VsCodeRpcCloudSyncService sut = new(new FakeJsInterop(), rpc.Object, TimeSpan.FromMilliseconds(10));

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.False(Try(out CloudAuthState? _, out ErrorResult? error, result));
        Assert.NotNull(error);
        Assert.Contains("timed out", error.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ShouldUseLoginTimeout_WhenRpcCallTimesOut()
    {
        Mock<ICloudSyncRpcService> rpc = new();
        rpc.Setup(r => r.LoginAsync()).Returns(new TaskCompletionSource<R<CloudAuthState>>().Task);
        VsCodeRpcCloudSyncService sut = new(
            new FakeJsInterop(),
            rpc.Object,
            requestTimeout: TimeSpan.FromMinutes(5),
            loginRequestTimeout: TimeSpan.FromMilliseconds(10)
        );

        R<CloudAuthState> result = await sut.LoginAsync();

        Assert.False(Try(out CloudAuthState? _, out ErrorResult? error, result));
        Assert.NotNull(error);
        Assert.Contains("timed out", error.ErrorMessage);
    }

    [Fact]
    public async Task GetAuthStateAsync_ShouldReturnError_WhenRpcCallThrows()
    {
        Mock<ICloudSyncRpcService> rpc = new();
        rpc.Setup(r => r.GetAuthStateAsync()).ThrowsAsync(new InvalidOperationException("rpc failed"));
        VsCodeRpcCloudSyncService sut = new(new FakeJsInterop(), rpc.Object, TimeSpan.FromSeconds(1));

        R<CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.False(Try(out CloudAuthState? _, out ErrorResult? error, result));
        Assert.NotNull(error);
    }

    [Fact]
    public async Task PushAsync_ShouldSendCompressedDocument_AndReturnMetadata()
    {
        ModelDto modelDto = new()
        {
            Name = "/models/sample.model",
            Nodes = [],
            Links = [],
            Lines = [],
        };
        CloudModelDocument? sentDocument = null;
        CloudModelMetadata expected = new("key", "/models/sample.model", DateTimeOffset.UtcNow, "hash", 1);
        Mock<ICloudSyncRpcService> rpc = new();
        rpc.Setup(r => r.PushAsync(It.IsAny<CloudModelDocument>()))
            .Callback<CloudModelDocument>(d => sentDocument = d)
            .ReturnsAsync((R<CloudModelMetadata>)expected);
        VsCodeRpcCloudSyncService sut = new(new FakeJsInterop(), rpc.Object, TimeSpan.FromSeconds(1));

        R<CloudModelMetadata> result = await sut.PushAsync("/models/sample.model", modelDto);

        Assert.True(Try(out CloudModelMetadata? metadata, out _, result));
        Assert.Same(expected, metadata);
        Assert.NotNull(sentDocument);
        Assert.Equal(CloudModelPath.CreateKey("/models/sample.model"), sentDocument.ModelKey);
    }

    [Fact]
    public async Task PullAsync_ShouldRequestByModelKey_AndDecodeDocument()
    {
        ModelDto modelDto = new()
        {
            Name = "/models/sample.model",
            Nodes = [],
            Links = [],
            Lines = [],
        };
        CloudModelDocument document = CloudModelSerializer.CreateDocument("/models/sample.model", modelDto);
        string expectedKey = CloudModelPath.CreateKey("/models/sample.model");
        Mock<ICloudSyncRpcService> rpc = new();
        rpc.Setup(r => r.PullAsync(expectedKey)).ReturnsAsync((R<CloudModelDocument>)document);
        VsCodeRpcCloudSyncService sut = new(new FakeJsInterop(), rpc.Object, TimeSpan.FromSeconds(1));

        R<ModelDto> result = await sut.PullAsync("/models/sample.model");

        Assert.True(Try(out ModelDto? pulledModel, out var error, result), error?.ErrorMessage);
        Assert.Equal(modelDto.Name, pulledModel.Name);
        rpc.Verify(r => r.PullAsync(expectedKey), Times.Once);
    }

    [Fact]
    public async Task PullAsync_ShouldReturnNone_WhenNoRemoteModelExists()
    {
        string expectedKey = CloudModelPath.CreateKey("/models/sample.model");
        Mock<ICloudSyncRpcService> rpc = new();
        R<CloudModelDocument> noRemoteModel = R.None;
        rpc.Setup(r => r.PullAsync(expectedKey)).ReturnsAsync(noRemoteModel);
        VsCodeRpcCloudSyncService sut = new(new FakeJsInterop(), rpc.Object, TimeSpan.FromSeconds(1));

        R<ModelDto> result = await sut.PullAsync("/models/sample.model");

        Assert.True(result.IsNone);
    }

    sealed class FakeJsInterop : IJSInterop
    {
        public ValueTask Call(string functionName, params object?[]? args) => ValueTask.CompletedTask;

        public ValueTask<T> Call<T>(string functionName, params object?[]? args) => new(default(T)!);

        public DotNetObjectReference<TValue> Reference<TValue>(TValue value)
            where TValue : class => DotNetObjectReference.Create(value);
    }
}
