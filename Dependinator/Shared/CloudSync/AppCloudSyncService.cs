using Dependinator.Diagrams;
using Dependinator.Modeling;
using Dependinator.Modeling.Dtos;
using Dependinator.Modeling.Models;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Dependinator.Shared.CloudSync;

sealed record AppCloudSyncTimings(
    TimeSpan ActiveRefreshInterval,
    TimeSpan AutoSyncMinInterval,
    TimeSpan IdleRefreshInterval,
    TimeSpan MaxIdleRefreshDuration
)
{
    public static readonly AppCloudSyncTimings Default = new(
        ActiveRefreshInterval: TimeSpan.FromSeconds(5),
        AutoSyncMinInterval: TimeSpan.FromSeconds(5),
        IdleRefreshInterval: TimeSpan.FromSeconds(10),
        MaxIdleRefreshDuration: TimeSpan.FromMinutes(5)
    );
}

// UI-facing service contract for cloud sync operations and derived sync state.
interface IAppCloudSyncService
{
    event Action Changed;
    event Action<string> BackgroundSyncError;

    bool IsAvailable { get; }
    CloudAuthState AuthState { get; }
    CloudSyncModelState? SyncState { get; }
    bool HasLocalChangesSinceLastSync { get; }
    bool HasRemoteChangesSinceLastSync { get; }
    IReadOnlyList<CloudModelMetadata> CloudModels { get; }
    string? CurrentNormalizedModelPath { get; }

    Task<R> RefreshSyncStateCoreAsync();

    // Returns a simplified state derived from auth and local/remote change flags.
    CloudSyncState GetCloudSyncState();

    // Starts authentication flow and refreshes derived sync state.
    Task<R> LoginAsync();

    // Starts logout flow and clears cloud-backed state from UI cache.
    Task<R> LogoutAsync();

    // Pushes the active model to cloud and updates the local sync marker.
    Task<R<CloudModelMetadata>> SyncUpAsync();

    // Pulls current model from cloud and replaces the active local model.
    Task<R<ModelInfo>> SyncDownAsync();

    // Downloads a selected remote model and opens it in the canvas.
    Task<R<CloudModelMetadata>> LoadCloudModelAsync(CloudModelMetadata cloudModel);
}

// Aggregates cloud sync concerns for the app: transport selection, auth state, model lists, and
// local/remote drift detection used by the user-facing sync indicator.
[Scoped]
class AppCloudSyncService : IAppCloudSyncService, IDisposable
{
    private readonly Lazy<ICanvasService> canvasServiceLazy;
    private readonly ICloudSyncService cloudSyncService;
    private readonly ICloudSyncStateService cloudSyncStateService;
    private readonly Lazy<IModelService> modelServiceLazy;
    private readonly IModelMgr modelMgr;
    private readonly IApplicationEvents applicationEvents;

    static readonly CloudAuthState unavailableAuthState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    readonly AppCloudSyncTimings syncTimings;
    readonly Func<DateTimeOffset> utcNow;
    readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    readonly SemaphoreSlim syncOperationLock = new(1, 1);
    readonly Debouncer uiStateRefreshDebouncer = new();
    readonly object idleRefreshLock = new();
    readonly object backgroundErrorLock = new();
    readonly HashSet<string> reportedBackgroundErrors = [];

    bool isUiStateRefreshInProgress;
    bool isUiStateRefreshQueued;
    bool isDisposed;
    CancellationTokenSource? idleRefreshCancellationTokenSource;
    DateTimeOffset lastSyncStateRefreshUtc = DateTimeOffset.MinValue;
    DateTimeOffset lastAutoSyncAttemptUtc = DateTimeOffset.MinValue;
    CloudAuthState authState = unavailableAuthState;
    CloudSyncModelState? syncState;
    bool hasLocalChangesSinceLastSync;
    bool hasRemoteChangesSinceLastSync;
    IReadOnlyList<CloudModelMetadata> cloudModels = [];

