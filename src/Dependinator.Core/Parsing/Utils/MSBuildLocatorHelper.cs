using Microsoft.Build.Locator;

namespace Dependinator.Core.Parsing.Utils;

internal static class MSBuildLocatorHelper
{
    // Roslyn's MSBuildWorkspace loads projects with the MSBuild assemblies of an installed
    // .NET SDK (or Visual Studio). The app itself is published self-contained and runs without
    // any installed .NET, so parsing is the one feature that needs the SDK, and a missing SDK
    // must be reported as an error rather than as an empty model.
    public const string NoSdkErrorMessage =
        "No .NET SDK found. Parsing C# code requires the .NET SDK to be installed. "
        + "Install it from https://dotnet.microsoft.com/download and then parse again.";

    static readonly object SyncLock = new();

    public static R Register()
    {
        if (MSBuildLocator.IsRegistered)
            return R.Ok;

        lock (SyncLock)
        {
            if (MSBuildLocator.IsRegistered)
                return R.Ok;

            VisualStudioInstance[] instances;
            try
            {
                instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            }
            catch (Exception e)
            {
                // Querying itself fails if e.g. the "dotnet" host cannot be located at all.
                Log.Exception(e, "Failed to query MSBuild instances");
                return R.Error(NoSdkErrorMessage, e);
            }

            if (instances.Length == 0)
            {
                Log.Warn("No MSBuild instances (no .NET SDK installed?)");
                return R.Error(NoSdkErrorMessage);
            }

            var instance = instances.OrderByDescending(i => i.Version).First();
            Log.Info($"Using MSBuild {instance.Version} at {instance.MSBuildPath}");

            try
            {
                MSBuildLocator.RegisterInstance(instance);
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to register MSBuild instance");
                return R.Error($"Failed to use the installed .NET SDK ({instance.MSBuildPath}).", e);
            }

            return R.Ok;
        }
    }
}
