using Dependinator.Core;
using Dependinator.Core.Shared;
using Dependinator.UI.Shared.CloudSync;

namespace Dependinator.UI.Shared;

record ModelItem(
    string Path,
    string DisplayName,
    bool IsLocal,
    bool IsCloud,
    global::Shared.CloudModelMetadata? CloudModel,
    bool IsDemo = false
);

// A model of the workspace as the Models menu lists it, with the folder that tells files of one
// name apart (see WorkspaceModels.RelativeFolders).
record WorkspaceModel(ModelItem Item, string Folder);

interface IModelListService
{
    Task InitAsync();

    IReadOnlyList<string> RecentModelPaths { get; }
    IReadOnlyList<string> LocalPaths { get; }
    IReadOnlyList<string> CloudPaths { get; }

    // The built-in demo models.
    IReadOnlyList<string> DemoPaths { get; }
    string? LastUsedPath { get; }
    string? StartupPath { get; }
    bool IsLocalPath(string path);

    // Whether this host shows the solutions and Bicep files of a folder (VS Code, the dev host).
    // There the workspace's models are what the user switches between, and a cloud, design or
    // demo model is a visit; the browser app has no workspace, only such models.
    bool HasWorkspace { get; }
    bool IsWorkspacePath(string path);
    IReadOnlyList<WorkspaceModel> GetWorkspaceModels();

    // The path of the model of the whole workspace: the sources found in the workspace folder
    // that the user chose, in one diagram. Null without a workspace folder. It is listed among
    // the workspace's models with two or more sources; with one it would be the same as that
    // source's own model. Listed or not, it is a workspace path, and a local path (it is
    // parsed here) while there is a source: a model that is open stays what it is when a source
    // is removed.
    string? WorkspaceModelPath { get; }

    // The sources found in the workspace (solutions, Bicep deployments, script projects), which
    // its workspace model can be made of, and the folder their keys are relative to.
    IReadOnlyList<string> WorkspaceSourcePaths { get; }
    string WorkspaceFolder { get; }

    // Lists the workspace's sources again: a project can have been added since the start.
    Task RefreshWorkspaceSourcesAsync();

    // The model that is open when it is none of the workspace's (null in a host without a
    // workspace), and the workspace model to go back to from it: the one used last.
    ModelItem? GetVisitedModel(string currentPath);
    string? HomePath { get; }

    IReadOnlyList<ModelItem> GetModelItems();

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
    IReadOnlyList<string> workspacePaths = [];
    IReadOnlyList<string> workspaceSourcePaths = [];
    string workspaceFolder = "";
    HashSet<string> workspaceKeys = new(StringComparer.OrdinalIgnoreCase);
    string? lastWorkspacePath;

    public IReadOnlyList<string> RecentModelPaths => recentPaths;
    public IReadOnlyList<string> LocalPaths => localPaths;
    public IReadOnlyList<string> CloudPaths => cloudPaths;
    public IReadOnlyList<string> DemoPaths => DemoModel.DemoNames;
    public string? LastUsedPath => recentPaths.Any() ? recentPaths[0] : null;

    // The model to show when the app starts. Browser hosts resume the most recently used
    // model, but the VS Code extension is opened for a specific workspace, so it starts on
    // that workspace's solution instead of a model last used in some other workspace. The
    // last used model is still kept when it is one of this workspace's solutions, since a
    // workspace can contain several.
    public string? StartupPath
    {
        get
        {
            if (!Build.IsVsCodeExtWasm)
                return LastUsedPath;

            if (LastUsedPath is not null && IsLocalPath(LastUsedPath))
                return LastUsedPath;

            return localPaths.FirstOrDefault() ?? LastUsedPath;
        }
    }

    public bool IsLocalPath(string path) =>
        LocalPaths.Contains(path) || (workspaceSourcePaths.Count > 0 && IsSamePath(path, WorkspaceModelPath));

    public bool HasWorkspace => Build.IsVsCodeExtWasm || Build.IsWeb;

    public bool IsWorkspacePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && (
            workspaceKeys.Contains(global::Shared.CloudModelPath.Normalize(path))
            || IsSamePath(path, WorkspaceModelPath)
        );

    public string? WorkspaceModelPath =>
        HasWorkspace && workspaceFolder != "" ? ModelPaths.WorkspacePath(workspaceFolder) : null;

    // The workspace model when it is one of the models to switch between.
    string? ListedWorkspaceModelPath => workspaceSourcePaths.Count >= 2 ? WorkspaceModelPath : null;

    public IReadOnlyList<string> WorkspaceSourcePaths => workspaceSourcePaths;
    public string WorkspaceFolder => workspaceFolder;

    public IReadOnlyList<WorkspaceModel> GetWorkspaceModels()
    {
        var items = GetModelItems();
        var folders = WorkspaceModels.RelativeFolders(workspacePaths);

        // The workspace's own spelling of the path, not that of an equal recent path:
        // parsing and the startup model compare paths exactly.
        WorkspaceModel ToModel(string path, string folder) =>
            new(
                (items.FirstOrDefault(i => IsSamePath(i.Path, path)) ?? ToLocalItem(path)) with
                {
                    Path = path,
                    DisplayName = ModelPaths.DisplayName(path),
                },
                folder
            );

        // The model of the whole workspace comes first, before the models of its single sources.
        return
        [
            .. ListedWorkspaceModelPath is { } workspaceModelPath
                ? new[] { ToModel(workspaceModelPath, WorkspaceModels.WholeWorkspace) }
                : [],
            .. workspacePaths.Select((path, index) => ToModel(path, folders[index])),
        ];
    }

