using Dependinator.Core.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.UI.Shared;

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
    readonly IHostFileSystem hostFileSystem;
    readonly IHostStoragePaths hostStoragePaths;

    public FileService(IDatabase database, IHostFileSystem hostFileSystem, IHostStoragePaths hostStoragePaths)
    {
        this.database = database;
        this.hostFileSystem = hostFileSystem;
        this.hostStoragePaths = hostStoragePaths;
    }

    public async Task<Result<IReadOnlyList<string>>> GetFilePathsAsync()
    {
        return await database.GetKeysAsync(DBCollectionName);
    }

    public async Task<Result> WriteAsync<T>(string path, T content)
    {
        return await database.SetAsync(DBCollectionName, path, content);
    }

    public async Task<Result<T>> ReadAsync<T>(string path)
        where T : notnull
    {
        return await database.GetAsync<T>(DBCollectionName, path);
    }

    public async Task<Result> DeleteAsync(string path)
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

    public async Task<Result<Stream>> ReadStreamAsync(string path)
    {
        Log.Info("ReadStream:", path);

        if (path.StartsWith(hostStoragePaths.WebFilesPrefix))
        {
            var binPath = BinPath(path);
            var base64Result = await ReadAsync<string>(binPath);
            if (base64Result is not string fileBase64)
                return base64Result.Error;
            var bytes = Convert.FromBase64String(fileBase64);
            var filesStream = new MemoryStream(bytes, writable: false);
            filesStream.Seek(0, SeekOrigin.Begin);
            return filesStream;
        }

        return Result.Catch(() => hostFileSystem.OpenRead(path));
    }

    string BinPath(string path) => $"{path}.bin";
}
