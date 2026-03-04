using Dependinator.Diagrams;
using Dependinator.Models;
using Dependinator.Shared;
using Dependinator.Shared.CloudSync;
using Shared;

namespace Dependinator.Tests.Shared;

public class AppCloudSyncServiceTests
{
    [Fact]
    public async Task GetCloudSyncState_ShouldReturnHasRemoteChanges_WhenCloudCopyChangedSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new()
        {
            LastPullUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            LastPullContentHash = syncedHash,
        };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        AppCloudSyncService sut = CreateSut(modelPath, syncedModel, syncState, [cloudModel]);

        await sut.InitializeAsync();

        Assert.Equal(CloudSyncState.HasRemoteChanges, sut.GetCloudSyncState());
        Assert.False(sut.HasLocalChangesSinceLastSync);
        Assert.True(sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task GetCloudSyncState_ShouldReturnHasConflicts_WhenLocalAndCloudChangedSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new()
        {
            LastPullUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            LastPullContentHash = syncedHash,
        };
        ModelDto localModel = CreateModelDto("local");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        AppCloudSyncService sut = CreateSut(modelPath, localModel, syncState, [cloudModel]);

        await sut.InitializeAsync();

        Assert.Equal(CloudSyncState.HasConflicts, sut.GetCloudSyncState());
        Assert.True(sut.HasLocalChangesSinceLastSync);
        Assert.True(sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task InitializeAsync_ShouldReturnError_WhenCloudModelsRefreshFails()
    {
        string modelPath = "/models/sample.model";
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState: null, cloudModels: []);
        context.CloudSyncService
            .Setup(x => x.ListAsync())
            .ReturnsAsync(R.Error("Cloud model list failed."));

        R result = await context.Sut.InitializeAsync();

        Assert.False(result);
        Assert.Equal("Cloud model list failed.", result.ErrorMessage);
        Assert.Empty(context.Sut.CloudModels);
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearSyncSnapshot()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new()
        {
            LastPullUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            LastPullContentHash = syncedHash,
        };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState, [cloudModel]);
        context.CloudSyncService
            .Setup(x => x.LogoutAsync())
            .ReturnsAsync(new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null));

        await context.Sut.InitializeAsync();

        R result = await context.Sut.LogoutAsync();

        Assert.True(result);
        Assert.Null(context.Sut.SyncState);
        Assert.Null(context.Sut.LatestSync);
        Assert.False(context.Sut.HasLocalChangesSinceLastSync);
        Assert.False(context.Sut.HasRemoteChangesSinceLastSync);
        Assert.Empty(context.Sut.CloudModels);
        Assert.Equal(CloudSyncState.NotAuthenticated, context.Sut.GetCloudSyncState());
    }

    static AppCloudSyncService CreateSut(
        string modelPath,
        ModelDto currentModelDto,
        CloudSyncModelState? syncState,
        IReadOnlyList<CloudModelMetadata> cloudModels
    )
    {
        return CreateSutContext(modelPath, currentModelDto, syncState, cloudModels).Sut;
    }

    static SutContext CreateSutContext(
        string modelPath,
        ModelDto currentModelDto,
        CloudSyncModelState? syncState,
        IReadOnlyList<CloudModelMetadata> cloudModels
    )
    {
        Mock<ICanvasService> canvasService = new();
        Mock<ICloudSyncService> cloudSyncService = new();
        Mock<ICloudSyncStateService> cloudSyncStateService = new();
        Mock<IModelService> modelService = new();
        ApplicationEvents applicationEvents = new();

        cloudSyncService.SetupGet(x => x.IsAvailable).Returns(true);
        cloudSyncService
            .Setup(x => x.GetAuthStateAsync())
            .ReturnsAsync(new CloudAuthState(IsAvailable: true, IsAuthenticated: true, User: null));
        cloudSyncService.Setup(x => x.ListAsync()).ReturnsAsync(new CloudModelList(cloudModels));

        cloudSyncStateService.Setup(x => x.GetAsync(modelPath)).ReturnsAsync(syncState);

        modelService.SetupGet(x => x.ModelPath).Returns(modelPath);
        modelService.Setup(x => x.GetCurrentModelDto()).Returns(currentModelDto);

        return new SutContext(
            new AppCloudSyncService(
                canvasService.Object,
                cloudSyncService.Object,
                cloudSyncStateService.Object,
                modelService.Object,
                applicationEvents
            ),
            cloudSyncService,
            cloudSyncStateService,
            modelService
        );
    }

    sealed record SutContext(
        AppCloudSyncService Sut,
        Mock<ICloudSyncService> CloudSyncService,
        Mock<ICloudSyncStateService> CloudSyncStateService,
        Mock<IModelService> ModelService
    );

    static CloudModelMetadata CreateCloudModelMetadata(string modelPath, ModelDto modelDto)
    {
        string normalizedPath = CloudModelPath.Normalize(modelPath);
        return new CloudModelMetadata(
            ModelKey: CloudModelPath.CreateKey(normalizedPath),
            NormalizedPath: normalizedPath,
            UpdatedUtc: new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
            ContentHash: CloudModelSerializer.GetContentHash(modelDto),
            CompressedSizeBytes: 10
        );
    }

    static ModelDto CreateModelDto(string name)
    {
        return new ModelDto()
        {
            Name = name,
            Nodes = [],
            Links = [],
        };
    }
}
