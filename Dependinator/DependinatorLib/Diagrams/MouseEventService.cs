using Microsoft.AspNetCore.Components.Web;


namespace Dependinator.Diagrams;

interface IMouseEventService
{
    event Action<WheelEventArgs> MouseWheel;
    event Action<MouseEventArgs> MouseMove;
    event Action<MouseEventArgs> MouseDown;
    event Action<MouseEventArgs> MouseUp;
    event Action<MouseEventArgs> Click;
    event Action<MouseEventArgs> DblClick;

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
        clickTimer = new Timer(OnClickTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event Action<WheelEventArgs> MouseWheel = null!;
    public event Action<MouseEventArgs> MouseMove = null!;
    public event Action<MouseEventArgs> MouseDown = null!;
    public event Action<MouseEventArgs> MouseUp = null!;
    public event Action<MouseEventArgs> Click = null!;
    public event Action<MouseEventArgs> DblClick = null!;


    public void OnMouse(MouseEventArgs e)
    {
        switch (e.Type)
        {
            case "wheel": MouseWheel?.Invoke((WheelEventArgs)e); break;
            case "mousemove": MouseMove?.Invoke(e); break;
            case "mousedown": OnMouseDown(e); break;
            case "mouseup": OnMouseUp(e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }
    }


    void OnMouseDown(MouseEventArgs e)
    {
        MouseDown?.Invoke(e);

        if (e.Button == 0)
        {
            clickLeftMouse = e;
        }
    }

    void OnMouseUp(MouseEventArgs e)
    {
        MouseUp?.Invoke(e);

        if (e.Button == 0 &&
            Math.Abs(e.OffsetX - clickLeftMouse.OffsetX) < 5 &&
            Math.Abs(e.OffsetY - clickLeftMouse.OffsetY) < 5)
        {
            OnClickEvent(e);
        }
    }

    void OnClickTimer(object? state)
    {
        timerRunning = false;
        Click?.Invoke(clickLeftMouse);
    }


    void OnClickEvent(MouseEventArgs e)
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
            DblClick?.Invoke(e);
        }
    }
}
