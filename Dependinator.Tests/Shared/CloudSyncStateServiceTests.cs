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
        Assert.NotEqual(default, state.LatestSync.Utc);
    }

    [Fact]
    public async Task GetAsync_ShouldNormalizeLegacyPushPullState()
    {
        InMemoryConfigService configService = new();
        DateTimeOffset pushUtc = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset pullUtc = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        configService.Config.CloudSyncStates[CloudModelPath.CreateKey("/models/sample.model")] = new CloudSyncModelState()
        {
            LastPushUtc = pushUtc,
            LastPushContentHash = "push-hash",
            LastPullUtc = pullUtc,
            LastPullContentHash = "pull-hash",
        };
        CloudSyncStateService sut = new(configService);

        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.NotNull(state.LatestSync);
        Assert.Equal(CloudSyncDirection.Down, state.LatestSync.Direction);
        Assert.Equal(pullUtc, state.LatestSync.Utc);
        Assert.Equal("pull-hash", state.LatestSync.ContentHash);
        Assert.Null(state.LastPushUtc);
        Assert.Null(state.LastPullUtc);
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
