using Dependinator.Models;
using Shared;

namespace Dependinator.Shared.CloudSync;

// Abstraction over all cloud-sync transports used by the app.
interface ICloudSyncService
{
    bool IsAvailable { get; }

    Task<R<CloudAuthState>> LoginAsync();
    Task<R<CloudAuthState>> LogoutAsync();
    Task<R<CloudAuthState>> GetAuthStateAsync();
    Task<R<CloudModelList>> ListAsync();
    Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto);
    Task<R<ModelDto>> PullAsync(string modelPath);
}

// Fallback implementation used when cloud sync is not supported in the current host.
[Scoped]
class NoCloudSyncService : ICloudSyncService
{
    static readonly CloudAuthState unavailableState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    public bool IsAvailable => false;

    public Task<R<CloudAuthState>> LoginAsync()
    {
        return Task.FromResult<R<CloudAuthState>>(R.Error("Cloud sync is not available in this host."));
    }

    public Task<R<CloudAuthState>> LogoutAsync()
    {
        return Task.FromResult<R<CloudAuthState>>(R.Error("Cloud sync is not available in this host."));
    }

    public Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return Task.FromResult<R<CloudAuthState>>(unavailableState);
    }

    public Task<R<CloudModelList>> ListAsync()
    {
        return Task.FromResult<R<CloudModelList>>(R.Error("Cloud sync is not available in this host."));
    }

    public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        return Task.FromResult<R<CloudModelMetadata>>(R.Error("Cloud sync is not available in this host."));
    }

    // Cloud pull attempt is rejected when no host transport is available.
    public Task<R<ModelDto>> PullAsync(string modelPath)
    {
        return Task.FromResult<R<ModelDto>>(R.Error("Cloud sync is not available in this host."));
    }
}
