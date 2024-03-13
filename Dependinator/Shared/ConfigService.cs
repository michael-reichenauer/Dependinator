namespace Dependinator.Shared;


class Config
{
    public string LastUsedPath { get; set; } = "";

}

interface IConfigService
{
    Task<Config> GetAsync();
    Task SetAsync(Action<Config> updateAction);
}


[Transient]
class ConfigService : IConfigService
{
    const string configPath = "DependinatorConfig.json";

    readonly IFileService fileService;

    public ConfigService(IFileService fileService)
    {
        this.fileService = fileService;
    }

    public async Task<Config> GetAsync()
    {
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(configPath)))
        {   // Return default config values
            return new Config();
        }
        return config;
    }

    public async Task SetAsync(Action<Config> updateAction)
    {
        if (!Try(out var config, out var e, await fileService.ReadAsync<Config>(configPath)))
        {   // Use default config values
            config = new Config();
        }
        updateAction(config);
        await fileService.WriteAsync(configPath, config);
    }
}