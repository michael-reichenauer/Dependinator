using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

interface IDatabase
{
    Task Init(string[] collectionNames);
    Task<R> SetAsync<T>(string collectionName, string id, T value);
    Task<R<T>> GetAsync<T>(string collectionName, string id);
    Task<R> DeleteAsync(string collectionName, string id);
    Task<R<IReadOnlyList<string>>> GetKeysAsync(string collectionName);
}

[Scoped]
class Database : IDatabase
{
    const int CurrentVersion = 2;
    const string DatabaseName = "Dependinator";
    static readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    record Pair<T>(string Id, T Value);

    readonly IJSInterop jSInterop;

    public Database(IJSInterop jSInterop)
    {
        this.jSInterop = jSInterop;
    }

    public async Task Init(string[] collectionNames)
    {
        await jSInterop.Call("initializeDatabase", DatabaseName, CurrentVersion, collectionNames);
    }

    public async Task<R<IReadOnlyList<string>>> GetKeysAsync(string collectionName)
    {
        var keys = await jSInterop.Call<IReadOnlyList<string>>("getDatabaseAllKeys", DatabaseName, collectionName);
        return keys?.ToList() ?? [];
    }

    public async Task<R> SetAsync<T>(string collectionName, string id, T value)
    {
        var pair = new Pair<T>(id, value);
        await jSInterop.Call("setDatabaseValue", DatabaseName, collectionName, pair);
        return R.Ok;
    }

    public async Task<R<T>> GetAsync<T>(string collectionName, string id)
    {
        if (!Try(out var pair, out var e, await GetDatabaseValueAsync<Pair<T>>(DatabaseName, collectionName, id)))
            return e;
        return pair.Value;
    }

    public async Task<R> DeleteAsync(string collectionName, string id)
    {
        await jSInterop.Call("deleteDatabaseValue", DatabaseName, collectionName, id);
        return R.Ok;
    }

    private async ValueTask<R<T>> GetDatabaseValueAsync<T>(string databaseName, string collectionName, string id)
    {
        using var _ = Timing.Start($"Got {databaseName}.{collectionName}.{id} ...");
        Log.Info($"Getting {databaseName}.{collectionName}.{id} ...");
        // For big values, the normal JSInterop call cannot handle big return values,
        // so we use a value handler, where values are returned in chunks using callback from js
        var valueHandler = new ValueHandler();
        using var valueHandlerRef = jSInterop.Reference(valueHandler);

        var result = await jSInterop.Call<bool>(
            "getDatabaseValue",
            databaseName,
            collectionName,
            id,
            valueHandlerRef,
            nameof(valueHandler.OnValue)
        );
        if (!result)
            return R.None;
        var valueText = valueHandler.GetValue();
        Log.Info($"Got value {valueText.Length} bytes");
        if (!Try(out var value, out var e, () => JsonSerializer.Deserialize<T>(valueText, options)))
            return e;
        return value!;
    }

    // This class is used when a javascript function returns a value that could be larger than 20k
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
}
