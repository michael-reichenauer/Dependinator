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
    const long MaxReadStreamSizeBytes = 1024L * 1024 * 50; // 50 MB
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
        try
        {
            var keys = await jSInterop.Call<IReadOnlyList<string>>("getDatabaseAllKeys", DatabaseName, collectionName);
            return keys?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    public async Task<R> SetAsync<T>(string collectionName, string id, T value)
    {
        try
        {
            var pair = new Pair<T>(id, value);
            await jSInterop.Call("setDatabaseValue", DatabaseName, collectionName, pair);
            return R.Ok;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    public async Task<R<T>> GetAsync<T>(string collectionName, string id)
    {
        if (!Try(out var pair, out var e, await GetDatabaseValueAsync<Pair<T>>(DatabaseName, collectionName, id)))
            return e;
        return pair.Value;
    }

    public async Task<R> DeleteAsync(string collectionName, string id)
    {
        try
        {
            await jSInterop.Call("deleteDatabaseValue", DatabaseName, collectionName, id);
            return R.Ok;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    private async ValueTask<R<T>> GetDatabaseValueAsync<T>(string databaseName, string collectionName, string id)
    {
        using var timing = Timing.Start($"Got {databaseName}.{collectionName}.{id} ...");
        Log.Info($"Getting {databaseName}.{collectionName}.{id} ...");

        var streamResult = await GetDatabaseValueWithStreamAsync<T>(databaseName, collectionName, id);
        if (streamResult.IsNone)
            return R.None;
        if (Try(out var streamValue, out var streamError, streamResult))
            return streamValue;

        Log.Info(
            $"Stream interop failed for {databaseName}.{collectionName}.{id}. "
                + $"Reason: {streamError?.ErrorMessage}. Falling back to chunked interop."
        );
        return await GetDatabaseValueWithChunkCallbackAsync<T>(databaseName, collectionName, id);
    }

    private async ValueTask<R<T>> GetDatabaseValueWithStreamAsync<T>(
        string databaseName,
        string collectionName,
        string id
    )
    {
        IJSStreamReference? valueStreamRef;
        try
        {
            valueStreamRef = await jSInterop.Call<IJSStreamReference?>(
                "getDatabaseValueStream",
                databaseName,
                collectionName,
                id
            );
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }

        if (valueStreamRef is null)
            return R.None;

        try
        {
            await using var _ = valueStreamRef;
            await using var stream = await valueStreamRef.OpenReadStreamAsync(MaxReadStreamSizeBytes);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, options);
            if (value is null)
                return R.Error($"Deserialized null value for {databaseName}.{collectionName}.{id}");

            return value;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    private async ValueTask<R<T>> GetDatabaseValueWithChunkCallbackAsync<T>(
        string databaseName,
        string collectionName,
        string id
    )
    {
        // Fallback path for runtimes where stream interop fails.
        var valueHandler = new ValueHandler();
        using var valueHandlerRef = jSInterop.Reference(valueHandler);

        bool hasValue;
        try
        {
            hasValue = await jSInterop.Call<bool>(
                "getDatabaseValue",
                databaseName,
                collectionName,
                id,
                valueHandlerRef,
                nameof(valueHandler.OnValue)
            );
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }

        if (!hasValue)
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
        readonly object sync = new();

        public string GetValue()
        {
            lock (sync)
            {
                return sb.ToString();
            }
        }

        [JSInvokable]
        public ValueTask OnValue(string value)
        {
            lock (sync)
            {
                sb.Append(value);
            }
            return ValueTask.CompletedTask;
        }
    }
}
