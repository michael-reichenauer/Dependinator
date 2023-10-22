namespace Dependinator.Models;

interface IModelDb
{
    ModelContext GetModel();
}



class ModelContext : IDisposable
{
    public ModelContext(Model model)
    {
        Monitor.Enter(model);
        Model = model;
    }

    public Model Model { get; private set; }

    public void Dispose()
    {
        if (Model == null) return;
        Monitor.Exit(Model.SyncRoot);
        Model = null!;
    }
}



[Singleton]
class ModelDb : IModelDb
{
    readonly Model model = new();

    public ModelContext GetModel() => new(model);
}

