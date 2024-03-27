using Dependinator.Models;
using Dependinator.Shared;
using Dependinator.Utils.UI;
using Microsoft.JSInterop;


namespace Dependinator.Diagrams;


interface IScreenService
{
    Rect SvgRect { get; }

    Task InitAsync(IUIComponent component);
    Task CheckResizeAsync();
}

// Inspired from https://stackoverflow.com/questions/75114524/getting-the-size-of-a-blazor-page-with-javascript
[Scoped]
class ScreenService : IScreenService
{
    const int SvgPageMargin = 2;
    readonly IApplicationEvents applicationEvents;
    readonly IJSInteropService jSInteropService;
    IUIComponent component = null!;
    bool isResizing = false;
    System.Timers.Timer resizeTimer;

    readonly object syncRoot = new();
    BrowserSizeDetails browserSizeDetails = new BrowserSizeDetails();

    public ScreenService(IApplicationEvents applicationEvents, IJSInteropService jSInteropService)
    {
        this.applicationEvents = applicationEvents;
        this.jSInteropService = jSInteropService;

        this.resizeTimer = new System.Timers.Timer(interval: 25);
        this.resizeTimer.Elapsed += async (sender, elapsedEventArgs) => await DimensionsChanged(sender!, elapsedEventArgs);
    }

    public Rect SvgRect { get; private set; } = Rect.None;


    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        var objRef = DotNetObjectReference.Create(this);

        this.browserSizeDetails = await jSInteropService.GetWindowSizeAsync();
        await jSInteropService.AddWindoResizeEventListenerAsync(objRef, nameof(WindowResizeEvent));
        await Task.CompletedTask;
    }


    public async Task CheckResizeAsync()
    {
        // Get Svg position (width and height are unreliable)
        var svg = await jSInteropService.GetBoundingRectangle(component.Ref);

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

    [JSInvokable]
    public ValueTask WindowResizeEvent()
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
                this.browserSizeDetails = await jSInteropService.GetWindowSizeAsync();
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
        this.browserSizeDetails = await jSInteropService.GetWindowSizeAsync();
        isResizing = false;
        OnResize();
    }
}


