using Shared;

namespace Dependinator.Shared.CloudSync;

class CloudSyncModelState
{
    public CloudSyncLatest? LatestSync { get; set; }

    // Preserve read compatibility with older config payloads that stored
    // separate push/pull timestamps and hashes.
    public DateTimeOffset? LastPushUtc { get; set; }
    public string? LastPushContentHash { get; set; }
    public DateTimeOffset? LastPullUtc { get; set; }
    public string? LastPullContentHash { get; set; }

    public void RecordSync(CloudSyncLatest latestSync)
    {
        LatestSync = latestSync;
        ClearLegacyState();
    }

    public CloudSyncLatest? GetLatestSync()
    {
        if (LatestSync is not null)
            return LatestSync;

        if (LastPushUtc is null && LastPullUtc is null)
            return null;

        return LastPushUtc >= LastPullUtc
            ? new CloudSyncLatest(LastPushUtc ?? default, CloudSyncDirection.Up, LastPushContentHash)
            : new CloudSyncLatest(LastPullUtc ?? default, CloudSyncDirection.Down, LastPullContentHash);
    }

    public void Normalize()
    {
        LatestSync = GetLatestSync();
        ClearLegacyState();
    }

    void ClearLegacyState()
    {
        LastPushUtc = null;
        LastPushContentHash = null;
        LastPullUtc = null;
        LastPullContentHash = null;
    }
}

interface ICloudSyncStateService
{
    Task<CloudSyncModelState?> GetAsync(string modelPath);
    Task RecordPushAsync(string modelPath, CloudModelMetadata metadata);
    Task RecordPullAsync(string modelPath, string contentHash);
}

[Scoped]
class CloudSyncStateService(IConfigService configService) : ICloudSyncStateService
{
    public async Task<CloudSyncModelState?> GetAsync(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return null;

        Config config = await configService.GetAsync();
        CloudSyncModelState? state = config.CloudSyncStates.GetValueOrDefault(GetKey(modelPath));
        state?.Normalize();
        return state;
    }

    public Task RecordPushAsync(string modelPath, CloudModelMetadata metadata)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.RecordSync(new CloudSyncLatest(metadata.UpdatedUtc, CloudSyncDirection.Up, metadata.ContentHash));
        });
    }

    public Task RecordPullAsync(string modelPath, string contentHash)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.RecordSync(new CloudSyncLatest(DateTimeOffset.UtcNow, CloudSyncDirection.Down, contentHash));
        });
    }

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

    static string GetKey(string modelPath) => CloudModelPath.CreateKey(modelPath);
}
