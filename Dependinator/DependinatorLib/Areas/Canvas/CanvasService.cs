using System.Drawing;
using Microsoft.AspNetCore.Components.Web;

namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string ViewBox { get; }
    double Width { get; }
    double Height { get; }

    string GetContent();


    Task InitJsAsync(Canvas canvas);

    void OnMouse(MouseEventArgs evt);
    void OnMouseWheel(WheelEventArgs e);
}

record Pos(double X, double Y);

[Scoped]
public class CanvasService : ICanvasService
{
    readonly List<IElement> elements = new List<IElement>();
    private readonly IJSInteropCoreService jSInteropCoreService;
    string svg = "";

    double x = 0;
    double y = 0;
    double width = 0;
    double height = 0;
    double zoom = 0.84;
    Canvas? canvas;
    Pos lastMouse = new Pos(0, 0);
    bool isDrag = false;

    public double Zoom => zoom;
    public string ViewBox => $"{x} {y} {Zoom * Width} {Zoom * Height}";

    public double Width => width - 0;

    public double Height => height - 0;

    public CanvasService(IJSInteropCoreService jSInteropCoreService)
    {
        elements.Add(new Node { X = 90, Y = 90, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Node { X = 190, Y = 190, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Connector { X1 = 120, Y1 = 130, X2 = 220, Y2 = 190, Color = "#555555" });
        Update();
        this.jSInteropCoreService = jSInteropCoreService;
        jSInteropCoreService.OnResize += () => OnResize();
        jSInteropCoreService.OnResizing += (r) => OnResizing(r);
    }

    private void OnResizing(bool r)
    {
        //Log.Info($"OnResizing: {r}, ({jSInteropCoreService.BrowserSizeDetails.InnerWidth}, {jSInteropCoreService.BrowserSizeDetails.InnerHeight})");
    }

    private void OnResize()
    {
        var w = jSInteropCoreService.BrowserSizeDetails.InnerWidth;
        var h = jSInteropCoreService.BrowserSizeDetails.InnerHeight;
        if (canvas != null && (w != width || h != height))
        {
            width = w;
            height = h;
            canvas.CallStateHasChanged();
        }
        Log.Info($"OnResize ({jSInteropCoreService.BrowserSizeDetails.InnerWidth}, {jSInteropCoreService.BrowserSizeDetails.InnerHeight})");
    }

    public string GetContent()
    {
        //Log.Debug($"Get\n{svg}");
        // Log.Info($"GetContent ({jSInteropCoreService.BrowserSizeDetails.InnerWidth}, {jSInteropCoreService.BrowserSizeDetails.InnerHeight})");
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
        // Log.Info($"FireMove {e.Type}: {e.Button} ({e.Buttons}) {e.OffsetX},{e.OffsetY} {e.Type}");
        if (e.Buttons == 1)
        {
            if (e.Type == "mousedown")
            {
                isDrag = true;
                lastMouse = new Pos(e.OffsetX, e.OffsetY);
            }
            else if (e.Type == "mouseup")
            {
                isDrag = false;
            }
            else if (e.Type == "mousemove" && isDrag)
            {
                var dx = e.OffsetX - lastMouse.X;
                var dy = e.OffsetY - lastMouse.Y;
                x += -dx;
                y += -dy;
                lastMouse = new Pos(e.OffsetX, e.OffsetY);
                canvas?.CallStateHasChanged();
            }
        }
        // OnMove?.Invoke(obj, e);
    }

    public void OnMouseWheel(WheelEventArgs e)
    {
        Log.Info($"Wheel {e.Type}: {e.OffsetX - x} {e.OffsetY - y} {e.DeltaY}");
        Log.Info($"({x},{y})");
        if (e.DeltaY > 0)
        {
            zoom *= 1.1;
            // x and y needs to be adjusted to keep the mouse position in the same place
            x -= (e.OffsetX - x) * 0.1;
            y -= (e.OffsetY - y) * 0.1;

        }
        else
        {
            zoom *= 0.9;
            // x and y needs to be adjusted to keep the mouse position in the same place
            x += (e.OffsetX - x) * 0.1;
            y += (e.OffsetY - y) * 0.1;
            // x += (e.OffsetX - x) * zoom;
            // y += (e.OffsetY - y) * zoom;

        }
    }

    public async Task InitJsAsync(Canvas canvas)
    {
        Log.Info("InitJsAsync");
        this.canvas = canvas;
        await this.jSInteropCoreService.InitializeAsync();
    }
}

interface IElement
{
    public string Svg { get; }
    void Update() { }
}


// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Fills_and_Strokes
// Use <defs> and style to create hoover effects and global styles to avoid repeating
// Gradients 
// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Texts 
// for texts
// Embedding SVG in HTML
// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Basic_Transformations
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