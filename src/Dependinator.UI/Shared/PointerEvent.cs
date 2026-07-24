namespace Dependinator.UI.Shared;

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

    // Signed count of wheel events the JS input batcher coalesced into this event (one per
    // animation frame); 0 for non-batched sources (e.g. pinch-synthesized wheel events).
    public int WheelTicks { get; init; }

    public bool IsLeftButton => Buttons == LeftMouseBtn;

    // How many zoom steps this wheel event represents: a coalesced event applies one step per
    // dropped tick so fast wheel spins keep their speed despite the per-frame batching.
    public int ZoomSteps => Math.Max(1, Math.Abs(WheelTicks));
}
