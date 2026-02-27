using Dependinator.Models;
using Dependinator.Core.Utils;
using Dependinator.Shared;
using Shared;

namespace Dependinator.Wasm;

sealed class HybridCloudSyncService : ICloudSyncService
{
    readonly HttpCloudSyncService httpCloudSyncService;
    readonly IVsCodeCloudSyncProxy vsCodeCloudSyncProxy;

    public HybridCloudSyncService(
        HttpCloudSyncService httpCloudSyncService,
        IVsCodeCloudSyncProxy vsCodeCloudSyncProxy
    )
    {
        this.httpCloudSyncService = httpCloudSyncService;
        this.vsCodeCloudSyncProxy = vsCodeCloudSyncProxy;
    }

    public bool IsAvailable => httpCloudSyncService.IsAvailable;

    public async Task<R<CloudAuthState>> LoginAsync()
    {
        ICloudSyncService service = await GetActiveServiceAsync();
        return await service.LoginAsync();
    }

    public async Task<R<CloudAuthState>> LogoutAsync()
    {
        ICloudSyncService service = await GetActiveServiceAsync();
        return await service.LogoutAsync();
    }

    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        ICloudSyncService service = await GetActiveServiceAsync();
        return await service.GetAuthStateAsync();
    }

    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        ICloudSyncService service = await GetActiveServiceAsync();
        return await service.PushAsync(modelPath, modelDto);
    }

    public async Task<R<ModelDto>> PullAsync(string modelPath)
    {
        ICloudSyncService service = await GetActiveServiceAsync();
        return await service.PullAsync(modelPath);
    }

    ICloudSyncService GetActiveService()
    {
        return httpCloudSyncService;
    }

    async Task<ICloudSyncService> GetActiveServiceAsync()
    {
        if (await vsCodeCloudSyncProxy.IsAvailableAsync())
            return new VsCodeCloudSyncProxyAdapter(vsCodeCloudSyncProxy, httpCloudSyncService.IsAvailable);

        return GetActiveService();
    }

    sealed class VsCodeCloudSyncProxyAdapter : ICloudSyncService
    {
        readonly IVsCodeCloudSyncProxy proxy;

        public VsCodeCloudSyncProxyAdapter(IVsCodeCloudSyncProxy proxy, bool isAvailable)
        {
            this.proxy = proxy;
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public Task<R<CloudAuthState>> LoginAsync() => proxy.LoginAsync();

        public Task<R<CloudAuthState>> LogoutAsync() => proxy.LogoutAsync();

        public Task<R<CloudAuthState>> GetAuthStateAsync() => proxy.GetAuthStateAsync();

        public Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
        {
            return proxy.PushAsync(modelPath, modelDto);
        }

        public Task<R<ModelDto>> PullAsync(string modelPath) => proxy.PullAsync(modelPath);
    }
}
