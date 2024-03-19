using Dependinator.Shared;

namespace Dependinator.Utils;

interface IDatabase
{
    Task Init(string[] collectionNames);
    Task<R> SetAsync<T>(string collectionName, string id, T value);
    Task<R<T>> GetAsync<T>(string collectionName, string id);
    Task<R> DeleteAsync(string collectionName, string id);
    Task<R<IReadOnlyList<string>>> GetKeysAsync(string collectionName);
}

public record Pair<T>(string Id, T Value);


[Scoped]
class Database : IDatabase
{
    const int CurrentVersion = 2;
    const string DatabaseName = "Dependinator";


    readonly IJSInteropService jSInteropService;

    public Database(IJSInteropService jSInteropService)
    {
        this.jSInteropService = jSInteropService;
    }

    public async Task Init(string[] collectionNames)
    {
        await jSInteropService.InitializeDatabaseAsync(DatabaseName, CurrentVersion, collectionNames);
    }

    public async Task<R<IReadOnlyList<string>>> GetKeysAsync(string collectionName)
    {
        if (!Try(out var keys, out var e, await jSInteropService.GetDatabaseKeysAsync(DatabaseName, collectionName))) return e;
        return keys.ToList();
    }

    public async Task<R> SetAsync<T>(string collectionName, string id, T value)
    {
        var pair = new Pair<T>(id, value);
        await jSInteropService.SetDatabaseValueAsync(DatabaseName, collectionName, pair);
        return R.Ok;
    }

    public async Task<R<T>> GetAsync<T>(string collectionName, string id)
    {
        if (!Try(out var pair, out var e, await jSInteropService.GetDatabaseValueAsync<Pair<T>>(DatabaseName, collectionName, id))) return e;

        return pair.Value;
    }

    public async Task<R> DeleteAsync(string collectionName, string id)
    {
        await jSInteropService.DeleteDatabaseValueAsync(DatabaseName, collectionName, id);
        return R.Ok;
    }
}
