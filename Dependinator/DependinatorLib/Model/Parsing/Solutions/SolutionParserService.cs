using Dependinator.Model.Parsing.Common;


namespace Dependinator.Model.Parsing.Solutions;

internal class SolutionParserService : IParser
{
    readonly IDataMonitorService dataMonitorService;


    public SolutionParserService(IDataMonitorService dataMonitorService)
    {
        this.dataMonitorService = dataMonitorService;
    }


    public event EventHandler DataChanged
    {
        add => dataMonitorService.DataChangedOccurred += value;
        remove => dataMonitorService.DataChangedOccurred -= value;
    }


    public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".sln");


    public void StartMonitorDataChanges(string path)
    {
        var dataFilePaths = SolutionParser.GetDataFilePaths(path);
        dataMonitorService.StartMonitorData(path, dataFilePaths);
    }


    public async Task<R> ParseAsync(
        string path,
        Action<Node> nodeCallback,
        Action<Link> linkCallback)
    {
        using var solutionParser = new SolutionParser(path, nodeCallback, linkCallback, false);
        if (!Try(out var e, await solutionParser.ParseAsync())) return e;
        return R.Ok;
    }


    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var solutionParser = new SolutionParser(path, _ => { }, _ => { }, true);
        if (!Try(out var source, out var e, await solutionParser.TryGetSourceAsync(nodeName))) return e;

        return source;
    }


    public async Task<R<string>> GetNodeAsync(string path, Source source)
    {
        using var solutionParser = new SolutionParser(path, _ => { }, _ => { }, true);
        if (!Try(out var nodeName, out var e, await solutionParser.TryGetNodeAsync(source))) return e;
        return nodeName;
    }


    public DateTime GetDataTime(string path)
    {
        DateTime time = DateTime.MaxValue;

        foreach (string dataPath in SolutionParser.GetDataFilePaths(path).Where(File.Exists))
        {
            DateTime fileTime = File.GetLastWriteTime(dataPath);
            if (fileTime < time)
            {
                time = fileTime;
            }
        }

        // return oldest file time
        return time != DateTime.MaxValue ? time : DateTime.MinValue;
    }
}

