using Dependinator.Modeling;
using Dependinator.Modeling.Models;
using Dependinator.Shared;

namespace Dependinator.Tests.Models;

public class ModelCleanupTests
{
    [Fact]
    public void ClearNotUpdated_ShouldRemoveStaleNodesAndParents()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var parent = new Node("Parent", model.Root) { UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(parent);
        model.TryAddNode(parent);

        var child = new Node("Child", parent) { UpdateStamp = stamp.AddDays(-1) };
        parent.AddChild(child);
        model.TryAddNode(child);

        var current = new Node("Current", model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(current);
        model.TryAddNode(current);

        structureService.ClearNotUpdated(model);

        Assert.False(model.Nodes.TryGetValue(NodeId.FromName("Child"), out _));
        Assert.False(model.Nodes.TryGetValue(NodeId.FromName("Parent"), out _));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("Current"), out _));
    }

    [Fact]
    public void ClearNotUpdated_ShouldRemoveStaleLinksAndLines()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var source = new Node("Source", model.Root) { UpdateStamp = stamp };
        var target = new Node("Target", model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        var link = new Link(source, target) { UpdateStamp = stamp.AddDays(-1) };
        var line = new Line(source, target);
        line.Add(link);
        link.AddLine(line);
        model.TryAddLine(line);
        model.TryAddLink(link);

        structureService.ClearNotUpdated(model);

        Assert.False(model.Links.TryGetValue(new LinkId("Source", "Target"), out _));
        Assert.False(model.Lines.TryGetValue(LineId.From("Source", "Target"), out _));
    }
}
