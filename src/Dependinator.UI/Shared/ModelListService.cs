using Dependinator.Core;
using Dependinator.Core.Shared;
using Dependinator.UI.Shared.CloudSync;

namespace Dependinator.UI.Shared;

interface IModelListService
{
    Task InitAsync();

    IReadOnlyList<string> RecentModelPaths { get; }
    IReadOnlyList<string> LocalPaths { get; }
    IReadOnlyList<string> CloudPaths { get; }
    string? LastUsedPath { get; }
    bool IsLocalPath(string path);

    Task AddModelAsync(string path);
    Task RemoveModelAsync(string path);
}

[Scoped]
class ModelListService(
    IConfigService configService,
    IWorkspaceFileService workspaceFileService,
    IAppCloudSyncService appCloudSyncService
) : IModelListService
{
    const int RecentCount = 5;

    IReadOnlyList<string> recentPaths = [];
    IReadOnlyList<string> localPaths = [];
    IReadOnlyList<string> cloudPaths = [];

    public IReadOnlyList<string> RecentModelPaths => recentPaths;
    public IReadOnlyList<string> LocalPaths => localPaths;
    public IReadOnlyList<string> CloudPaths => cloudPaths;
    public string? LastUsedPath => recentPaths.Any() ? recentPaths[0] : null;

    public bool IsLocalPath(string path) => LocalPaths.Contains(path);

    public async Task InitAsync()
    {
        recentPaths = (await configService.GetAsync()).RecentPaths;
        cloudPaths = GetCloudPaths();
        localPaths =
            Build.IsVsCodeExtWasm ? await workspaceFileService.GetSolutionFilePathsAsync()
            : Build.IsWeb ? [DemoModel.WorkingSolutionPath]
            : []; // Standalone Wasm has no local models

        // Seed recent paths with the first available model (cloud for standalone Wasm)
        if (!recentPaths.Any())
        {
            var defaultPaths = Build.IsStandaloneWasm ? cloudPaths : localPaths;
            if (defaultPaths.Any())
                recentPaths = [defaultPaths[0]];
        }

        await configService.SetAsync(c => c.RecentPaths = recentPaths.ToList());
    }

    public async Task AddModelAsync(string path)
    {
        recentPaths = (await configService.GetAsync()).RecentPaths;
        recentPaths = recentPaths.Prepend(path).Distinct().Take(RecentCount).ToList();
        await configService.SetAsync(c => c.RecentPaths = recentPaths.ToList());
    }

    public async Task RemoveModelAsync(string path)
    {
        recentPaths = (await configService.GetAsync()).RecentPaths;
        recentPaths = recentPaths.Where(rp => rp != path).Distinct().Take(RecentCount).ToList();
        await configService.SetAsync(c => c.RecentPaths = recentPaths.ToList());
    }

    IReadOnlyList<string> GetCloudPaths()
    {
        var cloudModelPaths = appCloudSyncService
            .CloudModels.OrderBy(cm => cm.UpdatedUtc)
            .Select(cm => cm.NormalizedPath)
            .ToList();
        Log.Info("Cloud models:");
        cloudModelPaths.ForEach(path => Log.Info("  Model", path));
        return cloudModelPaths;
    }
}
