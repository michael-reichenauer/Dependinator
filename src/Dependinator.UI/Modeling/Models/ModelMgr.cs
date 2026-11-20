namespace Dependinator.UI.Modeling.Models;

interface IModelMgr
{
    string ModelPath { get; }
    IModel UseModel();
    void WithModel(Action<IModel> modelAction);
    TResult WithModel<TResult>(Func<IModel, TResult> modelFunction);
}

[Scoped]
class ModelMgr(IStateMgr stateMgr) : IModelMgr, IDisposable
{
    private readonly IStateMgr stateMgr = stateMgr;

    readonly IModel model = new Model(stateMgr.Exit);

    // Lets go of the model's nodes, links and lines when the scope ends. The Blazor Server host
    // keeps a closed tab's whole circuit alive while a framework timer is pending (each JS stream
    // the cached model is read through leaves a JSInteropDefaultCallTimeout delay, 5 minutes in
    // the web host), so without this every closed tab held its model for minutes: parallel e2e
    // tests grew the host by gigabytes. A scope ends only once its browser is gone, so no save
    // can write the emptied model: its JS call fails as disconnected.
    public void Dispose() => WithModel(m => m.Clear());

    public string ModelPath => WithModel(m => m.Path);

    public IModel UseModel()
    {
        stateMgr.Enter();
        return model;
    }

    public void WithModel(Action<IModel> modelAction)
    {
        using var model = UseModel();
        modelAction(model);
    }

    public TResult WithModel<TResult>(Func<IModel, TResult> modelFunction)
    {
        using var model = UseModel();
        return modelFunction(model);
    }
}
