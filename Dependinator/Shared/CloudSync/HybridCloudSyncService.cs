using Dependinator.Models;
using Shared;

namespace Dependinator.Shared.CloudSync;

sealed class HybridCloudSyncService : ICloudSyncService
{
    readonly HttpCloudSyncService httpCloudSyncService;
    readonly IVsCodeCloudSyncProxy vsCodeCloudSyncProxy;

    public HybridCloudSyncService(HttpCloudSyncService httpCloudSyncService, IVsCodeCloudSyncProxy vsCodeCloudSyncProxy)
    {
        this.httpCloudSyncService = httpCloudSyncService;
        this.vsCodeCloudSyncProxy = vsCodeCloudSyncProxy;
    }

    public bool IsAvailable => httpCloudSyncService.IsAvailable;

    public Task<R<CloudAuthState>> LoginAsync() => ForwardAsync(service => service.LoginAsync());

    public Task<R<CloudAuthState>> LogoutAsync() => ForwardAsync(service => service.LogoutAsync());

    public Task<R<CloudAuthState>> GetAuthStateAsync() => ForwardAsync(service => service.GetAuthStateAsync());

    public Task<R<CloudModelList>> ListAsync() => ForwardAsync(service => service.ListAsync());

    public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto) =>
        ForwardAsync(service => service.PushAsync(modelPath, modelDto));

    public Task<R<ModelDto>> PullAsync(string modelPath) => ForwardAsync(service => service.PullAsync(modelPath));

    async Task<R<T>> ForwardAsync<T>(Func<ICloudSyncService, Task<R<T>>> action)
    {
        var isVsCodeProxyAvailable = await vsCodeCloudSyncProxy.IsAvailableAsync();

        ICloudSyncService service = isVsCodeProxyAvailable ? vsCodeCloudSyncProxy : httpCloudSyncService;
        return await action(service);
    }
}