    public AppCloudSyncService(
        Lazy<ICanvasService> canvasServiceLazy,
        ICloudSyncService cloudSyncService,
        ICloudSyncStateService cloudSyncStateService,
        Lazy<IModelService> modelServiceLazy,
        IModelMgr modelMgr,
        IApplicationEvents applicationEvents,
        AppCloudSyncTimings? appCloudSyncTimings = null,
        Func<DateTimeOffset>? utcNowProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsyncProvider = null
    )
    {
        this.canvasServiceLazy = canvasServiceLazy;
        this.cloudSyncService = cloudSyncService;
        this.cloudSyncStateService = cloudSyncStateService;
        this.modelServiceLazy = modelServiceLazy;
        this.modelMgr = modelMgr;
        this.applicationEvents = applicationEvents;

        this.syncTimings = appCloudSyncTimings ?? AppCloudSyncTimings.Default;
        this.utcNow = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
        this.delayAsync = delayAsyncProvider ?? TaskDelayAsync;

        applicationEvents.UIStateChanged += HandleUiStateChanged;
    }

    public event Action Changed = null!;
    public event Action<string> BackgroundSyncError = null!;

    public bool IsAvailable => cloudSyncService.IsAvailable;
    public CloudAuthState AuthState => authState;
    public CloudSyncModelState? SyncState => syncState;
    public bool HasLocalChangesSinceLastSync => hasLocalChangesSinceLastSync;
    public bool HasRemoteChangesSinceLastSync => hasRemoteChangesSinceLastSync;
    public IReadOnlyList<CloudModelMetadata> CloudModels => cloudModels;
    public string? CurrentNormalizedModelPath =>
        string.IsNullOrWhiteSpace(modelMgr.ModelPath) ? null : CloudModelPath.Normalize(modelMgr.ModelPath);

    public CloudSyncState GetCloudSyncState()
    {
        if (!IsAvailable)
            return CloudSyncState.NotAvailable;
        if (!AuthState.IsAuthenticated)
            return CloudSyncState.NotAuthenticated;
        if (HasLocalChangesSinceLastSync && HasRemoteChangesSinceLastSync)
            return CloudSyncState.HasConflicts;
        if (HasLocalChangesSinceLastSync)
            return CloudSyncState.HasLocalChanges;
        if (HasRemoteChangesSinceLastSync)
            return CloudSyncState.HasRemoteChanges;
        return CloudSyncState.IsSynced;
    }

    // Triggers login through transport and refreshes snapshot state afterwards.
    public async Task<R> LoginAsync()
    {
        if (!Try(out CloudAuthState? state, out ErrorResult? error, await cloudSyncService.LoginAsync()))
            return error;

        authState = state;
        return await RefreshSnapshotAndNotifyAsync(allowAutoSync: false);
    }

    public async Task<R> LogoutAsync()
    {
        if (!Try(out CloudAuthState? state, out ErrorResult? error, await cloudSyncService.LogoutAsync()))
            return error;

        authState = state;
        ResetSyncSnapshot(clearCloudModels: true);
        CancelIdleRefreshLoop();
        NotifyChanged();
        return R.Ok;
    }

    // Pushes current model DTO to cloud and records the successful sync baseline.
    public async Task<R<CloudModelMetadata>> SyncUpAsync()
    {
        return await ExecuteSyncOperationAsync(() => SyncUpCoreAsync(notifyChanged: true));
    }

    public async Task<R<ModelInfo>> SyncDownAsync()
    {
        return await ExecuteSyncOperationAsync(() => SyncDownCoreAsync(notifyChanged: true));
    }

    // Loads a selected cloud model into workspace and records local baseline on success.
    public async Task<R<CloudModelMetadata>> LoadCloudModelAsync(CloudModelMetadata cloudModel)
    {
        return await ExecuteSyncOperationAsync(() => LoadCloudModelCoreAsync(cloudModel, notifyChanged: true));
    }

    async Task<R<T>> ExecuteSyncOperationAsync<T>(Func<Task<R<T>>> syncOperation)
    {
        await syncOperationLock.WaitAsync();
        try
        {
            return await syncOperation();
        }
        finally
        {
            syncOperationLock.Release();
        }
    }

    async Task<R<CloudModelMetadata>> SyncUpCoreAsync(bool notifyChanged)
    {
        if (!Try(out ModelDto? modelDto, out ErrorResult? error, modelServiceLazy.Value.GetCurrentModelDto()))
            return error;

        string modelPath = modelMgr.ModelPath;
        if (!Try(out CloudModelMetadata? metadata, out error, await cloudSyncService.PushAsync(modelPath, modelDto)))
            return error;

        string localContentHash = CloudModelSerializer.GetContentHash(modelDto);
        await cloudSyncStateService.RecordPushAsync(modelPath, metadata, localContentHash);
        ApplySuccessfulSync(modelPath, localContentHash, metadata.ContentHash, metadata);

        Log.Info("SyncUpCoreAsync");
        if (notifyChanged)
            NotifyChanged();

        return metadata;
    }

