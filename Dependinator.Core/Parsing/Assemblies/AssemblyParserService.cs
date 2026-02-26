using Dependinator.Core.Parsing.Utils;
using Dependinator.Core.Shared;

namespace Dependinator.Core.Parsing.Assemblies;

[Transient]
internal class AssemblyParserService : IParser
{
    readonly IParserFileService fileService;

    public AssemblyParserService(IParserFileService fileService)
    {
        this.fileService = fileService;
    }

    public bool CanSupport(string path) =>
        Path.GetExtension(path).IsSameIc(".exe") || Path.GetExtension(path).IsSameIc(".dll");

    public async Task<R> ParseAsync(string path, IItems items)
    {
        if (!Try(out var parser, out var e, await AssemblyParser.CreateAsync(path, "", "", items, false, fileService)))
            return e;
        using var p = parser;
        return await p.ParseAsync();
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        if (!Try(out var parser, out var e, await AssemblyParser.CreateAsync(path, "", "", null!, true, fileService)))
            return e;
        using var p = parser;
        return await Task.Run(() => p.TryGetSource(nodeName));
    }

    public Task<R<string>> GetNodeAsync(string path, FileLocation fileLocation) => Task.FromResult((R<string>)"");

    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);
}
