using Dependinator.Core.Shared;
using Dependinator.Models;

namespace Dependinator.Shared;

class Config
{
    public List<string> RecentPaths { get; set; } = [];
    public NodeLayoutDensity LayoutDensity { get; set; } = NodeLayoutDensity.Balanced;
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
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(hostStoragePaths.ConfigPath)))
        { // Return default config values
            return new Config();
        }
        return config;
    }

    public async Task SetAsync(Action<Config> updateAction)
    {
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(hostStoragePaths.ConfigPath)))
        { // Use default config values
            config = new Config();
        }
        updateAction(config);
        await fileService.WriteAsync(hostStoragePaths.ConfigPath, config);
    }
}
