namespace Dependinator.Parsing;

interface IFileService
{
    Task<R> WriteAsync<T>(string path, T content);
    Task<R<T>> ReadAsync<T>(string path);
    Task<R> Deletesync<T>(string path);
}

[Transient]
class FileService : IFileService
{
    readonly IDatabase database;

    public FileService(IDatabase database)
    {
        this.database = database;
    }

    public async Task<R> WriteAsync<T>(string path, T content)
    {
        return await database.SetAsync(path, content);
    }

    public async Task<R<T>> ReadAsync<T>(string path)
    {
        return await database.GetAsync<T>(path);
    }

    public async Task<R> Deletesync<T>(string path)
    {
        return await database.DeleteAsync(path);
    }

}