    async Task<R<ModelInfo>> SyncDownCoreAsync(bool notifyChanged)
    {
        string modelPath = modelMgr.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
            return R.Error("Model is not loaded.");

        if (!Try(out ModelDto? modelDto, out ErrorResult? error, await cloudSyncService.PullAsync(modelPath)))
            return error;

        if (!Try(out ModelInfo? modelInfo, out error, await modelServiceLazy.Value.ReplaceCurrentModelAsync(modelDto)))
            return error;

        string pulledContentHash = CloudModelSerializer.GetContentHash(modelDto);
        string localContentHash = GetCurrentModelContentHashOrDefault() ?? pulledContentHash;
        string remoteContentHash = pulledContentHash;
        await cloudSyncStateService.RecordPullAsync(modelInfo.Path, localContentHash, remoteContentHash);
        ApplySuccessfulSync(
            modelInfo.Path,
            localContentHash,
            remoteContentHash,
            CreateKnownCloudModelMetadata(modelInfo.Path, remoteContentHash)
        );

        Log.Info("SyncDownCoreAsync");
        if (notifyChanged)
            NotifyChanged();

        return modelInfo;
    }

    async Task<R<CloudModelMetadata>> LoadCloudModelCoreAsync(CloudModelMetadata cloudModel, bool notifyChanged)
    {
        string normalizedPath = cloudModel.NormalizedPath;
        if (!Try(out ModelDto? modelDto, out ErrorResult? error, await cloudSyncService.PullAsync(normalizedPath)))
            return error;

        if (!Try(out error, await modelServiceLazy.Value.WriteModelAsync(normalizedPath, modelDto)))
            return error;

        await canvasServiceLazy.Value.LoadAsync(normalizedPath);
        string pulledContentHash = CloudModelSerializer.GetContentHash(modelDto);
        string localContentHash = GetCurrentModelContentHashOrDefault() ?? pulledContentHash;
        await cloudSyncStateService.RecordPullAsync(normalizedPath, localContentHash, pulledContentHash);
        ApplySuccessfulSync(
            normalizedPath,
            localContentHash,
            pulledContentHash,
            CreateKnownCloudModelMetadata(normalizedPath, pulledContentHash)
        );

        if (notifyChanged)
            NotifyChanged();

        return cloudModel;
    }

    // Rebuilds sync snapshot and optionally performs automatic sync work before notifying listeners.
    async Task<R> RefreshSnapshotAndNotifyAsync(bool allowAutoSync)
    {
        if (!Try(out ErrorResult? error, await RefreshSyncStateCoreAsync()))
            return error;

        if (allowAutoSync && !Try(out error, await TryAutoSyncIfNeededAsync()))
            return error;

        NotifyChanged();
        return R.Ok;
    }

    async Task<R> TryAutoSyncIfNeededAsync()
    {
        if (!ShouldEvaluateAutoSync())
            return R.Ok;

        if (!IsAutoSyncAttemptDue())
            return R.Ok;

        AutoSyncAction autoSyncAction = DetermineAutoSyncAction();
        if (autoSyncAction is AutoSyncAction.None)
            return R.Ok;

        lastAutoSyncAttemptUtc = utcNow();
        return autoSyncAction switch
        {
            AutoSyncAction.Push => await TryAutoSyncUpAsync(),
            AutoSyncAction.Pull => await TryAutoSyncDownAsync(),
            _ => R.Ok,
        };
    }

    bool ShouldEvaluateAutoSync()
    {
        if (!cloudSyncService.IsAvailable || !authState.IsAuthenticated)
            return false;

        return !string.IsNullOrWhiteSpace(modelMgr.ModelPath);
    }

    bool IsAutoSyncAttemptDue()
    {
        TimeSpan elapsedSinceLastAutoSync = utcNow() - lastAutoSyncAttemptUtc;
        return elapsedSinceLastAutoSync >= syncTimings.AutoSyncMinInterval;
    }

    AutoSyncAction DetermineAutoSyncAction()
    {
        string modelPath = modelMgr.ModelPath;
        CloudModelMetadata? currentCloudModel = string.IsNullOrWhiteSpace(modelPath)
            ? null
            : GetCurrentCloudModel(cloudModels, modelPath);

        if (hasLocalChangesSinceLastSync && hasRemoteChangesSinceLastSync)
            return AutoSyncAction.None;
        if (hasLocalChangesSinceLastSync)
            return AutoSyncAction.Push;
        if (hasRemoteChangesSinceLastSync)
            return currentCloudModel is null ? AutoSyncAction.Push : AutoSyncAction.Pull;

        return AutoSyncAction.None;
    }

