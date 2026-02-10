namespace Dependinator.Shared;

interface IVsCodeReceiveService
{
    Task ReceivedMessageAsync(string type, string message);
}

[Scoped]
class VsCodeReceiveService : IVsCodeReceiveService
{
    readonly INavigationService navigationService;

    public VsCodeReceiveService(INavigationService navigationService)
    {
        this.navigationService = navigationService;
    }

    public async Task ReceivedMessageAsync(string type, string message)
    {
        switch (type)
        {
            case "ui/ShowNode":
                await navigationService.ShowNodeAsync(message);
                break;
        }
    }
}
