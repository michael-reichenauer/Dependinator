using System.Runtime.CompilerServices;

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
        return VerifyJson(jsonText: Json.Serialize(instance), settings: settings, sourceFile: sourceFile);
    }
}
