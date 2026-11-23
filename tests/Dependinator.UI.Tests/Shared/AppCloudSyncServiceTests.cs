using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Dependinator.UI.Shared.Types;
using Shared;

namespace Dependinator.UI.Tests.Shared;

public class AppCloudSyncServiceTests
{
    [Fact]
    public async Task GetCloudSyncState_ShouldReturnHasRemoteChanges_WhenCloudCopyChangedSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(syncedHash, syncedHash) };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        AppCloudSyncService sut = CreateSut(modelPath, syncedModel, syncState, [cloudModel]);

        await sut.RefreshSyncStateAsync();

        Assert.Equal(CloudSyncState.HasRemoteChanges, sut.GetCloudSyncState());
        Assert.False(sut.HasLocalChangesSinceLastSync);
        Assert.True(sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task HasLoadedCloudModels_ShouldTurnTrue_OnceTheModelListHasBeenRead()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("synced");
        string hash = CloudModelSerializer.GetContentHash(model);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(hash, hash) };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model);
        AppCloudSyncService sut = CreateSut(modelPath, model, syncState, [cloudModel]);

        // Before the first refresh nothing is known: a share link to a cloud model has to wait.
        Assert.False(sut.HasLoadedCloudModels);
        Assert.Empty(sut.CloudModels);

        await sut.RefreshSyncStateAsync();

        Assert.True(sut.HasLoadedCloudModels);
        Assert.Single(sut.CloudModels);
    }

    [Fact]
    public async Task GetCloudSyncState_ShouldReturnHasConflicts_WhenLocalAndCloudChangedSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(syncedHash, syncedHash) };
        ModelDto localModel = CreateModelDto("local");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        AppCloudSyncService sut = CreateSut(modelPath, localModel, syncState, [cloudModel]);
        await sut.RefreshSyncStateAsync();

        Assert.Equal(CloudSyncState.HasConflicts, sut.GetCloudSyncState());
        Assert.True(sut.HasLocalChangesSinceLastSync);
        Assert.True(sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task GetCloudSyncState_ShouldReturnHasRemoteChanges_WhenCloudCopyIsMissingSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(syncedHash, syncedHash) };
        AppCloudSyncService sut = CreateSut(modelPath, syncedModel, syncState, cloudModels: []);

        await sut.RefreshSyncStateAsync();

        Assert.Equal(CloudSyncState.HasRemoteChanges, sut.GetCloudSyncState());
        Assert.False(sut.HasLocalChangesSinceLastSync);
        Assert.True(sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task RefreshSyncStateAsync_ShouldNotHashTheModelAgain_WhileItsContentIsUnchanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("synced");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model);
        SutContext context = CreateSutContext(modelPath, model, CreateSyncStateFromModel(model), [cloudModel]);
        context.ModelService.SetupGet(x => x.ContentStamp).Returns(Stamp(modelPath, saves: 7));

        // Panning and zooming refresh the sync state but leave the content version
        await context.Sut.RefreshSyncStateAsync();
        await context.Sut.RefreshSyncStateAsync();
        await context.Sut.RefreshSyncStateAsync();

        context.ModelService.Verify(x => x.GetCurrentModelDto(), Times.Once);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task RefreshSyncStateAsync_ShouldSeeALocalChange_WhenTheContentStampChanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("synced");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model);
        SutContext context = CreateSutContext(modelPath, model, CreateSyncStateFromModel(model), [cloudModel]);
        var stamp = Stamp(modelPath, saves: 1);
        context.ModelService.SetupGet(x => x.ContentStamp).Returns(() => stamp);
        await context.Sut.RefreshSyncStateAsync();
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());

        // An edit changes the content and requests a save
        context.ModelService.Setup(x => x.GetCurrentModelDto()).Returns(CreateModelDto("edited"));
        await context.Sut.RefreshSyncStateAsync();
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState()); // Same stamp
        stamp = stamp with { Saves = stamp.Saves + 1 };
        await context.Sut.RefreshSyncStateAsync();

        Assert.Equal(CloudSyncState.HasLocalChanges, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task SyncUpAsync_ShouldLeaveTheCheckedHashOnThePushedContent()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("synced");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model);
        SutContext context = CreateSutContext(modelPath, model, CreateSyncStateFromModel(model), [cloudModel]);
        var stamp = Stamp(modelPath, saves: 1);
        context.ModelService.SetupGet(x => x.ContentStamp).Returns(() => stamp);
        await context.Sut.RefreshSyncStateAsync();

        // The content changed since the last check, and the push sends the changed content
        ModelDto edited = CreateModelDto("edited");
        context.ModelService.Setup(x => x.GetCurrentModelDto()).Returns(edited);
        CloudModelMetadata pushed = CreateCloudModelMetadata(modelPath, edited);
        context.CloudSyncService.Setup(x => x.PushAsync(It.IsAny<string>(), It.IsAny<ModelDto>())).ReturnsAsync(pushed);
        context.CloudSyncService.Setup(x => x.ListAsync()).ReturnsAsync(new CloudModelList([pushed]));
        context
            .CloudSyncStateService.Setup(x =>
                x.RecordPushAsync(modelPath, pushed, It.IsAny<string>(), It.IsAny<string>())
            )
            .Callback<string, CloudModelMetadata, string, string>(
                (_, metadata, localHash, _) =>
                    context
                        .CloudSyncStateService.Setup(x => x.GetAsync(modelPath))
                        .ReturnsAsync(new CloudSyncModelState() { Baseline = new(localHash, metadata.ContentHash) })
            )
            .Returns(Task.CompletedTask);
        AssertOk(await context.Sut.SyncUpAsync());
        await context.Sut.RefreshSyncStateAsync();

        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
    }

    static ModelContentStamp Stamp(string modelPath, long saves) => new(modelPath, saves);

    [Fact]
    public async Task AutoSync_ShouldNotPushOrPull_WhenConflictExists()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto localModel = CreateModelDto("local");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(syncedHash, syncedHash) };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], CreateFastTimings());

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

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        Assert.True(context.Counters.PushCalls > 0);
        Assert.Equal(0, context.Counters.PullCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldPush_WhenCloudCopyIsMissingSinceLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        SutContext context = CreateSutContext(
            modelPath,
            syncedModel,
            syncState,
            cloudModels: [],
            timings: CreateFastTimings()
        );

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

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PullCalls > 0);

        Assert.True(context.Counters.PullCalls > 0);
        Assert.Equal(0, context.Counters.PushCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldNotPushOrPull_WhenNoBaselineAndLocalAndCloudDiffer()
    {
        string modelPath = "/models/sample.model";
        ModelDto localModel = CreateModelDto("local");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(
            modelPath,
            localModel,
            syncState: null,
            cloudModels: [cloudModel],
            timings: CreateFastTimings()
        );

        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(50);

        Assert.Equal(0, context.Counters.PullCalls);
        Assert.Equal(0, context.Counters.PushCalls);
        Assert.Equal(CloudSyncState.HasConflicts, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task AutoSync_ShouldStaySynced_WhenNoBaselineAndLocalAndCloudMatch()
    {
        string modelPath = "/models/sample.model";
        ModelDto sharedModel = CreateModelDto("shared");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, sharedModel);
        SutContext context = CreateSutContext(
            modelPath,
            sharedModel,
            syncState: null,
            cloudModels: [cloudModel],
            timings: CreateFastTimings()
        );

        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(50);

        Assert.Equal(0, context.Counters.PullCalls);
        Assert.Equal(0, context.Counters.PushCalls);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
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
            timings: CreateFastTimings()
        );

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
            AutoSyncMinInterval: TimeSpan.FromMinutes(5),
            IdleRefreshInterval: TimeSpan.FromHours(1),
            MaxIdleRefreshDuration: TimeSpan.Zero
        );
        // The clock is controlled by the test so the throttle window cannot expire behind its
        // back on a slow machine; refreshes still run on real (short) timers.
        DateTimeOffset currentUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        SutContext context = CreateSutContext(modelPath, localModel, syncState, [cloudModel], timings, () =>
            currentUtc
        );

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);
        int firstPushCount = context.Counters.PushCalls;

        context.ApplicationEvents.TriggerUIStateChanged();
        await Task.Delay(100);
        Assert.Equal(firstPushCount, context.Counters.PushCalls);

        currentUtc += timings.AutoSyncMinInterval;
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > firstPushCount);
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
        // The idle loop stops as soon as the clock passes its deadline, so with a real clock the
        // delays' overshoot on a loaded machine decides how many idle checks fit into the window
        // (a CI flake saw two instead of three). The test controls the clock instead: the window
        // cannot expire until the test moves the clock, while the checks themselves still run on
        // real (short) timers.
        DateTimeOffset currentUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], timings, () =>
            currentUtc
        );

        int listCallsAfterInitialize = context.Counters.ListCalls;

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(
            () => context.Counters.ListCalls >= listCallsAfterInitialize + 3,
            timeoutMilliseconds: 2000
        );

        // Past the deadline the loop stops at its next iteration without queuing another check;
        // one check whose delay was already running may still land. On a loaded CI machine it
        // landed more than 100 ms later, after a fixed wait had taken its snapshot (4 calls
        // instead of 3, Nov 2026). So wait for the checks to go quiet instead: a loop that never
        // stopped would keep checking every 20 ms and never be quiet for ten intervals.
        currentUtc += timings.MaxIdleRefreshDuration + TimeSpan.FromMilliseconds(1);
        await WaitUntilQuietAsync(() => context.Counters.ListCalls, quietMilliseconds: 200);
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
            .ReturnsAsync(new Error("Push failed."));

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        await Task.Delay(25);
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 1);

        Assert.Single(errorMessages);
        Assert.Equal("Push failed.", errorMessages[0]);
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearSyncSnapshot()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        string syncedHash = CloudModelSerializer.GetContentHash(syncedModel);
        CloudSyncModelState syncState = new() { Baseline = new CloudSyncBaseline(syncedHash, syncedHash) };
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState, [cloudModel]);
        context
            .CloudSyncService.Setup(x => x.LogoutAsync())
            .ReturnsAsync(new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null));

        Result result = await context.Sut.LogoutAsync();

        AssertOk(result);
        Assert.Null(context.Sut.SyncState);

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
        SutContext context = CreateSutContext(modelPath, localLoadedModel, syncState: null, cloudModels: [cloudModel]);

        context.CloudSyncService.Setup(x => x.PullAsync(modelPath)).ReturnsAsync(Pulled(remotePulledModel));

        Result<SyncDownOutcome> syncDownResult = await context.Sut.SyncDownAsync();

        Assert.IsType<ModelInfo>(AssertOk(syncDownResult).Value);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
        Assert.False(context.Sut.HasLocalChangesSinceLastSync);
        Assert.False(context.Sut.HasRemoteChangesSinceLastSync);
    }

    // The cloud's hash of a model is the pushing app's, which another app version makes
    // differently: a baseline made here from the pulled model made the cloud copy look changed
    // right after the pull, and it was pulled again and again.
    [Fact]
    public async Task SyncDownAsync_ShouldStaySynced_WhenTheCloudHashIsNotTheOneMadeHere()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("model");
        string cloudHash = "hash-of-another-app-version";
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model) with { ContentHash = cloudHash };
        SutContext context = CreateSutContext(modelPath, model, syncState: null, cloudModels: [cloudModel]);
        CloudSyncModelState? recorded = null;
        context.CloudSyncStateService.Setup(x => x.GetAsync(modelPath)).ReturnsAsync(() => recorded);
        context
            .CloudSyncStateService.Setup(x => x.RecordPullAsync(modelPath, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>(
                (_, local, remote) => recorded = new() { Baseline = new CloudSyncBaseline(local, remote) }
            )
            .Returns(Task.CompletedTask);
        context.CloudSyncService.Setup(x => x.PullAsync(modelPath)).ReturnsAsync(new PulledModel(model, cloudHash));

        AssertOk(await context.Sut.SyncDownAsync());
        AssertOk(await context.Sut.RefreshSyncStateAsync());

        Assert.Equal(cloudHash, recorded?.Baseline?.RemoteContentHash);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task SyncDownAsync_ShouldUploadLocalModel_WhenNoRemoteModelExists()
    {
        string modelPath = "/models/sample.model";
        ModelDto localModel = CreateModelDto("local");
        SutContext context = CreateSutContext(modelPath, localModel, syncState: null, cloudModels: []);
        Result<PulledModel> noRemoteModel = new NotFoundError("No remote model");
        context
            .CloudSyncService.Setup(x => x.PullAsync(modelPath))
            .Callback(() => context.Counters.PullCalls++)
            .ReturnsAsync(noRemoteModel);

        Result<SyncDownOutcome> syncDownResult = await context.Sut.SyncDownAsync();

        Assert.IsType<UploadedLocalModel>(AssertOk(syncDownResult).Value);
        Assert.True(context.Counters.PushCalls > 0);
        context.ModelService.Verify(
            x => x.ReplaceCurrentModelAsync(It.IsAny<string>(), It.IsAny<ModelDto>(), It.IsAny<bool>()),
            Times.Never
        );
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task AutoSync_ShouldUploadLocalModel_WhenPullFindsNoRemoteModel()
    {
        // The cloud model list still contains the model, but the remote copy is gone by the
        // time the auto-sync pull runs; recovery should push instead of reporting an error.
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], CreateFastTimings());
        Result<PulledModel> noRemoteModel = new NotFoundError("No remote model");
        context
            .CloudSyncService.Setup(x => x.PullAsync(It.IsAny<string>()))
            .Callback(() => context.Counters.PullCalls++)
            .ReturnsAsync(noRemoteModel);
        List<string> errorMessages = [];
        context.Sut.BackgroundSyncError += message => errorMessages.Add(message);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        Assert.True(context.Counters.PullCalls > 0);
        Assert.Empty(errorMessages);
    }

    [Fact]
    public async Task IsConnecting_ShouldBeTrueUntilInitialAuthResolves()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        AppCloudSyncService sut = CreateSut(modelPath, syncedModel, syncState, [cloudModel]);

        Assert.True(sut.IsConnecting);

        await sut.RefreshSyncStateAsync();

        Assert.False(sut.IsConnecting);
    }

    [Fact]
    public async Task IsConnecting_ShouldBecomeFalse_EvenWhenAuthStateCallFails()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, cloudModels: []);
        context.CloudSyncService.Setup(x => x.GetAuthStateAsync()).ReturnsAsync(new Error("auth failed"));

        Assert.True(context.Sut.IsConnecting);

        await context.Sut.RefreshSyncStateAsync();

        Assert.False(context.Sut.IsConnecting);
    }

    [Fact]
    public async Task DeleteCurrentCloudModel_ShouldClearBaselineAndCloudModelList()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel]);
        context.CloudSyncService.Setup(x => x.DeleteAsync(modelPath)).ReturnsAsync(Result.Ok);
        context.CloudSyncStateService.Setup(x => x.ClearAsync(modelPath)).Returns(Task.CompletedTask);
        await context.Sut.RefreshSyncStateAsync();

        Result result = await context.Sut.DeleteCurrentCloudModelAsync();

        AssertOk(result);
        context.CloudSyncService.Verify(x => x.DeleteAsync(modelPath), Times.Once);
        context.CloudSyncStateService.Verify(x => x.ClearAsync(modelPath), Times.Once);
        Assert.Empty(context.Sut.CloudModels);
        Assert.False(context.Sut.HasRemoteChangesSinceLastSync);
    }

    [Fact]
    public async Task DeleteCurrentCloudModel_ShouldReturnError_WhenTransportFails()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel]);
        context.CloudSyncService.Setup(x => x.DeleteAsync(modelPath)).ReturnsAsync(new Error("delete failed"));
        await context.Sut.RefreshSyncStateAsync();

        Result result = await context.Sut.DeleteCurrentCloudModelAsync();

        var error = AssertError(result);
        Assert.Contains("delete failed", error.Message);
        context.CloudSyncStateService.Verify(x => x.ClearAsync(It.IsAny<string>()), Times.Never);
        Assert.Single(context.Sut.CloudModels);
    }

    // The parse that follows opening a saved model asks for this first (ICloudCopyCheck)
    [Fact]
    public async Task PullNewerAsync_ShouldPull_WhenOnlyTheCloudCopyChanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel]);

        Assert.True(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(1, context.Counters.PullCalls);
        Assert.Equal(0, context.Counters.PushCalls);
    }

    [Fact]
    public async Task PullNewerAsync_ShouldNotPull_WhenTheCloudCopyIsUnchanged()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel]);

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(1, context.Counters.ListCalls);
        Assert.Equal(0, context.Counters.PullCalls);
    }

    // Both sides changed is the user's to resolve: nothing is pulled over the local changes
    [Fact]
    public async Task PullNewerAsync_ShouldNotPull_WhenTheModelChangedHereToo()
    {
        string modelPath = "/models/sample.model";
        CloudSyncModelState syncState = CreateSyncStateFromModel(CreateModelDto("synced"));
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState, [cloudModel]);

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(0, context.Counters.PullCalls);
        Assert.Equal(CloudSyncState.HasConflicts, context.Sut.GetCloudSyncState());
    }

    // A model that was never synced here has no newer cloud copy to wait for: no network call
    // holds its parse back.
    [Fact]
    public async Task PullNewerAsync_ShouldNotAskTheCloud_WhenTheModelWasNeverSynced()
    {
        string modelPath = "/models/sample.model";
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("local"), syncState: null, [cloudModel]);

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(0, context.Counters.ListCalls);
        context.CloudSyncService.Verify(x => x.GetAuthStateAsync(), Times.Never);
    }

    [Fact]
    public async Task PullNewerAsync_ShouldGiveUp_WhenTheCloudIsSlow()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        AppCloudSyncTimings timings = CreateFastTimings() with { StartCheckTimeout = TimeSpan.FromMilliseconds(20) };
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [], timings);
        TaskCompletionSource<Result<CloudModelList>> list = new();
        context.CloudSyncService.Setup(x => x.ListAsync()).Returns(list.Task);

        Assert.False(await context.Sut.PullNewerAsync(modelPath).WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(0, context.Counters.PullCalls);
    }

    // Opening a model also starts the usual check (a UI state change). When that one is done,
    // the model is checked: the parse does not ask the cloud a second time.
    [Fact]
    public async Task PullNewerAsync_ShouldNotCheckAgain_WhenTheModelWasJustChecked()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], CreateFastTimings());
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.ListCalls > 0);
        await WaitUntilQuietAsync(() => context.Counters.ListCalls, quietMilliseconds: 100);
        int listCalls = context.Counters.ListCalls;

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(listCalls, context.Counters.ListCalls);
    }

    // The pull of a model the user just left (it was on its way when they opened this one) is
    // not a pull of this one: its own parse must still be made.
    [Fact]
    public async Task PullNewerAsync_ShouldNotCountThePullOfAnotherModel()
    {
        string leftPath = "/models/left.model";
        string openedPath = "/models/opened.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(leftPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(leftPath, syncedModel, syncState, [cloudModel], CreateFastTimings());
        context.CloudSyncStateService.Setup(x => x.GetAsync(openedPath)).ReturnsAsync(syncState);
        TaskCompletionSource<Result<CloudModelList>> list = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context
            .CloudSyncService.Setup(x => x.ListAsync())
            .Callback(() => context.Counters.ListCalls++)
            .Returns(list.Task);

        // The check of the left model is waiting for the cloud when the other model asks
        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.ListCalls > 0);
        Task<bool> pullNewer = context.Sut.PullNewerAsync(openedPath);
        list.SetResult(new CloudModelList([cloudModel]));

        Assert.False(await pullNewer.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, context.Counters.PullCalls);
    }

    // A version before Nov 23 2026 saved layout flags that a load now clears: the loaded model
    // differs from the saved (and synced) one, and nobody changed it.
    [Fact]
    public async Task AcceptLoadChangeAsync_ShouldMoveTheBaselineToTheLoadedModel_WhenTheSavedModelWasSynced()
    {
        string modelPath = "/models/sample.model";
        ModelDto savedModel = CreateModelDto("saved");
        ModelDto loadedModel = CreateModelDto("loaded");
        CloudSyncModelState syncState = new()
        {
            Baseline = new(CloudModelSerializer.GetContentHash(savedModel), "cloud-hash"),
        };
        SutContext context = CreateSutContext(modelPath, loadedModel, syncState, []);

        await context.Sut.AcceptLoadChangeAsync(modelPath, savedModel);

        string loadedHash = CloudModelSerializer.GetContentHash(loadedModel);
        context.CloudSyncStateService.Verify(x => x.RecordLocalAsync(modelPath, loadedHash), Times.Once);
    }

    // A saved model that had changed since the last sync still has: the baseline stays
    [Fact]
    public async Task AcceptLoadChangeAsync_ShouldLeaveTheBaseline_WhenTheSavedModelWasNotTheSyncedOne()
    {
        string modelPath = "/models/sample.model";
        CloudSyncModelState syncState = CreateSyncStateFromModel(CreateModelDto("synced"));
        SutContext context = CreateSutContext(modelPath, CreateModelDto("loaded"), syncState, []);

        await context.Sut.AcceptLoadChangeAsync(modelPath, CreateModelDto("saved"));

        context.CloudSyncStateService.Verify(
            x => x.RecordLocalAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    // The user opened another model while the newer copy was on its way: the pull is dropped
    // (ReplaceCurrentModelAsync refuses it), which is nothing to report.
    [Fact]
    public async Task AutoSync_ShouldNotReportAnError_WhenAnotherModelWasOpenedDuringThePull()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], CreateFastTimings());
        List<string> errorMessages = [];
        context.Sut.BackgroundSyncError += errorMessages.Add;
        context
            .CloudSyncService.Setup(x => x.PullAsync(modelPath))
            .Callback(() =>
            {
                context.Counters.PullCalls++;
                context.ModelMgr.SetupGet(x => x.ModelPath).Returns("/models/other.model");
            })
            .ReturnsAsync(Pulled(syncedModel));
        context
            .ModelService.Setup(x => x.ReplaceCurrentModelAsync(modelPath, It.IsAny<ModelDto>(), It.IsAny<bool>()))
            .ReturnsAsync(new Error("No longer the open model"));

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(1, context.Counters.PullCalls);
        Assert.Empty(errorMessages);
    }

    // The user edited the model while the newer copy was on its way: the pull is dropped
    // (ReplaceCurrentModelAsync refuses it) without a word, the next check shows the conflict.
    [Fact]
    public async Task AutoSync_ShouldNotReportAnError_WhenTheModelWasEditedDuringThePull()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(modelPath, syncedModel, syncState, [cloudModel], CreateFastTimings());
        List<string> errorMessages = [];
        context.Sut.BackgroundSyncError += errorMessages.Add;
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(false);
        context
            .ModelService.Setup(x => x.ReplaceCurrentModelAsync(modelPath, It.IsAny<ModelDto>(), false))
            .Callback(() => context.ModelService.SetupGet(x => x.HasUserEdits).Returns(true))
            .ReturnsAsync(new Error("Edited while its cloud copy was fetched"));

        Assert.False(await context.Sut.PullNewerAsync(modelPath));

        Assert.Equal(1, context.Counters.PullCalls);
        Assert.Empty(errorMessages);
    }

    // Only the user's own edits can be in conflict with the cloud copy (Model.HasUserEdits):
    // what a parse or a layout made of the model here gives way to the newer copy.
    [Fact]
    public async Task AutoSync_ShouldPull_WhenBothSidesChangedButTheUserEditedNothing()
    {
        string modelPath = "/models/sample.model";
        CloudSyncModelState syncState = CreateSyncStateFromModel(CreateModelDto("synced"));
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(
            modelPath,
            CreateModelDto("parsed again"),
            syncState,
            [cloudModel],
            CreateFastTimings()
        );
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(false);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PullCalls > 0);

        Assert.Equal(0, context.Counters.PushCalls);
    }

    [Fact]
    public async Task AutoSync_ShouldNotPushOrPull_WhenBothSidesChangedAndTheUserEdited()
    {
        string modelPath = "/models/sample.model";
        CloudSyncModelState syncState = CreateSyncStateFromModel(CreateModelDto("synced"));
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(
            modelPath,
            CreateModelDto("edited"),
            syncState,
            [cloudModel],
            CreateFastTimings()
        );
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(true);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.ListCalls > 0);
        await Task.Delay(50);

        Assert.Equal(0, context.Counters.PullCalls);
        Assert.Equal(0, context.Counters.PushCalls);
        Assert.Equal(CloudSyncState.HasConflicts, context.Sut.GetCloudSyncState());
    }

    // A new codespace, or cleared site data: the model parsed here from scratch and the cloud
    // copy with the user's layout differ, and nothing says which changed. A fresh parse has
    // nothing of the user's, so the cloud copy is taken.
    [Fact]
    public async Task AutoSync_ShouldPull_WhenNeverSyncedHereAndTheModelIsAFreshParse()
    {
        string modelPath = "/models/sample.model";
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, CreateModelDto("remote"));
        SutContext context = CreateSutContext(
            modelPath,
            CreateModelDto("fresh parse"),
            syncState: null,
            [cloudModel],
            CreateFastTimings()
        );
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(false);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PullCalls > 0);

        Assert.Equal(0, context.Counters.PushCalls);
    }

    // A layout made while looking around, or the parse of unchanged code after pulling another
    // device's copy: the model differs from the last sync, but is made of what this device
    // pushed last. Pushed again, two devices on different code would push in turns for good.
    [Fact]
    public async Task AutoSync_ShouldNotPush_WhenOnlyTheLayoutChangedAndTheUserEditedNothing()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto laidOutModel = CreateModelDto("laid out");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        syncState.PushedStructureHash = CloudModelSerializer.GetStructureHash(laidOutModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, laidOutModel, syncState, [cloudModel], CreateFastTimings());
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(false);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.ListCalls > 0);
        await Task.Delay(50);

        Assert.Equal(0, context.Counters.PushCalls);
        Assert.Equal(CloudSyncState.IsSynced, context.Sut.GetCloudSyncState());
    }

    [Fact]
    public async Task AutoSync_ShouldPush_WhenAParseMadeSomethingNewOfTheModel()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto parsedModel = CreateModelDto("parsed") with
        {
            Nodes =
            [
                new NodeDto
                {
                    Name = "New",
                    ParentName = "",
                    Type = "Type",
                },
            ],
        };
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        syncState.PushedStructureHash = CloudModelSerializer.GetStructureHash(syncedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, parsedModel, syncState, [cloudModel], CreateFastTimings());
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(false);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);

        context.CloudSyncStateService.Verify(x =>
            x.RecordPushAsync(
                modelPath,
                It.IsAny<CloudModelMetadata>(),
                It.IsAny<string>(),
                CloudModelSerializer.GetStructureHash(parsedModel)
            )
        );
    }

    [Fact]
    public async Task AutoSync_ShouldPush_WhenTheUserEditedTheLayoutOnly()
    {
        string modelPath = "/models/sample.model";
        ModelDto syncedModel = CreateModelDto("synced");
        ModelDto editedModel = CreateModelDto("edited");
        CloudSyncModelState syncState = CreateSyncStateFromModel(syncedModel);
        syncState.PushedStructureHash = CloudModelSerializer.GetStructureHash(editedModel);
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, syncedModel);
        SutContext context = CreateSutContext(modelPath, editedModel, syncState, [cloudModel], CreateFastTimings());
        context.ModelService.SetupGet(x => x.HasUserEdits).Returns(true);

        context.ApplicationEvents.TriggerUIStateChanged();
        await WaitUntilAsync(() => context.Counters.PushCalls > 0);
    }

    // After a push the model has no edits left to sync, unless it was edited meanwhile (the
    // model service checks the stamp).
    [Fact]
    public async Task SyncUpAsync_ShouldMarkTheModelSynced_AsItWasWhenPushed()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("edited");
        SutContext context = CreateSutContext(modelPath, model, syncState: null, cloudModels: []);
        var stamp = Stamp(modelPath, saves: 7);
        context.ModelService.SetupGet(x => x.ContentStamp).Returns(stamp);

        AssertOk(await context.Sut.SyncUpAsync());

        context.ModelService.Verify(x => x.MarkSynced(stamp), Times.Once);
    }

    // A model saved before the mark existed does not say whether the user edited it. When it
    // is the same as at its last sync, it has nothing left to sync either way.
    [Fact]
    public async Task RefreshSyncStateAsync_ShouldMarkTheModelSynced_WhenItIsUnchangedSinceTheLastSync()
    {
        string modelPath = "/models/sample.model";
        ModelDto model = CreateModelDto("synced");
        CloudModelMetadata cloudModel = CreateCloudModelMetadata(modelPath, model);
        SutContext context = CreateSutContext(modelPath, model, CreateSyncStateFromModel(model), [cloudModel]);

        AssertOk(await context.Sut.RefreshSyncStateAsync());

        context.ModelService.Verify(x => x.MarkSynced(It.IsAny<ModelContentStamp>()), Times.Once);
    }

    static PulledModel Pulled(ModelDto model) => new(model, CloudModelSerializer.GetContentHash(model));

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
        AppCloudSyncTimings? timings = null,
        Func<DateTimeOffset>? utcNowProvider = null
    )
    {
        Mock<ICanvasService> canvasService = new();
        Mock<ICloudSyncService> cloudSyncService = new();
        Mock<ICloudSyncStateService> cloudSyncStateService = new();
        Mock<IModelService> modelService = new();
        Mock<IModelMgr> modelMgr = new();
        ApplicationEvents applicationEvents = new(new Mock<IJSInterop>().Object);
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
            .ReturnsAsync(Pulled(currentModelDto));

        cloudSyncStateService.Setup(x => x.GetAsync(modelPath)).ReturnsAsync(syncState);
        cloudSyncStateService
            .Setup(x =>
                x.RecordPushAsync(
                    It.IsAny<string>(),
                    It.IsAny<CloudModelMetadata>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()
                )
            )
            .Returns(Task.CompletedTask);
        cloudSyncStateService
            .Setup(x => x.RecordPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        modelMgr.SetupGet(x => x.ModelPath).Returns(modelPath);
        modelService.Setup(x => x.GetCurrentModelDto()).Returns(currentModelDto);
        // As in ModelService, a load moves the stamp
        long loads = 0;
        modelService.SetupGet(x => x.ContentStamp).Returns(() => new ModelContentStamp(modelPath, loads));
        modelService
            .Setup(x => x.ReplaceCurrentModelAsync(It.IsAny<string>(), It.IsAny<ModelDto>(), It.IsAny<bool>()))
            .Callback(() => loads++)
            .ReturnsAsync(new ModelInfo(modelPath, Rect.None, 0));
        modelService.Setup(x => x.WriteModelAsync(It.IsAny<string>(), It.IsAny<ModelDto>())).ReturnsAsync(Result.Ok);
        canvasService
            .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<bool?>()))
            .Callback(() => loads++)
            .Returns(Task.CompletedTask);

        return new SutContext(
            new AppCloudSyncService(
                new Lazy<ICanvasService>(() => canvasService.Object),
                cloudSyncService.Object,
                cloudSyncStateService.Object,
                new Lazy<IModelService>(() => modelService.Object),
                modelMgr.Object,
                applicationEvents,
                timings,
                utcNowProvider
            ),
            cloudSyncService,
            cloudSyncStateService,
            modelService,
            modelMgr,
            applicationEvents,
            counters
        );
    }

    sealed record SutContext(
        AppCloudSyncService Sut,
        Mock<ICloudSyncService> CloudSyncService,
        Mock<ICloudSyncStateService> CloudSyncStateService,
        Mock<IModelService> ModelService,
        Mock<IModelMgr> ModelMgr,
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
        string contentHash = CloudModelSerializer.GetContentHash(modelDto);
        return new CloudSyncModelState() { Baseline = new CloudSyncBaseline(contentHash, contentHash) };
    }

    static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMilliseconds = 500)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(predicate());
    }

    // Waits until the value has not changed for quietMilliseconds, and fails if it never does.
    static async Task WaitUntilQuietAsync(Func<int> value, int quietMilliseconds, int timeoutMilliseconds = 5000)
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        var quiet = System.Diagnostics.Stopwatch.StartNew();
        int last = value();
        while (quiet.ElapsedMilliseconds < quietMilliseconds)
        {
            Assert.True(
                total.ElapsedMilliseconds < timeoutMilliseconds,
                $"The value kept changing for {timeoutMilliseconds} ms (now {last})."
            );
            await Task.Delay(10);
            int current = value();
            if (current != last)
            {
                last = current;
                quiet.Restart();
            }
        }
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
