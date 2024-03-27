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
    readonly IMouseEventService mouseEventService;
    readonly IRecentModelsService recentModelsService;
    readonly IJSInterop jSInteropService;
    readonly IDatabase database;


    public InitService(
        IScreenService screenService,
        IMouseEventService mouseEventService,
        IRecentModelsService recentModelsService,
        IJSInterop jSInteropService,
        IDatabase database)
    {
        this.screenService = screenService;
        this.mouseEventService = mouseEventService;
        this.recentModelsService = recentModelsService;
        this.jSInteropService = jSInteropService;
        this.database = database;
    }


    public async Task InitAsync(IUIComponent component)
    {
        await screenService.InitAsync(component);
        await mouseEventService.InitAsync();
        await recentModelsService.InitAsync();
        await database.Init([FileService.DBCollectionName]);
    }
}