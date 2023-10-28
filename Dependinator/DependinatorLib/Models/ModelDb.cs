namespace Dependinator.Models;

interface IModelDb
{
    Model GetModel();
}


[Singleton]
class ModelDb : IModelDb
{
    readonly RootModel model = new();

    public Model GetModel() => new(model);
}



class Model : IDisposable
{
    RootModel rootModel;

    public Model(RootModel rootModel)
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

    internal static void SetIsModified()
    {
        throw new NotImplementedException();
    }
}
