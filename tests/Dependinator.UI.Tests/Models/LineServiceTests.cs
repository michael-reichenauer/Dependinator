using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Models;

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

    [Fact]
    public void AddLinesFromSourceToTarget_ShouldCreateInheritanceLineForSiblings()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new LineService();

        var parent = AddNode(model, "Parent", model.Root);
        var source = AddNode(model, "Source", parent);
        var target = AddNode(model, "Target", parent);

        var link = new Link(source, target) { IsInheritance = true };
        lineService.AddLinesFromSourceToTarget(model, link);

        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Source", "Target"), out var line));
        Assert.True(line.IsInheritance);
        Assert.True(line.HasInheritanceSourceEnd);
        Assert.True(line.HasInheritanceTargetEnd);
        Assert.False(model.Lines.TryGetValue(LineId.From("Source", "Target"), out _));
    }

    [Fact]
    public void AddLinesFromSourceToTarget_ShouldOnlySplitEndpointSegmentsForInheritance()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new LineService();

        var parentA = AddNode(model, "ParentA", model.Root);
        var parentB = AddNode(model, "ParentB", model.Root);
        var source = AddNode(model, "Source", parentA);
        var target = AddNode(model, "Target", parentB);
        var other = AddNode(model, "Other", parentA);

        var inheritanceLink = new Link(source, target) { IsInheritance = true };
        lineService.AddLinesFromSourceToTarget(model, inheritanceLink);

        // The endpoint segments are inheritance lines, the middle segment is a normal line
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("Source", "ParentA"), out var sourceLine));
        Assert.True(model.Lines.TryGetValue(LineId.FromInheritance("ParentB", "Target"), out var targetLine));
        Assert.True(model.Lines.TryGetValue(LineId.From("ParentA", "ParentB"), out var middleLine));

        // Only the end touching the real source/target node is specially anchored
        Assert.True(sourceLine.HasInheritanceSourceEnd);
        Assert.False(sourceLine.HasInheritanceTargetEnd);
        Assert.False(targetLine.HasInheritanceSourceEnd);
        Assert.True(targetLine.HasInheritanceTargetEnd);

        // A usage link from another node merges into the middle segment, not the endpoint ones
        var usageLink = new Link(other, target);
        lineService.AddLinesFromSourceToTarget(model, usageLink);

        Assert.Equal(2, middleLine.Links.Count);
        Assert.Single(targetLine.Links);
        Assert.True(model.Lines.TryGetValue(LineId.From("ParentB", "Target"), out var usageTargetLine));
        Assert.Single(usageTargetLine.Links);
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent);
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }
}
