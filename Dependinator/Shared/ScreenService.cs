using Dependinator.Models;
using Dependinator.Shared;
using Dependinator.Utils.UI;


namespace Dependinator.Diagrams;


interface IScreenService
{
    Rect SvgRect { get; }

    Task InitAsync(IUIComponent component);
    Task CheckResizeAsync();
}


[Scoped]
class ScreenService : IScreenService
{
    const int SvgPageMargin = 2;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInteropService jSInteropService;
    IUIComponent component = null!;

    readonly object syncRoot = new();

    public ScreenService(IApplicationEvents applicationEvents, IJSInteropService jSInteropService)
    {
        this.applicationEvents = applicationEvents;
        this.jSInteropService = jSInteropService;
        jSInteropService.OnResize += OnResize;
    }

    public Rect SvgRect { get; private set; } = Rect.None;


    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        await Task.CompletedTask;
    }


    public async Task CheckResizeAsync()
    {
        // Get Svg position (width and height are unreliable)
        var svg = await jSInteropService.GetBoundingRectangle(component.Ref);

        // Get window width and height
        var windowWidth = Math.Floor(jSInteropService.BrowserSizeDetails.InnerWidth);
        var windowHeight = Math.Floor(jSInteropService.BrowserSizeDetails.InnerHeight);

        // Calculate the SVG size to fit the window (with some margin and x,y position)
        var svgX = Math.Floor(svg.X);
        var svgY = Math.Floor(svg.Y);
        var svgWidth = windowWidth - svgX - SvgPageMargin * 2;
        var svgHeight = windowHeight - svgY - SvgPageMargin * 2;
        var newSwgRect = new Rect(0, 0, svgWidth, svgHeight);

        var isChanged = false;
        lock (syncRoot)
        {
            if (newSwgRect != SvgRect)
            {   // Svg size has changed, adjust svg to fit new window size window and trigger update
                SvgRect = newSwgRect;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            applicationEvents.TriggerUIStateChanged();
            applicationEvents.TriggerSaveNeeded();
            Log.Info($"Resized: {newSwgRect}");
        }
    }


    void OnResize() => CheckResizeAsync().RunInBackground();
}


