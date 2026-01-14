namespace DependinatorCore.Shared;

public interface IParserFileService
{
    Task<bool> ExistsAsync(string path);
    Task<R<Stream>> ReadStreamAsync(string path);
}

[Transient]
public class ParserFileService(IEmbeddedResources embeddedResources) : IParserFileService
{
    public async Task<bool> ExistsAsync(string path)
    {
        await Task.CompletedTask;

        return path == ExampleModel.EmbeddedExample || File.Exists(path);
    }

    public async Task<R<Stream>> ReadStreamAsync(string path)
    {
        await Task.CompletedTask;

        if (path == ExampleModel.EmbeddedExample)
            return embeddedResources.OpenResourceStream(ExampleModel.EmbeddedExample);

        return File.OpenRead(path);
    }
}
