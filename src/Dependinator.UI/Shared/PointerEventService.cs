using Microsoft.JSInterop;

namespace Dependinator.UI.Shared;

interface IPointerEventService
{
    event Action<PointerEvent>? Wheel;
    event Action<PointerEvent>? PointerMove;
    event Action<PointerEvent>? PointerDown;
    event Action<PointerEvent>? PointerUp;
    event Action<PointerEvent>? Click;
    event Action<PointerEvent>? DblClick;
    event Action<PointerEvent>? ContextMenu;

    Task InitAsync();
}

[Scoped]
class PointerEventService : IPointerEventService, IDisposable
{
    readonly IJSInterop jSInterop;

    const int ClickDelay = 300;
    const int ClickTimeout = 500;

    // Max distance between two clicks to count as a double-click; a second click further
    // away starts a new click sequence instead. Touch double-taps jitter more than mouse.
    const double DblClickMaxDistance = 10;
    const double DblClickMaxTouchDistance = 25;

    readonly Timer clickTimer;
    readonly Dictionary<int, PointerEvent> activePointers = [];
    bool timerRunning = false;
    PointerEvent pointerDown = new();
    PointerEvent firstClick = new();
    DateTime pointerDownTime = DateTime.MinValue;
    DotNetObjectReference<PointerEventService>? reference;

