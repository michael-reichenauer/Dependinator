using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Rubber-band area selection: armed (e.g. from the app menu), the next left-drag on the canvas
// draws a selection rectangle (the Canvas renders the overlay while IsSelecting is true) and
// resolves to the selected rectangle in canvas coordinates. Used by image export; designed to
// be reusable for future group operations (move/recolor the nodes within a rectangle).
interface IAreaSelectionService
{
    // Armed: crosshair mode, waiting for the press. Selecting: dragging, overlay visible.
    bool IsArmed { get; }
    bool IsSelecting { get; }

    // Drag start/end in viewport (client) coords, the coordinate space of the fixed overlay.
    Pos StartClient { get; }
    Pos EndClient { get; }
    event Action? StateChanged;

    // Arms the mode; the returned task resolves with the selected rectangle in canvas
    // coordinates, or null if canceled (Escape, right-click, a too-small drag, or re-arming).
    Task<Rect?> SelectAreaAsync();
    void Cancel();

    // Called only by the InteractionService gesture routing.
    void PointerDown(PointerEvent e);
    void PointerMove(PointerEvent e);
    Task PointerUpAsync(PointerEvent e);
}

[Scoped]
class AreaSelectionService(IModelMgr modelMgr, IScreenService screenService, IApplicationEvents applicationEvents)
    : IAreaSelectionService
{
    // A drag smaller than this (in client px) is treated as an accidental click and cancels;
    // matches the click-vs-drag threshold in PointerEventService.
    const double MinDragSize = 5;

    TaskCompletionSource<Rect?>? pending;

    public bool IsArmed { get; private set; }
    public bool IsSelecting { get; private set; }
    public Pos StartClient { get; private set; } = Pos.None;
    public Pos EndClient { get; private set; } = Pos.None;
    public event Action? StateChanged;

    public Task<Rect?> SelectAreaAsync()
    {
        Cancel(); // A previous armed/active selection is superseded (its task resolves null).
        pending = new TaskCompletionSource<Rect?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IsArmed = true;
        OnStateChanged();
        return pending.Task;
    }

    public void Cancel()
    {
        if (!IsArmed && !IsSelecting)
            return;
        Complete(null);
    }

    public void PointerDown(PointerEvent e)
    {
        if (!IsArmed)
            return;
        IsSelecting = true;
        StartClient = new Pos(e.ClientX, e.ClientY);
        EndClient = StartClient;
        OnStateChanged();
    }

    // Only the render trigger per move (no StateChanged): toolbars and other subscribers do
    // not need per-move notifications, but the canvas overlay must follow the drag.
    public void PointerMove(PointerEvent e)
    {
        if (!IsSelecting)
            return;
        EndClient = new Pos(e.ClientX, e.ClientY);
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task PointerUpAsync(PointerEvent e)
    {
        if (!IsSelecting)
        {
            Cancel();
            return;
        }

        EndClient = new Pos(e.ClientX, e.ClientY);
        var width = Math.Abs(EndClient.X - StartClient.X);
        var height = Math.Abs(EndClient.Y - StartClient.Y);
        if (e.Type == "pointercancel" || width < MinDragSize || height < MinDragSize)
        {
            Complete(null);
            return;
        }

        // Client → canvas: subtract the svg element's viewport origin, then apply zoom/offset.
        // ClientX/Y is used (not OffsetX/Y) since pointer capture retargets offsets during drags.
        if (!Try(out var svgBound, out var _, await screenService.GetBoundingRectangle(PointerId.CanvasElementId)))
        {
            Complete(null);
            return;
        }

        var (zoom, offset) = modelMgr.WithModel(m => (m.Zoom, m.Offset));
        var x = Math.Min(StartClient.X, EndClient.X) - svgBound.X;
        var y = Math.Min(StartClient.Y, EndClient.Y) - svgBound.Y;
        var canvasRect = new Rect(offset.X + x * zoom, offset.Y + y * zoom, width * zoom, height * zoom);
        Complete(canvasRect);
    }

    void Complete(Rect? result)
    {
        IsArmed = false;
        IsSelecting = false;
        StartClient = Pos.None;
        EndClient = Pos.None;
        var completed = pending;
        pending = null;
        OnStateChanged();
        completed?.TrySetResult(result);
    }

    void OnStateChanged()
    {
        StateChanged?.Invoke();
        applicationEvents.TriggerUIStateChanged();
    }
}
