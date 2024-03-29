using Dependinator.Diagrams;
using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator;


interface ISelectionService
{
    bool IsSelected { get; }
    PointerId SelectedId { get; }

    bool IsNodeMovable(double zoom);
    bool IsNodeSelected(string mouseDownId);
    void Select(PointerId targetId);
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

    PointerId selectedId = PointerId.Empty;

    public SelectionService(
        IModelService modelService,
        IApplicationEvents applicationEvents,
        IScreenService screenService)
    {
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.screenService = screenService;
    }

    public PointerId SelectedId => selectedId;
    public bool IsSelected => selectedId != PointerId.Empty;

    public bool IsNodeSelected(string nodeId) => IsSelected && selectedId.Id == nodeId;

    public void Select(PointerId targetId)
    {
        if (IsSelected && selectedId.Id == targetId.Id) return;

        if (IsSelected) Unselect(); // Clicked on some other node

        if (modelService.TryUpdateNode(targetId.Id, node => node.IsSelected = true))
        {
            selectedId = targetId;
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected) return;

        modelService.TryUpdateNode(selectedId.Id, node => node.IsSelected = false);
        selectedId = PointerId.Empty;
        applicationEvents.TriggerUIStateChanged();
    }

    public bool IsNodeMovable(double zoom)
    {
        if (!IsSelected) return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node)) return false;

        var v = screenService.SvgRect;
        var nodeZoom = 1 / node.GetZoom();
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }
}
