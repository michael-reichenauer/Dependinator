using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dependinator.Core.Utils;

// Result and Result<T> over the JSON-RPC tunnel (see JsonRpcMessageHandler), where the value or the
// error must survive as JSON. Wire format:
//   { "ok": true, "value": ... }                                        a value (Result<T>) or success (Result)
//   { "ok": false, "kind": "notFound"?, "messages": [ "outer", ... ] }   an error: AllMessages() outermost first,
//                                                                        NotFoundError marked by its kind
// Both tunnel ends ship together (the extension bundles the LSP and the web UI), so no older format is read.
static class ResultJson
{
    public const string Ok = "ok";
    public const string Value = "value";
    public const string Kind = "kind";
    public const string Messages = "messages";
    public const string NotFoundKind = "notFound";

    public static void WriteError(Utf8JsonWriter writer, Error error)
    {
        writer.WriteBoolean(Ok, false);
        if (error is NotFoundError)
            writer.WriteString(Kind, NotFoundKind);
        writer.WritePropertyName(Messages);
        writer.WriteStartArray();
        foreach (var message in error.AllMessages().Split(",\n"))
            writer.WriteStringValue(message);
        writer.WriteEndArray();
    }

    public static Error ReadError(string? kind, List<string> messages)
    {
        if (messages.Count == 0)
            messages.Add("Unknown error");
        Error? inner = null;
        for (int i = messages.Count - 1; i >= 1; i--)
            inner = inner is null ? new Error(messages[i]) : new Error(messages[i], inner);
        if (kind == NotFoundKind)
            return new NotFoundError(messages[0]);
        return inner is null ? new Error(messages[0]) : new Error(messages[0], inner);
    }
}

public sealed class ResultJsonConverter : JsonConverter<Result>
{
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object for Result.");
        bool? ok = null;
        string? kind = null;
        List<string> messages = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string name = reader.GetString() ?? "";
            reader.Read();
            if (name == ResultJson.Ok)
                ok = reader.GetBoolean();
            else if (name == ResultJson.Kind)
                kind = reader.GetString();
            else if (name == ResultJson.Messages)
                messages = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? [];
            else
                reader.Skip();
        }
        if (ok == true)
            return Result.Ok;
        if (ok == false)
            return ResultJson.ReadError(kind, messages);
        throw new JsonException("Result payload was missing 'ok'.");
    }

    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value.Value)
        {
            case Error e:
                ResultJson.WriteError(writer, e);
                break;
            case Success:
                writer.WriteBoolean(ResultJson.Ok, true);
                break;
            default:
                throw new JsonException("Cannot serialize an unset Result.");
        }
        writer.WriteEndObject();
    }
}

public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>
    where T : notnull
{
    public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {typeToConvert.Name}.");
        bool? ok = null;
        string? kind = null;
        List<string> messages = [];
        T? value = default;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string name = reader.GetString() ?? "";
            reader.Read();
            if (name == ResultJson.Ok)
                ok = reader.GetBoolean();
            else if (name == ResultJson.Value)
                value = JsonSerializer.Deserialize<T>(ref reader, options);
            else if (name == ResultJson.Kind)
                kind = reader.GetString();
            else if (name == ResultJson.Messages)
                messages = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? [];
            else
                reader.Skip();
        }
        if (ok == true)
            return value is null
                ? throw new JsonException($"{typeToConvert.Name} payload had no value.")
                : new Result<T>(value);
        if (ok == false)
            return ResultJson.ReadError(kind, messages);
        throw new JsonException($"{typeToConvert.Name} payload was missing 'ok'.");
    }

    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value.Value)
        {
            case Error e:
                ResultJson.WriteError(writer, e);
                break;
            case T v:
                writer.WriteBoolean(ResultJson.Ok, true);
                writer.WritePropertyName(ResultJson.Value);
                JsonSerializer.Serialize(writer, v, options);
                break;
            default:
                throw new JsonException($"Cannot serialize an unset {typeof(T).Name} result.");
        }
        writer.WriteEndObject();
    }
}
