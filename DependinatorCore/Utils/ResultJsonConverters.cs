using System.Text.Json;
using System.Text.Json.Serialization;

namespace DependinatorCore.Utils;

public sealed class ResultJsonConverter : JsonConverter<R>
{
    const string OkPropertyName = "ok";
    const string IsNonePropertyName = "isNone";
    const string ErrorMessagePropertyName = "errorMessage";
    const string ErrorDetailPropertyName = "errorDetail";

    public override R Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {nameof(R)}.");

        bool? ok = null;
        bool isNone = false;
        string? errorMessage = null;
        string? errorDetail = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected property name for {nameof(R)}.");

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, OkPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                ok = reader.GetBoolean();
            }
            else if (string.Equals(propertyName, IsNonePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                isNone = reader.GetBoolean();
            }
            else if (string.Equals(propertyName, ErrorMessagePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else if (string.Equals(propertyName, ErrorDetailPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                errorDetail = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        if (ok == true)
            return R.Ok;

        if (isNone)
            return R.None;

        if (ok == false || errorMessage is not null || errorDetail is not null)
            return BuildErrorException(errorMessage, errorDetail);

        throw new JsonException($"{nameof(R)} payload was missing required properties.");
    }

    public override void Write(Utf8JsonWriter writer, R value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var isError = value.IsResultError;
        writer.WriteBoolean(OkPropertyName, !isError);

        if (isError)
        {
            writer.WriteBoolean(IsNonePropertyName, value.IsNone);

            var exception = value.GetResultException();
            writer.WriteString(ErrorMessagePropertyName, exception.Message);
            writer.WriteString(ErrorDetailPropertyName, exception.ToString());
        }

        writer.WriteEndObject();
    }

    internal static Exception BuildErrorException(string? message, string? detail)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            if (string.IsNullOrWhiteSpace(detail))
                return new Exception("Unknown error");

            return new Exception(detail);
        }

        if (string.IsNullOrWhiteSpace(detail))
            return new Exception(message);

        if (string.Equals(message, detail, StringComparison.Ordinal))
            return new Exception(message);

        return new Exception(message, new Exception(detail));
    }
}

public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(R<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public sealed class ResultJsonConverter<T> : JsonConverter<R<T>>
{
    const string OkPropertyName = "ok";
    const string ValuePropertyName = "value";
    const string IsNonePropertyName = "isNone";
    const string ErrorMessagePropertyName = "errorMessage";
    const string ErrorDetailPropertyName = "errorDetail";

    public override R<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {typeToConvert.Name}.");

        bool? ok = null;
        bool isNone = false;
        string? errorMessage = null;
        string? errorDetail = null;
        T? value = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected property name for {typeToConvert.Name}.");

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, OkPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                ok = reader.GetBoolean();
            }
            else if (string.Equals(propertyName, ValuePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = JsonSerializer.Deserialize<T>(ref reader, options);
            }
            else if (string.Equals(propertyName, IsNonePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                isNone = reader.GetBoolean();
            }
            else if (string.Equals(propertyName, ErrorMessagePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else if (string.Equals(propertyName, ErrorDetailPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                errorDetail = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        if (ok == true)
            return R<T>.From(value!);

        if (isNone)
            return R.None;

        if (ok == false || errorMessage is not null || errorDetail is not null)
            return ResultJsonConverter.BuildErrorException(errorMessage, errorDetail);

        throw new JsonException($"{typeToConvert.Name} payload was missing required properties.");
    }

    public override void Write(Utf8JsonWriter writer, R<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var isError = value.IsResultError;
        writer.WriteBoolean(OkPropertyName, !isError);

        if (isError)
        {
            writer.WriteBoolean(IsNonePropertyName, value.IsNone);

            var exception = value.GetResultException();
            writer.WriteString(ErrorMessagePropertyName, exception.Message);
            writer.WriteString(ErrorDetailPropertyName, exception.ToString());
        }
        else
        {
            writer.WritePropertyName(ValuePropertyName);
            JsonSerializer.Serialize(writer, value.GetResultValue(), options);
        }

        writer.WriteEndObject();
    }
}
