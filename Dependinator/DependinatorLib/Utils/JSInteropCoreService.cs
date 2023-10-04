using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DependinatorLib.Utils;

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
public class BrowserSizeDetails
{
    public double InnerWidth { get; set; }
    public double InnerHeight { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
}


public interface IJSInteropCoreService
{
    event Action<bool> OnResizing;
    event Action OnResize;
    ValueTask InitializeAsync();
    ValueTask<BrowserSizeDetails> GetWindowSizeAsync();
    ValueTask<ElementBoundingRectangle> GetElementBoundingRectangleAsync(ElementReference elementReference);
    BrowserSizeDetails BrowserSizeDetails { get; }
}

[Scoped]
public class JSInteropCoreService : IJSInteropCoreService
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;
    private bool isResizing;
    private System.Timers.Timer resizeTimer;
    private DotNetObjectReference<JSInteropCoreService> jsInteropCoreServiceRef = null!;

    public JSInteropCoreService(IJSRuntime jsRuntime)
    {
        this.resizeTimer = new System.Timers.Timer(interval: 25);
        this.isResizing = false;
        this.resizeTimer.Elapsed += async (sender, elapsedEventArgs) => await DimensionsChanged(sender!, elapsedEventArgs);

        this.moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
          identifier: "import", args: "./bCore.js").AsTask());
    }

    public event Action<bool>? OnResizing;
    public event Action? OnResize;

    public async ValueTask InitializeAsync()
    {
        IJSObjectReference module = await GetModuleAsync();

        this.jsInteropCoreServiceRef = DotNetObjectReference.Create(this);

        await module.InvokeVoidAsync(identifier: "listenToWindowResize", this.jsInteropCoreServiceRef);

        this.BrowserSizeDetails = await module.InvokeAsync<BrowserSizeDetails>(identifier: "getWindowSizeDetails");

    }

    public async ValueTask<BrowserSizeDetails> GetWindowSizeAsync()
    {
        IJSObjectReference module = await GetModuleAsync();
        return await module.InvokeAsync<BrowserSizeDetails>(identifier: "getWindowSizeDetails");
    }

    public async ValueTask<ElementBoundingRectangle> GetElementBoundingRectangleAsync(ElementReference elementReference)
    {
        IJSObjectReference module = await GetModuleAsync();
        return await module.InvokeAsync<ElementBoundingRectangle>(identifier: "getBoundingRectangle", elementReference);
    }

    [JSInvokable]
    public ValueTask WindowResizeEvent()
    {
        if (this.isResizing is not true)
        {
            this.isResizing = true;
            OnResizing?.Invoke(this.isResizing);
        }
        DebounceResizeEvent();
        return ValueTask.CompletedTask;
    }

    public BrowserSizeDetails BrowserSizeDetails { get; private set; } = new BrowserSizeDetails();

    private void DebounceResizeEvent()
    {
        if (this.resizeTimer.Enabled is false)
        {
            Task.Run(async () =>
            {
                this.BrowserSizeDetails = await GetWindowSizeAsync();
                isResizing = false;
                OnResizing?.Invoke(this.isResizing);
                OnResize?.Invoke();
            });
            this.resizeTimer.Stop();
            this.resizeTimer.Start();
        }
    }

    private async ValueTask DimensionsChanged(object sender, System.Timers.ElapsedEventArgs e)
    {
        this.resizeTimer.Stop();
        this.BrowserSizeDetails = await GetWindowSizeAsync();
        isResizing = false;
        OnResizing?.Invoke(this.isResizing);
        OnResize?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.DisposeAsync();
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync()
        => await this.moduleTask.Value;
}