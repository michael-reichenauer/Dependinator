using Dependinator.Core.Shared;
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
    readonly IModelListService modelListService;

    bool isRefreshing;
    bool isRefreshPending;

    // The kinds of files that changed since the refresh that is running started, for the
    // refresh after it. (isAllPending: a request that named no kinds, so everything is parsed.)
    readonly HashSet<string> pendingKinds = [];
    bool isAllPending;

    public VsCodeReceiveService(
        INavigationService navigationService,
        ICanvasService canvasService,
        IApplicationEvents applicationEvents,
        IModelService modelService,
        IModelListService modelListService
    )
    {
        this.navigationService = navigationService;
        this.canvasService = canvasService;
        this.applicationEvents = applicationEvents;
        this.modelService = modelService;
        this.modelListService = modelListService;
    }

    public async Task ReceivedMessageAsync(string type, string message)
    {
        switch (type)
        {
            case "ui/ShowNode":
                await navigationService.ShowNodeAsync(message);
                break;
            case "ui/refresh":
                await ListSourcesIfChangedAsync(message);
                if (IsSourceOfModel(message))
                    await RefreshAsync(message);
                break;
            case "ui/sourceChanged": // Source files changed while automatic refresh is off
                await ListSourcesIfChangedAsync(message);
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

    // A solution, Bicep file or script project was added or removed: the Models menu lists the
    // workspace's sources, and a workspace model is made of them.
    async Task ListSourcesIfChangedAsync(string changedKinds)
    {
        if (!Kinds(changedKinds).Contains(WorkspaceSources.SourcesChangedKind))
            return;

        await modelListService.RefreshWorkspaceSourcesAsync();
        applicationEvents.TriggerUIStateChanged();
    }

    static string[] Kinds(string changedKinds) => changedKinds.Split(',', StringSplitOptions.RemoveEmptyEntries);

    // Parsing is one opaque, non-cancellable await, so requests arriving while a
    // refresh runs are coalesced into a single re-run once it completes, of all their kinds.
    async Task RefreshAsync(string changedKinds)
    {
        isAllPending |= changedKinds == "";
        pendingKinds.UnionWith(Kinds(changedKinds));
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
                string[] kinds = [.. pendingKinds];
                bool isAll = isAllPending;
                pendingKinds.Clear();
                isAllPending = false;

                if (isAll)
                    await canvasService.RefreshAsync();
                else
                    await canvasService.RefreshChangedAsync(kinds);
            } while (isRefreshPending);
        }
        finally
        {
            isRefreshing = false;
        }
    }
}
