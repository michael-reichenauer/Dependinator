using Dependinator.Core.Rpc;
using Shared;

namespace Dependinator.Core.CloudSync;

// Cloud sync operations the VS Code webview UI invokes on the LSP process, which owns the
// access token (provided by the extension) and talks HTTP to the cloud-sync API.
// Documents are already gzip-compressed by the UI (see CloudModelSerializer in Dependinator.UI).
[Rpc]
internal interface ICloudSyncRpcService
{
    Task<R<CloudAuthState>> LoginAsync();
    Task<R<CloudAuthState>> LogoutAsync();
    Task<R<CloudAuthState>> GetAuthStateAsync();
    Task<R<CloudModelList>> ListAsync();
    Task<R<CloudModelMetadata>> PushAsync(CloudModelDocument document);
    Task<R<CloudModelDocument>> PullAsync(string modelKey);
}
