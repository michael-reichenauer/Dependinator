namespace Dependinator.Parsing.Assemblies;

[Transient]
internal class AssemblyParserService : IParser
{
    readonly IStreamService streamService;
    readonly IEmbeddedResources embeddedResources;

    public AssemblyParserService(IStreamService streamService, IEmbeddedResources embeddedResources)
    {
        this.streamService = streamService;
        this.embeddedResources = embeddedResources;
    }

    public bool CanSupport(string path) =>
        Path.GetExtension(path).IsSameIc(".exe") || Path.GetExtension(path).IsSameIc(".dll");

    public async Task<R> ParseAsync(string path, IItems items)
    {
        using var parser = new AssemblyParser(embeddedResources, path, "", "", items, false, streamService);
        return await parser.ParseAsync();
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var parser = new AssemblyParser(embeddedResources, path, "", "", null!, true, streamService);
        return await Task.Run(() => parser.TryGetSource(nodeName));
    }

    public Task<R<string>> GetNodeAsync(string path, Source source) => Task.FromResult((R<string>)"");

    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);
}
