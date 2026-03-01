namespace Dependinator.Core.Shared;

public interface IFileService
{
    Task<bool> Exists(string path);
    Task<R> WriteAsync<T>(string path, T content);
    Task<R<T>> ReadAsync<T>(string path);
    Task<R<Stream>> ReadStreamAsync(string path);
    Task<R> DeleteAsync(string path);
    Task<R<IReadOnlyList<string>>> GetFilePathsAsync();
}
