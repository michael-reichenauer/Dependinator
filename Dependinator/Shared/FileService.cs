using DependinatorCore.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Shared;

interface IBrowserFileService
{
    Task<IReadOnlyList<string>> AddAsync(IReadOnlyList<IBrowserFile> browserFiles);
}

[Scoped]
class FileService : IBrowserFileService, IFileService
{
    public static readonly string DBCollectionName = "Files";

    const long MaxFileSize = 1024 * 1024 * 10; // 10 MB

    readonly IDatabase database;
    readonly IEmbeddedResources embeddedResources;
    readonly IHostFileSystem hostFileSystem;
    readonly IHostStoragePaths hostStoragePaths;

    public FileService(
        IDatabase database,
        IEmbeddedResources embeddedResources,
        IHostFileSystem hostFileSystem,
        IHostStoragePaths hostStoragePaths
    )
    {
        this.database = database;
        this.embeddedResources = embeddedResources;
        this.hostFileSystem = hostFileSystem;
        this.hostStoragePaths = hostStoragePaths;
    }

    public async Task<bool> Exists(string path)
    {
        if (path == ExampleModel.Path)
            return true;

        if (hostFileSystem.Exists(path))
            return true;

        if (!Try(out var paths, out var _, await database.GetKeysAsync(DBCollectionName)))
            return false;
        return paths.Contains(path) || paths.Contains(BinPath(path));
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
        var binPath = BinPath(path);
        await database.DeleteAsync(DBCollectionName, binPath);
        return await database.DeleteAsync(DBCollectionName, path);
    }

    public async Task<IReadOnlyList<string>> AddAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        using var _ = Timing.Start($"Added {browserFiles.Count} files");

        List<string> paths = [];

        foreach (var file in browserFiles)
        {
            try
            {
                Log.Info($"Adding file: {file.Name} {file.Size}");
                using var webFileStream = file.OpenReadStream(MaxFileSize);
                var filesStream = new MemoryStream();
                await webFileStream.CopyToAsync(filesStream);
                var modelPath = $"{hostStoragePaths.WebFilesPrefix}{file.Name}";
                var binPath = BinPath(modelPath);

                var fileBytes = filesStream.ToArray();
                var fileBase64 = Convert.ToBase64String(fileBytes);
                await WriteAsync(binPath, fileBase64);

                paths.Add(modelPath);
            }
            catch (Exception ex)
            {
                Log.Error($"File: {file.Name} Error: {ex.Message}");
            }
        }

        return paths;
    }

    public async Task<R<Stream>> ReadStreamAsync(string path)
    {
        Log.Info("ReadStream:", path);
        if (path == ExampleModel.Path)
        {
            return embeddedResources.OpenResourceStream(ExampleModel.Path);
        }

        if (path.StartsWith(hostStoragePaths.WebFilesPrefix))
        {
            var binPath = BinPath(path);
            if (!Try(out var fileBase64, out var e, await ReadAsync<string>(binPath)))
                return e;
            var bytes = Convert.FromBase64String(fileBase64);
            var filesStream = new MemoryStream(bytes, writable: false);
            filesStream.Seek(0, SeekOrigin.Begin);
            return filesStream;
        }

        if (!Try(out var fileStream, out var e2, () => hostFileSystem.OpenRead(path)))
            return e2;
        return fileStream;
    }

    string BinPath(string path) => $"{path}.bin";
}
