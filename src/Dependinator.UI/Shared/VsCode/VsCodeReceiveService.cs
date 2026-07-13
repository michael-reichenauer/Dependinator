using Dependinator.UI.Diagrams;

namespace Dependinator.UI.Shared.VsCode;

interface IVsCodeReceiveService
{
    Task ReceivedMessageAsync(string type, string message);
}

[Scoped]
class VsCodeReceiveService : IVsCodeReceiveService
{
    readonly INavigationService navigationService;
    readonly ICanvasService canvasService;

    bool isRefreshing;
    bool isRefreshPending;

    public VsCodeReceiveService(INavigationService navigationService, ICanvasService canvasService)
    {
        this.navigationService = navigationService;
        this.canvasService = canvasService;
    }

    public async Task ReceivedMessageAsync(string type, string message)
    {
        switch (type)
        {
            case "ui/ShowNode":
                await navigationService.ShowNodeAsync(message);
                break;
            case "ui/refresh":
                await RefreshAsync();
                break;
        }
    }

    // Parsing is one opaque, non-cancellable await, so requests arriving while a
    // refresh runs are coalesced into a single re-run once it completes.
    async Task RefreshAsync()
    {
        if (isRefreshing)
        {
            isRefreshPending = true;
            return;
        }

        isRefreshing = true;
        try
        {
            do
            {
                isRefreshPending = false;
                await canvasService.RefreshAsync();
            } while (isRefreshPending);
        }
        finally
        {
            isRefreshing = false;
        }
    }
}
