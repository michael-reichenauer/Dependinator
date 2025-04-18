using Dependinator.Diagrams;
using Dependinator.Models;

namespace Dependinator;

interface ISelectionService
{
    bool IsSelected { get; }
    bool IsEditMode { get; }
    PointerId SelectedId { get; }
    Pos SelectedNodePosition { get; set; }

    bool IsNodeMovable(double zoom);
    void Select(PointerId pointerId);
    void Select(NodeId nodeId);
    void SetEditMode(bool isEditMode);
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

    Pos selectedNodePosition = Pos.None;
    PointerId selectedId = PointerId.Empty;
    bool isEditMode = false;

    public SelectionService(
        IModelService modelService,
        IApplicationEvents applicationEvents,
        IScreenService screenService
    )
    {
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
        this.screenService = screenService;
    }

    public PointerId SelectedId => selectedId;
    public bool IsSelected => selectedId != PointerId.Empty;

    public bool IsEditMode => isEditMode;

    public Pos SelectedNodePosition
    {
        get => IsSelected ? selectedNodePosition : Pos.None;
        set => selectedNodePosition = value;
    }

    public void SetEditMode(bool isEditMode)
    {
        if (!IsSelected)
            return;

        modelService.UseNode(
            selectedId.Id,
            node =>
            {
                node.IsEditMode = isEditMode;
            }
        );
        this.isEditMode = isEditMode;
        applicationEvents.TriggerUIStateChanged();
    }

    public void Select(NodeId nodeId)
    {
        Select(PointerId.FromNode(nodeId));
    }

    public async void Select(PointerId pointerId)
    {
        if (IsSelected && selectedId.Id == pointerId.Id)
            return;

        if (IsSelected)
            Unselect(); // Clicked on some other node

        if (
            modelService.UseNode(
                pointerId.Id,
                node =>
                {
                    node.IsSelected = true;
                    node.IsEditMode = false;
                }
            )
        )
        {
            selectedId = pointerId;
            this.isEditMode = false;
            if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(pointerId.ElementId)))
                return;
            SelectedNodePosition = new Pos(bound.X, bound.Y);
            applicationEvents.TriggerUIStateChanged();
        }

        if (!IsNodeMovable(modelService.Zoom))
        {
            Unselect();
            SelectedNodePosition = Pos.None;
            applicationEvents.TriggerUIStateChanged();
            return;
        }

        if (IsSelected)
        {
            if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(pointerId.ElementId)))
                return;
            SelectedNodePosition = new Pos(bound.X, bound.Y);
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected)
            return;

        modelService.UseNode(
            selectedId.Id,
            node =>
            {
                node.IsSelected = false;
                node.IsEditMode = false;
            }
        );
        selectedId = PointerId.Empty;
        this.isEditMode = false;
        applicationEvents.TriggerUIStateChanged();
    }

    // Returns true if the node is movable, i.e. the node is not too large (to zoomed in) for the current screen.
    public bool IsNodeMovable(double zoom)
    {
        if (!IsSelected || IsEditMode)
            return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node))
            return false;

        var v = screenService.SvgRect;
        var nodeZoom = 1 / node.GetZoom();
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }
}
