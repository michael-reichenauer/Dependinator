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

    public InitService(
        IScreenService screenService,
        IPointerEventService pointerEventService,
        IModelListService modelListService,
        IConfigService configService,
        IDatabase database,
        ICanvasService canvasService,
        IVsCodeMessageService vsCodeMessageService
    )
    {
        this.screenService = screenService;
        this.pointerEventService = pointerEventService;
        this.modelListService = modelListService;
        this.configService = configService;
        this.database = database;
        this.canvasService = canvasService;
        this.vsCodeMessageService = vsCodeMessageService;
    }

    public async Task InitAsync(IUIComponent component)
    {
        await vsCodeMessageService.InitAsync();
        await database.Init([FileService.DBCollectionName]);
        var config = await configService.GetAsync();
        NodeLayout.SetDensity(config.LayoutDensity);
        ViewOptions.SetShowHiddenNodes(config.ShowHiddenNodes);
        await screenService.InitAsync(component);
        await pointerEventService.InitAsync();
        await modelListService.InitAsync();
        await canvasService.InitAsync();
    }
}