    async Task<R> TryAutoSyncUpAsync()
    {
        if (
            !Try(
                out _,
                out ErrorResult? error,
                await ExecuteSyncOperationAsync(() => SyncUpCoreAsync(notifyChanged: false))
            )
        )
            return error;

        return R.Ok;
    }

    async Task<R> TryAutoSyncDownAsync()
    {
        if (
            !Try(
                out _,
                out ErrorResult? error,
                await ExecuteSyncOperationAsync(() => SyncDownCoreAsync(notifyChanged: false))
            )
        )
            return error;

        return R.Ok;
    }

    // Refreshes authentication state and invalidates local cloud snapshot when unauthenticated.
    async Task<R> RefreshAuthStateCoreAsync()
    {
        if (!cloudSyncService.IsAvailable)
        {
            authState = unavailableAuthState;
            ResetSyncSnapshot(clearCloudModels: true);
            return R.Ok;
        }

        if (!Try(out CloudAuthState? state, out ErrorResult? error, await cloudSyncService.GetAuthStateAsync()))
            return error;

        authState = state;
        if (!authState.IsAuthenticated)
            ResetSyncSnapshot(clearCloudModels: true);

        return R.Ok;
    }

    // Refreshes cached cloud model list and current-model sync state.
    public async Task<R> RefreshSyncStateCoreAsync()
    {
        if (!Try(out var error, await RefreshAuthStateCoreAsync()))
        {
            NotifyChanged();
            return error;
        }

        if (!Try(out ErrorResult? error2, await RefreshCloudModelsCoreAsync()))
            return error2;

        string modelPath = modelMgr.ModelPath;
        if (!cloudSyncService.IsAvailable || string.IsNullOrWhiteSpace(modelPath))
        {
            ResetSyncSnapshot(clearCloudModels: false);
            return R.Ok;
        }

        return await RefreshSyncStateForCurrentModelAsync(modelPath);
    }

    // Loads latest local sync marker and compares against current model hash to determine drift.
    async Task<R> RefreshSyncStateForCurrentModelAsync(string modelPath)
    {
        syncState = await cloudSyncStateService.GetAsync(modelPath);
        CloudSyncBaseline? baseline = syncState?.Baseline;
        CloudModelMetadata? currentCloudModel = GetCurrentCloudModel(cloudModels, modelPath);
        string? currentLocalContentHash = GetCurrentModelContentHashOrDefault();
        string? currentRemoteContentHash = currentCloudModel?.ContentHash;
        (hasLocalChangesSinceLastSync, hasRemoteChangesSinceLastSync) = CompareToBaseline(
            baseline,
            currentLocalContentHash,
            currentRemoteContentHash
        );
        MarkSyncStateRefreshed();
        return R.Ok;
    }

    // Reads remote model metadata list for the current authenticated user.
    async Task<R> RefreshCloudModelsCoreAsync()
    {
        if (!cloudSyncService.IsAvailable || !authState.IsAuthenticated)
        {
            cloudModels = [];
            return R.Ok;
        }

        if (!Try(out CloudModelList? modelList, out ErrorResult? error, await cloudSyncService.ListAsync()))
        {
            cloudModels = [];
            return error;
        }

        cloudModels = modelList.Models;
        return R.Ok;
    }

    // Clears computed sync flags and optionally clears remote model list cache.
    void ResetSyncSnapshot(bool clearCloudModels)
    {
        syncState = null;
        hasLocalChangesSinceLastSync = false;
        hasRemoteChangesSinceLastSync = false;
        if (clearCloudModels)
            cloudModels = [];

        MarkSyncStateRefreshed();
    }

    // Tracks last sync-state refresh time for UI debounce calculations.
    void MarkSyncStateRefreshed()
    {
        lastSyncStateRefreshUtc = utcNow();
    }

    // Debounces sync-state refreshes when canvas/UI signals change.
    void HandleUiStateChanged()
    {
        QueueUiStateRefresh();
        RestartIdleRefreshLoop();
    }

    void QueueUiStateRefresh()
    {
        if (isDisposed)
            return;

        isUiStateRefreshQueued = true;
        ScheduleUiStateRefresh();
    }

