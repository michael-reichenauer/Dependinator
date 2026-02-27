using Dependinator.Models;
using Shared;

namespace Dependinator.Shared;

interface ICloudSyncService
{
    bool IsAvailable { get; }

    Task<R<CloudAuthState>> GetAuthStateAsync();
    Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto);
    Task<R<ModelDto>> PullAsync(string modelPath);
}

[Scoped]
class NoCloudSyncService : ICloudSyncService
{
    static readonly CloudAuthState unavailableState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    public bool IsAvailable => false;

    public Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return Task.FromResult<R<CloudAuthState>>(unavailableState);
    }

    public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        return Task.FromResult<R<CloudModelMetadata>>(R.Error("Cloud sync is not available in this host."));
    }

    public Task<R<ModelDto>> PullAsync(string modelPath)
    {
        return Task.FromResult<R<ModelDto>>(R.Error("Cloud sync is not available in this host."));
    }
}
