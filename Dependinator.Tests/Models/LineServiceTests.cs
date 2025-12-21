using Dependinator.Models;

namespace Dependinator.Tests.Models;

public class LineServiceTests
{
    [Fact]
    public void AddLinesFromSourceToTarget_ShouldCreateSingleLineForSiblings()
    {
        var model = new Model();
        var lineService = new LineService(model);

        var parent = new Node("Parent", model.Root);
        model.AddNode(parent);
        model.Root.AddChild(parent);

        var source = new Node("Source", parent);
        var target = new Node("Target", parent);
        parent.AddChild(source);
        parent.AddChild(target);
        model.AddNode(source);
        model.AddNode(target);

        var link = new Link(source, target);
        lineService.AddLinesFromSourceToTarget(link);

        Assert.True(model.TryGetLine(LineId.From("Source", "Target"), out var line));
        Assert.Single(link.Lines);
        Assert.Single(line.Links);
    }

    [Fact]
    public void AddLinesFromSourceToTarget_ShouldConnectAcrossParents()
    {
        var model = new Model();
        var lineService = new LineService(model);

        var parentA = new Node("ParentA", model.Root);
        var parentB = new Node("ParentB", model.Root);
        model.Root.AddChild(parentA);
        model.Root.AddChild(parentB);
        model.AddNode(parentA);
        model.AddNode(parentB);

        var source = new Node("Source", parentA);
        var target = new Node("Target", parentB);
        parentA.AddChild(source);
        parentB.AddChild(target);
        model.AddNode(source);
        model.AddNode(target);

        var link = new Link(source, target);
        lineService.AddLinesFromSourceToTarget(link);

        Assert.Equal(3, link.Lines.Count);
        Assert.True(model.TryGetLine(LineId.From("Source", "ParentA"), out _));
        Assert.True(model.TryGetLine(LineId.From("ParentB", "Target"), out _));
        Assert.True(model.TryGetLine(LineId.From("ParentA", "ParentB"), out _));
    }
}
