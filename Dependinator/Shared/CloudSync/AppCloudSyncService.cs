using Dependinator.Core;
using Dependinator.Core.Utils;
using Dependinator.Diagrams;
using Dependinator.Models;
using Shared;

namespace Dependinator.Shared.CloudSync;

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

    CloudSyncState GetCloudSyncState();

    Task<R> InitializeAsync();
    Task<R> LoginAsync();
    Task<R> LogoutAsync();
    Task<R<CloudModelMetadata>> SyncUpAsync();
    Task<R<ModelInfo>> SyncDownAsync();
    Task<R<CloudModelMetadata>> LoadCloudModelAsync(CloudModelMetadata cloudModel);
}

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
    public CloudSyncLatest? LatestSync => syncState?.GetLatestSync();
    public bool HasLocalChangesSinceLastSync => hasLocalChangesSinceLastSync;
    public bool HasRemoteChangesSinceLastSync => hasRemoteChangesSinceLastSync;
    public IReadOnlyList<CloudModelMetadata> CloudModels => cloudModels;
    public string? CurrentNormalizedModelPath =>
        string.IsNullOrWhiteSpace(modelService.ModelPath) ? null : CloudModelPath.Normalize(modelService.ModelPath);

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

    async Task EnsureInitializedAsync()
    {
        _ = await InitializeAsync();
    }

    async Task<R> RefreshSnapshotAndNotifyAsync()
    {
        if (!Try(out ErrorResult? error, await RefreshSyncStateCoreAsync()))
            return error;

        NotifyChanged();
        return R.Ok;
    }

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

    async Task<R> RefreshSyncStateForCurrentModelAsync()
    {
        syncState = await cloudSyncStateService.GetAsync(modelService.ModelPath);
        CloudSyncLatest? latestSync = syncState?.GetLatestSync();
        CloudModelMetadata? currentCloudModel = GetCurrentCloudModel(cloudModels, modelService.ModelPath);
        hasRemoteChangesSinceLastSync = HasRemoteChangesComparedToLatestSync(latestSync, currentCloudModel);

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

    void ResetSyncSnapshot(bool clearCloudModels)
    {
        syncState = null;
        hasLocalChangesSinceLastSync = false;
        hasRemoteChangesSinceLastSync = false;
        if (clearCloudModels)
            cloudModels = [];

        MarkSyncStateRefreshed();
    }

    void MarkSyncStateRefreshed()
    {
        lastSyncStateRefreshUtc = DateTimeOffset.UtcNow;
    }

    void HandleUiStateChanged()
    {
        if (!isInitialized)
            return;

        isUiStateRefreshQueued = true;
        ScheduleUiStateRefresh();
    }

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

    void NotifyChanged()
    {
        if (isDisposed)
            return;

        Changed?.Invoke();
    }

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

    static CloudModelMetadata? GetCurrentCloudModel(IReadOnlyList<CloudModelMetadata> cloudModels, string modelPath)
    {
        string normalizedModelPath = CloudModelPath.Normalize(modelPath);
        return cloudModels.FirstOrDefault(cloudModel =>
            string.Equals(cloudModel.NormalizedPath, normalizedModelPath, StringComparison.OrdinalIgnoreCase)
        );
    }

    public void Dispose()
    {
        isDisposed = true;
        applicationEvents.UIStateChanged -= HandleUiStateChanged;
        uiStateRefreshDebouncer.Dispose();
        initializeLock.Dispose();
    }
}
