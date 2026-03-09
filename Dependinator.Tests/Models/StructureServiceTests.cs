using Dependinator.Core.Parsing;
using Dependinator.Modeling;
using ParsingLink = Dependinator.Core.Parsing.Link;
using ParsingNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.Tests.Models;

public class StructureServiceTests
{
    [Fact]
    public void AddOrUpdateNode_ShouldCreateParentAndChild()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        var lineService = new Mock<ILineService>();
        var service = new StructureService(lineService.Object);

        var parsedNode = new ParsingNode(
            "Parent.Child",
            new Dependinator.Core.Parsing.NodeProperties { Parent = "Parent", Type = NodeType.Type }
        );

        service.AddOrUpdateNode(model, parsedNode);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("Parent"), out var parent));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("Parent.Child"), out var child));
        Assert.Equal("Parent", child.Parent.Name);
        Assert.Contains(child, parent.Children);
        Assert.Equal(model.UpdateStamp, child.UpdateStamp);
    }

    // [Fact]
    // public void AddOrUpdateNode_ShouldReparentWhenParentChanges()
    // {
    //     var model = new Model { UpdateStamp = new DateTime(2024, 1, 1) };
    //     var lineService = new Mock<ILineService>();
    //     var service = new StructureService(model, lineService.Object);

    //     var initialNode = new ParsingNode(
    //         "Child",
    //         new Dependinator.Core.Parsing.NodeAttributes { Parent = "ParentA", Type = NodeType.Type }
    //     );
    //     service.AddOrUpdateNode(initialNode);

    //     var updatedNode = new ParsingNode(
    //         "Child",
    //         new Dependinator.Core.Parsing.NodeAttributes { Parent = "ParentB", Type = NodeType.Type }
    //     );
    //     service.AddOrUpdateNode(updatedNode);

    //     var child = model.GetNode(NodeId.FromName("Child"));
    //     var oldParent = model.GetNode(NodeId.FromName("ParentA"));

    //     Assert.Equal("ParentB", child.Parent.Name);
    //     Assert.DoesNotContain(child, oldParent.Children);
    // }

    [Fact]
    public void AddOrUpdateLink_ShouldCreateNodesAndAddLinesOnce()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var lineService = new Mock<ILineService>();
        var service = new StructureService(lineService.Object);

        var parsedLink = new ParsingLink("Source", "Target", new Dependinator.Core.Parsing.LinkProperties());

        service.AddOrUpdateLink(model, parsedLink);
        service.AddOrUpdateLink(model, parsedLink);

        Assert.True(model.Links.TryGetValue(new LinkId("Source", "Target"), out var link));
        Assert.Equal("Source", link.Source.Name);
        Assert.Equal("Target", link.Target.Name);
        lineService.Verify(
            s =>
                s.AddLinesFromSourceToTarget(
                    model,
                    It.Is<Dependinator.Modeling.Link>(l => l.Source.Name == "Source" && l.Target.Name == "Target")
                ),
            Times.Once
        );
    }
}
