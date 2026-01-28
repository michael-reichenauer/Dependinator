namespace Dependinator.Shared;

interface IVsCodeIntegrationService
{
    Task ReceivedMessageAsync(string type, string message);
}

[Scoped]
class VsCodeIntegrationService : IVsCodeIntegrationService
{
    readonly INavigationService navigationService;

    public VsCodeIntegrationService(INavigationService navigationService)
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
