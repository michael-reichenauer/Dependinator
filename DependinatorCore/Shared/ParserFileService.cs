namespace DependinatorCore.Shared;

public interface IParserFileService
{
    Task<bool> ExistsAsync(string path);
    Task<R<Stream>> ReadStreamAsync(string path);
}

[Transient]
public class ParserFileService() : IParserFileService
{
    public async Task<bool> ExistsAsync(string path)
    {
        await Task.CompletedTask;

        return File.Exists(path);
    }

    public async Task<R<Stream>> ReadStreamAsync(string path)
    {
        await Task.CompletedTask;

        return File.OpenRead(path);
    }
}
