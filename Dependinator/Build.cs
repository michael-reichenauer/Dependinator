using System.Reflection;
using System.Runtime.InteropServices;

namespace Dependinator;

public static class Build
{
    public static readonly string Version = GetVersion().ToString();
    public static readonly string ProductVersion = GetProductVersion().ToString();
    public static readonly string Time = GetTime().IsoZone();
    public static readonly string CommitSid = GetCommitId().Sid();

    public static readonly bool IsWebAssembly = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
    public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static Version GetVersion() => typeof(Build).Assembly.GetName().Version!;

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
}
