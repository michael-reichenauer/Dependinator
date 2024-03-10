namespace Dependinator.Utils;

interface IDatabase
{
    Task Init();
    Task<R> SetAsync<T>(string id, T value);
    Task<R<T>> GetAsync<T>(string id);
    Task<R> DeleteAsync(string id);
}

public record Pair<T>(string Id, T Value);


[Scoped]
class Database : IDatabase
{
    const int CurrentVersion = 1;
    const string DatabaseName = "Dependinator";
    const string CollectionName = "Store";


    readonly IJSInteropService jSInteropService;

    public Database(IJSInteropService jSInteropService)
    {
        this.jSInteropService = jSInteropService;
    }

    public async Task Init()
    {
        await jSInteropService.InitializeDatabaseAsync(DatabaseName, CurrentVersion, CollectionName);
    }

    public async Task<R> SetAsync<T>(string id, T value)
    {
        var pair = new Pair<T>(id, value);
        await jSInteropService.SetDatabaseValueAsync(DatabaseName, CurrentVersion, CollectionName, pair);
        return R.Ok;
    }

    public async Task<R<T>> GetAsync<T>(string id)
    {
        if (!Try(out var pair, out var e, await jSInteropService.GetDatabaseValueAsync<Pair<T>>(DatabaseName, CurrentVersion, CollectionName, id))) return e;

        return pair.Value;
    }

    public async Task<R> DeleteAsync(string id)
    {
        await jSInteropService.DeleteDatabaseValueAsync(DatabaseName, CurrentVersion, CollectionName, id);
        return R.Ok;
    }
}
