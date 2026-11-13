using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Moq;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A go to marks the nodes it showed (HighlightService) instead of selecting them: the flags,
// the brackets in the node's SVG, and what clears it.
public class HighlightServiceTests
{
    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IApplicationEvents> applicationEvents = new();

    HighlightService CreateService() => new(modelMgr, applicationEvents.Object);

    Node AddNode(string name)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 100, 100) };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    [Fact]
    public void Highlight_ShouldMarkTheNodes_AndReplaceAnEarlierHighlight()
    {
        var a = AddNode("A");
        var b = AddNode("B");
        var c = AddNode("C");
        var service = CreateService();

        service.Highlight([a.Id, b.Id]);
        Assert.True(a.IsHighlighted);
        Assert.True(b.IsHighlighted);
        Assert.True(service.IsPulsing);
        Assert.Equal([a.Id, b.Id], service.HighlightedIds);

        service.Highlight([c.Id]);
        Assert.False(a.IsHighlighted);
        Assert.False(b.IsHighlighted);
        Assert.True(c.IsHighlighted);
        Assert.Equal([c.Id], service.HighlightedIds);

        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(2)); // The brackets are in the tiles
    }

    [Fact]
    public void Highlight_ShouldLeaveASelectedNodeToItsSelection()
    {
        var a = AddNode("A");
        var b = AddNode("B");
        a.IsSelected = true;
        var service = CreateService();

        service.Highlight([a.Id, b.Id]);

        Assert.False(a.IsHighlighted);
        Assert.True(a.IsSelected);
        Assert.True(b.IsHighlighted);
    }

    [Fact]
    public void Clear_ShouldRemoveTheHighlight_AndDoNothingWithoutOne()
    {
        var a = AddNode("A");
        var service = CreateService();

        service.Clear();
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Never); // No repaint for nothing

        service.Highlight([a.Id]);
        service.Clear();

        Assert.False(a.IsHighlighted);
        Assert.Empty(service.HighlightedIds);
        Assert.False(service.IsPulsing);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Exactly(2));
    }

    [Fact]
    public void SelectedNodeSvg_ShouldDrawTheBracketsOnlyForAHighlightedNode()
    {
        var a = AddNode("A");
        var geometry = new Rect(0, 0, 100, 100);

        Assert.DoesNotContain("node-highlight", NodeSvg.SelectedNodeSvg(a, geometry, isEditing: false));

        a.IsHighlighted = true;
        var svg = NodeSvg.SelectedNodeSvg(a, geometry, isEditing: false);
        Assert.Contains("node-highlight", svg);
        Assert.Contains(DColors.Highlight, svg);
        Assert.DoesNotContain("stroke-dasharray", svg); // Not the selection frame
    }

    [Fact]
    public void FindSelection_ShouldCarryTheHighlightedNodes()
    {
        var a = AddNode("A");
        var b = AddNode("B");
        CreateService().Highlight([a.Id]);
        b.IsSelected = true;

        using var model = modelMgr.UseModel();
        var selection = LineEmphasis.FindSelection(model);

        Assert.Same(b, selection.Node);
        Assert.Equal([a], selection.Highlighted!);
    }
}
