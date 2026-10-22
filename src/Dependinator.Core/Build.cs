using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dependinator.Core;

public static class Build
{
    static bool isVsCodeExtWasm = false;
    static bool isVsCodeExtLsp = false;
    static bool isTestMode = false;
    static string vsCodeExtensionVersion = "";
    public static readonly string Version = GetVersion();
    public static readonly string ProductVersion = GetProductVersion().ToString();
    public static readonly string Time = GetTime().ToLocalTime().IsoZone();
    public static readonly string CommitSid = GetCommitId().Sid();

    public static readonly bool IsWasm = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
    public static readonly bool IsWeb = RuntimeInformation.ProcessArchitecture != Architecture.Wasm;
    public static bool IsStandaloneWasm => IsWasm && !isVsCodeExtWasm;
    public static bool IsVsCodeExtWasm => IsWasm && isVsCodeExtWasm;
    public static bool IsVsCodeExtLsp => !IsWasm && isVsCodeExtLsp;

    // The version of the VS Code extension hosting the app, reported by the extension host.
    // Empty when not running inside the extension (or before the host has been queried).
    public static string VsCodeExtensionVersion => vsCodeExtensionVersion;

    // True during UI/e2e test runs (set from the DEPENDINATOR_E2E env var by the host).
    // Makes the app load the embedded demo model instead of parsing a real solution,
    // so tests get a deterministic model without a slow Roslyn parse.
    public static bool IsTestMode => isTestMode;

    public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static string Info =>
        $"{Version}, {Time}, ({CommitSid}, {RuntimeInformation.RuntimeIdentifier}, {BuildMode})";

    // major.minor.build, e.g. 0.84.423503, from the informational version (Directory.Build.props),
    // which SourceLink suffixes with '+<commit sha>'
    public static string GetVersion()
    {
        string? value = typeof(Build)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (value is null)
            return GetProductVersion().ToString();
        int index = value.IndexOf('+');
        return index > 0 ? value[..index] : value;
    }

    public static bool IsDebug
    {
#if DEBUG
        get => true;
#else
        get => false;
#endif
    }

    public static void SetIsVsCodeExtWasm() => isVsCodeExtWasm = true;

    public static void SetVsCodeExtensionVersion(string version) => vsCodeExtensionVersion = version;

    public static void SetIsVsCodeExtLsp() => isVsCodeExtLsp = true;

    public static void SetIsTestMode() => isTestMode = true;

    public static string BuildMode => IsDebug ? "IsDebug" : "IsRelease";

    // major.minor, the assembly version (Directory.Build.props)
    public static Version GetProductVersion()
    {
        Version version = typeof(Build).Assembly.GetName().Version!;
        return new Version(version.Major, version.Minor);
    }

    // The build time in UTC, set by the Release workflow or else at build (Dependinator.Core.csproj)
    public static DateTime GetTime()
    {
        string? value = typeof(Build)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTime")
            ?.Value;
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime time
        )
            ? time
            : DateTime.MinValue;
    }

    public static string GetCommitId()
    {
        var attribute = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion != null)
        {
            var value = attribute.InformationalVersion;
            var index = value.IndexOf("+");
            if (index > 0)
            {
                value = value[(index + 1)..];
                return value;
            }
        }

        return "0000000000000000000000000000000000000000";
    }

    // Build.cs lives at <repo>/src/Dependinator.Core/Build.cs; the solution folder is the
    // repo root, three levels up (file -> Dependinator.Core -> src -> repo root).
    public static readonly string SolutionFolderPath = Path.GetDirectoryName(
        Path.GetDirectoryName(Path.GetDirectoryName(CurrentFilePath()))
    )!;

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
