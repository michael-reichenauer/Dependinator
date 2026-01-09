using Dependinator.Diagrams;
using DependinatorCore;
using DependinatorCore.Rpc;

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
    readonly IServiceProvider serviceProvider;
    bool jsonRpcInitialized;

    public InitService(
        IScreenService screenService,
        IPointerEventService mouseEventService,
        IRecentModelsService recentModelsService,
        IDatabase database,
        ICanvasService canvasService,
        IVsCodeMessageService vsCodeMessageService,
        IServiceProvider serviceProvider
    )
    {
        this.screenService = screenService;
        this.mouseEventService = mouseEventService;
        this.recentModelsService = recentModelsService;
        this.database = database;
        this.canvasService = canvasService;
        this.vsCodeMessageService = vsCodeMessageService;
        this.serviceProvider = serviceProvider;
    }

    public async Task InitAsync(IUIComponent component)
    {
        EnsureJsonRpcInitialized();
        await database.Init([FileService.DBCollectionName]);
        await screenService.InitAsync(component);
        await mouseEventService.InitAsync();
        await recentModelsService.InitAsync();
        await canvasService.InitAsync();
        await vsCodeMessageService.InitAsync();
    }

    void EnsureJsonRpcInitialized()
    {
        if (jsonRpcInitialized)
            return;

        serviceProvider.UseJsonRpcClasses(typeof(DependinatorCore.RootClass));
        serviceProvider.UseJsonRpc();
        jsonRpcInitialized = true;
    }
}
