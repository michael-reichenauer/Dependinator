using System.Text.Json;
using System.Text.Json.Serialization;

namespace DependinatorCore.Utils;

public class JsonX
{
    static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize<T>(T instance)
    {
        return JsonSerializer.Serialize(instance, JsonSettings);
    }

    public static T? Deserialize<T>(string text)
    {
        return JsonSerializer.Deserialize<T>(text, JsonSettings);
    }
}
