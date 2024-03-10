using ICSharpCode.Decompiler.CSharp.Transforms;
using Microsoft.AspNetCore.Components.Forms;

namespace Dependinator.Parsing;

interface IFileService
{
    Task<R> WriteAsync<T>(string path, T content);
    Task<R<T>> ReadAsync<T>(string path);
    Task<R> Deletesync<T>(string path);
    Task AddAsync(IReadOnlyList<IBrowserFile> browserFiles);
    R<Stream> ReadStram(string path);
    bool Exists(string assemblyPath);
}

[Scoped]
class FileService : IFileService
{
    const long maxFileSize = 1024 * 1024 * 10; // 10 MB
    Dictionary<string, Stream> streamsByName = new();
    readonly IDatabase database;

    public FileService(IDatabase database)
    {
        this.database = database;
    }


    public async Task<R> WriteAsync<T>(string path, T content)
    {
        return await database.SetAsync(path, content);
    }

    public R<Stream> ReadStram(string path)
    {
        if (!streamsByName.TryGetValue(path, out var stream)) return R.None;

        return stream;
    }

    public async Task<R<T>> ReadAsync<T>(string path)
    {
        return await database.GetAsync<T>(path);
    }

    public async Task<R> Deletesync<T>(string path)
    {
        return await database.DeleteAsync(path);
    }

    public async Task AddAsync(IReadOnlyList<IBrowserFile> browserFiles)
    {
        streamsByName.Clear();

        foreach (var file in browserFiles)
        {
            try
            {
                var memoryStream = new MemoryStream();
                var stream = file.OpenReadStream(maxFileSize);
                await stream.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                streamsByName[file.Name] = memoryStream;
                Log.Info($"File: {file.Name} {file.Size}", memoryStream.Length);
                memoryStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Log.Error($"File: {file.Name} Error: {ex.Message}");
            }
        }
    }

    public bool Exists(string assemblyPath)
    {
        return streamsByName.ContainsKey(assemblyPath);
    }
}