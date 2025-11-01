using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dependinator.Tests;

public static class VerifierExtensions
{
    public static SettingsTask VerifyJson(
        string jsonText,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = ""
    )
    {
        return Verify(target: jsonText, sourceFile: sourceFile, settings: settings, extension: "json");
    }

    public static SettingsTask VerifyJson<T>(
        T instance,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = ""
    )
    {
        return VerifyJson(
            jsonText: JsonSerializer.Serialize(instance, JsonSettings),
            settings: settings,
            sourceFile: sourceFile
        );
    }

    static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };
}
