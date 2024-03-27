using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Dependinator.Shared;




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

public interface IJSInterop
{
    ValueTask Call(string functionName, params object?[]? args);
    ValueTask<T> Call<T>(string functionName, params object?[]? args);
    DotNetObjectReference<TValue> Instance<TValue>(TValue value) where TValue : class;

    ValueTask InitializeDatabaseAsync(string databaseName, int currentVersion, string[] collectionNames);
    ValueTask SetDatabaseValueAsync<T>(string databaseName, string collectionName, T value);
    ValueTask<R<T>> GetDatabaseValueAsync<T>(string databaseName, string collectionName, string id);
    ValueTask DeleteDatabaseValueAsync(string databaseName, string collectionName, string id);
    ValueTask<R<IReadOnlyList<string>>> GetDatabaseKeysAsync(string databaseName, string collectionName);
}


// Note: All DotNetObjectReference.Create() in the program code should be disposed.
[Scoped]
public class JSInterop : IJSInterop, IAsyncDisposable
{

    readonly Lazy<Task<IJSObjectReference>> moduleTask;

    static JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    public JSInterop(IJSRuntime jsRuntime)
    {
        // var version = "1.4";                    // Version is needed to avoid cached js file (prod) 
        var version = $"{DateTime.UtcNow.Ticks}";  // Vesion is needed to avoid cached js file (dev)

        this.moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            identifier: "import", args: $"./_content/Dependinator/jsInterop.js?v={version}").AsTask());
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

    public DotNetObjectReference<TValue> Instance<TValue>(TValue value) where TValue : class
    {
        // Save all instances to be disposed !!!!!
        return DotNetObjectReference.Create(value);
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