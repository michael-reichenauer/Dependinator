using Dependinator.UI.Modeling.Dtos;
using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Chooses between VS Code proxy and HTTP transport at call time based on bridge availability.
sealed class HybridCloudSyncService : ICloudSyncService
{
    readonly HttpCloudSyncService httpCloudSyncService;
    readonly IVsCodeCloudSyncProxy vsCodeCloudSyncProxy;
    bool? isVsCodeProxyAvailable;

    public HybridCloudSyncService(HttpCloudSyncService httpCloudSyncService, IVsCodeCloudSyncProxy vsCodeCloudSyncProxy)
    {
        this.httpCloudSyncService = httpCloudSyncService;
        this.vsCodeCloudSyncProxy = vsCodeCloudSyncProxy;
    }

    public bool IsAvailable => httpCloudSyncService.IsAvailable;

    // Forwards request to VS Code proxy when available, otherwise to HTTP.
    public Task<R<CloudAuthState>> LoginAsync() => ForwardAsync(service => service.LoginAsync());

    public Task<R<CloudAuthState>> LogoutAsync() => ForwardAsync(service => service.LogoutAsync());

    public Task<R<CloudAuthState>> GetAuthStateAsync() => ForwardAsync(service => service.GetAuthStateAsync());

    public Task<R<CloudModelList>> ListAsync() => ForwardAsync(service => service.ListAsync());

    public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto) =>
        ForwardAsync(service => service.PushAsync(modelPath, modelDto));

    // Gets current model over active transport selected by ForwardAsync{T}.
    public Task<R<ModelDto>> PullAsync(string modelPath) => ForwardAsync(service => service.PullAsync(modelPath));

    // Selects and invokes the sync service based on whether the VS Code webview bridge is present.
    // The bridge is injected by the VS Code webview before the app boots, so the answer cannot
    // change during a session and is cached after the first JS interop roundtrip.
    async Task<R<T>> ForwardAsync<T>(Func<ICloudSyncService, Task<R<T>>> action)
    {
        isVsCodeProxyAvailable ??= await vsCodeCloudSyncProxy.IsAvailableAsync();

        ICloudSyncService service = isVsCodeProxyAvailable.Value ? vsCodeCloudSyncProxy : httpCloudSyncService;
        return await action(service);
    }
}
