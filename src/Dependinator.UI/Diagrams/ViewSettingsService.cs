using Dependinator.UI.Modeling;

namespace Dependinator.UI.Diagrams;

// Changes the view options the menus toggle (View and Edit), saves the ones kept between
// sessions, and redraws: most of them change what the tiles hold, so the tile cache is cleared.
interface IViewSettingsService
{
    Task SetEditingAsync(bool isEnabled);
    Task SetLineFilterAsync(LineFilter filter);
    Task SetRowFilterAsync(RowFilter filter);
    Task ToggleMinimapAsync();
    void ToggleRevealHiddenNodes();
    void ToggleLegend();
}

[Scoped]
class ViewSettingsService(
    IViewOptions viewOptions,
    IConfigService configService,
    IModelService modelService,
    IApplicationEvents applicationEvents
) : IViewSettingsService
{
    public async Task SetEditingAsync(bool isEnabled)
    {
        viewOptions.SetIsEditingEnabled(isEnabled);
        await configService.SetAsync(config => config.IsEditingEnabled = isEnabled);
        // Edit-only markup (e.g. link handles) is baked into cached tiles, so the cache must be
        // cleared for the toggle to take effect immediately.
        modelService.ClearCache();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task SetLineFilterAsync(LineFilter filter)
    {
        viewOptions.SetLineFilter(filter);
        await configService.SetAsync(config =>
        {
            config.HideExternalLines = filter.HideExternal;
            config.HideInheritanceLines = filter.HideInheritance;
            config.HideMemberLines = filter.HideMember;
            config.HideConfigurationLines = filter.HideConfiguration;
            config.MinLinkCount = filter.MinLinkCount;
        });
        modelService.ClearCache(); // Which lines a tile holds depends on the filter
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task SetRowFilterAsync(RowFilter filter)
    {
        viewOptions.SetRowFilter(filter);
        await configService.SetAsync(config =>
        {
            config.HideParameterRows = filter.HideParameters;
            config.HideVariableRows = filter.HideVariables;
            config.HideOutputRows = filter.HideOutputs;
            config.HideSettingRows = filter.HideSettings;
        });
        modelService.ClearCache(); // Which rows and lines a tile holds depends on the filter
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task ToggleMinimapAsync()
    {
        var isShown = !viewOptions.IsMinimapShown;
        viewOptions.SetMinimapShown(isShown);
        await configService.SetAsync(config => config.ShowMinimap = isShown);
        applicationEvents.TriggerUIStateChanged();
    }

    // A temporary peek, not saved.
    public void ToggleRevealHiddenNodes()
    {
        viewOptions.SetRevealHiddenNodes(!viewOptions.RevealHiddenNodes);
        modelService.ClearCache();
        applicationEvents.TriggerUIStateChanged();
    }

    public void ToggleLegend()
    {
        viewOptions.SetLegendShown(!viewOptions.IsLegendShown);
        applicationEvents.TriggerUIStateChanged();
    }
}
