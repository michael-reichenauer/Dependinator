using Dependinator.Shared;
using Dependinator.Shared.CloudSync;
using Microsoft.JSInterop;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.Tests.Shared;

public class VsCodeCloudSyncProxyTests
{
    [Fact]
    public async Task GetAuthStateAsync_ShouldReturnError_WhenResponseTimesOut()
    {
        FakeJsInterop jsInterop = new((functionName, _) => functionName switch
        {
            "isVsCodeWebView" => true,
            "postVsCodeMessage" => true,
            _ => throw new InvalidOperationException($"Unexpected JS call: {functionName}"),
        });
        VsCodeCloudSyncProxy sut = new(jsInterop, TimeSpan.FromMilliseconds(10));

        R<global::Shared.CloudAuthState> result = await sut.GetAuthStateAsync();

        Assert.False(Try(out global::Shared.CloudAuthState? _, out ErrorResult? error, result));
        Assert.NotNull(error);
        Assert.Contains("timed out", error.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnError_WhenResponseTimesOut()
    {
        FakeJsInterop jsInterop = new((functionName, _) => functionName switch
        {
            "isVsCodeWebView" => true,
            "postVsCodeMessage" => true,
            _ => throw new InvalidOperationException($"Unexpected JS call: {functionName}"),
        });
        VsCodeCloudSyncProxy sut = new(
            jsInterop,
            requestTimeout: TimeSpan.FromSeconds(1),
            loginRequestTimeout: TimeSpan.FromMilliseconds(10)
        );

        R<global::Shared.CloudAuthState> result = await sut.LoginAsync();

        Assert.False(Try(out global::Shared.CloudAuthState? _, out ErrorResult? error, result));
        Assert.NotNull(error);
        Assert.Contains("timed out", error.ErrorMessage);
    }

    sealed class FakeJsInterop(Func<string, object?[]?, object?> onCall) : IJSInterop
    {
        public ValueTask Call(string functionName, params object?[]? args)
        {
            _ = onCall(functionName, args);
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> Call<T>(string functionName, params object?[]? args)
        {
            object? value = onCall(functionName, args);
            return new ValueTask<T>((T)value!);
        }

        public DotNetObjectReference<TValue> Reference<TValue>(TValue value)
            where TValue : class
        {
            return DotNetObjectReference.Create(value);
        }
    }
}
