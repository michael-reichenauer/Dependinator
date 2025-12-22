using Microsoft.JSInterop;

namespace Dependinator.Shared;

public interface IJSInterop
{
    ValueTask Call(string functionName, params object?[]? args);
    ValueTask<T> Call<T>(string functionName, params object?[]? args);
    DotNetObjectReference<TValue> Reference<TValue>(TValue value)
        where TValue : class;
}

// Note: All DotNetObjectReference.Create() in the program code should be disposed.
[Scoped]
public class JSInterop : IJSInterop, IAsyncDisposable
{
    readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public JSInterop(IJSRuntime jsRuntime)
    {
        // var version = "1.4";                    // Version is needed to avoid cached js file (prod)
        var version = $"{DateTime.UtcNow.Ticks}"; // Vesion is needed to avoid cached js file (dev)

        this.moduleTask = new(() => CreateModuleAsync(jsRuntime, version));
    }

    public async ValueTask Call(string functionName, params object?[]? args)
    {
        IJSObjectReference module = await GetModuleAsync();
        await module.InvokeVoidAsync(functionName, args);
    }

    public async ValueTask<T> Call<T>(string functionName, params object?[]? args)
    {
        IJSObjectReference module = await GetModuleAsync();
        return await module.InvokeAsync<T>(functionName, args);
    }

    public DotNetObjectReference<TValue> Reference<TValue>(TValue value)
        where TValue : class
    {
        // Save all instances to be disposed !!!!!
        return DotNetObjectReference.Create(value);
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            IJSObjectReference module = await GetModuleAsync();
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Ignore exception when the browser is closed
            }
        }
    }

    async Task<IJSObjectReference> GetModuleAsync() => await this.moduleTask.Value;

    static async Task<IJSObjectReference> CreateModuleAsync(IJSRuntime jsRuntime, string version)
    {
        var baseUri = await TryGetBaseUriAsync(jsRuntime);
        var modulePath = string.IsNullOrWhiteSpace(baseUri)
            ? $"./_content/Dependinator/jsInterop.js?v={version}"
            : $"{baseUri}_content/Dependinator/jsInterop.js?v={version}";

        return await jsRuntime.InvokeAsync<IJSObjectReference>(identifier: "import", args: modulePath);
    }

    static async Task<string?> TryGetBaseUriAsync(IJSRuntime jsRuntime)
    {
        try
        {
            var baseUri = await jsRuntime.InvokeAsync<string>("dependinator.getBaseUri");
            if (string.IsNullOrWhiteSpace(baseUri))
                return null;
            return baseUri.EndsWith("/") ? baseUri : $"{baseUri}/";
        }
        catch (JSException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
