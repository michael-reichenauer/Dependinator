namespace Dependinator.Utils.UI;

// https://www.w3schools.com/jsref/obj_mouseevent.asp
public class MouseEvent
{
    public string Type { get; set; } = "";
    public string TargetId { get; set; } = "";

    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int ClientX { get; set; }
    public int ClientY { get; set; }
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    public int PageX { get; set; }
    public int PageY { get; set; }
    public int MovementX { get; set; }
    public int MovementY { get; set; }
    public int Button { get; set; }
    public int Buttons { get; set; }
    public bool ShiftKey { get; set; }
    public bool CtrlKey { get; set; }
    public bool AltKey { get; set; }
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }
    public int DeltaZ { get; set; }
    public int DeltaMode { get; set; }
}
