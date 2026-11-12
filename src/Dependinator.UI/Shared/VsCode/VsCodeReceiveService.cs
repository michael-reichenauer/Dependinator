using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;

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
    readonly IApplicationEvents applicationEvents;
    readonly IModelService modelService;

    bool isRefreshing;
    bool isRefreshPending;

    public VsCodeReceiveService(
        INavigationService navigationService,
        ICanvasService canvasService,
        IApplicationEvents applicationEvents,
        IModelService modelService
    )
    {
        this.navigationService = navigationService;
        this.canvasService = canvasService;
        this.applicationEvents = applicationEvents;
        this.modelService = modelService;
    }

    public async Task ReceivedMessageAsync(string type, string message)
    {
        switch (type)
        {
            case "ui/ShowNode":
                await navigationService.ShowNodeAsync(message);
                break;
            case "ui/refresh":
                if (IsSourceOfModel(message))
                    await RefreshAsync();
                break;
            case "ui/sourceChanged": // Source files changed while automatic refresh is off
                if (IsSourceOfModel(message))
                    modelService.MarkSourceChanged();
                break;
            case "ui/search":
                applicationEvents.TriggerSearchRequested(); // The "Dependinator: Search Nodes" command
                break;
        }
    }

    // The message names the kinds of the files that changed (none named: any kind). A change
    // of a kind the open model is not parsed from is no reason to parse it again: a JavaScript
    // build writes .js files beside a C# solution.
    bool IsSourceOfModel(string changedKinds) => changedKinds == "" || modelService.IsParsedFrom(changedKinds);

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