    public PointerEventService(IJSInterop jSInterop)
    {
        clickTimer = new Timer(OnLeftClickTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.jSInterop = jSInterop;
    }

    public event Action<PointerEvent>? Wheel;
    public event Action<PointerEvent>? PointerMove;
    public event Action<PointerEvent>? PointerDown;
    public event Action<PointerEvent>? PointerUp;
    public event Action<PointerEvent>? Click;
    public event Action<PointerEvent>? DblClick;
    public event Action<PointerEvent>? ContextMenu;

    public async Task InitAsync()
    {
        await jSInterop.Call("preventDefaultTouchEvents", "svgcanvas");

        reference ??= jSInterop.Reference(this);
        var objRef = reference;
        await jSInterop.Call("addMouseEventListener", "svgcanvas", "wheel", objRef, nameof(MouseEventCallback));
        await jSInterop.Call("addMouseEventListener", "svgcanvas", "contextmenu", objRef, nameof(MouseEventCallback));
        await jSInterop.Call(
            "addPointerEventListener",
            "svgcanvas",
            "pointerdown",
            objRef,
            nameof(PointerEventCallback)
        );
        await jSInterop.Call(
            "addPointerEventListener",
            "svgcanvas",
            "pointermove",
            objRef,
            nameof(PointerEventCallback)
        );
        await jSInterop.Call("addPointerEventListener", "svgcanvas", "pointerup", objRef, nameof(PointerEventCallback));
        await jSInterop.Call(
            "addPointerEventListener",
            "svgcanvas",
            "pointercancel",
            objRef,
            nameof(PointerEventCallback)
        );
    }

    public void Dispose()
    {
        clickTimer.Dispose();
        reference?.Dispose();
    }

    [JSInvokable]
    public ValueTask MouseEventCallback(PointerEvent e)
    {
        switch (e.Type)
        {
            case "wheel":
                OnMouseWheelEvent(e);
                break;
            case "contextmenu":
                ContextMenu?.Invoke(e);
                break;
        }

        return ValueTask.CompletedTask;
    }

    [JSInvokable]
    public ValueTask PointerEventCallback(PointerEvent e)
    {
        switch (e.Type)
        {
            case "pointerdown":
                OnPointerDownEvent(e);
                break;
            case "pointermove":
                OnPointerMoveEvent(e);
                break;
            case "pointerup":
                OnPointerUpEvent(e);
                break;
            case "pointercancel":
                OnPointerUpEvent(e);
                break;
        }

        return ValueTask.CompletedTask;
    }

    void OnMouseWheelEvent(PointerEvent e) => Wheel?.Invoke(e);

    void OnPointerDownEvent(PointerEvent e)
    {
        // A single stale pointer (e.g. a touch that never got a pointerup) would make every
        // new touch look like a two-finger gesture; treat a pointer older than a second as stale.
        if (activePointers.Count == 1 && e.PointerId != activePointers.Keys.First())
        {
            var p1 = activePointers.Values.First();
            if (e.Time - p1.Time > 1000)
            {
                activePointers.Clear();
            }
        }

        activePointers[e.PointerId] = e;

        if (activePointers.Count > 2)
        {
            activePointers.Clear();
            activePointers[e.PointerId] = e;
        }

        PointerDown?.Invoke(e);

        if (e.Button == 0)
        {
            pointerDownTime = DateTime.UtcNow;
            pointerDown = e;
        }
    }

    void OnPointerMoveEvent(PointerEvent e)
    {
        if (activePointers.Count == 1)
        {
            PointerMove?.Invoke(e);
        }
        else if (activePointers.Count == 2)
        {
            // Two-finger pinch: convert the change in distance between the two pointers into
            // a synthetic wheel event (zoom) centered between them, so pinch and mouse wheel
            // share the same zoom handling downstream.
            var p1 = activePointers.Values.ElementAt(0);
            var p2 = activePointers.Values.ElementAt(1);

            var dx = p1.OffsetX - p2.OffsetX;
            var dy = p1.OffsetY - p2.OffsetY;
            var previousDistance = Math.Sqrt(dx * dx + dy * dy);

            activePointers[e.PointerId] = e;
            p1 = activePointers.Values.ElementAt(0);
            p2 = activePointers.Values.ElementAt(1);

            dx = p1.OffsetX - p2.OffsetX;
            dy = p1.OffsetY - p2.OffsetY;
            var currentDistance = Math.Sqrt(dx * dx + dy * dy);

            var delta = previousDistance - currentDistance;

            var centerX = (p1.OffsetX + p2.OffsetX) / 2;
            var centerY = (p1.OffsetY + p2.OffsetY) / 2;

            var wheelEvent = e with { Type = "wheel", DeltaY = delta, OffsetX = centerX, OffsetY = centerY };

            Wheel?.Invoke(wheelEvent);
        }
    }

    void OnPointerUpEvent(PointerEvent e)
    {
        activePointers.Remove(e.PointerId);

        PointerUp?.Invoke(e);

        // Treat as a click if the left button was released close to where it was pressed,
        // within the click timeout (i.e. not a drag or long press).
        if (
            e.Button == 0
            && Math.Abs(e.OffsetX - pointerDown.OffsetX) < 5
            && Math.Abs(e.OffsetY - pointerDown.OffsetY) < 5
            && (DateTime.UtcNow - pointerDownTime).TotalMilliseconds < ClickTimeout
        )
        {
            OnLeftClickEvent(e);
        }
    }

    void OnLeftClickTimer(object? state)
    {
        timerRunning = false;
    }

    // Click/double-click detection: Click fires immediately on the first click and a timer
    // starts; a second click near the first one before the timer expires also fires DblClick
    // (Click is not suppressed or delayed while waiting). A second click further away is a
    // new first click — two quick clicks on different elements (e.g. a node and then a
    // toolbar button) must not merge into a double-click.
    void OnLeftClickEvent(PointerEvent e)
    {
        pointerDown = e;
        if (timerRunning && IsNearFirstClick(e))
        {
            clickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            timerRunning = false;
            DblClick?.Invoke(e);
            activePointers.Clear();
        }
        else
        { // This is a first click, start the timer
            firstClick = e;
            timerRunning = true;
            Click?.Invoke(e);
            clickTimer.Change(ClickDelay, Timeout.Infinite);
        }
    }

    bool IsNearFirstClick(PointerEvent e)
    {
        // Client (viewport) coordinates: offsets are relative to the event target, which can
        // be a different element for each of the two clicks.
        var maxDistance = e.PointerType == "touch" ? DblClickMaxTouchDistance : DblClickMaxDistance;
        return Math.Abs(e.ClientX - firstClick.ClientX) < maxDistance
            && Math.Abs(e.ClientY - firstClick.ClientY) < maxDistance;
    }
}
