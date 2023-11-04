namespace Dependinator.Models;


class Model : IDisposable
{
    ModelBase rootModel;

    public Model(ModelBase rootModel)
    {
        Monitor.Enter(rootModel.SyncRoot);
        this.rootModel = rootModel;
    }

    public object SyncRoot => rootModel.SyncRoot;
    public Node Root => rootModel.Root;
    public int NodeCount => rootModel.NodeCount;
    public int LinkCount => rootModel.LinkCount;
    public int ItemCount => rootModel.Items.Count;

    public void AddOrUpdateLink(Parsing.Link parsedLink) => rootModel.AddOrUpdateLink(parsedLink);
    public void AddOrUpdateNode(Parsing.Node parsedNode) => rootModel.AddOrUpdateNode(parsedNode);
    internal void Clear() => rootModel.Clear();

    public void Dispose()
    {
        if (rootModel == null) return;
        var syncRoot = rootModel.SyncRoot;
        rootModel = null!;
        Monitor.Exit(syncRoot);
    }

    internal (string, Rect) GetSvg(Rect boundary, double zoom) => rootModel.GetSvg(boundary, zoom);
}

