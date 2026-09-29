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

    Task<Result<CloudAuthState>> LoginAsync();
    Task<Result<CloudAuthState>> LogoutAsync();
    Task<Result<CloudAuthState>> GetAuthStateAsync();
    Task<Result<CloudModelList>> ListAsync();
    Task<Result<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto);

    // Returns a NotFoundError when no remote model exists for the path.
    Task<Result<ModelDto>> PullAsync(string modelPath);

    // Deletes the remote model for the path; a missing remote copy counts as already deleted.
    Task<Result> DeleteAsync(string modelPath);
}

// Fallback implementation used when cloud sync is not supported in the current host.
[Scoped]
class NoCloudSyncService : ICloudSyncService
{
    const string NotAvailableError = "Device sync is not available in this host.";

    static readonly CloudAuthState unavailableState = new(IsAvailable: false, IsAuthenticated: false, User: null);

    public bool IsAvailable => false;

    public Task<Result<CloudAuthState>> LoginAsync() => NotAvailableAsync<CloudAuthState>();

    public Task<Result<CloudAuthState>> LogoutAsync() => NotAvailableAsync<CloudAuthState>();

    public Task<Result<CloudAuthState>> GetAuthStateAsync() =>
        Task.FromResult<Result<CloudAuthState>>(unavailableState);

    public Task<Result<CloudModelList>> ListAsync() => NotAvailableAsync<CloudModelList>();

    public Task<Result<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto) =>
        NotAvailableAsync<CloudModelMetadata>();

    public Task<Result<ModelDto>> PullAsync(string modelPath) => NotAvailableAsync<ModelDto>();

    public Task<Result> DeleteAsync(string modelPath) => Task.FromResult<Result>(new Error(NotAvailableError));

    static Task<Result<T>> NotAvailableAsync<T>()
        where T : notnull => Task.FromResult<Result<T>>(new Error(NotAvailableError));
}
