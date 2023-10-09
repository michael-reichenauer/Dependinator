using Dependinator.Model.Parsing;
using Dependinator.Model.Parsing.Common;


namespace Dependinator.Model.Parsers.Assemblies;

internal class AssemblyParserService : IParser
{
    private readonly IDataMonitorService dataMonitorService;


    public AssemblyParserService(IDataMonitorService dataMonitorService)
    {
        this.dataMonitorService = dataMonitorService;
    }

    public event EventHandler DataChanged
    {
        add => dataMonitorService.DataChangedOccurred += value;
        remove => dataMonitorService.DataChangedOccurred -= value;
    }


    public bool CanSupport(string path) =>
        Path.GetExtension(path).IsSameIc(".exe") ||
        Path.GetExtension(path).IsSameIc(".dll");


    public void StartMonitorDataChanges(string path) => dataMonitorService.StartMonitorData(path, new[] { path });


    public async Task<R> ParseAsync(
        string path,
        Action<Node> nodeCallback,
        Action<Link> linkCallback)
    {
        Node assemblyNode = CreateAssemblyNode(path);
        nodeCallback(assemblyNode);

        using var parser = new AssemblyParser(path, "", assemblyNode.Name, nodeCallback, linkCallback, false);
        return await parser.ParseAsync();
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var parser = new AssemblyParser(path, "", "", _ => { }, _ => { }, true);
        return await Task.Run(() => parser.TryGetSource(nodeName));
    }


    public Task<string> GetNodeAsync(string path, Source source) =>
        Task.FromResult("");


    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);


    private static Node CreateAssemblyNode(string path)
    {
        string name = Path.GetFileName(path).Replace(".", "*");
        if (name == "Example*exe")
        {
            // Special case for the example project to fix layout problem
            name = "Dependinator*sln";
        }

        return new Node(name, "", Node.AssemblyType, "Assembly file");
    }
}

