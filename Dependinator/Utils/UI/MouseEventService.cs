using Microsoft.AspNetCore.Components.Web;
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
    HammerEvent panLatest = new();
    HammerEvent pinchLatest = new();

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
        // await jSInteropService.AddMouseEventListener("svgcanvas", "mousemove", objRef, "MouseEventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mousedown", objRef, "MouseEventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mouseup", objRef, "MouseEventCallback");

        await jSInteropService.AddHammerListener("svgcanvas", objRef, "HammerEventCallback");
    }


    [JSInvokable]
    public ValueTask MouseEventCallback(MouseEvent e)
    {
        // Log.Info($"MouseEventCallback: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "wheel": OnMouseWheelEvent(e); break;
            case "mousedown": OnMouseDownEvent(e); break;
            case "mousemove": OnMouseMoveEvent(e); break;
            case "mouseup": OnMouseUpEvent(e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }

        uiService.TriggerUIStateChange();
        return ValueTask.CompletedTask;
    }

    // [JSInvokable]
    // public ValueTask TouchEventCallback(TouchEvent e)
    // {
    //     Log.Info("TouchEventCallback", e.Type, e.TargetId, e.Touches.Length);

    //     // // Log.Info($"Clicked: '{e.ToJson()}'");
    //     switch (e.Type)
    //     {
    //         case "touchstart": OnTouchDownEvent(e); break;
    //         case "touchmove": OnTouchMoveEvent(e); break;
    //         case "touchend": OnTouchEndEvent(e); break;
    //         // case "touchenter": Log.Info("", e); break;
    //         // case "touchleave": Log.Info("", e); break;
    //         // case "touchcancel": Log.Info("", e); break;
    //         default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
    //     }

    //     uiService.TriggerUIStateChange();
    //     return ValueTask.CompletedTask;
    // }

    [JSInvokable]
    public ValueTask HammerEventCallback(HammerEvent e)
    {
        // Log.Info("HammerEventCallback", e.Type);

        // // Log.Info($"Clicked: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "panstart": OnPanStartEvent(e); break;
            case "panmove": OnPanMoveEvent(e); break;
            case "panend": OnPanEndEvent(e); break;
            case "pinchmove": OnPinchMoveEvent(e); break;
            case "pinchstart": OnPinchStartEvent(e); break;
            // case "touchleave": Log.Info("", e); break;
            // case "touchcancel": Log.Info("", e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }

        uiService.TriggerUIStateChange();
        return ValueTask.CompletedTask;
    }

    private void OnPinchStartEvent(HammerEvent e)
    {
        pinchLatest = e;
        // Log.Info($"OnPinchStartEvent {e.Center}");
    }

    private void OnPinchMoveEvent(HammerEvent e)
    {
        var distance = Math.Sqrt(
         Math.Pow(e.Pointers[0].X - e.Pointers[1].X, 2) +
         Math.Pow(e.Pointers[0].Y - e.Pointers[1].Y, 2));

        var distanceLatest = Math.Sqrt(
             Math.Pow(pinchLatest.Pointers[0].X - pinchLatest.Pointers[1].X, 2) +
             Math.Pow(pinchLatest.Pointers[0].Y - pinchLatest.Pointers[1].Y, 2));

        var deltaY = (distanceLatest - distance);

        var mouseEvent = new MouseEvent
        {
            Type = "pinch",
            TargetId = e.TargetId,
            OffsetX = e.Center.X,
            OffsetY = e.Center.Y,
            ClientX = 0,
            ClientY = 0,
            ScreenX = 0,
            ScreenY = 0,
            PageX = 0,
            PageY = 0,
            MovementX = 0,
            MovementY = 0,
            Button = 0,
            Buttons = 1,
            ShiftKey = false,
            CtrlKey = false,
            AltKey = false,
            DeltaX = 0,
            DeltaY = deltaY,
            DeltaZ = 0,
            DeltaMode = 0,
        };
        pinchLatest = e;

        // Log.Info($"OnPinchMoveEvent {e.Center} [{distance}] ({e.Pointers[0]}) ({e.Pointers[1]})");
        OnMouseWheelEvent(mouseEvent);
    }

    void OnPanStartEvent(HammerEvent e)
    {
        // Log.Info("OnPanStartEvent", e.Type, e.TargetId);
        if (e.Pointers.Length != 1) return;

        var mouseEvent = new MouseEvent
        {
            Type = "mousedown",
            TargetId = e.TargetId,
            OffsetX = e.Pointers[0].X,
            OffsetY = e.Pointers[0].Y,
            ClientX = 0,
            ClientY = 0,
            ScreenX = 0,
            ScreenY = 0,
            PageX = 0,
            PageY = 0,
            MovementX = 0,
            MovementY = 0,
            Button = 0,
            Buttons = 1,
            ShiftKey = false,
            CtrlKey = false,
            AltKey = false,
            DeltaX = 0,
            DeltaY = 0,
            DeltaZ = 0,
            DeltaMode = 0,
        };
        panLatest = e;

        OnMouseDownEvent(mouseEvent);
    }

    void OnPanMoveEvent(HammerEvent e)
    {
        // Log.Info("OnPanMoveEvent", e);
        if (e.Pointers.Length != 1) return;

        var mouseEvent = new MouseEvent
        {
            Type = "mousemove",
            TargetId = e.TargetId,
            OffsetX = e.Pointers[0].X,
            OffsetY = e.Pointers[0].Y,
            ClientX = 0,
            ClientY = 0,
            ScreenX = 0,
            ScreenY = 0,
            PageX = 0,
            PageY = 0,
            MovementX = e.Pointers[0].X - panLatest.Pointers[0].X,
            MovementY = e.Pointers[0].Y - panLatest.Pointers[0].Y,
            Button = 0,
            Buttons = 1,
            ShiftKey = false,
            CtrlKey = false,
            AltKey = false,
            DeltaX = 0,
            DeltaY = 0,
            DeltaZ = 0,
            DeltaMode = 0,
        };
        panLatest = e;

        OnMouseMoveEvent(mouseEvent);
    }

    void OnPanEndEvent(HammerEvent e)
    {
        // Log.Info("OnPanEndEvent", e.Type);

        var mouseEvent = new MouseEvent
        {
            Type = "mouseup",
            TargetId = e.TargetId,
            OffsetX = e.Center.X,
            OffsetY = e.Center.Y,
            ClientX = 0,
            ClientY = 0,
            ScreenX = 0,
            ScreenY = 0,
            PageX = 0,
            PageY = 0,
            MovementX = 0,
            MovementY = 0,
            Button = 0,
            Buttons = 1,
            ShiftKey = false,
            CtrlKey = false,
            AltKey = false,
            DeltaX = 0,
            DeltaY = 0,
            DeltaZ = 0,
            DeltaMode = 0,
        };
        panLatest = e;

        OnMouseUpEvent(mouseEvent);
    }

    void OnMouseWheelEvent(MouseEvent e) => MouseWheel?.Invoke(e);

    void OnMouseDownEvent(MouseEvent e)
    {
        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            leftMouseDownTime = DateTime.UtcNow;
            leftMouseDown = e;
        }
    }

    void OnMouseMoveEvent(MouseEvent e)
    {
        MouseMove?.Invoke(e);
    }

    void OnMouseUpEvent(MouseEvent e)
    {
        MouseUp?.Invoke(e);

        if (e.Button == 0 &&
            Math.Abs(e.OffsetX - leftMouseDown.OffsetX) < 5 &&
            Math.Abs(e.OffsetY - leftMouseDown.OffsetY) < 5
            && (DateTime.UtcNow - leftMouseDownTime).TotalMilliseconds < ClickTimeout)
        {
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
        }
    }
}