    // Schedules a delayed refresh based on throttle window to avoid excessive recomputation.
    void ScheduleUiStateRefresh()
    {
        if (isDisposed || isUiStateRefreshInProgress)
            return;

        TimeSpan elapsedSinceLastRefresh = utcNow() - lastSyncStateRefreshUtc;
        if (elapsedSinceLastRefresh >= syncTimings.ActiveRefreshInterval)
        {
            _ = RefreshUiStateAsync();
            return;
        }

        TimeSpan remainingDelay = syncTimings.ActiveRefreshInterval - elapsedSinceLastRefresh;
        int delayMilliseconds = Math.Max(1, (int)Math.Ceiling(remainingDelay.TotalMilliseconds));
        uiStateRefreshDebouncer.Debounce(delayMilliseconds, () => _ = RefreshUiStateAsync());
    }

    // Refreshes sync state in the background while avoiding overlapping refresh loops.
    async Task RefreshUiStateAsync()
    {
        if (isDisposed || isUiStateRefreshInProgress || !isUiStateRefreshQueued)
            return;

        isUiStateRefreshInProgress = true;
        isUiStateRefreshQueued = false;

        try
        {
            R refreshResult = await RefreshSnapshotAndNotifyAsync(allowAutoSync: true);
            if (!Try(out ErrorResult? error, refreshResult))
                NotifyBackgroundSyncError(error.ErrorMessage);
        }
        finally
        {
            isUiStateRefreshInProgress = false;
            if (isUiStateRefreshQueued)
                ScheduleUiStateRefresh();
        }
    }

    void RestartIdleRefreshLoop()
    {
        CancellationTokenSource idleRefreshTokenSource = new();
        CancellationTokenSource? previousTokenSource;
        lock (idleRefreshLock)
        {
            previousTokenSource = idleRefreshCancellationTokenSource;
            idleRefreshCancellationTokenSource = idleRefreshTokenSource;
        }

        previousTokenSource?.Cancel();
        previousTokenSource?.Dispose();
        _ = RunIdleRefreshLoopAsync(idleRefreshTokenSource.Token);
    }

    async Task RunIdleRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (syncTimings.IdleRefreshInterval <= TimeSpan.Zero || syncTimings.MaxIdleRefreshDuration <= TimeSpan.Zero)
                return;

