namespace Dependinator.UI.Modeling.Models;

interface IModelMgr
{
    string ModelPath { get; }
    IModel UseModel();
    void WithModel(Action<IModel> modelAction);
    TResult WithModel<TResult>(Func<IModel, TResult> modelFunction);
}

[Scoped]
class ModelMgr(IStateMgr stateMgr) : IModelMgr
{
    private readonly IStateMgr stateMgr = stateMgr;

    readonly IModel model = new Model(stateMgr.Exit);

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
