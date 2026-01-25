using Dependinator.Models;

namespace Dependinator.Tests.Models;

public class ModelCleanupTests
{
    [Fact]
    public void ClearNotUpdated_ShouldRemoveStaleNodesAndParents()
    {
        var model = new Model();
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var parent = new Node("Parent", model.Root) { UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(parent);
        model.AddNode(parent);

        var child = new Node("Child", parent) { UpdateStamp = stamp.AddDays(-1) };
        parent.AddChild(child);
        model.AddNode(child);

        var current = new Node("Current", model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(current);
        model.AddNode(current);

        model.ClearNotUpdated();

        Assert.False(model.TryGetNode(NodeId.FromName("Child"), out _));
        Assert.False(model.TryGetNode(NodeId.FromName("Parent"), out _));
        Assert.True(model.TryGetNode(NodeId.FromName("Current"), out _));
    }

    [Fact]
    public void ClearNotUpdated_ShouldRemoveStaleLinksAndLines()
    {
        var model = new Model();
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var source = new Node("Source", model.Root) { UpdateStamp = stamp };
        var target = new Node("Target", model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.AddNode(source);
        model.AddNode(target);

        var link = new Link(source, target) { UpdateStamp = stamp.AddDays(-1) };
        var line = new Line(source, target);
        line.Add(link);
        link.AddLine(line);
        model.AddLine(line);
        model.AddLink(link);

        model.ClearNotUpdated();

        Assert.False(model.TryGetLink(new LinkId("Source", "Target"), out _));
        Assert.False(model.TryGetLine(LineId.From("Source", "Target"), out _));
    }
}
