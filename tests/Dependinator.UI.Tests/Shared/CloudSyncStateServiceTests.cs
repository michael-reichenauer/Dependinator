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

        await sut.RecordPushAsync(@"C:\repo\Model.json", metadata, localContentHash: "hash-123", "structure-1");
        CloudSyncModelState? state = await sut.GetAsync("C:/repo/Model.json");

        Assert.NotNull(state);
        Assert.Equal(new CloudSyncBaseline("hash-123", "hash-123"), state.Baseline);
        Assert.Equal("structure-1", state.PushedStructureHash);

        // A pull leaves what this device pushed last
        await sut.RecordPullAsync("C:/repo/Model.json", "hash-456", "hash-456");
        Assert.Equal("structure-1", (await sut.GetAsync("C:/repo/Model.json"))?.PushedStructureHash);
    }

    [Fact]
    public async Task RecordPullAsync_ShouldPersistPullHash()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);

        await sut.RecordPullAsync("/models/sample.model", "pull-hash", "pull-hash");
        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");

        Assert.NotNull(state);
        Assert.Equal(new CloudSyncBaseline("pull-hash", "pull-hash"), state.Baseline);
    }

    [Fact]
    public async Task RecordLocalAsync_ShouldMoveTheLocalHash_AndKeepTheRemoteOne()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);
        await sut.RecordPullAsync("/models/sample.model", "local-hash", "remote-hash");

        await sut.RecordLocalAsync("/models/sample.model", "loaded-hash");

        CloudSyncModelState? state = await sut.GetAsync("/models/sample.model");
        Assert.Equal(new CloudSyncBaseline("loaded-hash", "remote-hash"), state?.Baseline);
    }

    [Fact]
    public async Task RecordLocalAsync_ShouldMakeNoBaseline_ForAModelWithoutOne()
    {
        InMemoryConfigService configService = new();
        CloudSyncStateService sut = new(configService);

        await sut.RecordLocalAsync("/models/sample.model", "loaded-hash");

        Assert.Null(await sut.GetAsync("/models/sample.model"));
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
