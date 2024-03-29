using Dependinator.Diagrams;
using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator;


interface ISelectionService
{
    bool IsSelected { get; }
    string SelectedId { get; }

    bool IsNodeMovable(double zoom);
    bool IsNodeSelected(string mouseDownId);
    void Select(string nodeId);
    void Unselect();
}


[Scoped]
class SelectionService : ISelectionService
{
    const double MinCover = 0.5;
    const double MaxCover = 0.8;

    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;
    readonly IScreenService screenService;

    string selectedId = "";

    public SelectionService(
        IModelService modelService,
        IApplicationEvents applicationEvents,
        IScreenService screenService)
    {
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.screenService = screenService;
    }

    public string SelectedId => selectedId;
    public bool IsSelected => selectedId != "";

    public bool IsNodeSelected(string nodeId) => IsSelected && selectedId == nodeId;

    public void Select(string nodeId)
    {
        if (selectedId == nodeId) return;

        if (selectedId != "") Unselect(); // Clicked on some other node

        if (modelService.TryUpdateNode(nodeId, node => node.IsSelected = true))
        {
            selectedId = nodeId;
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected) return;

        modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
        selectedId = "";
        applicationEvents.TriggerUIStateChanged();
    }

    public bool IsNodeMovable(double zoom)
    {
        if (selectedId == "") return false;
        if (!modelService.TryGetNode(selectedId, out var node)) return false;

        var v = screenService.SvgRect;
        var nodeZoom = (1 / node.GetZoom());
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }
}
