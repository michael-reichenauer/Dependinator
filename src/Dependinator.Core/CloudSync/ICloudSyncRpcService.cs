using Dependinator.Core.Rpc;
using Shared;

namespace Dependinator.Core.CloudSync;

// Cloud sync operations the VS Code webview UI invokes on the LSP process, which owns the
// access token (provided by the extension) and talks HTTP to the cloud-sync API.
// Documents are already gzip-compressed by the UI (see CloudModelSerializer in Dependinator.UI).
[Rpc]
internal interface ICloudSyncRpcService
{
    Task<Result<CloudAuthState>> LoginAsync();
    Task<Result<CloudAuthState>> LogoutAsync();
    Task<Result<CloudAuthState>> GetAuthStateAsync();
    Task<Result<CloudModelList>> ListAsync();
    Task<Result<CloudModelMetadata>> PushAsync(CloudModelDocument document);
    Task<Result<CloudModelDocument>> PullAsync(string modelKey);
    Task<Result> DeleteAsync(string modelKey);
}
