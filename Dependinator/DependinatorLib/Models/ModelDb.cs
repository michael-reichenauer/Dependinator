namespace Dependinator.Models;

interface IModelDb
{
    IModel GetModel();
}


[Singleton]
class ModelDb : IModelDb
{
    readonly ModelBase model = new();

    public IModel GetModel() => new ModelTransaction(model);
}




class ModelTransaction : IModel
{
    ModelBase rootModel;

    public ModelTransaction(ModelBase rootModel)
    {
        Monitor.Enter(rootModel.SyncRoot);
        this.rootModel = rootModel;
    }

    public object SyncRoot => rootModel.SyncRoot;
    public Node Root => rootModel.Root;

    public void AddOrUpdateLink(Parsing.Link parsedLink) => rootModel.AddOrUpdateLink(parsedLink);
    public void AddOrUpdateNode(Parsing.Node parsedNode) => rootModel.AddOrUpdateNode(parsedNode);
    public void AddLine(Line line) => rootModel.AddLine(line);
    public Node GetOrCreateNode(string name) => rootModel.GetOrCreateNode(name);
    public void Clear() => rootModel.Clear();

    public void Dispose()
    {
        if (rootModel == null) return;
        var syncRoot = rootModel.SyncRoot;
        rootModel = null!;
        Monitor.Exit(syncRoot);
    }
}

