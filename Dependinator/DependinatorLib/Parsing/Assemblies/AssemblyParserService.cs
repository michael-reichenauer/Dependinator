using System.Threading.Channels;

namespace Dependinator.Parsing.Assemblies;

[Transient]
internal class AssemblyParserService : IParser
{
    public bool CanSupport(string path) =>
        Path.GetExtension(path).IsSameIc(".exe") ||
        Path.GetExtension(path).IsSameIc(".dll");


    public async Task<R> ParseAsync(string path, ChannelWriter<IItem> items)
    {
        Node assemblyNode = CreateAssemblyNode(path);
        await items.WriteAsync(assemblyNode);

        using var parser = new AssemblyParser(path, "", assemblyNode.Name, items, false);
        return await parser.ParseAsync();
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var parser = new AssemblyParser(path, "", "", null!, true);
        return await Task.Run(() => parser.TryGetSource(nodeName));
    }


    public Task<R<string>> GetNodeAsync(string path, Source source) =>
        Task.FromResult((R<string>)"");


    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);


    private static Node CreateAssemblyNode(string path)
    {
        string name = Path.GetFileName(path).Replace(".", "*");
        if (name == "Example*exe")
        {   // Special case for the example project to fix layout problem
            name = "Dependinator*sln";
        }

        return new Node(name, "", NodeType.AssemblyType, "Assembly file", true);
    }
}

