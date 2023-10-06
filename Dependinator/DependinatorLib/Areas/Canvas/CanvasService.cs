using Microsoft.AspNetCore.Components.Web;

namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string ViewBox { get; }
    double Width { get; }
    double Height { get; }

    string GetContent();

    Task OnMouseWheel(WheelEventArgs e);
    void OnMouse(MouseEventArgs e);

    Task InitJsAsync(Canvas canvas);
}

record Pos(double X, double Y);
record Size(double W, double H);
record Rect(double X, double Y, double W, double H);

[Scoped]
public class CanvasService : ICanvasService
{
    const double zoomSpeed = 0.1;

    readonly List<IElement> elements = new List<IElement>();
    private readonly IJSInteropCoreService jSInteropCoreService;
    private readonly IJsInterop jsInterop;
    string svg = "";

    double width = 0;
    double height = 0;
    double zoom = 0;
    Canvas? canvas;
    Pos lastMouse = new Pos(0, 0);
    bool isDrag = false;

    private Rect viewBoxRect = new Rect(0, 0, 400, 400);

    public double Zoom => zoom;
    public string ViewBox => $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.W} {viewBoxRect.H}";
    public double ViewWidth => Zoom * Width;
    public double ViewHeight => Zoom * Height;

    public double Width => width;
    public double Height => height;

    public CanvasService(IJSInteropCoreService jSInteropCoreService, IJsInterop jsInterop)
    {
        elements.Add(new Node { X = 90, Y = 90, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Node { X = 190, Y = 190, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Connector { X1 = 120, Y1 = 130, X2 = 220, Y2 = 190, Color = "#555555" });
        Update();
        this.jSInteropCoreService = jSInteropCoreService;
        this.jsInterop = jsInterop;
        jSInteropCoreService.OnResize += () => OnResize();
        jSInteropCoreService.OnResizing += (r) => OnResizing(r);
    }

    public async Task OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;

        double z = 1 - (e.DeltaY > 0 ? zoomSpeed : -zoomSpeed);

        var svgRect = await jsInterop.GetBoundingRectangle("canvasid");

        double mouseX = e.ClientX - svgRect.Left;
        double mouseY = e.ClientY - svgRect.Top;
        double svgX = mouseX / svgRect.Width * this.viewBoxRect.W + this.viewBoxRect.X;
        double svgY = mouseY / svgRect.Height * this.viewBoxRect.H + this.viewBoxRect.Y;

        var w = this.viewBoxRect.W * z;
        var h = this.viewBoxRect.H * z;
        var x = svgX - mouseX / svgRect.Width * w;
        var y = svgY - mouseY / svgRect.Height * h;
        this.viewBoxRect = new Rect(x, y, w, h);

        canvas?.TriggerStateHasChanged();
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
            canvas.TriggerStateHasChanged();
        }
        Log.Info($"OnResize ({jSInteropCoreService.BrowserSizeDetails.InnerWidth}, {jSInteropCoreService.BrowserSizeDetails.InnerHeight})");
    }

    public string GetContent()
    {
        return svg;
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
                viewBoxRect = viewBoxRect with { X = viewBoxRect.X - dx, Y = viewBoxRect.Y - dy };
                lastMouse = new Pos(e.OffsetX, e.OffsetY);
                canvas?.TriggerStateHasChanged();
            }
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