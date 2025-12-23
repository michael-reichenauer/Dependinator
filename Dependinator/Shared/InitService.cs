using Dependinator.Diagrams;
using Dependinator.Utils.UI;

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
    readonly IDatabase database;
    readonly ICanvasService canvasService;
    readonly IVsCodeMessageService vsCodeMessageService;

    public InitService(
        IScreenService screenService,
        IPointerEventService mouseEventService,
        IRecentModelsService recentModelsService,
        IDatabase database,
        ICanvasService canvasService,
        IVsCodeMessageService vsCodeMessageService
    )
    {
        this.screenService = screenService;
        this.mouseEventService = mouseEventService;
        this.recentModelsService = recentModelsService;
        this.database = database;
        this.canvasService = canvasService;
        this.vsCodeMessageService = vsCodeMessageService;
    }

    public async Task InitAsync(IUIComponent component)
    {
        await database.Init([FileService.DBCollectionName]);
        await screenService.InitAsync(component);
        await mouseEventService.InitAsync();
        await recentModelsService.InitAsync();
        await canvasService.InitAsync();
        await vsCodeMessageService.InitAsync();
    }
}
