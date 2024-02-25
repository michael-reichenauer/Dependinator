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

    readonly Timer clickTimer;
    bool timerRunning = false;
    MouseEvent leftMouseDown = new();
    MouseEvent leftMouseLatest = new();

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
        await jSInteropService.AddMouseEventListener("svgcanvas", "mousemove", objRef, "MouseEventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mousedown", objRef, "MouseEventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mouseup", objRef, "MouseEventCallback");

        await jSInteropService.AddTouchEventListener("svgcanvas", "touchstart", objRef, "TouchEventCallback");
        await jSInteropService.AddTouchEventListener("svgcanvas", "touchmove", objRef, "TouchEventCallback");
        await jSInteropService.AddTouchEventListener("svgcanvas", "touchend", objRef, "TouchEventCallback");
        // await jSInteropService.AddTouchEventListener("svgcanvas", "touchenter", objRef, "TouchEventCallback");
        // await jSInteropService.AddTouchEventListener("svgcanvas", "touchleave", objRef, "TouchEventCallback");
        // await jSInteropService.AddTouchEventListener("svgcanvas", "touchcancel", objRef, "TouchEventCallback");
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

    [JSInvokable]
    public ValueTask TouchEventCallback(TouchEvent e)
    {
        // Log.Info("TouchEventCallback", e.Type, e.TargetId, e.Touches.Length);

        // // Log.Info($"Clicked: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "touchstart": OnTouchDownEvent(e); break;
            case "touchmove": OnTouchMoveEvent(e); break;
            case "touchend": OnTouchEndEvent(e); break;
            // case "touchenter": Log.Info("", e); break;
            // case "touchleave": Log.Info("", e); break;
            // case "touchcancel": Log.Info("", e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }

        uiService.TriggerUIStateChange();
        return ValueTask.CompletedTask;
    }

    void OnTouchDownEvent(TouchEvent e)
    {
        if (e.Touches.Length == 1)
        {
            var mouseEvent = new MouseEvent
            {
                Type = "mouseup",
                TargetId = e.TargetId,
                OffsetX = e.Touches[0].ClientX,
                OffsetY = e.Touches[0].ClientY,
                ClientX = e.Touches[0].ClientX,
                ClientY = e.Touches[0].ClientY,
                ScreenX = e.Touches[0].ScreenX,
                ScreenY = e.Touches[0].ScreenY,
                PageX = e.Touches[0].PageX,
                PageY = e.Touches[0].PageY,
                MovementX = 0,
                MovementY = 0,
                Button = 0,
                Buttons = 1,
                ShiftKey = e.ShiftKey,
                CtrlKey = e.CtrlKey,
                AltKey = e.AltKey,
                DeltaX = 0,
                DeltaY = 0,
                DeltaZ = 0,
                DeltaMode = 0,
            };

            OnMouseDownEvent(mouseEvent);
        }
    }

    void OnTouchMoveEvent(TouchEvent e)
    {
        if (e.Touches.Length == 1)
        {
            var movementX = e.Touches[0].ClientX - leftMouseLatest.ClientX;
            var movementY = e.Touches[0].ClientY - leftMouseLatest.ClientY;
            // Log.Info("touchmove", movementX, movementY);

            var mouseEvent = new MouseEvent
            {
                Type = "mousemove",
                TargetId = leftMouseDown.TargetId,
                OffsetX = e.Touches[0].ClientX,
                OffsetY = e.Touches[0].ClientY,
                ClientX = e.Touches[0].ClientX,
                ClientY = e.Touches[0].ClientY,
                ScreenX = e.Touches[0].ScreenX,
                ScreenY = e.Touches[0].ScreenY,
                PageX = e.Touches[0].PageX,
                PageY = e.Touches[0].PageY,
                MovementX = movementX,
                MovementY = movementY,
                Button = 0,
                Buttons = 1,
                ShiftKey = e.ShiftKey,
                CtrlKey = e.CtrlKey,
                AltKey = e.AltKey,
                DeltaX = 0,
                DeltaY = 0,
                DeltaZ = 0,
                DeltaMode = 0,
            };

            OnMouseMoveEvent(mouseEvent);
        }
    }

    void OnTouchEndEvent(TouchEvent e)
    {
        if (e.Touches.Length == 0)
        {
            var mouseEvent = leftMouseLatest with { Type = "mouseup" };
            OnMouseUpEvent(mouseEvent);
        }
    }


    void OnMouseWheelEvent(MouseEvent e) => MouseWheel?.Invoke(e);



    void OnMouseDownEvent(MouseEvent e)
    {
        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            leftMouseDown = e;
            leftMouseLatest = e;
        }
    }

    void OnMouseMoveEvent(MouseEvent e)
    {
        leftMouseLatest = e;
        MouseMove?.Invoke(e);
    }

    void OnMouseUpEvent(MouseEvent e)
    {
        MouseUp?.Invoke(e);

        if (e.Button == 0 &&
            Math.Abs(e.OffsetX - leftMouseDown.OffsetX) < 5 &&
            Math.Abs(e.OffsetY - leftMouseDown.OffsetY) < 5)
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
