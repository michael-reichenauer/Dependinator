using Dependinator.Shared;
using Microsoft.JSInterop;


namespace Dependinator.Utils.UI;

interface IMouseEventService
{

    event Action<MouseEvent> MouseWheel;
    event Action<MouseEvent> MouseMove;
    event Action<MouseEvent> MouseDown;
    event Action<MouseEvent> MouseUp;
    event Action<MouseEvent> LeftClick;
    event Action<MouseEvent> LeftDblClick;

    Task InitAsync();
}


[Scoped]
class MouseEventService : IMouseEventService
{
    readonly IJSInterop jSInterop;
    private readonly IApplicationEvents applicationEvents;
    const int ClickDelay = 300;
    const int ClickTimeout = 500;

    readonly Timer clickTimer;
    bool timerRunning = false;
    MouseEvent leftMouseDown = new();
    DateTime leftMouseDownTime = DateTime.MinValue;

    Dictionary<int, MouseEvent> activePointers = [];


    public MouseEventService(
        IJSInterop jSInterop,
        IApplicationEvents applicationEvents)
    {
        clickTimer = new Timer(OnLeftClickTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.jSInterop = jSInterop;
        this.applicationEvents = applicationEvents;
    }

    public event Action<MouseEvent> MouseWheel = null!;
    public event Action<MouseEvent> MouseMove = null!;
    public event Action<MouseEvent> MouseDown = null!;
    public event Action<MouseEvent> MouseUp = null!;
    public event Action<MouseEvent> LeftClick = null!;
    public event Action<MouseEvent> LeftDblClick = null!;

    public async Task InitAsync()
    {
        await jSInterop.Call("preventDefaultTouchEvents", "svgcanvas");

        var objRef = jSInterop.Reference(this);
        await jSInterop.Call("addMouseEventListener", "svgcanvas", "wheel", objRef, nameof(MouseEventCallback));
        await jSInterop.Call("addPointerEventListener", "svgcanvas", "pointerdown", objRef, nameof(PointerEventCallback));
        await jSInterop.Call("addPointerEventListener", "svgcanvas", "pointermove", objRef, nameof(PointerEventCallback));
        await jSInterop.Call("addPointerEventListener", "svgcanvas", "pointerup", objRef, nameof(PointerEventCallback));
        await jSInterop.Call("addPointerEventListener", "svgcanvas", "pointercancel", objRef, nameof(PointerEventCallback));
    }


    [JSInvokable]
    public ValueTask MouseEventCallback(MouseEvent e)
    {
        // Log.Info($"MouseEventCallback: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "wheel": OnMouseWheelEvent(e); break;
        }

        applicationEvents.TriggerUIStateChanged();
        return ValueTask.CompletedTask;
    }


    [JSInvokable]
    public ValueTask PointerEventCallback(MouseEvent e)
    {
        // Log.Info($"PointerEventCallback: {e.Time} {e.Type} {e.PointerId} {e.PointerType} on {e.TargetId}'");
        switch (e.Type)
        {
            case "pointerdown": OnPointerDownEvent(e); break;
            case "pointermove": OnPointerMoveEvent(e); break;
            case "pointerup": OnPoinerUpEvent(e); break;
            case "pointercancel": OnPoinerUpEvent(e); break;
        }

        applicationEvents.TriggerUIStateChanged();
        return ValueTask.CompletedTask;
    }

    void OnMouseWheelEvent(MouseEvent e) => MouseWheel?.Invoke(e);

    void OnPointerDownEvent(MouseEvent e)
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

        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            leftMouseDownTime = DateTime.UtcNow;
            leftMouseDown = e;
        }
    }

    void OnPointerMoveEvent(MouseEvent e)
    {
        // Log.Info("Pointermove", pointerDowns.Count, e.Button, e.Type);

        if (activePointers.Count == 1)
        {
            MouseMove?.Invoke(e);
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

            var weelEvent = e with { Type = "wheel", DeltaY = delta, OffsetX = centerX, OffsetY = centerY };

            MouseWheel?.Invoke(weelEvent);
        }
    }


    void OnPoinerUpEvent(MouseEvent e)
    {
        activePointers.Remove(e.PointerId);

        MouseUp?.Invoke(e);

        if (e.Button == 0 &&
            Math.Abs(e.OffsetX - leftMouseDown.OffsetX) < 5 &&
            Math.Abs(e.OffsetY - leftMouseDown.OffsetY) < 5
            && (DateTime.UtcNow - leftMouseDownTime).TotalMilliseconds < ClickTimeout)
        {
            Log.Info("on click");
            OnLeftClickEvent(e);
        }
    }

    void OnLeftClickTimer(object? state)
    {
        timerRunning = false;
    }

    void OnLeftClickEvent(MouseEvent e)
    {
        leftMouseDown = e;
        if (!timerRunning)
        {   // This is the first click, start the timer
            timerRunning = true;
            LeftClick?.Invoke(leftMouseDown);
            clickTimer.Change(ClickDelay, Timeout.Infinite);
        }
        else
        {
            clickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            timerRunning = false;
            LeftDblClick?.Invoke(e);
            activePointers.Clear();
        }
    }
}