            DateTimeOffset idleChecksEndUtc = utcNow() + syncTimings.MaxIdleRefreshDuration;
            while (true)
            {
                TimeSpan remainingIdleDuration = idleChecksEndUtc - utcNow();
                if (remainingIdleDuration <= TimeSpan.Zero)
                    return;

                TimeSpan delayDuration =
                    remainingIdleDuration < syncTimings.IdleRefreshInterval
                        ? remainingIdleDuration
                        : syncTimings.IdleRefreshInterval;
                await delayAsync(delayDuration, cancellationToken);
                if (cancellationToken.IsCancellationRequested || isDisposed)
                    return;

                QueueUiStateRefresh();
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation since it is used to restart idle checks on new UI activity.
        }
    }

    void CancelIdleRefreshLoop()
    {
        CancellationTokenSource? tokenSourceToCancel;
        lock (idleRefreshLock)
        {
            tokenSourceToCancel = idleRefreshCancellationTokenSource;
            idleRefreshCancellationTokenSource = null;
        }

        tokenSourceToCancel?.Cancel();
        tokenSourceToCancel?.Dispose();
    }

    // Notifies all listeners if the service is still active.
    void NotifyChanged()
    {
        if (isDisposed)
            return;

        Changed?.Invoke();
    }

    void NotifyBackgroundSyncError(string errorMessage)
    {
        if (isDisposed || string.IsNullOrWhiteSpace(errorMessage))
            return;

        bool shouldNotify;
        lock (backgroundErrorLock)
        {
            shouldNotify = reportedBackgroundErrors.Add(errorMessage);
        }

        if (shouldNotify)
            BackgroundSyncError?.Invoke(errorMessage);
    }

    static (bool HasLocalChanges, bool HasRemoteChanges) CompareToBaseline(
        CloudSyncBaseline? baseline,
        string? currentLocalContentHash,
        string? currentRemoteContentHash
    )
    {
        if (baseline is null)
            return CompareWithoutBaseline(currentLocalContentHash, currentRemoteContentHash);

        bool hasLocalChanges = HashesDiffer(currentLocalContentHash, baseline.LocalContentHash);
        bool hasRemoteChanges = HashesDiffer(currentRemoteContentHash, baseline.RemoteContentHash);
        return (hasLocalChanges, hasRemoteChanges);
    }

    static (bool HasLocalChanges, bool HasRemoteChanges) CompareWithoutBaseline(
        string? currentLocalContentHash,
        string? currentRemoteContentHash
    )
    {
        bool hasLocalModel = !string.IsNullOrWhiteSpace(currentLocalContentHash);
        bool hasRemoteModel = !string.IsNullOrWhiteSpace(currentRemoteContentHash);

        if (!hasLocalModel && !hasRemoteModel)
            return (false, false);

        if (hasLocalModel && hasRemoteModel)
        {
            bool isSameContent = string.Equals(
                currentLocalContentHash,
                currentRemoteContentHash,
                StringComparison.OrdinalIgnoreCase
            );
            return isSameContent ? (false, false) : (true, true);
        }

        return (hasLocalModel, hasRemoteModel);
    }

    static bool HashesDiffer(string? currentHash, string? baselineHash)
    {
        bool hasCurrentHash = !string.IsNullOrWhiteSpace(currentHash);
        bool hasBaselineHash = !string.IsNullOrWhiteSpace(baselineHash);
        if (!hasCurrentHash && !hasBaselineHash)
            return false;

        return !string.Equals(currentHash, baselineHash, StringComparison.OrdinalIgnoreCase);
    }

    string? GetCurrentModelContentHashOrDefault()
    {
        if (!Try(out ModelDto? currentModelDto, out _, modelServiceLazy.Value.GetCurrentModelDto()))
            return null;

        return CloudModelSerializer.GetContentHash(currentModelDto);
    }

    void ApplySuccessfulSync(
        string modelPath,
        string localContentHash,
        string remoteContentHash,
        CloudModelMetadata cloudModel
    )
    {
        authState = authState with { IsAvailable = cloudSyncService.IsAvailable, IsAuthenticated = true };
        syncState = new CloudSyncModelState() { Baseline = new CloudSyncBaseline(localContentHash, remoteContentHash) };
        hasLocalChangesSinceLastSync = false;
        hasRemoteChangesSinceLastSync = false;
        UpsertCloudModel(cloudModel);
        MarkSyncStateRefreshed();
    }

    CloudModelMetadata CreateKnownCloudModelMetadata(string modelPath, string remoteContentHash)
    {
        string normalizedPath = CloudModelPath.Normalize(modelPath);
        CloudModelMetadata? existingCloudModel = GetCurrentCloudModel(cloudModels, modelPath);
        return new CloudModelMetadata(
            ModelKey: existingCloudModel?.ModelKey ?? CloudModelPath.CreateKey(normalizedPath),
            NormalizedPath: normalizedPath,
            UpdatedUtc: existingCloudModel?.UpdatedUtc ?? utcNow(),
            ContentHash: remoteContentHash,
            CompressedSizeBytes: existingCloudModel?.CompressedSizeBytes ?? 0
        );
    }

    void UpsertCloudModel(CloudModelMetadata cloudModel)
    {
        string normalizedPath = cloudModel.NormalizedPath;
        List<CloudModelMetadata> updatedCloudModels = [.. cloudModels];
        int existingIndex = updatedCloudModels.FindIndex(existingCloudModel =>
            string.Equals(existingCloudModel.NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
        );
        if (existingIndex >= 0)
            updatedCloudModels[existingIndex] = cloudModel;
        else
            updatedCloudModels.Add(cloudModel);

        cloudModels = updatedCloudModels;
    }

    // Finds remote metadata that matches the active local model path.
    static CloudModelMetadata? GetCurrentCloudModel(IReadOnlyList<CloudModelMetadata> cloudModels, string modelPath)
    {
        string normalizedModelPath = CloudModelPath.Normalize(modelPath);
        return cloudModels.FirstOrDefault(cloudModel =>
            string.Equals(cloudModel.NormalizedPath, normalizedModelPath, StringComparison.OrdinalIgnoreCase)
        );
    }

    // Stops listeners and disposes service-level resources.
    public void Dispose()
    {
        isDisposed = true;
        applicationEvents.UIStateChanged -= HandleUiStateChanged;
        CancelIdleRefreshLoop();
        uiStateRefreshDebouncer.Dispose();
        syncOperationLock.Dispose();
    }

    static Task TaskDelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

    enum AutoSyncAction
    {
        None,
        Push,
        Pull,
    }
}
