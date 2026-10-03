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

    // Edit mode is off by default in every host: most sessions only explore, and edit-only
    // chrome (link handles, resize handles) would otherwise clutter the diagram.
    public bool IsEditingEnabled { get; set; } = false;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool DimUnrelatedLines { get; set; } = true;
    public bool HideExternalLines { get; set; } = false;
    public bool HideInheritanceLines { get; set; } = false;
    public bool HideMemberLines { get; set; } = false;
    public int MinLinkCount { get; set; } = 1;

    // The first-run tour (CoachService) has been shown or skipped.
    public bool IsCoachSeen { get; set; } = false;

    // The minimap in the corner of the diagram (View › Show Minimap).
    public bool ShowMinimap { get; set; } = false;
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
