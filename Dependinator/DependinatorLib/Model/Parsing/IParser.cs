namespace Dependinator.Model.Parsing;

public interface IParser
{
    event EventHandler DataChanged;

    bool CanSupport(string path);

    void StartMonitorDataChanges(string path);

    Task<R> ParseAsync(string path, Action<NodeData> nodeCallback, Action<LinkData> linkCallback);

    Task<R<NodeDataSource>> GetSourceAsync(string path, string nodeName);

    Task<string> GetNodeAsync(string path, NodeDataSource source);

    DateTime GetDataTime(string path);
}

