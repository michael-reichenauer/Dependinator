using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Shared;

interface IFileService
{
    Task<bool> Exists(string path);
    Task<R> WriteAsync<T>(string path, T content);
    Task<R<T>> ReadAsync<T>(string path);
    Task<R> DeleteAsync(string path);
    Task<IReadOnlyList<string>> AddAsync(IReadOnlyList<IBrowserFile> browserFiles);

    R<Stream> ReadStream(string path);

    Task<R<IReadOnlyList<string>>> GetFilePathsAsync();
}

[Scoped]
class FileService : IFileService
{
    static readonly string WebFilesPrefix = "/.dependinator/web-files/";

    public static readonly string DBCollectionName = "Files";

    const long MaxFileSize = 1024 * 1024 * 10; // 10 MB
    Dictionary<string, Stream> streamsByName = [];
    readonly IDatabase database;
    readonly IEmbeddedResources embeddedResources;

    public FileService(IDatabase database, IEmbeddedResources embeddedResources)
    {
        this.database = database;
        this.embeddedResources = embeddedResources;
    }

    public async Task<bool> Exists(string path)
    {
        if (path == Models.ExampleModel.Path || streamsByName.ContainsKey(path))
            return true;

        if (!Try(out var paths, out var _, await database.GetKeysAsync(DBCollectionName)))
            return false;
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

    public async Task<IReadOnlyList<string>> AddAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        using var _ = Timing.Start($"Added {browserFiles.Count} files");

        streamsByName.Clear();

        List<string> paths = new();

        foreach (var file in browserFiles)
        {
            try
            {
                Log.Info($"Adding file: {file.Name} {file.Size}");
                using var stream = file.OpenReadStream(MaxFileSize);
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var streamPath = $"{WebFilesPrefix}{file.Name}";

                streamsByName[streamPath] = memoryStream;
                paths.Add(streamPath);
            }
            catch (Exception ex)
            {
                Log.Error($"File: {file.Name} Error: {ex.Message}");
            }
        }

        return paths;
    }

    public R<Stream> ReadStream(string path)
    {
        Log.Info("ReadStram:", path);
        if (path == Models.ExampleModel.Path)
        {
            return embeddedResources.OpenResource(Models.ExampleModel.Path);
        }

        if (path.StartsWith(WebFilesPrefix))
        {
            Log.Info("Reading Web File:", path);
            if (!streamsByName.TryGetValue(path, out var webFileStream))
                return R.None;

            //streamsByName.Remove(path);
            webFileStream.Seek(0, SeekOrigin.Begin);
            return webFileStream;
        }

        if (!Try(out var fileStream, out var e, () => File.OpenRead(path)))
            return e;
        return fileStream;
    }
}
