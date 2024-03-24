using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Shared;

interface IFileService
{
    Task<bool> Exists(string path);
    Task<R> WriteAsync<T>(string path, T content);
    Task<R<T>> ReadAsync<T>(string path);
    Task<R> DeleteAsync(string path);
    Task AddAsync(IReadOnlyList<IBrowserFile> browserFiles);

    R<Stream> ReadStram(string path);
    bool ExistsStream(string assemblyPath);

    Task<R<IReadOnlyList<string>>> GetFilePathsAsync();
}

[Scoped]
class FileService : IFileService
{
    public static readonly string DBCollectionName = "Files";

    const long maxFileSize = 1024 * 1024 * 10; // 10 MB
    Dictionary<string, Stream> streamsByName = new();
    readonly IDatabase database;

    public FileService(IDatabase database)
    {
        this.database = database;
    }

    public async Task<bool> Exists(string path)
    {
        if (!Try(out var paths, out var _, await database.GetKeysAsync(DBCollectionName))) return false;
        return paths.Contains(path);
    }

    public async Task<R<IReadOnlyList<string>>> GetFilePathsAsync()
    {
        return await database.GetKeysAsync(DBCollectionName);
    }

    public async Task<R> WriteAsync<T>(string path, T content)
    {
        return await database.SetAsync(DBCollectionName, path, content);
    }


    public async Task<R<T>> ReadAsync<T>(string path)
    {
        return await database.GetAsync<T>(DBCollectionName, path);
    }

    public async Task<R> DeleteAsync(string path)
    {
        return await database.DeleteAsync(DBCollectionName, path);
    }

    public async Task AddAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        using var _ = Timing.Start($"Added {browserFiles.Count} files");

        streamsByName.Clear();

        foreach (var file in browserFiles)
        {
            try
            {
                Log.Info($"Adding file: {file.Name} {file.Size}");
                using var stream = file.OpenReadStream(maxFileSize);
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                streamsByName[file.Name] = memoryStream;
                memoryStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Log.Error($"File: {file.Name} Error: {ex.Message}");
            }
        }
    }

    public R<Stream> ReadStram(string path)
    {
        if (!streamsByName.TryGetValue(path, out var stream)) return R.None;

        return stream;
    }

    public bool ExistsStream(string assemblyPath)
    {
        return streamsByName.ContainsKey(assemblyPath);
    }
}