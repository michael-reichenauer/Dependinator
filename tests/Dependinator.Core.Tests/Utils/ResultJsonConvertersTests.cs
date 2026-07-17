using System.Text.Json;

namespace Dependinator.Core.Tests.Utils;

// Verifies that R/R<T> survive the JSON-RPC tunnel serialization, especially the R.None
// "no value" marker that cloud sync uses to signal a missing remote model.
public class ResultJsonConvertersTests
{
    static readonly JsonSerializerOptions options = CreateOptions();

    static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions serializerOptions = new();
        serializerOptions.Converters.Add(new ResultJsonConverter());
        serializerOptions.Converters.Add(new ResultJsonConverterFactory());
        return serializerOptions;
    }

    [Fact]
    public void Roundtrip_ShouldPreserveNone_ForGenericResult()
    {
        R<string> noneResult = R.None;

        string json = JsonSerializer.Serialize(noneResult, options);
        R<string> roundtripped = JsonSerializer.Deserialize<R<string>>(json, options)!;

        Assert.True(roundtripped.IsNone);
    }

    [Fact]
    public void Roundtrip_ShouldPreserveValue_ForGenericResult()
    {
        R<string> valueResult = "the value";

        string json = JsonSerializer.Serialize(valueResult, options);
        R<string> roundtripped = JsonSerializer.Deserialize<R<string>>(json, options)!;

        Assert.True(Try(out string? value, out var error, roundtripped), error?.ErrorMessage);
        Assert.Equal("the value", value);
    }

    [Fact]
    public void Roundtrip_ShouldPreserveErrorMessage_ForGenericResult()
    {
        R<string> errorResult = R.Error("Something failed.");

        string json = JsonSerializer.Serialize(errorResult, options);
        R<string> roundtripped = JsonSerializer.Deserialize<R<string>>(json, options)!;

        Assert.False(Try(out string? _, out var error, roundtripped));
        Assert.False(roundtripped.IsNone);
        Assert.Contains("Something failed.", error!.ErrorMessage);
    }
}
