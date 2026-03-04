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

    static AppCloudSyncService CreateSut(
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

        return new AppCloudSyncService(
            canvasService.Object,
            cloudSyncService.Object,
            cloudSyncStateService.Object,
            modelService.Object,
            applicationEvents
        );
    }

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
