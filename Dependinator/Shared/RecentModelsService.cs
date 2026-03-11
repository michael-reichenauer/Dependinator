using Dependinator.Core;
using Dependinator.Core.Shared;

namespace Dependinator.Diagrams;

interface IRecentModelsService
{
    Task InitAsync();

    IReadOnlyList<string> ModelPaths { get; }
    string LastUsedPath { get; }

    Task AddModelAsync(string path);
    Task RemoveModelAsync(string path);
}

[Scoped]
class RecentModelsService : IRecentModelsService
{
    const int RecentCount = 5;

    readonly IConfigService configService;
    readonly IFileService fileService;
    private readonly IWorkspaceFileService workspaceFileService;
    List<string> modelPaths = [];

    public RecentModelsService(
        IConfigService configService,
        IFileService fileService,
        IWorkspaceFileService workspaceFileService
    )
    {
        this.configService = configService;
        this.fileService = fileService;
        this.workspaceFileService = workspaceFileService;
    }

    public async Task InitAsync()
    {
        if (Build.IsVsCodeExtWasm)
        {
            Log.Info("Get solution paths:");
            var paths = await workspaceFileService.GetSolutionFiles();
            modelPaths = paths.ToList();
            paths.ForEach(path => Log.Info("  Solution", path));
            return;
        }

        modelPaths = (await GetExistingRecentFilePathsAsync()).ToList();
        await configService.SetAsync(c => c.RecentPaths = modelPaths);
    }

    public IReadOnlyList<string> ModelPaths => modelPaths;

    public string LastUsedPath => modelPaths.Any() ? modelPaths[0] : ExampleModel.Path;

    public async Task AddModelAsync(string path)
    {
        if (Build.IsVsCodeExtWasm)
            return;

        modelPaths = (await GetExistingRecentFilePathsAsync()).Prepend(path).Distinct().Take(RecentCount).ToList();
        await configService.SetAsync(c => c.RecentPaths = modelPaths);
    }

    public async Task RemoveModelAsync(string path)
    {
        if (Build.IsVsCodeExtWasm)
            return;

        modelPaths = (await GetExistingRecentFilePathsAsync())
            .Where(rp => rp != path)
            .Distinct()
            .Take(RecentCount)
            .ToList();
        await configService.SetAsync(c => c.RecentPaths = modelPaths);
    }

    async Task<IReadOnlyList<string>> GetExistingRecentFilePathsAsync()
    {
        if (!Try(out var paths, out var e, await fileService.GetFilePathsAsync()))
            return [];
        var recentPaths = (await configService.GetAsync()).RecentPaths;
        return recentPaths.Where(rp => paths.Contains(rp) || rp == ExampleModel.Path).ToList();
    }
}
