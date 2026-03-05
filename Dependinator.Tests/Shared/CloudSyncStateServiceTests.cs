using Dependinator.Shared;
using Dependinator.Shared.CloudSync;
using Shared;

namespace Dependinator.Tests.Shared;

public class CloudSyncStateServiceTests
{
    [Fact]
    public async Task RecordPushAsync_ShouldPersistState_ByNormalizedModelPathKey()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);
        CloudModelMetadata metadata = new(
            ModelKey: CloudModelPath.CreateKey("C:/repo/Model.json"),
            NormalizedPath: "C:/repo/Model.json",
            UpdatedUtc: DateTimeOffset.UtcNow,
            ContentHash: "hash-123",
            CompressedSizeBytes: 10
        );

        await sut.RecordPushAsync(@"C:\repo\Model.json", metadata);
        CloudSyncModelState? state = await sut.GetAsync("C:/repo/Model.json");

        Assert.NotNull(state);
        Assert.NotNull(state.LatestSync);
        Assert.Equal(CloudSyncDirection.Up, state.LatestSync.Direction);
        Assert.Equal(metadata.UpdatedUtc, state.LatestSync.Utc);
        Assert.Equal("hash-123", state.LatestSync.ContentHash);
        Assert.Equal("hash-123", state.LatestSync.LocalContentHash);
        Assert.Equal("hash-123", state.LatestSync.RemoteContentHash);
    }

    [Fact]
    public async Task RecordPullAsync_ShouldPersistPullHash()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);

        await sut.RecordPullAsync("/models/sample.model", "pull-hash");
        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.NotNull(state.LatestSync);
        Assert.Equal(CloudSyncDirection.Down, state.LatestSync.Direction);
        Assert.Equal("pull-hash", state.LatestSync.ContentHash);
        Assert.Equal("pull-hash", state.LatestSync.LocalContentHash);
        Assert.Equal("pull-hash", state.LatestSync.RemoteContentHash);
        Assert.NotEqual(default, state.LatestSync.Utc);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnPersistedLatestSync()
    {
        InMemoryConfigService configService = new();
        DateTimeOffset pullUtc = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        configService.Config.CloudSyncStates[CloudModelPath.CreateKey("/models/sample.model")] = new CloudSyncModelState()
        {
            LatestSync = new CloudSyncLatest(pullUtc, CloudSyncDirection.Down, "pull-hash"),
        };
        CloudSyncStateService sut = new(configService);

        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.NotNull(state.LatestSync);
        Assert.Equal(CloudSyncDirection.Down, state.LatestSync.Direction);
        Assert.Equal(pullUtc, state.LatestSync.Utc);
        Assert.Equal("pull-hash", state.LatestSync.ContentHash);
        Assert.Null(state.LatestSync.LocalContentHash);
        Assert.Null(state.LatestSync.RemoteContentHash);
    }

    sealed class InMemoryConfigService : IConfigService
    {
        public Config Config { get; } = new();

        public Task<Config> GetAsync()
        {
            return Task.FromResult(Config);
        }

        public Task SetAsync(Action<Config> updateAction)
        {
            updateAction(Config);
            return Task.CompletedTask;
        }
    }
}
