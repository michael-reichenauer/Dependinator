using Dependinator.Diagrams;
using Dependinator.Modeling;
using Dependinator.Modeling.Persistence;
using Dependinator.Shared;
using Dependinator.Shared.CloudSync;
using Dependinator.Shared.Types;
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
            LatestSync = new CloudSyncLatest(
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                CloudSyncDirection.Down,
                syncedHash
            ),
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
            LatestSync = new CloudSyncLatest(
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                CloudSyncDirection.Down,
                syncedHash
            ),
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
    public async Task AutoSync_ShouldNotPushOrPull_WhenConflictExists()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto localModel = CreateModelDto("local");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new()
        {
            LatestSync = new CloudSyncLatest(
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                CloudSyncDirection.Down,
                syncedHash
            ),
        };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], CreateFastTimings());

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(50);

        context.CloudSyncService.Verify(x => x.PushAsync(It.IsAny<string>(), It.IsAny<ModelDto>()), Times.Never);
        context.CloudSyncService.Verify(x => x.PullAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AutoSync_ShouldPush_WhenOnlyLocalChanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto localModel = CreateModelDto("local");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], CreateFastTimings());

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        Assert.True(context.Counters.PushCalls > 0);
        Assert.Equal(0, context.Counters.PullCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldPull_WhenOnlyRemoteChanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], CreateFastTimings());

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PullCalls > 0);

        Assert.True(context.Counters.PullCalls > 0);
        Assert.Equal(0, context.Counters.PushCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldPull_WhenNoLocalBaselineButCloudModelExists()
    {
        string modelPath = "/models/sample.model";
        ModelDto localModel = CreateModelDto("local");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(
            modelPath,
            localModel,
            syncState: null,
            [cloudModel],
            CreateFastTimings()
        );

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PullCalls > 0);

        Assert.True(context.Counters.PullCalls > 0);
        Assert.Equal(0, context.Counters.PushCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldPush_WhenNoLocalBaselineAndNoCloudModelExists()
    {
        string modelPath = "/models/sample.model";
        ModelDto localModel = CreateModelDto("local");
        SutContext context = CreateSutContext(
            modelPath,
            localModel,
            syncState: null,
            cloudModels: [],
            CreateFastTimings()
        );

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        Assert.True(context.Counters.PushCalls > 0);
        Assert.Equal(0, context.Counters.PullCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldThrottleAttempts_ByConfiguredMinimumInterval()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto localModel = CreateModelDto("local");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        AppCloudSyncTimings timings = new(
            ActiveRefreshInterval: TimeSpan.FromMilliseconds(5),
            AutoSyncMinInterval: TimeSpan.FromMilliseconds(80),
            IdleRefreshInterval: TimeSpan.FromHours(1),
            MaxIdleRefreshDuration: TimeSpan.Zero
        );
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], timings);

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);
        int firstPushCount = context.Counters.PushCalls;

        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(30);

        Assert.Equal(firstPushCount, context.Counters.PushCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldRunIdleChecks_ForConfiguredWindowThenStop()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        AppCloudSyncTimings timings = new(
            ActiveRefreshInterval: TimeSpan.FromMilliseconds(5),
            AutoSyncMinInterval: TimeSpan.FromHours(1),
            IdleRefreshInterval: TimeSpan.FromMilliseconds(20),
            MaxIdleRefreshDuration: TimeSpan.FromMilliseconds(60)
        );
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], timings);

        await context.Sut.InitializeAsync();
        int listCallsAfterInitialize = context.Counters.ListCalls;

        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(120);

        int callsAfterIdleWindow = context.Counters.ListCalls;
        Assert.True(callsAfterIdleWindow >= listCallsAfterInitialize + 3);

        await Task.Delay(80);
        Assert.Equal(callsAfterIdleWindow, context.Counters.ListCalls);
    }

    [Fact]
    public async Task BackgroundSyncError_ShouldOnlyNotifyFirstFailurePerMessage()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto localModel = CreateModelDto("local");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        AppCloudSyncTimings timings = new(
            ActiveRefreshInterval: TimeSpan.FromMilliseconds(5),
            AutoSyncMinInterval: TimeSpan.FromMilliseconds(10),
            IdleRefreshInterval: TimeSpan.FromHours(1),
            MaxIdleRefreshDuration: TimeSpan.Zero
        );
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], timings);
        List<string> errorMessages = [];
        context.Sut.BackgroundSyncError += message => errorMessages.Add(message);
        context
            .CloudSyncService.Setup(x => x.PushAsync(It.IsAny<string>(), It.IsAny<ModelDto>()))
            .Callback(() => context.Counters.PushCalls++)
            .ReturnsAsync(R.Error("Push failed."));

        await context.Sut.InitializeAsync();
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        await Task.Delay(25);
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 1);

        Assert.Single(errorMessages);
        Assert.Equal("Push failed.", errorMessages[0]);
    }

    [Fact]
    public async Task InitializeAsync_ShouldReturnError_WhenCloudModelsRefreshFails()
    {
        string modelPath = "/models/sample.model";
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState: null, cloudModels: []);
        context.CloudSyncService.Setup(x => x.ListAsync()).ReturnsAsync(R.Error("Cloud model list failed."));

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
            LatestSync = new CloudSyncLatest(
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                CloudSyncDirection.Down,
                syncedHash
            ),
        };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState, [cloudModel]);
        context
            .CloudSyncService.Setup(x => x.LogoutAsync())
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

    [Fact]
    public async Task SyncDownAsync_ShouldStaySynced_WhenLoadedLocalHashDiffersFromRemoteHash()
    {
        string modelPath = "/models/sample.model";
        ModelDto localLoadedModel = CreateModelDto("local-loaded");
        ModelDto remotePulledModel = CreateModelDto("remote-pulled");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("stale-remote-list"));
        SutContext context = CreateSutContext(modelPath, localLoadedModel, syncState: null, [cloudModel]);

        context.CloudSyncService.Setup(x => x.PullAsync(modelPath)).ReturnsAsync(remotePulledModel);

        await context.Sut.InitializeAsync();
        R<ModelInfo> syncDownResult = await context.Sut.SyncDownAsync();

        Assert.True(syncDownResult);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
        Assert.False(context.Sut.HasLocalChangesSinceLastSync);
        Assert.False(context.Sut.HasRemoteChangesSinceLastSync);
    }

    static AppCloudSyncService CreateSut(
        string modelPath,
        ModelDto currentModelDto,
        CloudSyncModelState? syncState,
        IReadOnlyList<CloudModelMetadata> cloudModels,
        AppCloudSyncTimings? timings = null
    )
    {
        return CreateSutContext(modelPath, currentModelDto, syncState, cloudModels, timings).Sut;
    }

    static SutContext CreateSutContext(
        string modelPath,
        ModelDto currentModelDto,
        CloudSyncModelState? syncState,
        IReadOnlyList<CloudModelMetadata> cloudModels,
        AppCloudSyncTimings? timings = null
    )
    {
        Mock<ICanvasService> canvasService = new();
        Mock<ICloudSyncService> cloudSyncService = new();
        Mock<ICloudSyncStateService> cloudSyncStateService = new();
        Mock<IModelService> modelService = new();
        Mock<IModelMgr> modelMgr = new();
        ApplicationEvents applicationEvents = new();
        SyncCallCounters counters = new();

        cloudSyncService.SetupGet(x => x.IsAvailable).Returns(true);
        cloudSyncService
            .Setup(x => x.GetAuthStateAsync())
            .ReturnsAsync(new CloudAuthState(IsAvailable: true, IsAuthenticated: true, User: null));
        cloudSyncService
            .Setup(x => x.ListAsync())
            .Callback(() => counters.ListCalls++)
            .ReturnsAsync(new CloudModelList(cloudModels));
        cloudSyncService
            .Setup(x => x.PushAsync(It.IsAny<string>(), It.IsAny<ModelDto>()))
            .Callback(() => counters.PushCalls++)
            .ReturnsAsync(CreateCloudModelMetadata(modelPath, currentModelDto));
        cloudSyncService
            .Setup(x => x.PullAsync(It.IsAny<string>()))
            .Callback(() => counters.PullCalls++)
            .ReturnsAsync(currentModelDto);

        cloudSyncStateService.Setup(x => x.GetAsync(modelPath)).ReturnsAsync(syncState);
        cloudSyncStateService
            .Setup(x => x.RecordPushAsync(It.IsAny<string>(), It.IsAny<CloudModelMetadata>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        cloudSyncStateService
            .Setup(x => x.RecordPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        modelMgr.SetupGet(x => x.ModelPath).Returns(modelPath);
        modelService.Setup(x => x.GetCurrentModelDto()).Returns(currentModelDto);
        modelService
            .Setup(x => x.ReplaceCurrentModelAsync(It.IsAny<ModelDto>()))
            .ReturnsAsync(new ModelInfo(modelPath, Rect.None, 0));
        modelService.Setup(x => x.WriteModelAsync(It.IsAny<string>(), It.IsAny<ModelDto>())).ReturnsAsync(R.Ok);
        canvasService.Setup(x => x.LoadAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        return new SutContext(
            new AppCloudSyncService(
                canvasService.Object,
                cloudSyncService.Object,
                cloudSyncStateService.Object,
                modelService.Object,
                modelMgr.Object,
                applicationEvents,
                timings
            ),
            cloudSyncService,
            cloudSyncStateService,
            modelService,
            applicationEvents,
            counters
        );
    }

    sealed record SutContext(
        AppCloudSyncService Sut,
        Mock<ICloudSyncService> CloudSyncService,
        Mock<ICloudSyncStateService> CloudSyncStateService,
        Mock<IModelService> ModelService,
        ApplicationEvents ApplicationEvents,
        SyncCallCounters Counters
    );

    sealed class SyncCallCounters
    {
        public int PushCalls;
        public int PullCalls;
        public int ListCalls;
    }

    static AppCloudSyncTimings CreateFastTimings()
    {
        return new AppCloudSyncTimings(
            ActiveRefreshInterval: TimeSpan.FromMilliseconds(5),
            AutoSyncMinInterval: TimeSpan.FromMilliseconds(5),
            IdleRefreshInterval: TimeSpan.FromHours(1),
            MaxIdleRefreshDuration: TimeSpan.Zero
        );
    }

    static CloudSyncModelState CreateSyncStateFromModel(ModelDto modelDto)
    {
        return new CloudSyncModelState()
        {
            LatestSync = new CloudSyncLatest(
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
                CloudSyncDirection.Down,
                CloudModelSerializer.GetContentHash(modelDto)
            ),
        };
    }

    static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMilliseconds = 500)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(predicate());
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
