using Shared;

namespace Dependinator.Shared;

class CloudSyncModelState
{
    public DateTimeOffset? LastPushUtc { get; set; }
    public string? LastPushContentHash { get; set; }
    public DateTimeOffset? LastPullUtc { get; set; }
    public string? LastPullContentHash { get; set; }
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
        return config.CloudSyncStates.GetValueOrDefault(GetKey(modelPath));
    }

    public Task RecordPushAsync(string modelPath, CloudModelMetadata metadata)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.LastPushUtc = metadata.UpdatedUtc;
            state.LastPushContentHash = metadata.ContentHash;
        });
    }

    public Task RecordPullAsync(string modelPath, string contentHash)
    {
        return configService.SetAsync(config =>
        {
            CloudSyncModelState state = GetOrCreateState(config, modelPath);
            state.LastPullUtc = DateTimeOffset.UtcNow;
            state.LastPullContentHash = contentHash;
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
