using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
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
        IKeyboardService keyboardService
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
    }

    public async Task InitAsync(IUIComponent component)
    {
        await vsCodeMessageService.InitAsync();
        await database.Init([FileService.DBCollectionName]);
        var config = await configService.GetAsync();
        NodeLayout.SetDensity(config.LayoutDensity);
        viewOptions.SetShowHiddenNodes(config.ShowHiddenNodes);
        viewOptions.SetIsEditingEnabled(config.IsEditingEnabled);
        viewOptions.SetDimUnrelatedLines(config.DimUnrelatedLines);
        viewOptions.SetLineFilter(
            new LineFilter(
                config.HideExternalLines,
                config.HideInheritanceLines,
                config.HideMemberLines,
                Math.Max(1, config.MinLinkCount)
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
    }
}
