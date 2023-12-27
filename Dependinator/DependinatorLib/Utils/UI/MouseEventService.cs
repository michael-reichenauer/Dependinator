using Microsoft.AspNetCore.Components.Web;


namespace Dependinator.Utils.UI;

interface IMouseEventService
{
    event Action<WheelEventArgs> MouseWheel;
    event Action<MouseEventArgs> MouseMove;
    event Action<MouseEventArgs> MouseDown;
    event Action<MouseEventArgs> MouseUp;
    event Action<MouseEventArgs> LeftClick;
    event Action<MouseEventArgs> LeftDblClick;

    void OnMouse(MouseEventArgs e);
}


[Scoped]
class MouseEventService : IMouseEventService
{
    const int ClickDelay = 300;

    readonly Timer clickTimer;
    bool timerRunning = false;
    MouseEventArgs clickLeftMouse = new();

    public MouseEventService()
    {
        clickTimer = new Timer(OnLeftClickTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event Action<WheelEventArgs> MouseWheel = null!;
    public event Action<MouseEventArgs> MouseMove = null!;
    public event Action<MouseEventArgs> MouseDown = null!;
    public event Action<MouseEventArgs> MouseUp = null!;
    public event Action<MouseEventArgs> LeftClick = null!;
    public event Action<MouseEventArgs> LeftDblClick = null!;


    public void OnMouse(MouseEventArgs e)
    {
        switch (e.Type)
        {
            case "wheel": OnMouseWheelEvent(e); break;
            case "mousemove": OnMouseMoveEvent(e); break;
            case "mousedown": OnMouseDownEvent(e); break;
            case "mouseup": OnMouseUpEvent(e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }
    }

    void OnMouseWheelEvent(MouseEventArgs e) => MouseWheel?.Invoke((WheelEventArgs)e);

    void OnMouseMoveEvent(MouseEventArgs e) => MouseMove?.Invoke(e);


    void OnMouseDownEvent(MouseEventArgs e)
    {
        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            clickLeftMouse = e;
        }
    }

    void OnMouseUpEvent(MouseEventArgs e)
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
        LeftClick?.Invoke(clickLeftMouse);
    }

    void OnLeftClickEvent(MouseEventArgs e)
    {
        clickLeftMouse = e;
        if (!timerRunning)
        {   // This is the first click, start the timer
            timerRunning = true;
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
