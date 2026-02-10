using Dependinator.Shared;
using Microsoft.JSInterop;
using static DependinatorCore.Utils.Result;

namespace Dependinator.Tests.Shared;

public class DatabaseTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnValue_WhenJsInteropReturnsChunkedJson()
    {
        var jsInterop = new FakeJsInterop(
            (functionName, args) =>
            {
                Assert.Equal("getDatabaseValue", functionName);
                var valueHandlerRef = args![3]!;
                var valueHandler = valueHandlerRef.GetType().GetProperty("Value")!.GetValue(valueHandlerRef)!;
                var onValueMethod = valueHandler.GetType().GetMethod("OnValue")!;

                _ = onValueMethod.Invoke(valueHandler, ["{\"Id\":\"item-1\",\"Value\":\"payload\"}"]);
                return true;
            }
        );

        var sut = new Database(jsInterop);

        var result = await sut.GetAsync<string>("Files", "item-1");

        Assert.True(Try(out var value, out var e, result), e?.ErrorMessage);
        Assert.Equal("payload", value);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnError_WhenJsInteropThrows()
    {
        var jsInterop = new FakeJsInterop((_, _) => throw new JSException("read failed"));
        var sut = new Database(jsInterop);

        var result = await sut.GetAsync<string>("Files", "item-1");

        Assert.False(Try(out string? _, out var e, result));
        Assert.NotNull(e);
        Assert.Contains("read failed", e.ErrorMessage);
    }

    [Fact]
    public async Task SetAsync_ShouldReturnError_WhenJsInteropThrows()
    {
        var jsInterop = new FakeJsInterop((_, _) => throw new JSException("write failed"));
        var sut = new Database(jsInterop);

        var result = await sut.SetAsync("Files", "item-1", "payload");

        Assert.False(Try(out var e, result));
        Assert.NotNull(e);
        Assert.Contains("write failed", e.ErrorMessage);
    }

    [Fact]
    public async Task GetKeysAsync_ShouldReturnError_WhenJsInteropThrows()
    {
        var jsInterop = new FakeJsInterop((_, _) => throw new JSException("keys failed"));
        var sut = new Database(jsInterop);

        var result = await sut.GetKeysAsync("Files");

        Assert.False(Try(out IReadOnlyList<string>? _, out var e, result));
        Assert.NotNull(e);
        Assert.Contains("keys failed", e.ErrorMessage);
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
            var value = onCall(functionName, args);
            return new ValueTask<T>((T)value!);
        }

        public DotNetObjectReference<TValue> Reference<TValue>(TValue value)
            where TValue : class
        {
            return DotNetObjectReference.Create(value);
        }
    }
}
