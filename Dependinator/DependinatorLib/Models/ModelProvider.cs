namespace Dependinator.Models;

interface IModelProvider
{
    IModel GetModel();
}


[Singleton]
class ModelProvider : IModelProvider
{
    readonly Model model = new();

    public IModel GetModel() => new ModelTransaction(model);
}




class ModelTransaction : IModel
{
    Model rootModel;

    public ModelTransaction(Model rootModel)
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

