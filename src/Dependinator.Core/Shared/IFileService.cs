// File-access abstractions and services shared across parsers and hosts for reading and
// writing workspace, model, and parser files (IFileService and its implementations).
namespace Dependinator.Core.Shared;

public interface IFileService
{
    //Task<bool> Exists(string path);
    Task<Result> WriteAsync<T>(string path, T content);
    Task<Result<T>> ReadAsync<T>(string path)
        where T : notnull;
    Task<Result<Stream>> ReadStreamAsync(string path);
    Task<Result> DeleteAsync(string path);
    Task<Result<IReadOnlyList<string>>> GetFilePathsAsync();
}
