using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Common;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.Model.Parsing.Solutions;

internal class SolutionParserService : IParser
{
    private readonly IDataMonitorService dataMonitorService;


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


    public async Task ParseAsync(
        string path,
        Action<NodeData> nodeCallback,
        Action<LinkData> linkCallback)
    {
        using (SolutionParser solutionParser = new SolutionParser(path, nodeCallback, linkCallback, false))
        {
            M result = await solutionParser.ParseAsync();

            if (result.IsFaulted)
            {
                throw new Exception(result.ErrorMessage);
            }
        }
    }


    public async Task<NodeDataSource> GetSourceAsync(string path, string nodeName)
    {
        using (SolutionParser solutionParser = new SolutionParser(path, null, null, true))
        {
            M<NodeDataSource> source = await solutionParser.TryGetSourceAsync(nodeName);

            if (source.IsFaulted)
            {
                throw new Exception(source.ErrorMessage);
            }

            return source.Value;
        }
    }


    public async Task<string> GetNodeAsync(string path, NodeDataSource source)
    {
        using (SolutionParser solutionParser = new SolutionParser(path, null, null, true))
        {
            M<string> nodeName = await solutionParser.TryGetNodeAsync(source);

            if (nodeName.IsFaulted)
            {
                throw new Exception(nodeName.ErrorMessage);
            }

            return nodeName.Value;
        }
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

