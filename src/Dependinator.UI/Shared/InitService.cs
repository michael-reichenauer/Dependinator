using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.VsCode;

// App-wide shared UI services and helpers used across the diagram and modeling features:
// initialization, navigation, storage/file access, host and VS Code integration, application
// state and events, progress, and theming/colors.
namespace Dependinator.UI.Shared;

interface IInitService
{
    Task InitAsync(IUIComponent component);
}

[Scoped]
class InitService : IInitService
{
    readonly IScreenService screenService;
    readonly IPointerEventService pointerEventService;
    readonly IModelListService modelListService;
    readonly IConfigService configService;
    readonly IDatabase database;
    readonly ICanvasService canvasService;
    readonly IVsCodeMessageService vsCodeMessageService;
    readonly IViewOptions viewOptions;
    readonly IThemeService themeService;
    readonly IKeyboardService keyboardService;
    readonly ILayoutRefineService layoutRefineService;

    public InitService(
        IScreenService screenService,
        IPointerEventService pointerEventService,
        IModelListService modelListService,
        IConfigService configService,
        IDatabase database,
        ICanvasService canvasService,
        IVsCodeMessageService vsCodeMessageService,
        IViewOptions viewOptions,
        IThemeService themeService,
        IKeyboardService keyboardService,
        ILayoutRefineService layoutRefineService
    )
    {
        this.screenService = screenService;
        this.pointerEventService = pointerEventService;
        this.modelListService = modelListService;
        this.configService = configService;
        this.database = database;
        this.canvasService = canvasService;
        this.vsCodeMessageService = vsCodeMessageService;
        this.viewOptions = viewOptions;
        this.themeService = themeService;
        this.keyboardService = keyboardService;
        this.layoutRefineService = layoutRefineService;
    }

    public async Task InitAsync(IUIComponent component)
    {
        await vsCodeMessageService.InitAsync();
        await database.Init([FileService.DBCollectionName]);
        var config = await configService.GetAsync();
        NodeLayout.SetDensity(config.LayoutDensity);
        Node.LegacyHiddenVisibility = config.ShowHiddenNodes ? NodeVisibility.Dimmed : NodeVisibility.Hidden;
        viewOptions.SetIsEditingEnabled(config.IsEditingEnabled);
        viewOptions.SetDimUnrelatedLines(config.DimUnrelatedLines);
        viewOptions.SetLinkCountWidthsShown(config.ShowLinkCountWidths);
        viewOptions.SetMinimapShown(config.ShowMinimap);
        viewOptions.SetProblemBadgesShown(config.ShowProblemBadges);
        viewOptions.SetHiddenBadgesShown(config.ShowHiddenBadges);
        viewOptions.SetLineFilter(
            new LineFilter(
                config.HideExternalLines,
                config.HideInheritanceLines,
                config.HideMemberLines,
                config.HideConfigurationLines,
                Math.Max(1, config.MinLinkCount)
            )
        );
        viewOptions.SetRowFilter(
            new RowFilter(
                config.HideParameterRows,
                config.HideVariableRows,
                config.HideOutputRows,
                config.HideSettingRows
            )
        );
        pointerEventService.InvertScrollZoom = config.InvertScrollZoom;
        // The theme must be applied before the first model render, since the SVG tiles bake the
        // palette in.
        await themeService.InitAsync(config.Theme);
        await keyboardService.InitAsync();
        await screenService.InitAsync(component);
        await pointerEventService.InitAsync();
        await modelListService.InitAsync();
        await canvasService.InitAsync();
        layoutRefineService.Start();
    }
}
