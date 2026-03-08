using Dependinator.Models;

namespace Dependinator.Tests.Models;

public class LineServiceTests
{
    [Fact]
    public void AddLinesFromSourceToTarget_ShouldCreateSingleLineForSiblings()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new LineService();

        var parent = new Node("Parent", model.Root);
        model.TryAddNode(parent);
        model.Root.AddChild(parent);

        var source = new Node("Source", parent);
        var target = new Node("Target", parent);
        parent.AddChild(source);
        parent.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        var link = new Link(source, target);
        lineService.AddLinesFromSourceToTarget(model, link);

        Assert.True(model.Lines.TryGetValue(LineId.From("Source", "Target"), out var line));
        Assert.Single(link.Lines);
        Assert.Single(line.Links);
    }

    [Fact]
    public void AddLinesFromSourceToTarget_ShouldConnectAcrossParents()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new LineService();

        var parentA = new Node("ParentA", model.Root);
        var parentB = new Node("ParentB", model.Root);
        model.Root.AddChild(parentA);
        model.Root.AddChild(parentB);
        model.TryAddNode(parentA);
        model.TryAddNode(parentB);

        var source = new Node("Source", parentA);
        var target = new Node("Target", parentB);
        parentA.AddChild(source);
        parentB.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        var link = new Link(source, target);
        lineService.AddLinesFromSourceToTarget(model, link);

        Assert.Equal(3, link.Lines.Count);
        Assert.True(model.Lines.TryGetValue(LineId.From("Source", "ParentA"), out _));
        Assert.True(model.Lines.TryGetValue(LineId.From("ParentB", "Target"), out _));
        Assert.True(model.Lines.TryGetValue(LineId.From("ParentA", "ParentB"), out _));
    }
}
