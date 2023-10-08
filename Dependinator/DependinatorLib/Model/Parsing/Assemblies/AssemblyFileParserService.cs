using Dependinator.Model.Parsing;
using Dependinator.Model.Parsing.Common;


namespace Dependinator.Model.Parsers.Assemblies;

internal class AssemblyFileParserService : IParser
{
    private readonly IDataMonitorService dataMonitorService;


    public AssemblyFileParserService(IDataMonitorService dataMonitorService)
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
        Action<NodeData> nodeCallback,
        Action<LinkData> linkCallback)
    {
        NodeData fileNode = GetAssemblyFileNode(path);
        nodeCallback(fileNode);

        using (AssemblyParser assemblyParser = new AssemblyParser(
            path, null, fileNode.Name, nodeCallback, linkCallback, false))
        {
            return await assemblyParser.ParseAsync();
        }
    }


    public async Task<R<NodeDataSource>> GetSourceAsync(string path, string nodeName)
    {
        using (AssemblyParser assemblyParser = new AssemblyParser(
            path, null, null, null, null, true))
        {
            return await Task.Run(() => assemblyParser.TryGetSource(nodeName));
        }
    }


    public Task<string> GetNodeAsync(string path, NodeDataSource source) =>
        Task.FromResult((string)null);


    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);


    private static NodeData GetAssemblyFileNode(string path)
    {
        string name = Path.GetFileName(path).Replace(".", "*");
        if (name == "Example*exe")
        {
            // Special case for the example project to fix layout problem
            name = "Dependinator*sln";
        }

        return new NodeData(name, null, NodeData.AssemblyType, "Assembly file");
    }
}

