using System.IO.Compression;
using System.Text;
using Dependinator.Shared;
using Microsoft.JSInterop;
using static DependinatorCore.Utils.Result;

namespace Dependinator.Tests.Shared;

public class DatabaseTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnValue_WhenJsInteropReturnsStream()
    {
        var compressedPayload = CompressToBase64(Encoding.UTF8.GetBytes("\"payload\""));
        var json = $"{{\"Id\":\"item-1\",\"Value\":\"{compressedPayload}\"}}";
        var streamReference = new FakeJsStreamReference(Encoding.UTF8.GetBytes(json));
        var jsInterop = new FakeJsInterop(
            (functionName, args) =>
            {
                Assert.Equal("getDatabaseValueStream", functionName);
                return streamReference;
            }
        );

        var sut = new Database(jsInterop);

        var result = await sut.GetAsync<string>("Files", "item-1");

        Assert.True(Try(out var value, out var e, result), e?.ErrorMessage);
        Assert.Equal("payload", value);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNone_WhenJsInteropReturnsNoValue()
    {
        var jsInterop = new FakeJsInterop((_, _) => null);
        var sut = new Database(jsInterop);

        var result = await sut.GetAsync<string>("Files", "item-1");

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnError_WhenStreamInteropThrows()
    {
        var jsInterop = new FakeJsInterop((_, _) => throw new JSException("read failed"));
        var sut = new Database(jsInterop);

        var result = await sut.GetAsync<string>("Files", "item-1");

        Assert.False(Try(out string? _, out var e, result));
        Assert.NotNull(e);
        Assert.Contains("No value", e.ErrorMessage);
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
    public async Task SetAsync_ShouldStoreCompressedValue()
    {
        object? savedPair = null;
        var jsInterop = new FakeJsInterop(
            (functionName, args) =>
            {
                if (functionName == "setDatabaseValue")
                    savedPair = args![2];

                return null;
            }
        );
        var sut = new Database(jsInterop);

        var result = await sut.SetAsync("Files", "item-1", "payload");

        Assert.True(Try(out var e, result), e?.ErrorMessage);
        Assert.NotNull(savedPair);
        var pairType = savedPair!.GetType();
        var id = (string)pairType.GetProperty("Id")!.GetValue(savedPair)!;
        var compressedValue = (string)pairType.GetProperty("Value")!.GetValue(savedPair)!;
        var decompressedValue = DecompressFromBase64(compressedValue);
        Assert.Equal("item-1", id);
        Assert.Equal("\"payload\"", Encoding.UTF8.GetString(decompressedValue));
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

    sealed class FakeJsStreamReference(byte[] content) : IJSStreamReference
    {
        public long Length => content.LongLength;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<Stream> OpenReadStreamAsync(
            long maxAllowedSize = 512000,
            CancellationToken cancellationToken = default
        )
        {
            if (Length > maxAllowedSize)
                throw new ArgumentOutOfRangeException(nameof(maxAllowedSize));
            return ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false));
        }
    }

    static string CompressToBase64(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(bytes);

        return Convert.ToBase64String(output.ToArray());
    }

    static byte[] DecompressFromBase64(string base64)
    {
        var compressedBytes = Convert.FromBase64String(base64);
        using var input = new MemoryStream(compressedBytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
