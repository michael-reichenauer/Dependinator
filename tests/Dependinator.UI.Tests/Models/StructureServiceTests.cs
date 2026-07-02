using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using ParsingLineDescription = Dependinator.Core.Parsing.LineDescription;
using ParsingLink = Dependinator.Core.Parsing.Link;
using ParsingNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

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
                    It.Is<Modeling.Models.Link>(l => l.Source.Name == "Source" && l.Target.Name == "Target")
                ),
            Times.Once
        );
    }

    [Fact]
    public void SetLineDescription_ShouldSetDescriptionOnExactLine()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        var service = new StructureService(new Mock<ILineService>().Object);

        var (_, _, line) = AddNodesWithLine(model, "Source", "Target");

        service.SetLineDescription(model, new ParsingLineDescription("Source", "Target", "Uses <the> target"));

        Assert.Equal("Uses <the> target", line.Description);
        Assert.Equal("Uses &lt;the&gt; target", line.HtmlDescription);
        Assert.Equal(model.UpdateStamp, line.DescriptionUpdateStamp);
    }

    [Fact]
    public void SetLineDescription_ShouldResolveRelativeTargetViaAncestors()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        var service = new StructureService(new Mock<ILineService>().Object);

        // Node names carry a '*' module prefix (e.g. "Dependinator*Core"), so a relative
        // target like "Models" must resolve via the source node's ancestors
        var module = AddNode(model, "Dependinator*Core", model.Root);
        var source = AddNode(model, "Dependinator*Core.Parsing", module);
        var target = AddNode(model, "Dependinator*Core.Models", module);
        var line = new Line(source, target);
        model.TryAddLine(line);

        service.SetLineDescription(model, new ParsingLineDescription("Dependinator*Core.Parsing", "Models", "text"));

        Assert.Equal("text", line.Description);
    }

    [Fact]
    public void SetLineDescription_ShouldNotCreateNodesOrLines()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        var service = new StructureService(new Mock<ILineService>().Object);

        AddNode(model, "Source", model.Root);
        var nodeCount = model.Nodes.Count;
        var lineCount = model.Lines.Count;

        service.SetLineDescription(model, new ParsingLineDescription("Source", "Missing", "text"));

        Assert.Equal(nodeCount, model.Nodes.Count);
        Assert.Equal(lineCount, model.Lines.Count);
    }

    [Fact]
    public void SetLineLayoutDto_ShouldRoundtripLineDescription()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        var service = new StructureService(new Mock<ILineService>().Object);

        var (_, _, line) = AddNodesWithLine(model, "Source", "Target");
        line.SetDescription("Uses the target", model.UpdateStamp);

        // The line has no segment points but is persisted anyway because it has a description
        var lineDto = Assert.Single(model.SerializeToDto().Lines);
        Assert.Equal("Uses the target", lineDto.Description);

        line.ClearDescription();
        service.SetLineLayoutDto(model, lineDto);

        Assert.Equal("Uses the target", line.Description);
    }

    static Modeling.Models.Node AddNode(IModel model, string name, Modeling.Models.Node parent)
    {
        var node = new Modeling.Models.Node(name, parent) { UpdateStamp = model.UpdateStamp };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static (Modeling.Models.Node, Modeling.Models.Node, Line) AddNodesWithLine(
        IModel model,
        string sourceName,
        string targetName
    )
    {
        var source = AddNode(model, sourceName, model.Root);
        var target = AddNode(model, targetName, model.Root);
        var line = new Line(source, target);
        model.TryAddLine(line);
        return (source, target, line);
    }
}
