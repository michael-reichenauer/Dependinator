using Dependinator.Models;
using Dependinator.Utils.UI;
using Microsoft.JSInterop;

namespace Dependinator.Diagrams;

public class BrowserSizeDetails
{
    public double InnerWidth { get; set; }
    public double InnerHeight { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
}

public class ElementBoundingRectangle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
}

interface IScreenService
{
    Rect SvgRect { get; }

    Task InitAsync(IUIComponent component);
    Task<R<ElementBoundingRectangle>> GetBoundingRectangle(string elementId);
    Task CheckResizeAsync();
}

// Inspired from https://stackoverflow.com/questions/75114524/getting-the-size-of-a-blazor-page-with-javascript
[Scoped]
class ScreenService : IScreenService
{
    const int SvgPageMargin = 2;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInterop;
    IUIComponent component = null!;
    bool isResizing = false;
    System.Timers.Timer resizeTimer;

    readonly object syncRoot = new();
    BrowserSizeDetails browserSizeDetails = new BrowserSizeDetails();

    public ScreenService(IApplicationEvents applicationEvents, IJSInterop jSInteropService)
    {
        this.applicationEvents = applicationEvents;
        this.jSInterop = jSInteropService;

        this.resizeTimer = new System.Timers.Timer(interval: 25);
        this.resizeTimer.Elapsed += async (sender, elapsedEventArgs) =>
            await DimensionsChanged(sender!, elapsedEventArgs);
    }

    public Rect SvgRect { get; private set; } = Rect.None;

    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;

        this.browserSizeDetails = await GetBrowserSizeDetails();
        await RegisterWindowResizeEvents();
    }

    public async Task CheckResizeAsync()
    {
        // Get Svg position (width and height are unreliable)
        if (!Try(out var svg, out var _, await GetBoundingRectangle("svgcanvas")))
            return;

        // Get window width and height
        var windowWidth = Math.Floor(browserSizeDetails.InnerWidth);
        var windowHeight = Math.Floor(browserSizeDetails.InnerHeight);

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
            { // Svg size has changed, adjust svg to fit new window size window and trigger update
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

    public async Task<R<ElementBoundingRectangle>> GetBoundingRectangle(string elementId)
    {
        var r = await jSInterop.Call<ElementBoundingRectangle>("getBoundingRectangle", elementId);
        if (r == null)
            return R.None;
        return r;
    }

    async Task RegisterWindowResizeEvents() =>
        await jSInterop.Call("listenToWindowResize", "svgcanvas", jSInterop.Reference(this), nameof(OnWindowResized));

    async Task<BrowserSizeDetails> GetBrowserSizeDetails() =>
        await jSInterop.Call<BrowserSizeDetails>("getWindowSizeDetails");

    void OnResize() => CheckResizeAsync().RunInBackground();

    [JSInvokable]
    public ValueTask OnWindowResized()
    {
        if (this.isResizing is not true)
        {
            this.isResizing = true;
        }
        DebounceResizeEvent();
        return ValueTask.CompletedTask;
    }

    private void DebounceResizeEvent()
    {
        if (this.resizeTimer.Enabled is false)
        {
            Task.Run(async () =>
            {
                this.browserSizeDetails = await GetBrowserSizeDetails();
                isResizing = false;

                OnResize();
            });
            this.resizeTimer.Stop();
            this.resizeTimer.Start();
        }
    }

    private async ValueTask DimensionsChanged(object sender, System.Timers.ElapsedEventArgs e)
    {
        this.resizeTimer.Stop();
        this.browserSizeDetails = await GetBrowserSizeDetails();
        isResizing = false;
        OnResize();
    }
}
