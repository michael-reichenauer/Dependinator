using Dependinator.Diagrams;
using Dependinator.Models;
using Shared;

namespace Dependinator.Shared.CloudSync;

// UI-facing service contract for cloud sync operations and derived sync state.
interface IAppCloudSyncService
{
    event Action Changed;

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
    IApplicationEvents applicationEvents
) : IAppCloudSyncService, IDisposable
{
    const int UiStateRefreshThrottleMilliseconds = 5000;
    static readonly CloudAuthState unavailableAuthState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    readonly SemaphoreSlim initializeLock = new(1, 1);
    readonly Debouncer uiStateRefreshDebouncer = new();

    bool isInitialized;
    bool isUiStateRefreshInProgress;
    bool isUiStateRefreshQueued;
    bool isDisposed;
    DateTimeOffset lastSyncStateRefreshUtc = DateTimeOffset.MinValue;
    CloudAuthState authState = unavailableAuthState;
    CloudSyncModelState? syncState;
    bool hasLocalChangesSinceLastSync;
    bool hasRemoteChangesSinceLastSync;
    IReadOnlyList<CloudModelMetadata> cloudModels = [];

    public event Action Changed = null!;

    public bool IsAvailable => cloudSyncService.IsAvailable;
    public CloudAuthState AuthState => authState;
    public CloudSyncModelState? SyncState => syncState;
    public CloudSyncLatest? LatestSync => syncState?.LatestSync;
    public bool HasLocalChangesSinceLastSync => hasLocalChangesSinceLastSync;
    public bool HasRemoteChangesSinceLastSync => hasRemoteChangesSinceLastSync;
    public IReadOnlyList<CloudModelMetadata> CloudModels => cloudModels;
    public string? CurrentNormalizedModelPath =>
        string.IsNullOrWhiteSpace(modelService.ModelPath) ? null : CloudModelPath.Normalize(modelService.ModelPath);

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
        return await RefreshSnapshotAndNotifyAsync();
    }

    public async Task<R> LogoutAsync()
    {
        await EnsureInitializedAsync();

        if (!Try(out CloudAuthState? state, out ErrorResult? error, await cloudSyncService.LogoutAsync()))
            return error;

        authState = state;
        ResetSyncSnapshot(clearCloudModels: true);
        NotifyChanged();
        return R.Ok;
    }

    // Pushes current model DTO to cloud and records the successful sync baseline.
    public async Task<R<CloudModelMetadata>> SyncUpAsync()
    {
        await EnsureInitializedAsync();

        if (!Try(out ModelDto? modelDto, out ErrorResult? error, modelService.GetCurrentModelDto()))
            return error;

        if (
            !Try(
                out CloudModelMetadata? metadata,
                out error,
                await cloudSyncService.PushAsync(modelService.ModelPath, modelDto)
            )
        )
            return error;

        await cloudSyncStateService.RecordPushAsync(modelService.ModelPath, metadata);
        if (!Try(out error, await RefreshSnapshotAndNotifyAsync()))
            return error;

        return metadata;
    }

    public async Task<R<ModelInfo>> SyncDownAsync()
    {
        await EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(modelService.ModelPath))
            return R.Error("Model is not loaded.");

        if (
            !Try(
                out ModelDto? modelDto,
                out ErrorResult? error,
                await cloudSyncService.PullAsync(modelService.ModelPath)
            )
        )
            return error;

        if (!Try(out ModelInfo? modelInfo, out error, await modelService.ReplaceCurrentModelAsync(modelDto)))
            return error;

        await cloudSyncStateService.RecordPullAsync(modelInfo.Path, CloudModelSerializer.GetContentHash(modelDto));
        if (!Try(out error, await RefreshSnapshotAndNotifyAsync()))
            return error;

        return modelInfo;
    }

    // Loads a selected cloud model into workspace and records local baseline on success.
    public async Task<R<CloudModelMetadata>> LoadCloudModelAsync(CloudModelMetadata cloudModel)
    {
        await EnsureInitializedAsync();

        if (
            !Try(
                out ModelDto? modelDto,
                out ErrorResult? error,
                await cloudSyncService.PullAsync(cloudModel.NormalizedPath)
            )
        )
            return error;

        if (!Try(out error, await modelService.WriteModelAsync(cloudModel.NormalizedPath, modelDto)))
            return error;

        await canvasService.LoadAsync(cloudModel.NormalizedPath);
        await cloudSyncStateService.RecordPullAsync(
            cloudModel.NormalizedPath,
            CloudModelSerializer.GetContentHash(modelDto)
        );
        if (!Try(out error, await RefreshSnapshotAndNotifyAsync()))
            return error;

        return cloudModel;
    }

    // Ensures one-time initialization is complete before calling public operations.
    async Task EnsureInitializedAsync()
    {
        _ = await InitializeAsync();
    }

    // Rebuilds sync snapshot and notifies listeners when no transport error occurs.
    async Task<R> RefreshSnapshotAndNotifyAsync()
    {
        if (!Try(out ErrorResult? error, await RefreshSyncStateCoreAsync()))
            return error;

        NotifyChanged();
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
        if (!cloudSyncService.IsAvailable || !authState.IsAuthenticated)
            cloudModels = [];
        else if (!Try(out ErrorResult? error, await RefreshCloudModelsCoreAsync()))
            return error;

        if (!cloudSyncService.IsAvailable || string.IsNullOrWhiteSpace(modelService.ModelPath))
        {
            ResetSyncSnapshot(clearCloudModels: false);
            return R.Ok;
        }

        return await RefreshSyncStateForCurrentModelAsync();
    }

    // Loads latest local sync marker and compares against current model hash to determine drift.

    async Task<R> RefreshSyncStateForCurrentModelAsync()
    {
        syncState = await cloudSyncStateService.GetAsync(modelService.ModelPath);
        CloudSyncLatest? latestSync = syncState?.LatestSync;
        CloudModelMetadata? currentCloudModel = GetCurrentCloudModel(cloudModels, modelService.ModelPath);
        hasRemoteChangesSinceLastSync = HasRemoteChangesComparedToLatestSync(latestSync, currentCloudModel);

        Log.Info("RefreshSyncStateForCurrentModelAsync");

        if (latestSync is null)
        {
            hasLocalChangesSinceLastSync = false;
            MarkSyncStateRefreshed();
            return R.Ok;
        }

        if (!Try(out ModelDto? modelDto, out _, modelService.GetCurrentModelDto()))
        {
            hasLocalChangesSinceLastSync = false;
            MarkSyncStateRefreshed();
            return R.Ok;
        }

        string currentHash = CloudModelSerializer.GetContentHash(modelDto);
        hasLocalChangesSinceLastSync = !string.Equals(
            currentHash,
            latestSync.ContentHash,
            StringComparison.OrdinalIgnoreCase
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
        lastSyncStateRefreshUtc = DateTimeOffset.UtcNow;
    }

    // Debounces sync-state refreshes when canvas/UI signals change.
    void HandleUiStateChanged()
    {
        if (!isInitialized)
            return;

        isUiStateRefreshQueued = true;
        ScheduleUiStateRefresh();
    }

    // Schedules a delayed refresh based on throttle window to avoid excessive recomputation.
    void ScheduleUiStateRefresh()
    {
        if (isDisposed || isUiStateRefreshInProgress)
            return;

        TimeSpan elapsedSinceLastRefresh = DateTimeOffset.UtcNow - lastSyncStateRefreshUtc;
        if (elapsedSinceLastRefresh >= TimeSpan.FromMilliseconds(UiStateRefreshThrottleMilliseconds))
        {
            _ = RefreshUiStateAsync();
            return;
        }

        TimeSpan remainingDelay =
            TimeSpan.FromMilliseconds(UiStateRefreshThrottleMilliseconds) - elapsedSinceLastRefresh;
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
            _ = await RefreshSnapshotAndNotifyAsync();
        }
        finally
        {
            isUiStateRefreshInProgress = false;
            if (isUiStateRefreshQueued)
                ScheduleUiStateRefresh();
        }
    }

    // Notifies all listeners if the service is still active.
    void NotifyChanged()
    {
        if (isDisposed)
            return;

        Changed?.Invoke();
    }

    // Compares remote model hash against the latest known local sync marker.
    static bool HasRemoteChangesComparedToLatestSync(CloudSyncLatest? latestSync, CloudModelMetadata? currentCloudModel)
    {
        if (latestSync is null || currentCloudModel is null)
            return false;

        return !string.Equals(
            currentCloudModel.ContentHash,
            latestSync.ContentHash,
            StringComparison.OrdinalIgnoreCase
        );
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
        uiStateRefreshDebouncer.Dispose();
        initializeLock.Dispose();
    }
}
