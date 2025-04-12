namespace Dependinator.Shared;

class Config
{
    public List<string> RecentPaths { get; set; } = [];
}

interface IConfigService
{
    Task<Config> GetAsync();
    Task SetAsync(Action<Config> updateAction);
}

[Transient]
class ConfigService : IConfigService
{
    const string ConfigPath = "/.dependinator/DependinatorConfig.json";

    readonly IFileService fileService;

    public ConfigService(IFileService fileService)
    {
        this.fileService = fileService;
    }

    public async Task<Config> GetAsync()
    {
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(ConfigPath)))
        { // Return default config values
            return new Config();
        }
        return config;
    }

    public async Task SetAsync(Action<Config> updateAction)
    {
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(ConfigPath)))
        { // Use default config values
            config = new Config();
        }
        updateAction(config);
        await fileService.WriteAsync(ConfigPath, config);
    }
}
