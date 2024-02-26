namespace Dependinator.Utils.UI;

// https://www.w3schools.com/jsref/obj_mouseevent.asp
public record MouseEvent
{
    const int LeftMouseBtn = 1;

    public string Type { get; init; } = "";
    public string TargetId { get; init; } = "";

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


public record TouchEvent
{
    public string Type { get; init; } = "";
    public string TargetId { get; init; } = "";

    public long Detail { get; init; }

    public TouchPoint[] Touches { get; init; } = Array.Empty<TouchPoint>();
    public TouchPoint[] TargetTouches { get; init; } = Array.Empty<TouchPoint>();
    public TouchPoint[] ChangedTouches { get; init; } = Array.Empty<TouchPoint>();

    public bool CtrlKey { get; init; }
    public bool ShiftKey { get; init; }
    public bool AltKey { get; init; }
    public bool MetaKey { get; init; }
}

public record TouchPoint
{
    public long Identifier { get; init; }
    public double ScreenX { get; init; }
    public double ScreenY { get; init; }
    public double ClientX { get; init; }
    public double ClientY { get; init; }
    public double PageX { get; init; }
    public double PageY { get; init; }
}

public record HammerEvent
{
    public string Type { get; init; } = "";
    public string TargetId { get; init; } = "";
    public HammerPoint Center { get; init; } = null!;
    public HammerPoint[] Pointers { get; init; } = Array.Empty<HammerPoint>();
    public double DeltaX { get; init; }
    public double DeltaY { get; init; }
    public double Rotation { get; init; }
    public bool IsFinal { get; init; }
}

public record HammerPoint
{
    public double X { get; init; }
    public double Y { get; init; }
}