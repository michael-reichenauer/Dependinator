using Dependinator.UI.Modeling.Dtos;
using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Chooses between VS Code proxy and HTTP transport at call time based on bridge availability.
sealed class HybridCloudSyncService : ICloudSyncService
{
    readonly HttpCloudSyncService httpCloudSyncService;
    readonly IVsCodeCloudSyncService vsCodeCloudSyncProxy;
    bool? isVsCodeProxyAvailable;

    public HybridCloudSyncService(
        HttpCloudSyncService httpCloudSyncService,
        IVsCodeCloudSyncService vsCodeCloudSyncProxy
    )
    {
        this.httpCloudSyncService = httpCloudSyncService;
        this.vsCodeCloudSyncProxy = vsCodeCloudSyncProxy;
    }

    public bool IsAvailable => httpCloudSyncService.IsAvailable;

    // Forwards request to VS Code proxy when available, otherwise to HTTP.
    public Task<Result<CloudAuthState>> LoginAsync() => ForwardAsync(service => service.LoginAsync());

    public Task<Result<CloudAuthState>> LogoutAsync() => ForwardAsync(service => service.LogoutAsync());

    public Task<Result<CloudAuthState>> GetAuthStateAsync() => ForwardAsync(service => service.GetAuthStateAsync());

    public Task<Result<CloudModelList>> ListAsync() => ForwardAsync(service => service.ListAsync());

    public Task<Result<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto) =>
        ForwardAsync(service => service.PushAsync(modelPath, modelDto));

    // Gets current model over active transport selected by ForwardAsync{T}.
    public Task<Result<ModelDto>> PullAsync(string modelPath) => ForwardAsync(service => service.PullAsync(modelPath));

    public Task<Result> DeleteAsync(string modelPath) => ForwardAsync(service => service.DeleteAsync(modelPath));

    // Selects and invokes the sync service based on whether the VS Code webview bridge is present.
    // The bridge is injected by the VS Code webview before the app boots, so the answer cannot
    // change during a session and is cached after the first JS interop roundtrip.
    async Task<Result<T>> ForwardAsync<T>(Func<ICloudSyncService, Task<Result<T>>> action)
        where T : notnull
    {
        isVsCodeProxyAvailable ??= await vsCodeCloudSyncProxy.IsAvailableAsync();

        ICloudSyncService service = isVsCodeProxyAvailable.Value ? vsCodeCloudSyncProxy : httpCloudSyncService;
        return await action(service);
    }

    async Task<Result> ForwardAsync(Func<ICloudSyncService, Task<Result>> action)
    {
        isVsCodeProxyAvailable ??= await vsCodeCloudSyncProxy.IsAvailableAsync();

        ICloudSyncService service = isVsCodeProxyAvailable.Value ? vsCodeCloudSyncProxy : httpCloudSyncService;
        return await action(service);
    }
}
