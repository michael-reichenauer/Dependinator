using Dependinator.Core.Shared;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Shared;

namespace Dependinator.UI.Tests.Shared;

// The unit tests run as the dev host does: a host with a workspace, whose models are the working
// solution and the model files the workspace file service finds.
public class ModelListServiceTests
{
    const string ToolsPath = "/workspaces/Dependinator/tools/Tools.sln";
    const string BicepPath = "/workspaces/Dependinator/infra/main.bicep";
    const string OtherPath = "/home/me/other/Billing.sln";

    readonly Config config = new();
    readonly Mock<IConfigService> configService = new();
    readonly Mock<IWorkspaceFileService> workspaceFileService = new();
    readonly Mock<IAppCloudSyncService> appCloudSyncService = new();
    readonly List<CloudModelMetadata> cloudModels = [];

    public ModelListServiceTests()
    {
        configService.Setup(c => c.GetAsync()).ReturnsAsync(config);
        configService
            .Setup(c => c.SetAsync(It.IsAny<Action<Config>>()))
            .Callback<Action<Config>>(update => update(config));
        workspaceFileService.Setup(w => w.GetModelFilePathsAsync()).ReturnsAsync([ToolsPath, BicepPath]);
        appCloudSyncService.Setup(a => a.CloudModels).Returns(cloudModels);
    }

    async Task<ModelListService> CreateAsync()
    {
        var service = new ModelListService(
            configService.Object,
            workspaceFileService.Object,
            appCloudSyncService.Object
        );
        await service.InitAsync();
        return service;
    }

    [Fact]
    public async Task GetWorkspaceModels_ShouldListTheWorkspaceFilesWithTheirFolders()
    {
        var service = await CreateAsync();

        var models = service.GetWorkspaceModels();

        Assert.True(service.HasWorkspace);
        Assert.Equal([DemoModel.WorkingSolutionPath, ToolsPath, BicepPath], models.Select(m => m.Item.Path));
        Assert.Equal(["", "tools", "infra"], models.Select(m => m.Folder));
    }

    [Fact]
    public async Task GetWorkspaceModels_ShouldLeaveOutCloudDemoAndOtherFolderModels()
    {
        cloudModels.Add(Cloud("/elsewhere/Cloud.sln"));
        var service = await CreateAsync();
        await service.AddModelAsync(OtherPath);

        var models = service.GetWorkspaceModels();

        Assert.Equal(3, models.Count);
        Assert.All(models, m => Assert.True(service.IsWorkspacePath(m.Item.Path)));
        // They are still among all models, for the Models dialog.
        var allPaths = service.GetModelItems().Select(i => i.Path).ToList();
        Assert.Contains(OtherPath, allPaths);
        Assert.Contains("/elsewhere/Cloud.sln", allPaths);
        Assert.Contains(DemoModel.DemoSolutionName, allPaths);
        Assert.Contains(DemoModel.DemoBicepName, allPaths);
    }

    [Fact]
    public async Task GetWorkspaceModels_ShouldMarkAWorkspaceModelThatIsSynced()
    {
        cloudModels.Add(Cloud(ToolsPath));
        var service = await CreateAsync();

        var tools = service.GetWorkspaceModels().Single(m => m.Item.Path == ToolsPath);

        Assert.True(tools.Item.IsLocal);
        Assert.True(tools.Item.IsCloud);
    }

    [Fact]
    public async Task GetWorkspaceModels_ShouldKeepTheWorkspacesSpellingOfAPath()
    {
        var service = await CreateAsync();
        await service.AddModelAsync(ToolsPath.ToUpperInvariant().Replace('/', '\\'));

        var tools = service.GetWorkspaceModels()[1];

        Assert.Equal(ToolsPath, tools.Item.Path);
        Assert.True(service.IsLocalPath(tools.Item.Path));
    }

    // The model of the whole workspace is listed first, before the models of its single sources,
    // but it is not what a new workspace starts on or goes back to.
    [Fact]
    public async Task GetWorkspaceModels_ShouldListTheModelOfTheWholeWorkspaceFirst()
    {
        workspaceFileService.Setup(w => w.GetWorkspaceFoldersAsync()).ReturnsAsync(["/workspaces/Dependinator"]);
        var service = await CreateAsync();

        var models = service.GetWorkspaceModels();

        const string workspaceModelPath = "/workspaces/Dependinator/.workspace";
        Assert.Equal(workspaceModelPath, service.WorkspaceModelPath);
        Assert.Equal(
            [workspaceModelPath, DemoModel.WorkingSolutionPath, ToolsPath, BicepPath],
            models.Select(m => m.Item.Path)
        );
        Assert.Equal(["Dependinator", "whole workspace"], [models[0].Item.DisplayName, models[0].Folder]);
        Assert.Equal(["", "tools", "infra"], models.Skip(1).Select(m => m.Folder));

        Assert.True(service.IsWorkspacePath(workspaceModelPath));
        Assert.True(service.IsLocalPath(workspaceModelPath));
        Assert.Null(service.GetVisitedModel(workspaceModelPath));
        Assert.Equal([ToolsPath, BicepPath], service.WorkspaceSourcePaths);
        Assert.Equal("/workspaces/Dependinator", service.WorkspaceFolder);

        Assert.Equal(DemoModel.WorkingSolutionPath, service.LocalPaths[0]);
        Assert.Equal(DemoModel.WorkingSolutionPath, service.HomePath);

        // Once it is opened, it is the workspace model to go back to
        await service.AddModelAsync(workspaceModelPath);
        Assert.Equal(workspaceModelPath, service.HomePath);
    }

