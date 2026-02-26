using Microsoft.Build.Locator;

namespace Dependinator.Core.Parsing.Utils;

internal static class MSBuildLocatorHelper
{
    static readonly object SyncLock = new();

    public static void Register()
    {
        if (MSBuildLocator.IsRegistered)
            return;

        lock (SyncLock)
        {
            if (MSBuildLocator.IsRegistered)
                return;

            var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            if (instances.Length == 0)
            {
                MSBuildLocator.RegisterDefaults();
                return;
            }

            var instance = instances.OrderByDescending(i => i.Version).First();
            MSBuildLocator.RegisterInstance(instance);
        }
    }
}
