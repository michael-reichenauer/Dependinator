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