    public ModelItem? GetVisitedModel(string currentPath)
    {
        if (!HasWorkspace || string.IsNullOrWhiteSpace(currentPath) || IsWorkspacePath(currentPath))
            return null;

        // A model opened by a share link is in none of the lists.
        return GetModelItems().FirstOrDefault(i => IsSamePath(i.Path, currentPath)) ?? ToLocalItem(currentPath);
    }

    // The recent paths are shared by every window of the app, so what this one opened last wins.
    public string? HomePath =>
        lastWorkspacePath ?? recentPaths.FirstOrDefault(IsWorkspacePath) ?? workspacePaths.FirstOrDefault();

    // Whether two model paths are of one model: paths are compared as the cloud names them, so
    // the case of a drive letter or the kind of slash makes no difference.
    public static bool IsSamePath(string? path1, string? path2) =>
        !string.IsNullOrWhiteSpace(path1)
        && !string.IsNullOrWhiteSpace(path2)
        && string.Equals(
            global::Shared.CloudModelPath.Normalize(path1),
            global::Shared.CloudModelPath.Normalize(path2),
            StringComparison.OrdinalIgnoreCase
        );

    static ModelItem ToLocalItem(string path) =>
        new(
            path,
            ModelPaths.DisplayName(path),
            IsLocal: true,
            IsCloud: false,
            CloudModel: null,
            IsDemo: DemoModel.IsDemo(path)
        );

    public IReadOnlyList<ModelItem> GetModelItems()
    {
        Dictionary<string, ModelItem> itemsByNormalizedPath = new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedNormalizedPaths = [];

        void AddLocal(string path)
        {
            string normalizedPath = global::Shared.CloudModelPath.Normalize(path);
            if (itemsByNormalizedPath.ContainsKey(normalizedPath))
                return;

            itemsByNormalizedPath[normalizedPath] = ToLocalItem(path);
            orderedNormalizedPaths.Add(normalizedPath);
        }

        recentPaths.ForEach(AddLocal);
        localPaths.ForEach(AddLocal);
        DemoPaths.ForEach(AddLocal);

        foreach (
            global::Shared.CloudModelMetadata cloudModel in appCloudSyncService.CloudModels.OrderByDescending(cm =>
                cm.UpdatedUtc
            )
        )
        {
            string normalizedPath = global::Shared.CloudModelPath.Normalize(cloudModel.NormalizedPath);
            if (itemsByNormalizedPath.TryGetValue(normalizedPath, out ModelItem? item))
            {
                itemsByNormalizedPath[normalizedPath] = item with { IsCloud = true, CloudModel = cloudModel };
                continue;
            }

            itemsByNormalizedPath[normalizedPath] = new ModelItem(
                cloudModel.NormalizedPath,
                ModelPaths.DisplayName(cloudModel.NormalizedPath),
                IsLocal: false,
                IsCloud: true,
                CloudModel: cloudModel
            );
            orderedNormalizedPaths.Add(normalizedPath);
        }

        return orderedNormalizedPaths.Select(np => itemsByNormalizedPath[np]).ToList();
    }

    public async Task InitAsync()
    {
        recentPaths = (await configService.GetAsync()).RecentPaths;
        cloudPaths = GetCloudPaths();
        await RefreshWorkspaceSourcesAsync();

        // Seed recent paths with the first available model (cloud for standalone Wasm)
        if (!recentPaths.Any())
        {
            var defaultPaths = Build.IsStandaloneWasm ? cloudPaths : localPaths;
            if (defaultPaths.Any())
                recentPaths = [defaultPaths[0]];
        }

        await configService.SetAsync(c => c.RecentPaths = recentPaths.ToList());
    }

    public async Task RefreshWorkspaceSourcesAsync()
    {
        workspaceSourcePaths = HasWorkspace ? await workspaceFileService.GetModelFilePathsAsync() ?? [] : [];
        workspaceFolder = HasWorkspace
            ? (await workspaceFileService.GetWorkspaceFoldersAsync())?.FirstOrDefault() ?? ""
            : "";

        // Standalone Wasm has no local models. The Blazor Server host keeps the working solution
        // first and adds whatever else its workspace folder holds (e.g. sample Bicep deployments).
        IReadOnlyList<string> sourceModelPaths =
            Build.IsVsCodeExtWasm ? workspaceSourcePaths
            : Build.IsWeb ? [DemoModel.WorkingSolutionPath, .. workspaceSourcePaths]
            : [];
        sourceModelPaths = sourceModelPaths.Distinct().ToList();

        // The workspace model is a local model too (it is parsed here), but it comes last: the
        // first local path is the model a new workspace starts on, which stays a single source.
        localPaths = ListedWorkspaceModelPath is { } workspaceModelPath
            ? [.. sourceModelPaths, workspaceModelPath]
            : sourceModelPaths;

        // The test host opens the embedded demo instead of parsing the working solution, so there
        // the demo stands in for it as the workspace's first model.
        workspacePaths =
            !HasWorkspace ? []
            : Build.IsTestMode ? [DemoModel.Path, .. sourceModelPaths]
            : sourceModelPaths;
        workspaceKeys = workspacePaths
            .Select(global::Shared.CloudModelPath.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddModelAsync(string path)
    {
        if (IsWorkspacePath(path))
            lastWorkspacePath = path;

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