    // With one source the model of the workspace would be the same as that source's own model,
    // so it is not offered. It is still this workspace's model: one that is open (a source was
    // removed, or it was opened from the recent models) refreshes and is no visit.
    [Fact]
    public async Task GetWorkspaceModels_ShouldLeaveOutTheModelOfTheWholeWorkspace_WithFewerThanTwoSources()
    {
        const string workspaceModelPath = "/workspaces/Dependinator/.workspace";
        workspaceFileService.Setup(w => w.GetWorkspaceFoldersAsync()).ReturnsAsync(["/workspaces/Dependinator"]);
        workspaceFileService.Setup(w => w.GetModelFilePathsAsync()).ReturnsAsync([ToolsPath]);
        var service = await CreateAsync();

        Assert.Equal(workspaceModelPath, service.WorkspaceModelPath);
        Assert.Equal([DemoModel.WorkingSolutionPath, ToolsPath], service.GetWorkspaceModels().Select(m => m.Item.Path));
        Assert.DoesNotContain(workspaceModelPath, service.LocalPaths);
        Assert.True(service.IsLocalPath(workspaceModelPath));
        Assert.True(service.IsLocalPath("/Workspaces/Dependinator/.workspace"));
        Assert.True(service.IsWorkspacePath(workspaceModelPath));
        Assert.Null(service.GetVisitedModel(workspaceModelPath));

        // Without a source there is nothing to parse it from
        workspaceFileService.Setup(w => w.GetModelFilePathsAsync()).ReturnsAsync([]);
        await service.RefreshWorkspaceSourcesAsync();
        Assert.False(service.IsLocalPath(workspaceModelPath));

        // A project added later is found when the sources are listed again
        workspaceFileService.Setup(w => w.GetModelFilePathsAsync()).ReturnsAsync([ToolsPath, BicepPath]);
        await service.RefreshWorkspaceSourcesAsync();

        Assert.Equal(4, service.GetWorkspaceModels().Count);
        Assert.Contains(workspaceModelPath, service.LocalPaths);
    }

    [Theory]
    [InlineData("c:\\repo\\.workspace", "C:/repo/.workspace", true)]
    [InlineData("/repo/.workspace", "/repo/.workspace", true)]
    [InlineData("/repo/.workspace", "/other/.workspace", false)]
    [InlineData("/repo/.workspace", null, false)]
    [InlineData("", "", false)]
    public void IsSamePath_ShouldCompareThePathsAsOneModelsPaths(string path1, string? path2, bool expected) =>
        Assert.Equal(expected, ModelListService.IsSamePath(path1, path2));

    [Fact]
    public async Task GetVisitedModel_ShouldBeNull_ForAWorkspaceModel()
    {
        var service = await CreateAsync();

        Assert.Null(service.GetVisitedModel(ToolsPath));
        Assert.Null(service.GetVisitedModel(ToolsPath.Replace('/', '\\')));
        Assert.Null(service.GetVisitedModel(""));
    }

    [Fact]
    public async Task GetVisitedModel_ShouldBeTheOpenModel_WhenItIsNotOfTheWorkspace()
    {
        cloudModels.Add(Cloud("/elsewhere/Cloud.sln"));
        var service = await CreateAsync();

        Assert.Equal("cloud model", KindOf(service.GetVisitedModel("/elsewhere/Cloud.sln")));
        Assert.Equal("demo model", KindOf(service.GetVisitedModel(DemoModel.DemoBicepName)));
        Assert.Equal("design model", KindOf(service.GetVisitedModel("Target architecture")));
        // In none of the lists, as a model opened by a share link.
        Assert.Equal("not in this workspace", KindOf(service.GetVisitedModel(OtherPath)));
    }

    [Fact]
    public async Task HomePath_ShouldBeTheWorkspaceModelUsedLast()
    {
        var service = await CreateAsync();
        Assert.Equal(DemoModel.WorkingSolutionPath, service.HomePath);

        await service.AddModelAsync(BicepPath);
        await service.AddModelAsync(DemoModel.DemoBicepName);
        await service.AddModelAsync(OtherPath);

        Assert.Equal(BicepPath, service.HomePath);
    }

    [Fact]
    public async Task HomePath_ShouldBeWhatThisWindowOpenedLast_WhenAnotherWindowChangedTheRecentPaths()
    {
        var service = await CreateAsync();
        await service.AddModelAsync(BicepPath);

        config.RecentPaths = [ToolsPath]; // Another window opened its own model
        await service.AddModelAsync(DemoModel.DemoBicepName);

        Assert.Equal(BicepPath, service.HomePath);
    }

    static string KindOf(ModelItem? item) => WorkspaceModels.VisitKind(Assert.IsType<ModelItem>(item));

    static CloudModelMetadata Cloud(string path) =>
        new(CloudModelPath.CreateKey(path), CloudModelPath.Normalize(path), DateTimeOffset.UtcNow, "hash", 1);
}
