using Microsoft.AspNetCore.Components.Web;

namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string GetContent();

    void OnMouse(MouseEventArgs evt);
    void OnMouseWheel(WheelEventArgs e);
}


[Singleton]
public class CanvasService : ICanvasService
{
    readonly List<IElement> elements = new List<IElement>();
    string svg = "";

    public CanvasService()
    {
        elements.Add(new Node { X = 90, Y = 90, W = 60, H = 40, Color = "#00aa00" });
        elements.Add(new Node { X = 190, Y = 190, W = 60, H = 40, Color = "#00aa00" });
        elements.Add(new Connector { X1 = 120, Y1 = 130, X2 = 220, Y2 = 190, Color = "#555555" });
        Update();
    }

    public string GetContent()
    {
        //Log.Debug($"Get\n{svg}");
        return svg;

        // return """
        //         <rect x="90" y="90" width="60" height="40" rx="5" fill="transparent "stroke="#00aa00" />
        //         <rect x="190" y="190" width="60" height="40" rx="5" fill="transparent "stroke="#00aa00" />

        //         <path d="M120 130 220 190" stroke="rgb(108, 117, 125)" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;"/>
        //  """;
    }

    public void Update()
    {
        elements.ForEach(n => n.Update());
        svg = elements.Select(n => n.Svg).Join("\n");
    }

    public void OnMouse(MouseEventArgs e)
    {
        Log.Info($"FireMove {e.Type}: {e.OffsetX},{e.OffsetY}");
        // OnMove?.Invoke(obj, e);
    }

    public void OnMouseWheel(WheelEventArgs e)
    {
        Log.Info($"Wheel {e.Type}: {e.OffsetX},{e.OffsetY},{e.DeltaY},,{e.DeltaX}, {e.DeltaMode}");
        // OnMove?.Invoke(obj, e);
    }
}

interface IElement
{
    public string Svg { get; }
    void Update() { }
}

class Node : IElement
{
    string Id { get; set; } = "";
    string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int RX { get; set; } = 5;
    public string Color { get; set; } = "";
    public string Background { get; set; } = "green";

    public string Svg { get; private set; } = "";

    public void Update()
    {
        Svg = $"""<rect x="{X}" y="{Y}" width="{W}" height="{H}" rx="{RX}" fill="{Background}" fill-opacity="0.2" stroke="{Color}" stroke-width="2"/>""";
    }
}
class Connector : IElement
{
    string Id { get; set; } = "";
    string Name { get; set; } = "";
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public string Color { get; set; } = "";

    public string Svg { get; private set; } = "";

    public void Update()
    {
        Svg = $"""<polyline points="{X1} {Y1} {X1} {Y1 + 20} {X2} {Y2}" stroke="{Color}" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;" stroke-width="3"/ stroke-linejoin="round">""";
    }
}