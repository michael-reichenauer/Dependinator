using Dependinator.Core.Tests.Parsing.Utils;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Models;

public class ModelTestData
{
    public int number;

    public void FirstFunction()
    {
        var a = number;
    }

    public void SecondFunction() { }
}

public class ModelTests
{
    [Fact]
    public async Task TestAsync()
    {
        var items = new ItemsMock();
        await TestHelper.ParseType<ModelTestData>(items);
        var modelMgr = new ModelMgr(new StateMgr());

        var modelDto = modelMgr.WithModel(model =>
        {
            TestHelper.AddItems(model, items);
            return model.SerializeToDto();
        });

        await VerifyJson(modelDto);
    }

    [Fact]
    public async Task TestParsingAsync()
    {
        var items = new ItemsMock();
        await TestHelper.ParseType<ModelTestData>(items);
        var dto = new ModelDto(items.Nodes.ToList(), items.Links.ToList());
        await VerifyJson(dto);
    }

    record ModelDto(
        IReadOnlyList<Dependinator.Core.Parsing.Node> Nodes,
        IReadOnlyList<Dependinator.Core.Parsing.Link> Links
    );

    [Fact]
    public void RemoveLink_LinkWithoutLines_ShouldDetachFromEndpointNodes()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var (source, target) = AddSiblingNodes(model);

        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);

        model.RemoveLink(link);

        Assert.False(model.Links.ContainsKey(link.Id));
        Assert.DoesNotContain(link, source.SourceLinks);
        Assert.DoesNotContain(link, target.TargetLinks);
    }

    [Fact]
    public void RemoveLink_LinkWithLines_ShouldRemoveEmptyLinesAndClearLineReferences()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var (source, target) = AddSiblingNodes(model);

        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new Dependinator.UI.Modeling.LineService().AddLinesFromSourceToTarget(model, link);
        Assert.Single(link.Lines);

        model.RemoveLink(link);

        Assert.False(model.Links.ContainsKey(link.Id));
        Assert.False(model.Lines.ContainsKey(LineId.From("Parent.Source", "Parent.Target")));
        Assert.Empty(link.Lines);
        Assert.Empty(source.SourceLines);
        Assert.Empty(target.TargetLines);
        Assert.DoesNotContain(link, source.SourceLinks);
        Assert.DoesNotContain(link, target.TargetLinks);
    }

    static (Node Source, Node Target) AddSiblingNodes(IModel model)
    {
        var parent = new Node("Parent", model.Root);
        model.TryAddNode(parent);
        model.Root.AddChild(parent);

        var source = new Node("Parent.Source", parent);
        var target = new Node("Parent.Target", parent);
        parent.AddChild(source);
        parent.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        return (source, target);
    }
}
