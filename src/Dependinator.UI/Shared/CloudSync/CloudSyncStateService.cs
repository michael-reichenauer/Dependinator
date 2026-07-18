using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Tracks the most recent cloud sync marker for a specific local model.
class CloudSyncModelState
{
    public CloudSyncBaseline? Baseline { get; set; }
}

// Abstraction for reading and updating per-model cloud sync metadata in local config.
interface ICloudSyncStateService
{
    // Gets the persisted sync state for a model, if any.
    Task<CloudSyncModelState?> GetAsync(string modelPath);

    // Stores sync metadata after a successful push operation.
    Task RecordPushAsync(string modelPath, CloudModelMetadata metadata, string localContentHash);

    // Stores sync metadata after a successful pull operation.
    Task RecordPullAsync(string modelPath, string localContentHash, string remoteContentHash);

    // Removes the persisted sync state for a model, e.g. after its remote copy is deleted.
    Task ClearAsync(string modelPath);
}

// Persists cloud sync progress in the shared Config object so the UI can
// show whether local/remote work is stale relative to the last successful sync.
[Scoped]
class CloudSyncStateService(IConfigService configService) : ICloudSyncStateService
{
    // Gets sync state for modelPath and returns null for empty keys.
    public async Task<CloudSyncModelState?> GetAsync(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return null;

        Config config = await configService.GetAsync();
        return config.CloudSyncStates.GetValueOrDefault(GetKey(modelPath));
    }

    // Writes a push event snapshot as the latest sync marker.
    public Task RecordPushAsync(string modelPath, CloudModelMetadata metadata, string localContentHash)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.Baseline = new CloudSyncBaseline(localContentHash, metadata.ContentHash);
        });
    }

    // Writes a pull event snapshot as the latest sync marker.
    public Task RecordPullAsync(string modelPath, string localContentHash, string remoteContentHash)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.Baseline = new CloudSyncBaseline(localContentHash, remoteContentHash);
        });
    }

    // Drops the sync marker so the model no longer appears synced to a deleted remote copy.
    public Task ClearAsync(string modelPath)
    {
        return configService.SetAsync(config => config.CloudSyncStates.Remove(GetKey(modelPath)));
    }

    // Loads an existing model state, or creates and stores one when missing.
    static CloudSyncModelState GetOrCreateState(Config config, string modelPath)
    {
        string key = GetKey(modelPath);
        if (!config.CloudSyncStates.TryGetValue(key, out CloudSyncModelState? state))
        {
            state = new CloudSyncModelState();
            config.CloudSyncStates[key] = state;
        }

        return state;
    }

    // Normalizes and indexes a model path for config persistence.
    static string GetKey(string modelPath) => CloudModelPath.CreateKey(modelPath);
}
