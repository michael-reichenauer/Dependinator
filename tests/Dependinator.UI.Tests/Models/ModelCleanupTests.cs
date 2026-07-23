using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using ParsingLink = Dependinator.Core.Parsing.Link;

namespace Dependinator.UI.Tests.Models;

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

    [Fact]
    public void ClearNotUpdated_ShouldClearStaleLineDescriptions()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var source = new Node("Source", model.Root) { UpdateStamp = stamp };
        var target = new Node("Target", model.Root) { UpdateStamp = stamp };
        var other = new Node("Other", model.Root) { UpdateStamp = stamp };
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.Root.AddChild(other);
        model.TryAddNode(source);
        model.TryAddNode(target);
        model.TryAddNode(other);

        var staleLine = AddLine(model, source, target, stamp);
        staleLine.SetDescription("Stale description", stamp.AddDays(-1));

        var freshLine = AddLine(model, source, other, stamp);
        freshLine.SetDescription("Fresh description", stamp);

        structureService.ClearNotUpdated(model);

        Assert.Null(staleLine.Description);
        Assert.Equal("Fresh description", freshLine.Description);
    }

    // Regression: a new link to a node from an earlier parse (e.g. an external target only
    // referenced by links, whose sole referencer was renamed) must keep that node alive;
    // removing it left the link with a detached endpoint, crashing ancestor walks on render.
    [Fact]
    public void ClearNotUpdated_ShouldKeepStaleNodeReferencedByNewLink()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var target = new Node("Target", model.Root) { UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(target);
        model.TryAddNode(target);

        structureService.AddOrUpdateLink(model, new ParsingLink("Source", "Target", new()));

        structureService.ClearNotUpdated(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("Target"), out var kept));
        Assert.Same(model.Root, kept.Parent);
        Assert.Equal(stamp, kept.UpdateStamp);
        Assert.True(model.Links.TryGetValue(new LinkId("Source", "Target"), out _));
    }

    // Defense in depth for the same invariant: even when a current parsed link exists on a
    // stale node without the endpoint having been restamped, cleanup must not remove the node.
    [Fact]
    public void ClearNotUpdated_ShouldKeepStaleNodeWithCurrentParsedLink()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var source = new Node("Source", model.Root) { UpdateStamp = stamp };
        var target = new Node("Target", model.Root) { UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        var link = new Link(source, target) { UpdateStamp = stamp };
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);

        structureService.ClearNotUpdated(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("Target"), out var kept));
        Assert.Same(model.Root, kept.Parent);
    }

    // A manual link must not keep a deleted parsed node alive: the node goes and the link is
    // dropped as dangling (the pre-existing behavior the parsed-link guard must not change).
    [Fact]
    public void ClearNotUpdated_ShouldRemoveStaleNodeWithOnlyManualLink()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var structureService = new StructureService(lineService.Object);
        var stamp = new DateTime(2024, 1, 1);
        model.UpdateStamp = stamp;

        var source = new Node("Source", model.Root) { UpdateStamp = stamp };
        var target = new Node("Target", model.Root) { UpdateStamp = stamp.AddDays(-1) };
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);

        var link = new Link(source, target) { IsManual = true, UpdateStamp = stamp };
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);

        structureService.ClearNotUpdated(model);

        Assert.False(model.Nodes.TryGetValue(NodeId.FromName("Target"), out _));
        Assert.False(model.Links.TryGetValue(new LinkId("Source", "Target"), out _));
    }

    static Line AddLine(IModel model, Node source, Node target, DateTime stamp)
    {
        var link = new Link(source, target) { UpdateStamp = stamp };
        var line = new Line(source, target);
        line.Add(link);
        link.AddLine(line);
        model.TryAddLine(line);
        model.TryAddLink(link);
        return line;
    }
}
