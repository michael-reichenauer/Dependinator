using Dependinator.Diagrams;
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

    Task InitAsync(IUIComponent component);
}


[Scoped]
class MouseEventService : IMouseEventService
{
    readonly IJSInteropService jSInteropService;
    private readonly IUIService uiService;
    const int ClickDelay = 300;

    IUIComponent component = null!;
    readonly Timer clickTimer;
    bool timerRunning = false;
    MouseEvent clickLeftMouse = new();

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

    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        var objRef = DotNetObjectReference.Create(this);
        await jSInteropService.AddMouseEventListener("svgcanvas", "wheel", objRef, "EventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mousemove", objRef, "EventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mousedown", objRef, "EventCallback");
        await jSInteropService.AddMouseEventListener("svgcanvas", "mouseup", objRef, "EventCallback");
    }


    [JSInvokable]
    public ValueTask EventCallback(MouseEvent e)
    {
        // Log.Info($"Clicked: '{e.ToJson()}'");
        switch (e.Type)
        {
            case "wheel": OnMouseWheelEvent(e); break;
            case "mousemove": OnMouseMoveEvent(e); break;
            case "mousedown": OnMouseDownEvent(e); break;
            case "mouseup": OnMouseUpEvent(e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }

        uiService.TriggerUIStateChange();
        return ValueTask.CompletedTask;
    }


    void OnMouseWheelEvent(MouseEvent e) => MouseWheel?.Invoke(e);

    void OnMouseMoveEvent(MouseEvent e) => MouseMove?.Invoke(e);


    void OnMouseDownEvent(MouseEvent e)
    {
        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            clickLeftMouse = e;
        }
    }

    void OnMouseUpEvent(MouseEvent e)
    {
        MouseUp?.Invoke(e);

        if (e.Button == 0 &&
            Math.Abs(e.OffsetX - clickLeftMouse.OffsetX) < 5 &&
            Math.Abs(e.OffsetY - clickLeftMouse.OffsetY) < 5)
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
        clickLeftMouse = e;
        if (!timerRunning)
        {   // This is the first click, start the timer
            timerRunning = true;
            LeftClick?.Invoke(clickLeftMouse);
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
