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
    readonly IJSInteropService jSInteropService;
    private readonly IUIService uiService;
    const int ClickDelay = 300;
    const int ClickTimeout = 500;

    readonly Timer clickTimer;
    bool timerRunning = false;
    MouseEvent leftMouseDown = new();
    DateTime leftMouseDownTime = DateTime.MinValue;

    Dictionary<int, MouseEvent> activePointers = [];


    public MouseEventService(
        IJSInteropService jSInteropService,
        IUIService uiService)
    {
        clickTimer = new Timer(OnLeftClickTimer, null, Timeout.Infinite, Timeout.Infinite);
        this.jSInteropService = jSInteropService;
        this.uiService = uiService;
    }

    public event Action<MouseEvent> MouseWheel = null!;
    public event Action<MouseEvent> MouseMove = null!;
    public event Action<MouseEvent> MouseDown = null!;
    public event Action<MouseEvent> MouseUp = null!;
    public event Action<MouseEvent> LeftClick = null!;
    public event Action<MouseEvent> LeftDblClick = null!;

    public async Task InitAsync()
    {
        var objRef = DotNetObjectReference.Create(this);
        await jSInteropService.AddMouseEventListener("svgcanvas", "wheel", objRef, "MouseEventCallback");

        await jSInteropService.AddPointerEventListener("svgcanvas", "pointerdown", objRef, "PointerEventCallback");
        await jSInteropService.AddPointerEventListener("svgcanvas", "pointermove", objRef, "PointerEventCallback");
        await jSInteropService.AddPointerEventListener("svgcanvas", "pointerup", objRef, "PointerEventCallback");
        await jSInteropService.AddPointerEventListener("svgcanvas", "pointercancel", objRef, "PointerEventCallback");
    }


    [JSInvokable]
    public ValueTask MouseEventCallback(MouseEvent e)
    {
        // Log.Info($"MouseEventCallback: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "wheel": OnMouseWheelEvent(e); break;
        }

        uiService.TriggerUIStateChange();
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

        uiService.TriggerUIStateChange();
        return ValueTask.CompletedTask;
    }

    void OnMouseWheelEvent(MouseEvent e) => MouseWheel?.Invoke(e);

    void OnPointerDownEvent(MouseEvent e)
    {
        activePointers[e.PointerId] = e;
        Log.Info("Pointerdown", activePointers.Count, e.PointerType);
        if (activePointers.Count > 2)
        {
            var oldest = activePointers.Values.MinBy(e => e.Time);
            activePointers.Remove(oldest!.PointerId);
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

            if (p1.Time - p2.Time > 1000)
            {   // Seems to be a bug on IOS where pointer sometiems is lost
                var oldest = activePointers.Values.MinBy(e => e.Time);
                activePointers.Remove(oldest!.PointerId);
                return;
            }

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
