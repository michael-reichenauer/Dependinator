using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

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

// This class is used when a javascript function returns a value that could larger than 20k
class ValueHandler
{
    readonly StringBuilder sb = new();

    public string GetValue() => sb.ToString();

    [JSInvokable]
    public ValueTask OnValue(string value)
    {
        sb.Append(value);
        return ValueTask.CompletedTask;
    }
}

public interface IJSInteropService
{
    ValueTask<BrowserSizeDetails> GetWindowSizeAsync();
    ValueTask<ElementBoundingRectangle> GetBoundingRectangle(ElementReference elementReference);

    ValueTask AddWindoResizeEventListenerAsync(object dotNetObjectReference, string functionName);

    ValueTask AddMouseEventListenerAsync(string elementId, string eventName, object dotNetObjectReference, string functionName);
    ValueTask AddPointerEventListenerAsync(string elementId, string eventName, object dotNetObjectReference, string functionName);

    ValueTask InitializeDatabaseAsync(string databaseName, int currentVersion, string[] collectionNames);
    ValueTask SetDatabaseValueAsync<T>(string databaseName, string collectionName, T value);
    ValueTask<R<T>> GetDatabaseValueAsync<T>(string databaseName, string collectionName, string id);
    ValueTask DeleteDatabaseValueAsync(string databaseName, string collectionName, string id);
    ValueTask<R<IReadOnlyList<string>>> GetDatabaseKeysAsync(string databaseName, string collectionName);
    ValueTask InitializeFileDropZone(ElementReference? dropZoneElement, ElementReference? inputFileElement);
    ValueTask ClickElement(string elementId);
}


// Note: All DotNetObjectReference.Create() in the program code should be disposed.
[Scoped]
public class JSInteropService : IJSInteropService, IAsyncDisposable
{
    readonly Lazy<Task<IJSObjectReference>> moduleTask;

    static JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    public JSInteropService(IJSRuntime jsRuntime)
    {
        // var version = "1.4";                    // Version is needed to avoid cached js file (prod) 
        var version = $"{DateTime.UtcNow.Ticks}";  // Vesion is needed to avoid cached js file (dev)

        this.moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            identifier: "import", args: $"./_content/Dependinator/jsInterop.js?v={version}").AsTask());
    }


    public async ValueTask AddWindoResizeEventListenerAsync(object dotNetObjectReference, string functionName)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "listenToWindowResize", dotNetObjectReference, functionName);
    }

    public async ValueTask AddMouseEventListenerAsync(string elementId, string eventName, object dotNetObjectReference, string functionName)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "addMouseEventListener", elementId, eventName, dotNetObjectReference, functionName);
    }

    public async ValueTask AddPointerEventListenerAsync(string elementId, string eventName, object dotNetObjectReference, string functionName)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "addPointerEventListener", elementId, eventName, dotNetObjectReference, functionName);
    }

    public async ValueTask<BrowserSizeDetails> GetWindowSizeAsync()
    {
        IJSObjectReference module = await GetModuleAsync();
        return await module.InvokeAsync<BrowserSizeDetails>(identifier: "getWindowSizeDetails");
    }

    public async ValueTask<ElementBoundingRectangle> GetBoundingRectangle(ElementReference elementReference)
    {
        IJSObjectReference module = await GetModuleAsync();
        return await module.InvokeAsync<ElementBoundingRectangle>(identifier: "getBoundingRectangle", elementReference);
    }

    public async ValueTask InitializeDatabaseAsync(string databaseName, int currentVersion, string[] collectionNames)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "initializeDatabase", databaseName, currentVersion, collectionNames);
    }

    public async ValueTask SetDatabaseValueAsync<T>(string databaseName, string collectionName, T value)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "setDatabaseValue", databaseName, collectionName, value);
    }

    public async ValueTask<R<T>> GetDatabaseValueAsync<T>(string databaseName, string collectionName, string id)
    {
        IJSObjectReference module = await GetModuleAsync();

        var valueHandler = new ValueHandler();
        using var valueHandlerRef = DotNetObjectReference.Create(valueHandler);

        var result = await module.InvokeAsync<bool>(identifier: "getDatabaseValue", databaseName, collectionName, id, valueHandlerRef, "OnValue");

        if (!result) return R.None;

        var valueText = valueHandler.GetValue();
        var value = JsonSerializer.Deserialize<T>(valueText, options);
        return value!;
    }

    public async ValueTask DeleteDatabaseValueAsync(string databaseName, string collectionName, string id)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "deleteDatabaseValue", databaseName, collectionName, id);
    }

    public async ValueTask<R<IReadOnlyList<string>>> GetDatabaseKeysAsync(string databaseName, string collectionName)
    {
        IJSObjectReference module = await GetModuleAsync();
        var result = await module.InvokeAsync<string[]>(identifier: "getDatabaseAllKeys", databaseName, collectionName);
        if (result == null) return new string[0];
        return result;
    }

    public async ValueTask InitializeFileDropZone(ElementReference? dropZoneElement, ElementReference? inputFileElement)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "initializeFileDropZone", dropZoneElement, inputFileElement);
    }

    public async ValueTask ClickElement(string elementId)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(identifier: "clickElement", elementId);
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