using Dependinator.Core.Shared;
using Dependinator.UI.Modeling;
using Dependinator.UI.Shared.CloudSync;

namespace Dependinator.UI.Shared;

class Config
{
    public List<string> RecentPaths { get; set; } = [];
    public NodeLayoutDensity LayoutDensity { get; set; } = NodeLayoutDensity.Balanced;
    public Dictionary<string, CloudSyncModelState> CloudSyncStates { get; set; } = [];
    public bool ShowHiddenNodes { get; set; } = true;
    public bool InvertScrollZoom { get; set; } = false;
}

interface IConfigService
{
    Task<Config> GetAsync();
    Task SetAsync(Action<Config> updateAction);
}

[Transient]
class ConfigService : IConfigService
{
    readonly IFileService fileService;
    readonly IHostStoragePaths hostStoragePaths;

    public ConfigService(IFileService fileService, IHostStoragePaths hostStoragePaths)
    {
        this.fileService = fileService;
        this.hostStoragePaths = hostStoragePaths;
    }

    public async Task<Config> GetAsync()
    {
        // Default config values when none is stored yet
        return await fileService.ReadAsync<Config>(hostStoragePaths.ConfigPath) is Config config
            ? config
            : new Config();
    }

    public async Task SetAsync(Action<Config> updateAction)
    {
        // Default config values when none is stored yet
        Config config = await fileService.ReadAsync<Config>(hostStoragePaths.ConfigPath) is Config stored
            ? stored
            : new Config();
        updateAction(config);
        await fileService.WriteAsync(hostStoragePaths.ConfigPath, config);
    }
}
