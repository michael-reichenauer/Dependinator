namespace Dependinator.Modeling.Models;

interface IModelMgr
{
    string ModelPath { get; }
    IModel UseModel();
    void WithModel(Action<IModel> modelAction);
    TResult WithModel<TResult>(Func<IModel, TResult> modelFunction);

    bool TryWithNode(NodeId nodeId, Action<Node> action);
    bool TryWithNode(NodeId nodeId, Func<Node, bool> action);
    bool TryWithLink(LinkId linkId, Action<Link> action);
    bool TryWithLink(LinkId linkId, Func<Link, bool> action);
    bool TryWithLine(LineId lineId, Action<Line> action);
    bool TryWithLine(LineId lineId, Func<Line, bool> action);
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

    public bool TryWithNode(NodeId nodeId, Action<Node> action)
    {
        using var model = UseModel();
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return false;
        action(node);
        return true;
    }

    public bool TryWithNode(NodeId nodeId, Func<Node, bool> action)
    {
        using var model = UseModel();
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return false;
        return action(node);
    }

    public bool TryWithLink(LinkId linkId, Action<Link> action)
    {
        using var model = UseModel();
        if (!model.Links.TryGetValue(linkId, out var link))
            return false;
        action(link);
        return true;
    }

    public bool TryWithLink(LinkId linkId, Func<Link, bool> action)
    {
        using var model = UseModel();
        if (!model.Links.TryGetValue(linkId, out var link))
            return false;
        return action(link);
    }

    public bool TryWithLine(LineId lineId, Action<Line> action)
    {
        using var model = UseModel();
        if (!model.Lines.TryGetValue(lineId, out var line))
            return false;
        action(line);
        return true;
    }

    public bool TryWithLine(LineId lineId, Func<Line, bool> action)
    {
        using var model = UseModel();
        if (!model.Lines.TryGetValue(lineId, out var line))
            return false;
        return action(line);
    }
}
