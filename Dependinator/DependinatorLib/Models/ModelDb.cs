namespace Dependinator.Models;

interface IModelDb
{
    Model GetModel();
}

class Model : IDisposable
{
    readonly Action release;

    public Model(IDictionary<string, IItem> items, Action release)
    {
        Items = items;
        this.release = release;
    }

    public IDictionary<string, IItem> Items { get; }

    public void Dispose() => release();
}


[Singleton]
class ModelDb : IModelDb
{
    readonly object rootLock = new object();
    readonly IDictionary<string, IItem> items = new Dictionary<string, IItem>();

    public Model GetModel()
    {
        Monitor.Enter(rootLock);
        return new Model(items, () => Monitor.Exit(rootLock));
    }
}
