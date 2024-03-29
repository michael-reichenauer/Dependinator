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

    public InitService(
        IScreenService screenService,
        IPointerEventService mouseEventService,
        IRecentModelsService recentModelsService,
        IDatabase database,
        ICanvasService canvasService)
    {
        this.screenService = screenService;
        this.mouseEventService = mouseEventService;
        this.recentModelsService = recentModelsService;
        this.database = database;
        this.canvasService = canvasService;
    }


    public async Task InitAsync(IUIComponent component)
    {
        await screenService.InitAsync(component);
        await mouseEventService.InitAsync();
        await recentModelsService.InitAsync();
        await database.Init([FileService.DBCollectionName]);
        await canvasService.InitAsync();
    }
}