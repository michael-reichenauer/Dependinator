using Microsoft.JSInterop;

namespace Dependinator.Shared;

interface IPointerEventService
{
    event Action<PointerEvent> Wheel;
    event Action<PointerEvent> PointerMove;
    event Action<PointerEvent> PointerDown;
    event Action<PointerEvent> PointerUp;
    event Action<PointerEvent> Click;
    event Action<PointerEvent> DblClick;

    Task InitAsync();
}

[Scoped]
class PointerEventService : IPointerEventService
{
    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInterop;

    const int ClickDelay = 300;
    const int ClickTimeout = 500;

    readonly Timer clickTimer;
    readonly Dictionary<int, PointerEvent> activePointers = [];
    bool timerRunning = false;
    PointerEvent pointerDown = new();
    DateTime pointerDownTime = DateTime.MinValue;

    public PointerEventService(IJSInterop jSInterop, IApplicationEvents applicationEvents)
    {
        clickTimer = new Timer(OnLeftClickTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.jSInterop = jSInterop;
        this.applicationEvents = applicationEvents;
    }

    public event Action<PointerEvent> Wheel = null!;
    public event Action<PointerEvent> PointerMove = null!;
    public event Action<PointerEvent> PointerDown = null!;
    public event Action<PointerEvent> PointerUp = null!;
    public event Action<PointerEvent> Click = null!;
    public event Action<PointerEvent> DblClick = null!;

    public async Task InitAsync()
    {
        await jSInterop.Call("preventDefaultTouchEvents", "svgcanvas");

        var objRef = jSInterop.Reference(this);
        await jSInterop.Call("addMouseEventListener", "svgcanvas", "wheel", objRef, nameof(MouseEventCallback));
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

    [JSInvokable]
    public ValueTask MouseEventCallback(PointerEvent e)
    {
        // Log.Info($"MouseEventCallback: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "wheel":
                OnMouseWheelEvent(e);
                break;
        }

        applicationEvents.TriggerUIStateChanged();
        return ValueTask.CompletedTask;
    }

    [JSInvokable]
    public ValueTask PointerEventCallback(PointerEvent e)
    {
        // Log.Info($"PointerEventCallback: {e.Time} {e.Type} {e.PointerId} {e.PointerType} on {e.TargetId}'");
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

        applicationEvents.TriggerUIStateChanged();
        return ValueTask.CompletedTask;
    }

    void OnMouseWheelEvent(PointerEvent e) => Wheel?.Invoke(e);

    void OnPointerDownEvent(PointerEvent e)
    {
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
        // Log.Info("Pointermove", pointerDowns.Count, e.Button, e.Type);

        if (activePointers.Count == 1)
        {
            PointerMove?.Invoke(e);
        }
        else if (activePointers.Count == 2)
        {
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

        if (
            e.Button == 0
            && Math.Abs(e.OffsetX - pointerDown.OffsetX) < 5
            && Math.Abs(e.OffsetY - pointerDown.OffsetY) < 5
            && (DateTime.UtcNow - pointerDownTime).TotalMilliseconds < ClickTimeout
        )
        {
            // Log.Info("on click");
            OnLeftClickEvent(e);
        }
    }

    void OnLeftClickTimer(object? state)
    {
        timerRunning = false;
    }

    void OnLeftClickEvent(PointerEvent e)
    {
        pointerDown = e;
        if (!timerRunning)
        { // This is the first click, start the timer
            timerRunning = true;
            Click?.Invoke(pointerDown);
            clickTimer.Change(ClickDelay, Timeout.Infinite);
        }
        else
        {
            clickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            timerRunning = false;
            DblClick?.Invoke(e);
            activePointers.Clear();
        }
    }
}
