using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dependinator.Core;

public static class Build
{
    static bool isVsCodeExtWasm = false;
    static bool isVsCodeExtLsp = false;
    static bool isTestMode = false;
    public static readonly string Version = GetVersion().ToString();
    public static readonly string ProductVersion = GetProductVersion().ToString();
    public static readonly string Time = GetTime().IsoZone();
    public static readonly string CommitSid = GetCommitId().Sid();

    public static readonly bool IsWasm = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
    public static readonly bool IsWeb = RuntimeInformation.ProcessArchitecture != Architecture.Wasm;
    public static bool IsStandaloneWasm => IsWasm && !isVsCodeExtWasm;
    public static bool IsVsCodeExtWasm => IsWasm && isVsCodeExtWasm;
    public static bool IsVsCodeExtLsp => !IsWasm && isVsCodeExtLsp;

    // True during UI/e2e test runs (set from the DEPENDINATOR_E2E env var by the host).
    // Makes the app load the embedded demo model instead of parsing a real solution,
    // so tests get a deterministic model without a slow Roslyn parse.
    public static bool IsTestMode => isTestMode;

    public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static string Info =>
        $"{Version}, {Time}, ({CommitSid}, {RuntimeInformation.RuntimeIdentifier}, {BuildMode})";

    public static Version GetVersion() => typeof(Build).Assembly.GetName().Version!;

    public static bool IsDebug
    {
#if DEBUG
        get => true;
#else
        get => false;
#endif
    }

    public static void SetIsVsCodeExtWasm() => isVsCodeExtWasm = true;

    public static void SetIsVsCodeExtLsp() => isVsCodeExtLsp = true;

    public static void SetIsTestMode() => isTestMode = true;

    public static string BuildMode => IsDebug ? "IsDebug" : "IsRelease";

    public static Version GetProductVersion()
    {
        var version = GetVersion();
        return new Version(version.Major, version.Minor);
    }

    public static DateTime GetTime()
    {
        var assemblyVersion = typeof(Build).Assembly.GetName().Version!;

        // The Build number is the days since 2000-01-01
        int daysSince2000 = assemblyVersion.Build;

        // The Revision is the number of 2-second intervals since midnight
        int twoSecondIntervalsSinceMidnight = assemblyVersion.Revision;

        // Construct the build date
        DateTime buildDate = new DateTime(2000, 1, 1)
            .AddDays(daysSince2000)
            .AddSeconds(twoSecondIntervalsSinceMidnight * 2);
        return buildDate;
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
