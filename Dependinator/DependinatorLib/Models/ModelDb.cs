namespace Dependinator.Models;

interface IModelDb
{
    ModelContext GetModel();
}


[Singleton]
class ModelDb : IModelDb
{
    readonly Model model = new();

    public ModelContext GetModel() => new(model);
}

class ModelContext : IDisposable
{
    public ModelContext(Model model)
    {
        Monitor.Enter(model.SyncRoot);
        Model = model;
    }

    public Model Model { get; private set; }

    public void Dispose()
    {
        if (Model == null) return;
        var syncRoot = Model.SyncRoot;
        Model = null!;
        Monitor.Exit(syncRoot);
    }
}

