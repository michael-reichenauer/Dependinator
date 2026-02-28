using Dependinator.Shared;
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
        Assert.Equal(metadata.UpdatedUtc, state.LastPushUtc);
        Assert.Equal("hash-123", state.LastPushContentHash);
    }

    [Fact]
    public async Task RecordPullAsync_ShouldPersistPullHash()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);

        await sut.RecordPullAsync("/models/sample.model", "pull-hash");
        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.Equal("pull-hash", state.LastPullContentHash);
        Assert.NotNull(state.LastPullUtc);
    }

    sealed class InMemoryConfigService : IConfigService
    {
        readonly Config config = new();

        public Task<Config> GetAsync()
        {
            return Task.FromResult(config);
        }

        public Task SetAsync(Action<Config> updateAction)
        {
            updateAction(config);
            return Task.CompletedTask;
        }
    }
}
