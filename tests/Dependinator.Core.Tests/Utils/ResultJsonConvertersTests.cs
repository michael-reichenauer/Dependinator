using System.Text.Json;

namespace Dependinator.Core.Tests.Utils;

// Verifies that Result/Result<T> survive the JSON-RPC tunnel serialization: the value, the chain of
// wrapped error messages, and the NotFoundError that cloud sync uses to signal a missing remote model.
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

    static Result<string> Roundtrip(Result<string> result) =>
        JsonSerializer.Deserialize<Result<string>>(JsonSerializer.Serialize(result, options), options);

    static Result Roundtrip(Result result) =>
        JsonSerializer.Deserialize<Result>(JsonSerializer.Serialize(result, options), options);

    [Fact]
    public void Roundtrip_ShouldPreserveValue_ForGenericResult()
    {
        Assert.Equal("the value", AssertOk(Roundtrip("the value")));
    }

    [Fact]
    public void Roundtrip_ShouldPreserveNotFound_ForGenericResult()
    {
        var error = AssertError(Roundtrip(new NotFoundError("No remote model")));

        Assert.IsType<NotFoundError>(error);
        Assert.Equal("No remote model", error.Message);
    }

    [Fact]
    public void Roundtrip_ShouldPreserveErrorMessages_ForGenericResult()
    {
        var error = AssertError(Roundtrip(new Error("Something failed.", new Error("Because of this."))));

        Assert.IsNotType<NotFoundError>(error);
        Assert.Equal("Something failed.", error.Message);
        Assert.Equal("Something failed.,\nBecause of this.", error.AllMessages());
    }

    [Fact]
    public void Roundtrip_ShouldPreserveOutcome_ForResult()
    {
        AssertOk(Roundtrip(Result.Ok));
        Assert.Equal("Something failed.", AssertError(Roundtrip(new Error("Something failed."))).Message);
    }

    [Fact]
    public void Serialize_ShouldRejectAnUnsetResult()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(Result<string>), options));
    }
}
