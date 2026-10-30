using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Shared.CloudSync;

namespace Dependinator.UI.Shared;

class Config
{
    public List<string> RecentPaths { get; set; } = [];
    public NodeLayoutDensity LayoutDensity { get; set; } = NodeLayoutDensity.Balanced;
    public Dictionary<string, CloudSyncModelState> CloudSyncStates { get; set; } = [];

    // The former View › Show Hidden Nodes (on: hidden nodes faint, off: left out). Now only read
    // to give nodes hidden before dimming existed the look they had (Node.LegacyHiddenVisibility).
    public bool ShowHiddenNodes { get; set; } = true;
    public bool InvertScrollZoom { get; set; } = false;

    // Edit mode is off by default in every host: most sessions only explore, and edit-only
    // chrome (link handles, resize handles) would otherwise clutter the diagram.
    public bool IsEditingEnabled { get; set; } = false;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool DimUnrelatedLines { get; set; } = true;

    // The selection's lines drawn at their link-count width (View › Line Widths by Link Count);
    // off by default, since thick lines on every selection made the diagram look heavy.
    public bool ShowLinkCountWidths { get; set; } = false;
    public bool HideExternalLines { get; set; } = false;
    public bool HideInheritanceLines { get; set; } = false;
    public bool HideMemberLines { get; set; } = false;

    // Configuration lines (Bicep values flowing into parameter and variable rows) are plumbing:
    // left out by default, the explorer still shows them.
    public bool HideConfigurationLines { get; set; } = true;
    public int MinLinkCount { get; set; } = 1;

    // Row kinds left out of the diagram (View › Rows), see RowFilter.
    public bool HideParameterRows { get; set; } = false;
    public bool HideVariableRows { get; set; } = false;
    public bool HideOutputRows { get; set; } = false;
    public bool HideSettingRows { get; set; } = false;

    // The first-run tour (CoachService) has been shown or skipped.
    public bool IsCoachSeen { get; set; } = false;

    // The minimap in the corner of the diagram (View › Show Minimap).
    public bool ShowMinimap { get; set; } = false;

    // Badges on the nodes that contain problems (View › Show Problem Badges).
    public bool ShowProblemBadges { get; set; } = true;

    // The badge on a node with children the user hid (View › Show Hidden Node Badges).
    public bool ShowHiddenBadges { get; set; } = true;

    // The experimental Claude Code provider (Settings › AI), turned on after a warning: `claude -p`
    // with the user's own Claude login.
    public bool UseClaudeCode { get; set; } = false;

    // The AI the app asks by default (Settings › AI); none until the user picks one, the first
    // time they ask or in Settings. Stored as the AiSource number, so its values never change.
    public AiSource? DefaultAiProvider { get; set; }
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
