using Dependinator.Diagrams;
using Dependinator.Models;

namespace Dependinator.Shared;

interface IInitService
{
    Task InitAsync(IUIComponent component);
}

[Scoped]
class InitService : IInitService
{
    readonly IScreenService screenService;
    readonly IPointerEventService mouseEventService;
    readonly IRecentModelsService recentModelsService;
    readonly IConfigService configService;
    readonly IDatabase database;
    readonly ICanvasService canvasService;
    readonly IVsCodeMessageService vsCodeMessageService;

    public InitService(
        IScreenService screenService,
        IPointerEventService mouseEventService,
        IRecentModelsService recentModelsService,
        IConfigService configService,
        IDatabase database,
        ICanvasService canvasService,
        IVsCodeMessageService vsCodeMessageService
    )
    {
        this.screenService = screenService;
        this.mouseEventService = mouseEventService;
        this.recentModelsService = recentModelsService;
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
        await screenService.InitAsync(component);
        await mouseEventService.InitAsync();
        await recentModelsService.InitAsync();
        await canvasService.InitAsync();
    }
}
