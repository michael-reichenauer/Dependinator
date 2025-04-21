using Dependinator.Diagrams;
using Dependinator.Models;

namespace Dependinator;

interface ISelectionService
{
    bool IsSelected { get; }
    bool IsEditMode { get; }
    PointerId SelectedId { get; }
    Pos SelectedPosition { get; }

    Task UpdateSelectedPositionAsync();
    bool IsSelectedNodeMovable(double zoom);
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

    public Pos SelectedPosition
    {
        get => IsSelected ? selectedNodePosition : Pos.None;
        private set => selectedNodePosition = value;
    }

    public async Task UpdateSelectedPositionAsync()
    {
        if (!IsSelected)
            return;

        var id = SelectedId.ElementId;
        if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(id)))
        {
            // Selected Element is not visible on the screen
            if (selectedNodePosition != Pos.None)
            {
                Log.Info($"UpdateToolbar hide !!!");
                selectedNodePosition = Pos.None;
                applicationEvents.TriggerUIStateChanged();
            }
            return;
        }

        if (selectedNodePosition.X == bound.X && selectedNodePosition.Y == bound.Y)
            return;

        Log.Info($"UpdateToolbar {bound}");
        selectedNodePosition = new Pos(bound.X, bound.Y);
        applicationEvents.TriggerUIStateChanged();
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
            Unselect(); // Clicked on some other item or outside the diagram

        if (pointerId.IsNode)
        {
            var zoom = modelService.Zoom;

            if (
                modelService.UseNode(
                    pointerId.Id,
                    node =>
                    {
                        if (!IsNodeMovable(node, zoom))
                            return false;
                        node.IsSelected = true;
                        node.IsEditMode = false;
                        return true;
                    }
                )
            )
            {
                selectedId = pointerId;
                this.isEditMode = false;
                await UpdateSelectedPositionAsync();
            }
        }
        if (pointerId.IsLine)
        {
            modelService.UseLine(
                pointerId.Id,
                line =>
                {
                    line.IsSelected = true;
                    return true;
                }
            );
            selectedId = pointerId;
            this.isEditMode = false;
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected)
            return;

        if (selectedId.IsNode)
        {
            modelService.UseNode(
                selectedId.Id,
                node =>
                {
                    node.IsSelected = false;
                    node.IsEditMode = false;
                }
            );
        }
        if (selectedId.IsLine)
        {
            modelService.UseLine(
                selectedId.Id,
                line =>
                {
                    line.IsSelected = false;
                }
            );
        }
        selectedId = PointerId.Empty;
        this.isEditMode = false;
        applicationEvents.TriggerUIStateChanged();
    }

    // Returns true if the node is movable, i.e. the node is not too large (to zoomed in) for the current screen.
    public bool IsSelectedNodeMovable(double zoom)
    {
        if (!IsSelected || IsEditMode)
            return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node))
            return false;

        return IsNodeMovable(node, zoom);
    }

    private bool IsNodeMovable(Node node, double zoom)
    {
        var v = screenService.SvgRect;
        var nodeZoom = 1 / node.GetZoom();
        var vx = (node.Boundary.Width * nodeZoom) / (v.Width * zoom);
        var vy = (node.Boundary.Height * nodeZoom) / (v.Height * zoom);
        var maxCovers = Math.Max(vx, vy);
        var minCovers = Math.Min(vx, vy);

        return minCovers < MinCover && maxCovers < MaxCover;
    }
}
