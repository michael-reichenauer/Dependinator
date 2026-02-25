using Dependinator.Models;

namespace Dependinator.Diagrams;

interface ISelectionService
{
    bool IsSelected { get; }
    bool IsEditMode { get; }
    PointerId SelectedId { get; }
    Pos SelectedPosition { get; }
    Pos SelectedNodePosition { get; }
    Pos SelectedLinePosition { get; }
    bool IsSelectedLineDirect { get; }

    Task UpdateSelectedPositionAsync();
    void HideSelectedPosition();
    bool IsSelectedNodeMovable(double zoom);
    bool IsSelectedNodeHidden();
    bool IsSelectedNodeParentHidden();
    void Select(PointerId pointerId, PointerEvent e);
    void Select(NodeId nodeId);
    void SetEditMode(bool isEditMode);
    void Unselect();
    void ToggleNodeHide();
}

[Scoped]
class SelectionService : ISelectionService
{
    const double toolbarOffsetX = 40;
    const double toolbarOffsetY = 20;
    const double MinCover = 0.5;
    const double MaxCover = 0.8;

    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;
    readonly IScreenService screenService;

    Pos selectedPosition = Pos.None;
    double clickedRelativePosition = 0.5;
    PointerId selectedId = PointerId.Empty;
    bool isEditMode = false;
    bool isSelectedLineDirect = false;

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

    public Pos SelectedPosition => IsSelected ? selectedPosition : Pos.None;
    public Pos SelectedNodePosition => selectedId.IsNode ? SelectedPosition : Pos.None;
    public Pos SelectedLinePosition => selectedId.IsLine ? SelectedPosition : Pos.None;
    public bool IsSelectedLineDirect => IsSelected && selectedId.IsLine && isSelectedLineDirect;

    public void HideSelectedPosition()
    {
        if (!IsSelected)
            return;

        selectedPosition = Models.Pos.None;
        return;
    }

    public async Task UpdateSelectedPositionAsync()
    {
        if (!IsSelected)
            return;

        var id = SelectedId.ElementId;
        if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(id)))
        {
            // Selected Element is not visible on the screen
            if (selectedPosition != Pos.None)
            {
                selectedPosition = Pos.None;
                applicationEvents.TriggerUIStateChanged();
            }
            return;
        }
        (double x, double y) = (bound.X, bound.Y);

        if (selectedId.IsLine)
        {
            // Calculate the clicked relative position on the line, to show the toolbar at the clicked position on the line
            x = bound.X + clickedRelativePosition * bound.Width - toolbarOffsetX;
            y = bound.Y + clickedRelativePosition * bound.Height - toolbarOffsetY;

            modelService.UseLine(
                selectedId.Id,
                line =>
                {
                    // For some lines based int its direction we need to flip the y coordinate
                    if (line.IsUpHill)
                        y = bound.Bottom - clickedRelativePosition * bound.Height - toolbarOffsetY;
                }
            );
        }

        if (selectedPosition.X == x && selectedPosition.Y == y)
            return;

        selectedPosition = new Pos(x, y);
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
        Select(PointerId.FromNode(nodeId), new PointerEvent());
    }

    public async void Select(PointerId pointerId, PointerEvent e)
    {
        if (IsSelected && selectedId.Id == pointerId.Id)
            return;

        if (IsSelected)
            Unselect(); // Clicked on some other item or outside the diagram
        else
            isSelectedLineDirect = false;

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
                isSelectedLineDirect = false;
                await UpdateSelectedPositionAsync();
            }
        }

        if (pointerId.IsLine)
        {
            Log.Info("Select line at", e);

            if (!Try(out var bound, out var _, await screenService.GetBoundingRectangle(pointerId.ElementId)))
            {
                Log.Info("Selected line is not visible on the screen");
                return;
            }
            Log.Info("Line bound", bound);

            var (x1, y1, x2, y2) = (bound.X, bound.Y, bound.Right, bound.Bottom);
            // var (x1, y1, x2, y2) = (bound.X, bound.Bottom, bound.Right, bound.Y);
            var (x, y) = (e.ClientX, e.ClientY);

            modelService.UseLine(
                pointerId.Id,
                line =>
                {
                    line.IsSelected = true;
                    isSelectedLineDirect = line.IsDirect;

                    // Calculate the clicked relative position on the line, this is used to
                    // show the toolbar at the clicked position on the line
                    if (line.IsUpHill)
                        (y1, y2) = (y2, y1);
                    var dx = x2 - x1;
                    var dy = y2 - y1;
                    var len2 = dx * dx + dy * dy;
                    var t = ((x - x1) * dx + (y - y1) * dy) / len2; // projection factor t ∈ [0,1]
                    t = Math.Max(0, Math.Min(1, t));
                    clickedRelativePosition = t;

                    return true;
                }
            );
            selectedId = pointerId;
            this.isEditMode = false;
            await UpdateSelectedPositionAsync();
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
        isSelectedLineDirect = false;
        applicationEvents.TriggerUIStateChanged();
    }

    // Returns true if the node is movable, i.e. the node is not too large (to zoomed in) for the current screen.
    public bool IsSelectedNodeMovable(double zoom)
    {
        if (!IsSelected || IsEditMode)
            return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node))
            return false;

        if (node.IsHidden && node.Parent.IsHidden)
            return false;
        return IsNodeMovable(node, zoom);
    }

    public bool IsSelectedNodeHidden()
    {
        if (!IsSelected)
            return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node))
            return false;
        return node.IsHidden;
    }

    public bool IsSelectedNodeParentHidden()
    {
        if (!IsSelected)
            return false;
        if (!modelService.TryGetNode(selectedId.Id, out var node))
            return false;
        return node.Parent.IsHidden;
    }

    public void ToggleNodeHide()
    {
        if (!IsSelected)
            return;
        modelService.UseNode(selectedId.Id, node => node.SetHidden(!node.IsHidden, true));
        modelService.CheckLineVisibility();
        applicationEvents.TriggerUIStateChanged();
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
