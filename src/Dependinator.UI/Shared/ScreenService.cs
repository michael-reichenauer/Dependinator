using Dependinator.UI.Shared.Types;
using Microsoft.JSInterop;

namespace Dependinator.UI.Shared;

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
class ScreenService : IScreenService, IDisposable
{
    const int SvgPageMargin = 2;
    const int ResizeDebounceMs = 25;

    readonly IApplicationEvents applicationEvents;
    readonly IJSInterop jSInterop;
    readonly Timer resizeTimer;
    DotNetObjectReference<ScreenService>? reference;

    readonly object syncRoot = new();
    BrowserSizeDetails browserSizeDetails = new BrowserSizeDetails();

    public ScreenService(IApplicationEvents applicationEvents, IJSInterop jSInteropService)
    {
        this.applicationEvents = applicationEvents;
        this.jSInterop = jSInteropService;
        this.resizeTimer = new Timer(_ => OnResizeDebounced(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public Rect SvgRect { get; private set; } = Rect.None;

    public async Task InitAsync(IUIComponent component)
    {
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

        // Calculate the SVG size to fit the window (with some margin and x,y position).
        // The extra 10px of height lets the canvas reach the bottom edge of the window.
        var svgX = Math.Floor(svg.X);
        var svgY = Math.Floor(svg.Y);
        var svgWidth = windowWidth - svgX - SvgPageMargin * 2;
        var svgHeight = windowHeight - svgY - SvgPageMargin * 2 + 10;
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
        }
    }

    public async Task<R<ElementBoundingRectangle>> GetBoundingRectangle(string elementId)
    {
        var r = await jSInterop.Call<ElementBoundingRectangle>("getBoundingRectangle", elementId);
        if (r == null)
            return R.None;
        return r;
    }

    [JSInvokable]
    public ValueTask OnWindowResized()
    {
        // Debounce: restart the timer on every event, act once the burst has settled.
        resizeTimer.Change(ResizeDebounceMs, Timeout.Infinite);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        resizeTimer.Dispose();
        reference?.Dispose();
    }

    async Task RegisterWindowResizeEvents()
    {
        reference ??= jSInterop.Reference(this);
        await jSInterop.Call("listenToWindowResize", "svgcanvas", reference, nameof(OnWindowResized));
    }

    async Task<BrowserSizeDetails> GetBrowserSizeDetails() =>
        await jSInterop.Call<BrowserSizeDetails>("getWindowSizeDetails");

    void OnResizeDebounced() =>
        Task.Run(async () =>
            {
                this.browserSizeDetails = await GetBrowserSizeDetails();
                await CheckResizeAsync();
            })
            .RunInBackground();
}
