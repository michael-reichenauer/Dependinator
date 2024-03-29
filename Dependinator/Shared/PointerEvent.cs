namespace Dependinator.Utils.UI;

// https://www.w3schools.com/jsref/obj_mouseevent.asp
public record PointerEvent
{
    const int LeftMouseBtn = 1;

    public string Type { get; init; } = "";
    public long Time { get; init; }
    public string TargetId { get; init; } = "";
    public int PointerId { get; init; }
    public string PointerType { get; init; } = "";

    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double ClientX { get; init; }
    public double ClientY { get; init; }
    public double ScreenX { get; init; }
    public double ScreenY { get; init; }
    public double PageX { get; init; }
    public double PageY { get; init; }
    public double MovementX { get; init; }
    public double MovementY { get; init; }
    public long Button { get; init; }
    public long Buttons { get; init; }
    public bool ShiftKey { get; init; }
    public bool CtrlKey { get; init; }
    public bool AltKey { get; init; }
    public double DeltaX { get; init; }
    public double DeltaY { get; init; }
    public double DeltaZ { get; init; }
    public double DeltaMode { get; init; }

    public bool IsLeftButton => Buttons == LeftMouseBtn;
}
