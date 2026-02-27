namespace Dependinator.Shared;

interface IVsCodeReceiveService
{
    Task ReceivedMessageAsync(string type, string message);
}

[Scoped]
class VsCodeReceiveService : IVsCodeReceiveService
{
    readonly INavigationService navigationService;
    readonly IVsCodeCloudSyncProxy vsCodeCloudSyncProxy;

    public VsCodeReceiveService(INavigationService navigationService, IVsCodeCloudSyncProxy vsCodeCloudSyncProxy)
    {
        this.navigationService = navigationService;
        this.vsCodeCloudSyncProxy = vsCodeCloudSyncProxy;
    }

    public async Task ReceivedMessageAsync(string type, string message)
    {
        switch (type)
        {
            case "ui/ShowNode":
                await navigationService.ShowNodeAsync(message);
                break;
            case "ui/cloudSync/response":
                await vsCodeCloudSyncProxy.HandleResponseAsync(message);
                break;
        }
    }
}
