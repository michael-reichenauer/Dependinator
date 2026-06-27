using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Shared;

namespace Dependinator.UI.Tests.Shared;

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
        Assert.Equal(new CloudSyncBaseline("hash-123", "hash-123"), state.Baseline);
    }

    [Fact]
    public async Task RecordPullAsync_ShouldPersistPullHash()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);

        await sut.RecordPullAsync("/models/sample.model", "pull-hash");
        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.Equal(new CloudSyncBaseline("pull-hash", "pull-hash"), state.Baseline);
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
