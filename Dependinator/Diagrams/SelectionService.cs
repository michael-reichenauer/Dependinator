﻿using Dependinator.Diagrams;
using Dependinator.Models;

namespace Dependinator;


interface ISelectionService
{
    bool IsSelected { get; }
    bool IsEditMode { get; }
    PointerId SelectedId { get; }

    bool IsNodeMovable(double zoom);
    void Select(PointerId pointerId);
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

    PointerId selectedId = PointerId.Empty;
    bool isEditMode = false;

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

    public bool IsEditMode => isEditMode;


    public void SetEditMode(bool isEditMode)
    {
        if (!IsSelected) return;

        modelService.UseNode(selectedId.Id, node =>
        {
            node.IsEditMode = isEditMode;
        });
        this.isEditMode = isEditMode;
        applicationEvents.TriggerUIStateChanged();
    }


    public void Select(PointerId pointerId)
    {
        if (IsSelected && selectedId.Id == pointerId.Id) return;

        if (IsSelected) Unselect(); // Clicked on some other node

        if (modelService.UseNode(pointerId.Id, node =>
        {
            node.IsSelected = true;
            node.IsEditMode = false;
        }))
        {
            selectedId = pointerId;
            this.isEditMode = false;
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected) return;

        modelService.UseNode(selectedId.Id, node =>
        {
            node.IsSelected = false;
            node.IsEditMode = false;
        });
        selectedId = PointerId.Empty;
        this.isEditMode = false;
        applicationEvents.TriggerUIStateChanged();
    }

    public bool IsNodeMovable(double zoom)
    {
        if (!IsSelected || IsEditMode) return false;
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
