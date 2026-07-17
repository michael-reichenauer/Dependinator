using Dependinator.UI.Modeling.Dtos;
using Shared;

// Client-side cloud sync: the transports and services that sign in via Clerk and upload/download
// models to the API, including the browser (direct HTTP) and VS Code-hosted (extension proxy)
// variants and the sync state tracking.
namespace Dependinator.UI.Shared.CloudSync;

// Abstraction over all cloud-sync transports used by the app.
interface ICloudSyncService
{
    bool IsAvailable { get; }

    Task<R<CloudAuthState>> LoginAsync();
    Task<R<CloudAuthState>> LogoutAsync();
    Task<R<CloudAuthState>> GetAuthStateAsync();
    Task<R<CloudModelList>> ListAsync();
    Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto);

    // Returns R.None when no remote model exists for the path.
    Task<R<ModelDto>> PullAsync(string modelPath);
}

// Fallback implementation used when cloud sync is not supported in the current host.
[Scoped]
class NoCloudSyncService : ICloudSyncService
{
    const string NotAvailableError = "Cloud sync is not available in this host.";

    static readonly CloudAuthState unavailableState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    public bool IsAvailable => false;

    public Task<R<CloudAuthState>> LoginAsync() => NotAvailableAsync<CloudAuthState>();

    public Task<R<CloudAuthState>> LogoutAsync() => NotAvailableAsync<CloudAuthState>();

    public Task<R<CloudAuthState>> GetAuthStateAsync() => Task.FromResult<R<CloudAuthState>>(unavailableState);

    public Task<R<CloudModelList>> ListAsync() => NotAvailableAsync<CloudModelList>();

    public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto) =>
        NotAvailableAsync<CloudModelMetadata>();

    public Task<R<ModelDto>> PullAsync(string modelPath) => NotAvailableAsync<ModelDto>();

    static Task<R<T>> NotAvailableAsync<T>() => Task.FromResult<R<T>>(R.Error(NotAvailableError));
}
