using System.IO.Compression;
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
    const int CurrentVersion = 3;
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
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
            var compressedValue = CompressToBase64(jsonBytes);
            var pair = new Pair<string>(id, compressedValue);
            await jSInterop.Call("setDatabaseValue", DatabaseName, collectionName, pair);
            Log.Info(
                $"Wrote '{id}': {jsonBytes.Length}=>{compressedValue.Length} bytes ({Math.Round(100.0 * compressedValue.Length / jsonBytes.Length)}%)"
            );
            return R.Ok;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    public async Task<R<T>> GetAsync<T>(string collectionName, string id)
    {
        if (!Try(out var pair, out var e, await GetDatabaseValueAsync<Pair<string>>(DatabaseName, collectionName, id)))
            return e;
        try
        {
            var compressedValue = pair.Value;
            var jsonBytes = DecompressFromBase64(pair.Value);
            var value = JsonSerializer.Deserialize<T>(jsonBytes, options);
            if (value is null)
                return R.Error($"Deserialized null value for {DatabaseName}.{collectionName}.{id}");
            Log.Info(
                $"Read '{id}': {jsonBytes.Length}<={compressedValue.Length} bytes ({Math.Round(100.0 * compressedValue.Length / jsonBytes.Length)}%)"
            );
            return value;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
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
        var streamResult = await GetDatabaseValueWithStreamAsync<T>(databaseName, collectionName, id);
        if (streamResult.IsNone)
            return R.None;
        if (!Try(out var streamValue, out var streamError, streamResult))
            return streamError;
        return streamValue;
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
        catch (Exception)
        {
            return R.None;
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
            Log.Info("Failed to read stream", id);
            return R.Error(ex);
        }
    }

    static string CompressToBase64(byte[] jsonBytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(jsonBytes);

        return Convert.ToBase64String(output.ToArray());
    }

    static byte[] DecompressFromBase64(string base64)
    {
        var compressedBytes = Convert.FromBase64String(base64);
        using var input = new MemoryStream(compressedBytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
