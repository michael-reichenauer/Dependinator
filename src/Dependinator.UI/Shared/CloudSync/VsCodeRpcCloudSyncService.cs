using Dependinator.Core;
using Dependinator.Core.CloudSync;
using Dependinator.UI.Modeling.Dtos;
using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Cloud sync transport used when the app runs inside the VS Code webview.
interface IVsCodeCloudSyncService : ICloudSyncService
{
    Task<bool> IsAvailableAsync();
}

// Implements ICloudSyncService by calling the LSP-hosted ICloudSyncRpcService over the
// JSON-RPC tunnel (webview -> extension -> LSP). The LSP owns the access token and performs
// the HTTP calls; documents are gzip-compressed/decompressed here in the UI.
[Scoped]
class VsCodeRpcCloudSyncService : IVsCodeCloudSyncService
{
    readonly IJSInterop jSInterop;
    readonly ICloudSyncRpcService rpcService;
    readonly TimeSpan requestTimeout;
    readonly TimeSpan loginRequestTimeout;

    public VsCodeRpcCloudSyncService(IJSInterop jSInterop, ICloudSyncRpcService rpcService)
        : this(jSInterop, rpcService, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5)) { }

    internal VsCodeRpcCloudSyncService(
        IJSInterop jSInterop,
        ICloudSyncRpcService rpcService,
        TimeSpan requestTimeout,
        TimeSpan? loginRequestTimeout = null
    )
    {
        this.jSInterop = jSInterop;
        this.rpcService = rpcService;
        this.requestTimeout = requestTimeout;
        this.loginRequestTimeout = loginRequestTimeout ?? TimeSpan.FromMinutes(5);
    }

    public bool IsAvailable => true;

    // Checks whether the active host exposes the VS Code bridge object.
    public async Task<bool> IsAvailableAsync()
    {
        if (!Build.IsWasm)
            return false;
        return await jSInterop.Call<bool>("isVsCodeWebView");
    }

    // Starts the extension-hosted login flow (interactive, so a much longer timeout).
    public async Task<R<CloudAuthState>> LoginAsync()
    {
        return await CallAsync("login", () => rpcService.LoginAsync(), loginRequestTimeout);
    }

    public async Task<R<CloudAuthState>> LogoutAsync()
    {
        return await CallAsync("logout", () => rpcService.LogoutAsync());
    }

    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return await CallAsync("getAuthState", () => rpcService.GetAuthStateAsync());
    }

    public async Task<R<CloudModelList>> ListAsync()
    {
        return await CallAsync("list", () => rpcService.ListAsync());
    }

    // Compresses the model locally and pushes the document via the LSP.
    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await CallAsync("push", () => rpcService.PushAsync(document));
    }

    // Pulls a document via the LSP and decodes it back to a local model DTO.
    // Returns R.None when no remote model exists for the path.
    public async Task<R<ModelDto>> PullAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        R<CloudModelDocument> documentResult = await CallAsync("pull", () => rpcService.PullAsync(modelKey));
        if (documentResult.IsNone)
            return R.None;

        if (!Try(out var document, out var error, documentResult))
            return error;

        return CloudModelSerializer.ReadModel(document);
    }

    // Deletes the remote model for the path via the LSP.
    public async Task<R> DeleteAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        return await CallAsync("delete", () => rpcService.DeleteAsync(modelKey));
    }

    // Awaits an RPC call with a timeout; a dead or missing LSP must not hang the UI forever.
    async Task<R<T>> CallAsync<T>(string action, Func<Task<R<T>>> call, TimeSpan? timeoutOverride = null)
    {
        try
        {
            return await call().WaitAsync(timeoutOverride ?? requestTimeout);
        }
        catch (TimeoutException)
        {
            return R.Error($"VS Code cloud sync action '{action}' timed out.");
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    async Task<R> CallAsync(string action, Func<Task<R>> call, TimeSpan? timeoutOverride = null)
    {
        try
        {
            return await call().WaitAsync(timeoutOverride ?? requestTimeout);
        }
        catch (TimeoutException)
        {
            return R.Error($"VS Code cloud sync action '{action}' timed out.");
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }
}
