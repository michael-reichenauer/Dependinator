using Dependinator.Diagrams;
using Dependinator.Modeling;
using Dependinator.Modeling.Dtos;
using Dependinator.Modeling.Models;
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
        ActiveRefreshInterval: TimeSpan.FromSeconds(10),
        AutoSyncMinInterval: TimeSpan.FromSeconds(10),
        IdleRefreshInterval: TimeSpan.FromSeconds(15),
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
    CloudSyncLatest? LatestSync { get; }
    bool HasLocalChangesSinceLastSync { get; }
    bool HasRemoteChangesSinceLastSync { get; }
    IReadOnlyList<CloudModelMetadata> CloudModels { get; }
    string? CurrentNormalizedModelPath { get; }

    // Returns a simplified state derived from auth and local/remote change flags.
    CloudSyncState GetCloudSyncState();

    Task<R> InitializeAsync();

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
class AppCloudSyncService(
    ICanvasService canvasService,
    ICloudSyncService cloudSyncService,
    ICloudSyncStateService cloudSyncStateService,
    IModelService modelService,
    IModelMgr modelMgr,
    IApplicationEvents applicationEvents,
    AppCloudSyncTimings? appCloudSyncTimings = null,
    Func<DateTimeOffset>? utcNowProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsyncProvider = null
) : IAppCloudSyncService, IDisposable
{
    static readonly CloudAuthState unavailableAuthState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    readonly AppCloudSyncTimings syncTimings = appCloudSyncTimings ?? AppCloudSyncTimings.Default;
    readonly Func<DateTimeOffset> utcNow = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    readonly Func<TimeSpan, CancellationToken, Task> delayAsync = delayAsyncProvider ?? TaskDelayAsync;
    readonly SemaphoreSlim initializeLock = new(1, 1);
    readonly SemaphoreSlim syncOperationLock = new(1, 1);
    readonly Debouncer uiStateRefreshDebouncer = new();
    readonly object idleRefreshLock = new();
    readonly object backgroundErrorLock = new();
    readonly HashSet<string> reportedBackgroundErrors = [];

    bool isInitialized;
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

    public event Action Changed = null!;
    public event Action<string> BackgroundSyncError = null!;

    public bool IsAvailable => cloudSyncService.IsAvailable;
    public CloudAuthState AuthState => authState;
    public CloudSyncModelState? SyncState => syncState;
    public CloudSyncLatest? LatestSync => syncState?.LatestSync;
    public bool HasLocalChangesSinceLastSync => hasLocalChangesSinceLastSync;
    public bool HasRemoteChangesSinceLastSync => hasRemoteChangesSinceLastSync;
    public IReadOnlyList<CloudModelMetadata> CloudModels => cloudModels;
    public string? CurrentNormalizedModelPath =>
        string.IsNullOrWhiteSpace(modelMgr.ModelPath) ? null : CloudModelPath.Normalize(modelMgr.ModelPath);

    // Initializes service state once and wires UI refresh events.
    public async Task<R> InitializeAsync()
    {
        if (isInitialized)
            return R.Ok;

        await initializeLock.WaitAsync();
        try
        {
            if (isInitialized)
                return R.Ok;

            isInitialized = true;
            applicationEvents.UIStateChanged += HandleUiStateChanged;

            if (!Try(out var error, await RefreshAuthStateCoreAsync()))
            {
                NotifyChanged();
                return error;
            }

            if (!Try(out error, await RefreshSyncStateCoreAsync()))
            {
                NotifyChanged();
                return error;
            }

            NotifyChanged();
            return R.Ok;
        }
        finally
        {
            initializeLock.Release();
        }
    }

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
        await EnsureInitializedAsync();

        if (!Try(out CloudAuthState? state, out ErrorResult? error, await cloudSyncService.LoginAsync()))
            return error;

        authState = state;
        return await RefreshSnapshotAndNotifyAsync(allowAutoSync: false);
    }

    public async Task<R> LogoutAsync()
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        return await ExecuteSyncOperationAsync(() => SyncUpCoreAsync(notifyChanged: true));
    }

    public async Task<R<ModelInfo>> SyncDownAsync()
    {
        await EnsureInitializedAsync();
        return await ExecuteSyncOperationAsync(() => SyncDownCoreAsync(notifyChanged: true));
    }

    // Loads a selected cloud model into workspace and records local baseline on success.
    public async Task<R<CloudModelMetadata>> LoadCloudModelAsync(CloudModelMetadata cloudModel)
    {
        await EnsureInitializedAsync();
        return await ExecuteSyncOperationAsync(() => LoadCloudModelCoreAsync(cloudModel, notifyChanged: true));
    }

    // Ensures one-time initialization is complete before calling public operations.
    async Task EnsureInitializedAsync() => _ = await InitializeAsync();

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
        if (!Try(out ModelDto? modelDto, out ErrorResult? error, modelService.GetCurrentModelDto()))
            return error;

        string modelPath = modelMgr.ModelPath;
        if (!Try(out CloudModelMetadata? metadata, out error, await cloudSyncService.PushAsync(modelPath, modelDto)))
            return error;

        string localContentHash = CloudModelSerializer.GetContentHash(modelDto);
        await cloudSyncStateService.RecordPushAsync(modelPath, metadata, localContentHash);
        if (!Try(out error, await RefreshSyncStateCoreAsync()))
            return error;

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

        if (!Try(out ModelInfo? modelInfo, out error, await modelService.ReplaceCurrentModelAsync(modelDto)))
            return error;

        string pulledContentHash = CloudModelSerializer.GetContentHash(modelDto);
        string localContentHash = TryGetCurrentModelContentHash(out var hash) ? hash : pulledContentHash;
        string remoteContentHash = pulledContentHash;
        await cloudSyncStateService.RecordPullAsync(modelInfo.Path, localContentHash, remoteContentHash);
        if (!Try(out error, await RefreshSyncStateCoreAsync()))
            return error;

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

        if (!Try(out error, await modelService.WriteModelAsync(normalizedPath, modelDto)))
            return error;

        await canvasService.LoadAsync(normalizedPath);
        string pulledContentHash = CloudModelSerializer.GetContentHash(modelDto);
        string localContentHash = TryGetCurrentModelContentHash(out var hash) ? hash : pulledContentHash;
        await cloudSyncStateService.RecordPullAsync(normalizedPath, localContentHash, pulledContentHash);
        if (!Try(out error, await RefreshSyncStateCoreAsync()))
            return error;

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
        if (string.IsNullOrWhiteSpace(modelPath))
            return AutoSyncAction.None;

        CloudModelMetadata? currentCloudModel = GetCurrentCloudModel(cloudModels, modelPath);
        CloudSyncLatest? latestSync = syncState?.LatestSync;
        if (latestSync is null)
            return currentCloudModel is null ? AutoSyncAction.Push : AutoSyncAction.Pull;

        if (hasLocalChangesSinceLastSync && hasRemoteChangesSinceLastSync)
            return AutoSyncAction.None;
        if (hasLocalChangesSinceLastSync)
            return AutoSyncAction.Push;
        if (hasRemoteChangesSinceLastSync)
            return AutoSyncAction.Pull;

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
    async Task<R> RefreshSyncStateCoreAsync()
    {
        if (!Try(out ErrorResult? error, await RefreshCloudModelsCoreAsync()))
            return error;

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
        CloudSyncLatest? latestSync = syncState?.LatestSync;
        CloudModelMetadata? currentCloudModel = GetCurrentCloudModel(cloudModels, modelPath);
        hasRemoteChangesSinceLastSync = HasRemoteChangesComparedToLatestSync(latestSync, currentCloudModel);
        hasLocalChangesSinceLastSync = HasLocalChangesComparedToLatestSync(latestSync);
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

    bool HasLocalChangesComparedToLatestSync(CloudSyncLatest? latestSync)
    {
        if (latestSync is null)
            return false;

        if (!Try(out ModelDto? modelDto, out _, modelService.GetCurrentModelDto()))
            return false;

        string? localBaselineHash = latestSync.LocalContentHash ?? latestSync.ContentHash;
        if (string.IsNullOrWhiteSpace(localBaselineHash))
            return false;

        string currentHash = CloudModelSerializer.GetContentHash(modelDto);
        return !string.Equals(currentHash, localBaselineHash, StringComparison.OrdinalIgnoreCase);
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
        if (!isInitialized)
            return;

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

    // Compares remote model hash against the latest known local sync marker.
    static bool HasRemoteChangesComparedToLatestSync(CloudSyncLatest? latestSync, CloudModelMetadata? currentCloudModel)
    {
        if (latestSync is null || currentCloudModel is null)
            return false;

        string? remoteBaselineHash = latestSync.RemoteContentHash ?? latestSync.ContentHash;
        if (string.IsNullOrWhiteSpace(remoteBaselineHash))
            return false;

        return !string.Equals(currentCloudModel.ContentHash, remoteBaselineHash, StringComparison.OrdinalIgnoreCase);
    }

    bool TryGetCurrentModelContentHash(out string contentHash)
    {
        contentHash = string.Empty;
        if (!Try(out ModelDto? currentModelDto, out _, modelService.GetCurrentModelDto()))
            return false;

        contentHash = CloudModelSerializer.GetContentHash(currentModelDto);
        return true;
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
        initializeLock.Dispose();
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
